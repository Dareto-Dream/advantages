using System;

#pragma warning disable 0649

public static class BackendModels
{
    [Serializable]
    public class Player
    {
        public string id;
        public string name;
        public string tag;

        public string handle;
        public bool guest;
    }

    [Serializable]
    public class LoginResponse
    {
        public string token;
        public int expiresIn;
        public Player player;
    }

    [Serializable]
    public class MeResponse
    {
        public Player player;
    }

    [Serializable]
    public class RegionInfo
    {
        public string id;
        public string relayUrl;
        public int servers;
        public int freeSlots;
        public int players;
        public bool available;

        [NonSerialized] public float pingMs = -1f;
    }

    [Serializable]
    public class RegionsResponse
    {
        public RegionInfo[] regions;
        public string[] modes;
    }

    [Serializable]
    public class MatchInfo
    {
        public string roomId;
        public string region;
        public string mode;
        public string relayUrl;
        public string joinToken;
    }

    [Serializable]
    public class Ticket
    {
        public string id;

        public string state;
        public int waitSeconds;
        public int players;
        public string reason;
        public MatchInfo match;
    }

    [Serializable]
    public class TicketResponse
    {
        public Ticket ticket;
    }

    [Serializable]
    public class FriendEntry
    {
        public Player player;

        public string status;

        public bool incoming;
        public string since;

        public string presence;
        public string roomId;
    }

    [Serializable]
    public class FriendsResponse
    {
        public FriendEntry[] friends;
        public FriendEntry[] incoming;
        public FriendEntry[] outgoing;
        public FriendEntry[] blocked;
    }

    [Serializable]
    public class RoomInfo
    {
        public string id;
        public string region;
        public string mode;
        public string state;
        public string joinCode;
        public int teamSize;
        public bool botsEnabled;
        public string hostId;
    }

    [Serializable]
    public class JoinResponse
    {
        public RoomInfo room;
        public string relayUrl;
        public int team;
        public string joinToken;
    }

    [Serializable]
    public class RoomResponse
    {
        public RoomInfo room;
    }

    [Serializable]
    public class ErrorResponse
    {
        public string error;
        public string message;
    }

    [Serializable]
    public class QueueRequest
    {
        public string[] modes;
        public string[] regions;
    }
}
#pragma warning restore 0649
