using System;
using UnityEngine;

public static class NetConfig
{

    public const byte ProtocolVersion = 1;

    public const int TickRate = 30;

    public const float TickInterval = 1f / TickRate;

    public const int InputSendRate = 60;

    public const float InterpolationDelay = 2f / TickRate;

    public const float LagCompensationWindow = 0.5f;

    public const float HardCorrectionDistance = 2.5f;

    public const float CorrectionSmoothing = 12f;

    public const int GamePort = 7777;
    public const int QueryPort = 7778;
    public const int RconPort = 7779;

    private const string DefaultGatewayUrl = "https://gateway-production-a3c7.up.railway.app";

    private static string cachedGateway;

    public static string GatewayUrl
    {
        get
        {
            if (string.IsNullOrEmpty(cachedGateway))
            {
                cachedGateway = Resolve("-gateway", "ADVANTAGE_GATEWAY_URL", DefaultGatewayUrl).TrimEnd('/');
            }

            return cachedGateway;
        }
    }

    public static string RelayInternalUrl => Resolve("-relay", "RELAY_INTERNAL_URL", string.Empty).TrimEnd('/');

    public static string RelayPublicUrl => Resolve("-relay-public", "RELAY_PUBLIC_URL", string.Empty).TrimEnd('/');

    public static string InternalSecret => Resolve("-secret", "INTERNAL_SECRET", string.Empty);

    public static string RconPassword => Resolve("-rcon-password", "RCON_PASSWORD", string.Empty);

    public static string Region => Resolve("-region", "REGION", "us-east");

    public static string ServerId
    {
        get
        {
            string configured = Resolve("-server-id", "SERVER_ID", string.Empty);
            if (!string.IsNullOrEmpty(configured))
            {
                return configured;
            }

            string replica = Environment.GetEnvironmentVariable("RAILWAY_REPLICA_ID");
            if (!string.IsNullOrEmpty(replica))
            {
                return replica;
            }

            return $"local-{Guid.NewGuid()}";
        }
    }

    public static string BuildVersion => Resolve("-build", "BUILD_VERSION", Application.version);

    public static bool WantsServerRole()
    {
#if UNITY_SERVER
        return true;
#else
#if UNITY_EDITOR

        if (UnityEditor.EditorPrefs.GetBool(EditorServerModeKey, false))
        {
            return true;
        }
#endif

        if (HasSwitch("-advantage-server"))
        {
            return true;
        }

        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ADVANTAGE_SERVER"));
#endif
    }

    public const string EditorServerModeKey = "Advantage.Net.RunAsServer";

    private static string Resolve(string switchName, string envName, string fallback)
    {
        string fromArgs = ReadSwitch(switchName);
        if (!string.IsNullOrEmpty(fromArgs))
        {
            return fromArgs;
        }

        string fromEnv = Environment.GetEnvironmentVariable(envName);
        if (!string.IsNullOrEmpty(fromEnv))
        {
            return fromEnv;
        }

        return fallback;
    }

    private static string ReadSwitch(string name)
    {
        string[] args = Environment.GetCommandLineArgs();

        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return string.Empty;
    }

    private static bool HasSwitch(string name)
    {
        foreach (string arg in Environment.GetCommandLineArgs())
        {
            if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
