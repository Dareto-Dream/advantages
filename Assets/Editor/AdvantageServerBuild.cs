using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class AdvantageServerBuild
{
    private const string OutputRoot = "Build/Server";
    private const string ExecutableName = "AdvantageServer";

    private const string RunAsServerMenu = "Advantage/Net/Run As Dedicated Server";

    [MenuItem(RunAsServerMenu, priority = 100)]
    private static void ToggleRunAsServer()
    {
        bool wanted = !EditorPrefs.GetBool(NetConfig.EditorServerModeKey, false);
        EditorPrefs.SetBool(NetConfig.EditorServerModeKey, wanted);

        Debug.Log(wanted
            ? "[Server] play mode will now start as a dedicated server. Set REGION, RELAY_INTERNAL_URL, "
              + "RELAY_PUBLIC_URL and INTERNAL_SECRET in the environment first."
            : "[Server] play mode is back to being a normal client.");
    }

    [MenuItem(RunAsServerMenu, validate = true)]
    private static bool ToggleRunAsServerValidate()
    {
        Menu.SetChecked(RunAsServerMenu, EditorPrefs.GetBool(NetConfig.EditorServerModeKey, false));
        return true;
    }

    [MenuItem("Advantage/Build/5 - Dedicated Server (Linux)", priority = 4)]
    public static void BuildLinuxServer()
    {
        BuildServer(BuildTarget.StandaloneLinux64, "linux");
    }

    [MenuItem("Advantage/Build/6 - Dedicated Server (Windows, local testing)", priority = 5)]
    public static void BuildWindowsServer()
    {
        BuildServer(BuildTarget.StandaloneWindows64, "windows");
    }

    public static void BuildFromCommandLine()
    {
        BuildLinuxServer();
    }

    private static bool HasServerSubtarget(BuildTarget target)
    {
        string playbackEngine = BuildPipeline.GetPlaybackEngineDirectory(target, BuildOptions.None);

        if (string.IsNullOrEmpty(playbackEngine) || !Directory.Exists(playbackEngine))
        {
            return false;
        }

        return Directory.GetDirectories(playbackEngine)
            .Any(dir => Path.GetFileName(dir).ToLowerInvariant().Contains("server"));
    }

    private static void BuildServer(BuildTarget target, string folder)
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
        {
            string module = target == BuildTarget.StandaloneLinux64
                ? "Linux Dedicated Server Build Support (or Linux Build Support (Mono))"
                : "Windows Dedicated Server Build Support";

            Debug.LogError($"[Server] cannot build {target}: that platform is not installed. "
                + $"Install it in Unity Hub > Installs > {Application.unityVersion} > Add Modules > {module}, "
                + "then run this menu item again.");
            return;
        }

        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            Debug.LogError("[Server] no scenes in Build Settings. Run Advantage > Build > 3 - Build Scenes first.");
            return;
        }

        string directory = Path.Combine(OutputRoot, folder);
        Directory.CreateDirectory(directory);

        string extension = target == BuildTarget.StandaloneWindows64 ? ".exe" : ".x86_64";
        string outputPath = Path.Combine(directory, ExecutableName + extension);

        bool dedicated = HasServerSubtarget(target);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = target,

            subtarget = (int)(dedicated ? StandaloneBuildSubtarget.Server : StandaloneBuildSubtarget.Player),
            options = BuildOptions.CompressWithLz4HC,
        };

        if (!dedicated)
        {
            Debug.LogWarning($"[Server] the Dedicated Server module for {target} is not installed. "
                + "Building a normal player instead - it works as a server when launched with "
                + "-batchmode -nographics, but ships rendering code it will never use. Install "
                + "\"Dedicated Server Build Support\" in Unity Hub for a proper headless build.");
        }

        Debug.Log($"[Server] building {target} {(dedicated ? "dedicated server" : "player (headless fallback)")} into {outputPath}");

        NamedBuildTarget namedTarget = NamedBuildTarget.Standalone;
        ScriptingImplementation previousBackend = PlayerSettings.GetScriptingBackend(namedTarget);
        bool changedBackend = target == BuildTarget.StandaloneLinux64
            && previousBackend != ScriptingImplementation.IL2CPP;

        if (changedBackend)
        {
            PlayerSettings.SetScriptingBackend(namedTarget, ScriptingImplementation.IL2CPP);
        }

        BuildReport report;
        try
        {
            report = BuildPipeline.BuildPlayer(options);
        }
        finally
        {
            if (changedBackend)
            {
                PlayerSettings.SetScriptingBackend(namedTarget, previousBackend);
            }
        }

        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[Server] build succeeded: {summary.totalSize / (1024 * 1024)} MB at {outputPath}");

            string backupFolder = Path.Combine(directory, ExecutableName + "_BackUpThisFolder_ButDontShipItWithYourGame");
            if (Directory.Exists(backupFolder))
            {
                Directory.Delete(backupFolder, true);
            }

            WriteDockerAssets(directory);
        }
        else
        {
            Debug.LogError($"[Server] build {summary.result}: {summary.totalErrors} error(s). "
                + "If the target is missing, install the Linux Dedicated Server module in Unity Hub.");
        }
    }

    private static void WriteDockerAssets(string directory)
    {
        string dockerfile = string.Join(Environment.NewLine, new[]
        {
            "FROM ubuntu:22.04",
            "",
            "RUN apt-get update && apt-get install -y --no-install-recommends \\",
            "      ca-certificates libc6 libstdc++6 libcurl4 libssl3 wget \\",
            "    && wget -q http://security.ubuntu.com/ubuntu/pool/main/o/openssl/libssl1.1_1.1.1f-1ubuntu2.24_amd64.deb \\",
            "    && dpkg -i libssl1.1_1.1.1f-1ubuntu2.24_amd64.deb \\",
            "    && rm libssl1.1_1.1.1f-1ubuntu2.24_amd64.deb \\",
            "    && apt-get remove -y wget \\",
            "    && rm -rf /var/lib/apt/lists/*",
            "",
            "WORKDIR /app",
            "COPY . /app",
            "RUN chmod +x /app/" + ExecutableName + ".x86_64",
            "",
            "ENV ADVANTAGE_SERVER=1",
            "CMD [\"/app/" + ExecutableName + ".x86_64\", \"-batchmode\", \"-nographics\", \"-job-worker-count\", \"4\", \"-logFile\", \"/dev/stdout\"]",
        });

        File.WriteAllText(Path.Combine(directory, "Dockerfile"), dockerfile);

        string ignore = string.Join(Environment.NewLine, new[]
        {
            "*_BurstDebugInformation_DoNotShip/",
            "*_BackUpThisFolder_ButDontShipItWithYourGame/",
        });

        File.WriteAllText(Path.Combine(directory, ".dockerignore"), ignore);

        Debug.Log($"[Server] wrote Dockerfile into {directory}");
    }
}
