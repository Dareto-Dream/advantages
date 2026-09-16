using UnityEngine;

public static class ServerBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (!NetConfig.WantsServerRole())
        {
            return;
        }

        NetContext.SetRole(NetRole.Server);

        GameObject host = new GameObject("~NetServer");
        host.AddComponent<NetServer>();
        host.AddComponent<RconServer>();
        Object.DontDestroyOnLoad(host);

        Debug.Log("[Server] running as a dedicated server");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AfterSceneLoad()
    {
        if (!NetConfig.WantsServerRole())
        {
            return;
        }

        Debug.Log("[Server] after scene load reached");
    }
}
