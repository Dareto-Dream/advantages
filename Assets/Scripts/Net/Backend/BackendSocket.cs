using System;
using UnityEngine;

public class BackendSocket : MonoBehaviour
{
    private const float ReconnectDelay = 3f;
    private const float PresenceInterval = 20f;

    private static BackendSocket instance;

    public static BackendSocket Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject host = new GameObject("~BackendSocket");
                instance = host.AddComponent<BackendSocket>();
                DontDestroyOnLoad(host);
            }

            return instance;
        }
    }

#pragma warning disable 0649
    [Serializable]
    private class Envelope
    {
        public string t;
        public BackendModels.Player player;
        public BackendModels.Player from;
        public string playerId;
        public string status;
        public string roomId;
        public string ticketId;
        public string region;
        public string mode;
        public string relayUrl;
        public string joinToken;
        public string reason;
        public string code;
        public string partyId;
        public int team;
    }
#pragma warning restore 0649

    private WebSocketTransport transport;
    private float reconnectAt;
    private float nextPresenceAt;
    private string presenceStatus = "menu";
    private bool wantsConnection;

    public event Action<BackendModels.MatchInfo, int> MatchFound;

    public event Action<string> MatchFailed;

    public event Action<BackendModels.Player> FriendRequestReceived;
    public event Action<BackendModels.Player> FriendAccepted;
    public event Action<string> FriendRemoved;

    public event Action<string, string> FriendPresenceChanged;

    public event Action<BackendModels.Player, string> PartyInvited;

    public event Action PartyUpdated;

    public event Action<string, BackendModels.Player> RoomInvited;

    public event Action<string> RoomClosed;

    public bool IsConnected => transport != null && transport.State == TransportState.Open;

    public void Connect()
    {
        wantsConnection = true;
        reconnectAt = 0f;
    }

    public void Disconnect()
    {
        wantsConnection = false;
        transport?.Close("signed out");
        transport?.Dispose();
        transport = null;
    }

    public void SetPresence(string status, string roomId = null)
    {
        presenceStatus = status;

        if (!IsConnected)
        {
            return;
        }

        string payload = string.IsNullOrEmpty(roomId)
            ? $"{{\"t\":\"presence\",\"status\":\"{status}\"}}"
            : $"{{\"t\":\"presence\",\"status\":\"{status}\",\"roomId\":\"{roomId}\"}}";

        transport.SendText(payload);
        nextPresenceAt = Time.unscaledTime + PresenceInterval;
    }

    private void Update()
    {
        if (!wantsConnection)
        {
            return;
        }

        EnsureConnection();
        Pump();

        if (IsConnected && Time.unscaledTime >= nextPresenceAt)
        {
            SetPresence(presenceStatus);
        }
    }

    private void EnsureConnection()
    {
        if (transport != null)
        {
            TransportState state = transport.State;
            if (state == TransportState.Open || state == TransportState.Connecting)
            {
                return;
            }

            transport.Dispose();
            transport = null;
            reconnectAt = Time.unscaledTime + ReconnectDelay;
        }

        if (Time.unscaledTime < reconnectAt)
        {
            return;
        }

        string token = DeviceIdentity.SessionToken;
        if (string.IsNullOrEmpty(token))
        {
            return;
        }

        string url = $"{AdvantageBackend.WsFromHttp(NetConfig.GatewayUrl)}/ws?token={Uri.EscapeDataString(token)}";
        transport = new WebSocketTransport();
        transport.Connect(url);
    }

    private void Pump()
    {
        if (transport == null)
        {
            return;
        }

        while (transport.TryReceive(out TransportMessage message))
        {
            if (!message.isText)
            {
                continue;
            }

            Handle(message.AsText());
        }
    }

    private void Handle(string json)
    {
        Envelope envelope;

        try
        {
            envelope = JsonUtility.FromJson<Envelope>(json);
        }
        catch (Exception err)
        {
            Debug.LogWarning($"[BackendSocket] unreadable message: {err.Message}");
            return;
        }

        if (envelope == null || string.IsNullOrEmpty(envelope.t))
        {
            return;
        }

        switch (envelope.t)
        {
            case "ready":
                SetPresence(presenceStatus);
                break;

            case "match.found":
                MatchFound?.Invoke(new BackendModels.MatchInfo
                {
                    roomId = envelope.roomId,
                    region = envelope.region,
                    mode = envelope.mode,
                    relayUrl = envelope.relayUrl,
                    joinToken = envelope.joinToken,
                }, envelope.team);
                break;

            case "match.failed":
                MatchFailed?.Invoke(envelope.reason);
                break;

            case "friend.request":
                FriendRequestReceived?.Invoke(envelope.player);
                break;

            case "friend.accepted":
                FriendAccepted?.Invoke(envelope.player);
                break;

            case "friend.removed":
                FriendRemoved?.Invoke(envelope.playerId);
                break;

            case "friend.presence":
                FriendPresenceChanged?.Invoke(envelope.playerId, envelope.status);
                break;

            case "party.invite":
                PartyInvited?.Invoke(envelope.from, envelope.partyId);
                break;

            case "party.updated":
                PartyUpdated?.Invoke();
                break;

            case "room.invite":
                RoomInvited?.Invoke(envelope.code, envelope.from);
                break;

            case "room.closed":
                RoomClosed?.Invoke(envelope.roomId);
                break;

            case "pong":
                break;
        }
    }

    private void OnDestroy()
    {
        Disconnect();

        if (instance == this)
        {
            instance = null;
        }
    }
}
