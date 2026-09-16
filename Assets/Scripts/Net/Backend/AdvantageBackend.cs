using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

public class AdvantageBackend : MonoBehaviour
{
    private const int TimeoutSeconds = 15;

    private static AdvantageBackend instance;

    public static AdvantageBackend Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject host = new GameObject("~AdvantageBackend");
                instance = host.AddComponent<AdvantageBackend>();
                DontDestroyOnLoad(host);
            }

            return instance;
        }
    }

    public BackendModels.Player Player { get; private set; }

    public bool IsSignedIn => Player != null && !string.IsNullOrEmpty(DeviceIdentity.SessionToken);

    public List<BackendModels.RegionInfo> Regions { get; } = new List<BackendModels.RegionInfo>();

    public event Action<BackendModels.Player> SignedIn;
    public event Action<string> RequestFailed;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SignIn(Action<bool, string> done)
    {
        StartCoroutine(SignInRoutine(done));
    }

    private IEnumerator SignInRoutine(Action<bool, string> done)
    {

        if (!string.IsNullOrEmpty(DeviceIdentity.SessionToken))
        {
            bool resumed = false;

            yield return Send("GET", "/me", null, (ok, body) =>
            {
                if (!ok)
                {
                    return;
                }

                BackendModels.MeResponse parsed = Parse<BackendModels.MeResponse>(body);
                if (parsed?.player == null)
                {
                    return;
                }

                Player = parsed.player;
                resumed = true;
            });

            if (resumed)
            {
                OnSignedIn();
                done?.Invoke(true, string.Empty);
                yield break;
            }

            DeviceIdentity.ClearSession();
        }

        string payload = BuildJson(
            ("deviceId", DeviceIdentity.DeviceId),
            ("name", DeviceIdentity.PreferredName));

        string failure = "could not reach the server";
        bool success = false;

        yield return Send("POST", "/auth/guest", payload, (ok, body) =>
        {
            if (!ok)
            {
                failure = ErrorMessage(body, failure);
                return;
            }

            BackendModels.LoginResponse parsed = Parse<BackendModels.LoginResponse>(body);
            if (parsed == null || string.IsNullOrEmpty(parsed.token))
            {
                failure = "the server sent a login we could not read";
                return;
            }

            DeviceIdentity.SessionToken = parsed.token;
            Player = parsed.player;
            success = true;
        });

        if (success)
        {
            OnSignedIn();
        }

        done?.Invoke(success, success ? string.Empty : failure);
    }

    private void OnSignedIn()
    {
        if (Player == null)
        {
            return;
        }

        DeviceIdentity.PreferredName = Player.name;
        MatchSettings.PlayerName = Player.name;
        NetContext.SetSession(NetContext.RoomId, Player.id);
        SignedIn?.Invoke(Player);
    }

    public void Rename(string name, Action<bool, string> done)
    {
        StartCoroutine(Send("PATCH", "/me", BuildJson(("name", name)), (ok, body) =>
        {
            if (ok)
            {
                BackendModels.MeResponse parsed = Parse<BackendModels.MeResponse>(body);
                if (parsed?.player != null)
                {
                    Player = parsed.player;
                    OnSignedIn();
                }
            }

            done?.Invoke(ok, ok ? string.Empty : ErrorMessage(body, "could not change your name"));
        }));
    }

    public void RefreshRegions(Action<bool> done)
    {
        StartCoroutine(RefreshRegionsRoutine(done));
    }

    private IEnumerator RefreshRegionsRoutine(Action<bool> done)
    {
        BackendModels.RegionsResponse regions = null;

        yield return Send("GET", "/regions", null, (ok, body) =>
        {
            if (ok)
            {
                regions = Parse<BackendModels.RegionsResponse>(body);
            }
        });

        if (regions?.regions == null)
        {
            done?.Invoke(false);
            yield break;
        }

        Regions.Clear();
        Regions.AddRange(regions.regions);

        foreach (BackendModels.RegionInfo region in Regions)
        {
            yield return MeasureRegion(region);
        }

        Regions.Sort((a, b) =>
        {
            float left = a.pingMs < 0f ? float.MaxValue : a.pingMs;
            float right = b.pingMs < 0f ? float.MaxValue : b.pingMs;
            return left.CompareTo(right);
        });

        done?.Invoke(true);
    }

    private IEnumerator MeasureRegion(BackendModels.RegionInfo region)
    {
        region.pingMs = -1f;

        if (string.IsNullOrEmpty(region.relayUrl))
        {
            yield break;
        }

        string pingUrl = $"{HttpFromWs(region.relayUrl)}/ping";
        float best = float.MaxValue;

        for (int attempt = 0; attempt < 3; attempt++)
        {
            Stopwatch clock = Stopwatch.StartNew();

            using (UnityWebRequest request = UnityWebRequest.Get(pingUrl))
            {
                request.timeout = 5;
                yield return request.SendWebRequest();

                clock.Stop();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    best = Mathf.Min(best, (float)clock.Elapsed.TotalMilliseconds);
                }
            }
        }

        if (best < float.MaxValue)
        {
            region.pingMs = best;
        }
    }

    public string[] PreferredRegionIds()
    {
        List<string> ids = new List<string>();

        foreach (BackendModels.RegionInfo region in Regions)
        {
            if (region.pingMs >= 0f)
            {
                ids.Add(region.id);
            }
        }

        if (ids.Count == 0)
        {
            foreach (BackendModels.RegionInfo region in Regions)
            {
                ids.Add(region.id);
            }
        }

        return ids.ToArray();
    }

    public void QueueForMatch(string[] modes, Action<BackendModels.Ticket, string> done)
    {
        BackendModels.QueueRequest request = new BackendModels.QueueRequest
        {
            modes = modes ?? Array.Empty<string>(),
            regions = PreferredRegionIds(),
        };

        StartCoroutine(Send("POST", "/matchmaking/queue", JsonUtility.ToJson(request), (ok, body) =>
        {
            BackendModels.TicketResponse parsed = ok ? Parse<BackendModels.TicketResponse>(body) : null;
            done?.Invoke(parsed?.ticket, ok ? string.Empty : ErrorMessage(body, "could not join the queue"));
        }));
    }

    public void PollTicket(string ticketId, Action<BackendModels.Ticket> done)
    {
        StartCoroutine(Send("GET", $"/matchmaking/ticket/{ticketId}", null, (ok, body) =>
        {
            BackendModels.TicketResponse parsed = ok ? Parse<BackendModels.TicketResponse>(body) : null;
            done?.Invoke(parsed?.ticket);
        }));
    }

    public void CancelTicket(string ticketId, Action done = null)
    {
        StartCoroutine(Send("DELETE", $"/matchmaking/ticket/{ticketId}", null, (_, __) => done?.Invoke()));
    }

    public void CreatePrivateRoom(string region, string mode, int teamSize, bool bots, Action<BackendModels.JoinResponse, string> done)
    {
        string payload = "{"
            + $"\"region\":{Quote(region)},"
            + $"\"mode\":{Quote(mode)},"
            + $"\"teamSize\":{teamSize},"
            + $"\"botsEnabled\":{(bots ? "true" : "false")}"
            + "}";

        StartCoroutine(Send("POST", "/rooms", payload, (ok, body) =>
        {
            BackendModels.JoinResponse parsed = ok ? Parse<BackendModels.JoinResponse>(body) : null;
            done?.Invoke(parsed, ok ? string.Empty : ErrorMessage(body, "could not create the room"));
        }));
    }

    public void JoinRoomByCode(string code, Action<BackendModels.JoinResponse, string> done)
    {
        StartCoroutine(Send("POST", "/rooms/join", BuildJson(("code", code)), (ok, body) =>
        {
            BackendModels.JoinResponse parsed = ok ? Parse<BackendModels.JoinResponse>(body) : null;
            done?.Invoke(parsed, ok ? string.Empty : ErrorMessage(body, "could not join that room"));
        }));
    }

    public void InviteToRoom(string roomId, string playerId, Action<bool> done = null)
    {
        StartCoroutine(Send("POST", $"/rooms/{roomId}/invite", BuildJson(("playerId", playerId)),
            (ok, _) => done?.Invoke(ok)));
    }

    public void FetchFriends(Action<BackendModels.FriendsResponse> done)
    {
        StartCoroutine(Send("GET", "/friends", null, (ok, body) =>
        {
            done?.Invoke(ok ? Parse<BackendModels.FriendsResponse>(body) : null);
        }));
    }

    public void AddFriend(string handle, Action<bool, string> done)
    {
        StartCoroutine(Send("POST", "/friends/request", BuildJson(("handle", handle)), (ok, body) =>
        {
            done?.Invoke(ok, ok ? string.Empty : ErrorMessage(body, "could not send that request"));
        }));
    }

    public void AcceptFriend(string playerId, Action<bool> done = null)
    {
        StartCoroutine(Send("POST", $"/friends/{playerId}/accept", null, (ok, _) => done?.Invoke(ok)));
    }

    public void RemoveFriend(string playerId, Action<bool> done = null)
    {
        StartCoroutine(Send("DELETE", $"/friends/{playerId}", null, (ok, _) => done?.Invoke(ok)));
    }

    public void BlockPlayer(string playerId, Action<bool> done = null)
    {
        StartCoroutine(Send("POST", $"/friends/{playerId}/block", null, (ok, _) => done?.Invoke(ok)));
    }

    public void InviteToParty(string playerId, Action<bool> done = null)
    {
        StartCoroutine(Send("POST", "/party/invite", BuildJson(("playerId", playerId)), (ok, _) => done?.Invoke(ok)));
    }

    public void JoinParty(string partyId, Action<bool> done = null)
    {
        StartCoroutine(Send("POST", "/party/join", BuildJson(("partyId", partyId)), (ok, _) => done?.Invoke(ok)));
    }

    public void LeaveParty(Action<bool> done = null)
    {
        StartCoroutine(Send("POST", "/party/leave", null, (ok, _) => done?.Invoke(ok)));
    }

    private IEnumerator Send(string method, string path, string body, Action<bool, string> done)
    {
        string url = NetConfig.GatewayUrl + path;

        using UnityWebRequest request = new UnityWebRequest(url, method);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.timeout = TimeoutSeconds;

        if (!string.IsNullOrEmpty(body))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            request.SetRequestHeader("Content-Type", "application/json");
        }

        string token = DeviceIdentity.SessionToken;
        if (!string.IsNullOrEmpty(token))
        {
            request.SetRequestHeader("Authorization", $"Bearer {token}");
        }

        yield return request.SendWebRequest();

        string text = request.downloadHandler?.text ?? string.Empty;
        bool ok = request.result == UnityWebRequest.Result.Success;

        if (!ok)
        {
            string reason = ErrorMessage(text, request.error);
            Debug.LogWarning($"[Backend] {method} {path} failed: {reason}");
            RequestFailed?.Invoke(reason);
        }

        done?.Invoke(ok, text);
    }

    private static T Parse<T>(string json) where T : class
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            return JsonUtility.FromJson<T>(json);
        }
        catch (Exception err)
        {
            Debug.LogWarning($"[Backend] could not parse {typeof(T).Name}: {err.Message}");
            return null;
        }
    }

    private static string ErrorMessage(string body, string fallback)
    {
        BackendModels.ErrorResponse parsed = Parse<BackendModels.ErrorResponse>(body);
        return string.IsNullOrEmpty(parsed?.message) ? fallback : parsed.message;
    }

    private static string BuildJson(params (string key, string value)[] fields)
    {
        StringBuilder builder = new StringBuilder("{");
        bool first = true;

        foreach ((string key, string value) in fields)
        {
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            if (!first)
            {
                builder.Append(',');
            }

            builder.Append(Quote(key)).Append(':').Append(Quote(value));
            first = false;
        }

        return builder.Append('}').ToString();
    }

    private static string Quote(string value)
    {
        if (value == null)
        {
            return "null";
        }

        StringBuilder builder = new StringBuilder("\"");

        foreach (char c in value)
        {
            switch (c)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if (c < 0x20)
                    {
                        builder.Append("\\u").Append(((int)c).ToString("x4"));
                    }
                    else
                    {
                        builder.Append(c);
                    }

                    break;
            }
        }

        return builder.Append('"').ToString();
    }

    public static string HttpFromWs(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return string.Empty;
        }

        if (url.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
        {
            return "https://" + url.Substring(6);
        }

        if (url.StartsWith("ws://", StringComparison.OrdinalIgnoreCase))
        {
            return "http://" + url.Substring(5);
        }

        return url;
    }

    public static string WsFromHttp(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return string.Empty;
        }

        if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return "wss://" + url.Substring(8);
        }

        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            return "ws://" + url.Substring(7);
        }

        return url;
    }
}
