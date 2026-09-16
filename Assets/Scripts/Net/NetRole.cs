using System;

public enum NetRole
{

    Offline,

    Server,

    Client
}

public static class NetContext
{
    private static NetRole role = NetRole.Offline;

    public static event Action<NetRole> RoleChanged;

    public static NetRole Role => role;

    public static bool IsServer => role == NetRole.Server;
    public static bool IsClient => role == NetRole.Client;
    public static bool IsOffline => role == NetRole.Offline;

    public static bool HasAuthority => role != NetRole.Client;

    public static bool IsNetworked => role != NetRole.Offline;

    public static string RoomId { get; private set; } = string.Empty;

    public static string LocalPlayerId { get; private set; } = string.Empty;

    public static bool LocalPlayerIsHost { get; internal set; }

    public static void SetRole(NetRole value)
    {
        if (role == value)
        {
            return;
        }

        role = value;
        RoleChanged?.Invoke(role);
    }

    public static void SetSession(string roomId, string localPlayerId)
    {
        RoomId = roomId ?? string.Empty;
        LocalPlayerId = localPlayerId ?? string.Empty;
    }

    public static void Reset()
    {
        RoomId = string.Empty;
        LocalPlayerId = string.Empty;
        LocalPlayerIsHost = false;
        SetRole(NetRole.Offline);
    }
}
