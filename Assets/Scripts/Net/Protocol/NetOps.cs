public enum ClientOp : byte
{

    Hello = 1,

    Input = 2,

    HeroPick = 3,

    Ready = 4,

    RoomCommand = 5,

    Pong = 6,

    Leave = 7
}

public enum ServerOp : byte
{

    Welcome = 1,

    Snapshot = 2,

    MatchState = 3,

    Roster = 4,

    Event = 5,

    Ping = 6,

    Reject = 7
}

public enum RoomCommand : byte
{

    SetBotsEnabled = 1,

    AddBot = 2,

    RemoveBot = 3,

    ClearBots = 4,

    SkipPhase = 5,

    RestartMatch = 6,

    SetTeamSize = 7,

    HealAll = 8,

    SetMode = 9,

    KickPlayer = 10,

    SetPaused = 11
}

public enum NetEventKind : byte
{

    Kill = 1,

    HitConfirm = 2,

    AbilityCast = 3,

    Damage = 4,

    Shot = 5,

    RoomSettings = 6
}

public static class InputButton
{
    public const byte Jump = 1 << 0;
    public const byte Fire = 1 << 1;
    public const byte Aim = 1 << 2;
    public const byte Reload = 1 << 3;
    public const byte Crouch = 1 << 4;
    public const byte Interact = 1 << 5;
    public const byte Descend = 1 << 6;
}

public static class InputAbility
{
    public const byte Primary = 1 << 0;
    public const byte Secondary = 1 << 1;
    public const byte Special = 1 << 2;
    public const byte Ultimate = 1 << 3;
}

public static class EntityFlag
{
    public const byte Alive = 1 << 0;
    public const byte Grounded = 1 << 1;
    public const byte Firing = 1 << 2;
    public const byte Aiming = 1 << 3;
    public const byte Reloading = 1 << 4;
    public const byte Bot = 1 << 5;
    public const byte Crouching = 1 << 6;
}
