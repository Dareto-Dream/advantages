using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class NetServer : MonoBehaviour
{
    private const float HeartbeatInterval = 5f;
    private const float RelayRetryDelay = 3f;
    private const float RegisterRetryDelay = 5f;

    public static NetServer Instance { get; private set; }

    private WebSocketTransport relayLink;
    private float nextHeartbeatAt;
    private float nextRelayAttemptAt;
    private float nextRegisterAttemptAt;
    private bool registered;

    private readonly List<NetConnection> connections = new List<NetConnection>();
    private readonly List<NetConnection> dropped = new List<NetConnection>();
    private readonly List<NetConnection> pumpScratch = new List<NetConnection>();

    public NetRoom Room { get; private set; }

    public IReadOnlyList<NetConnection> Connections => connections;

    public string ServerId { get; private set; }

    public bool RelayLinked => relayLink != null && relayLink.State == TransportState.Open;

#pragma warning disable 0649
    [Serializable]
    private class RelayMessage
    {
        public string t;
        public string session;
        public string room;
        public string player;
        public string token;
        public string region;
    }
#pragma warning restore 0649

    private void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);

        ServerId = NetConfig.ServerId;
        Room = new NetRoom(this);

        Application.targetFrameRate = NetConfig.TickRate * 2;
        QualitySettings.vSyncCount = 0;

        Debug.Log($"[Server] starting id={ServerId} region={NetConfig.Region} build={NetConfig.BuildVersion}");
    }

    private void Update()
    {
        EnsureRegistered();
        EnsureRelayLink();
        PumpRelayLink();
        PumpConnections();

        Room?.Tick();

        if (Time.unscaledTime >= nextHeartbeatAt)
        {
            nextHeartbeatAt = Time.unscaledTime + HeartbeatInterval;
            StartCoroutine(Heartbeat());
        }
    }

    private void FixedUpdate()
    {

        Room?.ApplyInputs();
    }

    private void EnsureRegistered()
    {
        if (registered || Time.unscaledTime < nextRegisterAttemptAt)
        {
            return;
        }

        nextRegisterAttemptAt = Time.unscaledTime + RegisterRetryDelay;
        StartCoroutine(Register());
    }

    private IEnumerator Register()
    {
        string relayPublic = NetConfig.RelayPublicUrl;

        if (string.IsNullOrEmpty(relayPublic))
        {
            Debug.LogError("[Server] RELAY_PUBLIC_URL is not set - players would have no address to reach this region on.");
            yield break;
        }

        string payload = "{"
            + $"\"id\":\"{Escape(ServerId)}\","
            + $"\"region\":\"{Escape(NetConfig.Region)}\","
            + $"\"relayUrl\":\"{Escape(relayPublic)}\","
            + $"\"buildVersion\":\"{Escape(NetConfig.BuildVersion)}\","
            + $"\"gamePort\":{NetConfig.GamePort},"
            + $"\"queryPort\":{NetConfig.QueryPort},"
            + $"\"rconPort\":{NetConfig.RconPort},"
            + "\"capacity\":1"
            + "}";

        yield return Internal("POST", "/internal/servers/register", payload, (ok, _) =>
        {
            registered = ok;
            if (ok)
            {
                Debug.Log($"[Server] registered with the gateway as {ServerId}");
                Debug.Log($"[Server] ports - game:{NetConfig.GamePort} query:{NetConfig.QueryPort} rcon:{NetConfig.RconPort}");
            }
        });
    }

    private IEnumerator Heartbeat()
    {
        if (!registered)
        {
            yield break;
        }

        int players = 0;
        foreach (NetConnection connection in connections)
        {
            if (connection.Authenticated)
            {
                players++;
            }
        }

        string payload = "{"
            + $"\"id\":\"{Escape(ServerId)}\","
            + $"\"activeRooms\":{(Room != null && Room.IsActive ? 1 : 0)},"
            + $"\"players\":{players},"
            + "\"status\":\"ready\""
            + "}";

        yield return Internal("POST", "/internal/servers/heartbeat", payload, (ok, body) =>
        {

            if (ok && body.Contains("\"known\":false"))
            {
                Debug.LogWarning("[Server] gateway no longer knows this server; re-registering");
                registered = false;
                nextRegisterAttemptAt = 0f;
            }
        });
    }

    public IEnumerator Internal(string method, string path, string body, Action<bool, string> done)
    {
        using UnityWebRequest request = new UnityWebRequest(NetConfig.GatewayUrl + path, method);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.timeout = 10;

        if (!string.IsNullOrEmpty(body))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            request.SetRequestHeader("Content-Type", "application/json");
        }

        request.SetRequestHeader("Authorization", $"Bearer {NetConfig.InternalSecret}");

        yield return request.SendWebRequest();

        bool ok = request.result == UnityWebRequest.Result.Success;
        if (!ok)
        {
            Debug.LogWarning($"[Server] {method} {path} failed: {request.error}");
        }

        done?.Invoke(ok, request.downloadHandler?.text ?? string.Empty);
    }

    private void EnsureRelayLink()
    {
        if (relayLink != null)
        {
            TransportState state = relayLink.State;
            if (state == TransportState.Open || state == TransportState.Connecting)
            {
                return;
            }

            Debug.LogWarning($"[Server] relay link lost ({relayLink.LastError}); reconnecting");
            relayLink.Dispose();
            relayLink = null;
            nextRelayAttemptAt = Time.unscaledTime + RelayRetryDelay;
        }

        if (Time.unscaledTime < nextRelayAttemptAt)
        {
            return;
        }

        string relayUrl = NetConfig.RelayInternalUrl;
        if (string.IsNullOrEmpty(relayUrl))
        {
            nextRelayAttemptAt = Time.unscaledTime + RelayRetryDelay;
            return;
        }

        string url = $"{relayUrl}/server?id={Uri.EscapeDataString(ServerId)}"
            + $"&secret={Uri.EscapeDataString(NetConfig.InternalSecret)}";

        relayLink = new WebSocketTransport();
        relayLink.Connect(url);
        nextRelayAttemptAt = Time.unscaledTime + RelayRetryDelay;
    }

    private void PumpRelayLink()
    {
        if (relayLink == null)
        {
            return;
        }

        while (relayLink.TryReceive(out TransportMessage message))
        {
            if (!message.isText)
            {
                continue;
            }

            RelayMessage parsed;

            try
            {
                parsed = JsonUtility.FromJson<RelayMessage>(message.AsText());
            }
            catch (Exception err)
            {
                Debug.LogWarning($"[Server] unreadable relay message: {err.Message}");
                continue;
            }

            if (parsed == null)
            {
                continue;
            }

            if (parsed.t == "linked")
            {
                Debug.Log($"[Server] linked to the {parsed.region} relay");
                ClaimRooms();
            }
            else if (parsed.t == "attach")
            {
                AcceptPlayer(parsed);
            }
        }
    }

    public void ClaimRooms()
    {
        if (!RelayLinked || Room == null || string.IsNullOrEmpty(Room.RoomId))
        {
            return;
        }

        relayLink.SendText($"{{\"t\":\"claim\",\"rooms\":[\"{Escape(Room.RoomId)}\"]}}");
    }

    public void ReleaseRoom(string roomId)
    {
        if (!RelayLinked || string.IsNullOrEmpty(roomId))
        {
            return;
        }

        relayLink.SendText($"{{\"t\":\"release\",\"rooms\":[\"{Escape(roomId)}\"]}}");
    }

    private void AcceptPlayer(RelayMessage message)
    {
        if (string.IsNullOrEmpty(message.session))
        {
            return;
        }

        string url = $"{NetConfig.RelayInternalUrl}/attach?session={Uri.EscapeDataString(message.session)}"
            + $"&secret={Uri.EscapeDataString(NetConfig.InternalSecret)}";

        WebSocketTransport transport = new WebSocketTransport();
        transport.Connect(url);

        connections.Add(new NetConnection(transport, message.session));
        Debug.Log($"[Server] dialling back for session {message.session} (room {message.room}, player {message.player})");
    }

    private void PumpConnections()
    {
        dropped.Clear();

        pumpScratch.Clear();
        pumpScratch.AddRange(connections);

        foreach (NetConnection connection in pumpScratch)
        {
            if (connection.ShouldDrop)
            {
                dropped.Add(connection);
                continue;
            }

            while (connection.Transport.TryReceive(out TransportMessage message))
            {
                if (message.isText || message.length == 0)
                {
                    continue;
                }

                connection.LastHeardFrom = Time.unscaledTime;
                Room?.HandlePacket(connection, message);
            }
        }

        foreach (NetConnection connection in dropped)
        {
            string detail = string.IsNullOrEmpty(connection.Transport.LastError)
                ? connection.Transport.State.ToString()
                : $"{connection.Transport.State}: {connection.Transport.LastError}";

            Debug.Log($"[Server] {connection.DisplayName} disconnected ({detail})");
            Room?.RemovePlayer(connection);
            connections.Remove(connection);
            connection.Close("dropped");
        }
    }

    public void Disconnect(NetConnection connection, string reason)
    {
        Room?.RemovePlayer(connection);
        connections.Remove(connection);
        connection.Close(reason);
    }

    public void Broadcast(NetWriter writer)
    {
        ArraySegment<byte> payload = writer.Segment;

        foreach (NetConnection connection in connections)
        {
            if (connection.Authenticated)
            {
                connection.Transport.SendBinary(payload);
            }
        }
    }

    private static string Escape(string value)
    {
        return value == null ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private void OnDestroy()
    {
        foreach (NetConnection connection in connections)
        {
            connection.Close("server shutting down");
        }

        connections.Clear();

        relayLink?.Close("server shutting down");
        relayLink?.Dispose();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnApplicationQuit()
    {

        if (registered)
        {
            StartCoroutine(Internal("DELETE", $"/internal/servers/{ServerId}", null, null));
        }
    }
}
