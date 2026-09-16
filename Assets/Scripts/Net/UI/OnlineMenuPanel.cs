using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class OnlineMenuPanel : MonoBehaviour
{
    private const float RefreshInterval = 10f;

    public static OnlineMenuPanel Instance { get; private set; }

    private Canvas canvas;
    private Transform regionList;
    private Transform friendList;
    private TextMeshProUGUI identityLabel;
    private TextMeshProUGUI statusLabel;
    private Button queueButton;
    private TMP_InputField codeField;
    private TMP_InputField friendField;

    private string activeTicketId = string.Empty;
    private bool matchEntered;
    private float nextFriendRefreshAt;
    private float nextTicketPollAt;
    private bool busy;
    private bool subscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {

        if (NetConfig.WantsServerRole())
        {
            return;
        }

        SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (scene.name != SceneFlow.MainMenu)
            {
                return;
            }

            GameObject host = new GameObject("~OnlineMenu");
            host.AddComponent<OnlineMenuPanel>();
        };

        if (SceneManager.GetActiveScene().name == SceneFlow.MainMenu)
        {
            GameObject host = new GameObject("~OnlineMenu");
            host.AddComponent<OnlineMenuPanel>();
        }
    }

    private void Start()
    {
        Instance = this;
        Build();
        SignIn();
    }

    private void Build()
    {
        canvas = NetUi.Canvas("OnlineMenuCanvas", 90);
        canvas.transform.SetParent(transform, false);

        RectTransform panel = NetUi.Box(
            canvas.transform,
            "Panel",
            new Vector2(1f, 1f),
            new Vector2(-24f, -24f),
            new Vector2(430f, 900f),
            NetUi.Panel);

        VerticalLayoutGroup column = NetUi.Column(panel, "Column", 8f, 16);

        identityLabel = NetUi.Label(column.transform, "Signing in...", 20f, Color.white);
        statusLabel = NetUi.Label(column.transform, string.Empty, 14f, NetUi.Dim);

        NetUi.Button(column.transform, "CHANGE NAME", PromptRename, null, 28f);

        Spacer(column.transform, 8f);
        NetUi.Label(column.transform, "REGIONS", 14f, NetUi.Accent);
        regionList = NewList(column.transform, "Regions", 130f);

        Spacer(column.transform, 8f);
        queueButton = NetUi.Button(column.transform, "FIND MATCH", ToggleQueue, NetUi.Accent * 0.6f, 42f);

        NetUi.Button(column.transform, "CREATE PRIVATE ROOM", CreatePrivateRoom, null, 32f);

        HorizontalLayoutGroup joinRow = NetUi.Row(column.transform, "JoinRow", 6f, 32f);
        codeField = NetUi.Field(joinRow.transform, "JOIN CODE", 32f);
        NetUi.Button(joinRow.transform, "JOIN", JoinByCode, null, 32f);

        Spacer(column.transform, 12f);
        NetUi.Label(column.transform, "FRIENDS", 14f, NetUi.Accent);

        HorizontalLayoutGroup addRow = NetUi.Row(column.transform, "AddRow", 6f, 32f);
        friendField = NetUi.Field(addRow.transform, "Name#0000", 32f);
        NetUi.Button(addRow.transform, "ADD", AddFriend, null, 32f);

        friendList = NewList(column.transform, "Friends", 340f);
    }

    private static void Spacer(Transform parent, float height)
    {
        GameObject go = new GameObject("Spacer");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        go.AddComponent<LayoutElement>().minHeight = height;
    }

    private static Transform NewList(Transform parent, string name, float height)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        VerticalLayoutGroup layout = go.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 3f;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childControlWidth = true;

        LayoutElement element = go.AddComponent<LayoutElement>();
        element.minHeight = height;
        element.flexibleHeight = 0f;

        return go.transform;
    }

    private void SignIn()
    {
        Status("Signing in...");

        AdvantageBackend.Instance.SignIn((ok, message) =>
        {
            if (!ok)
            {
                identityLabel.text = "OFFLINE";
                identityLabel.color = NetUi.Bad;
                Status(message);
                return;
            }

            BackendModels.Player player = AdvantageBackend.Instance.Player;
            identityLabel.text = player.handle;
            identityLabel.color = Color.white;
            Status("Signed in. Measuring regions...");

            Subscribe(BackendSocket.Instance);

            AdvantageBackend.Instance.RefreshRegions(RefreshRegionList);
            RefreshFriends();
        });
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextFriendRefreshAt && AdvantageBackend.Instance.IsSignedIn)
        {
            nextFriendRefreshAt = Time.unscaledTime + RefreshInterval;
            RefreshFriends();
        }

        if (!string.IsNullOrEmpty(activeTicketId) && Time.unscaledTime >= nextTicketPollAt)
        {
            nextTicketPollAt = Time.unscaledTime + 2f;
            AdvantageBackend.Instance.PollTicket(activeTicketId, HandleTicket);
        }
    }

    private void RefreshRegionList(bool ok)
    {
        if (regionList == null)
        {
            return;
        }

        NetUi.ClearChildren(regionList);

        if (!ok || AdvantageBackend.Instance.Regions.Count == 0)
        {
            NetUi.Label(regionList, "no regions reachable", 14f, NetUi.Bad);
            Status("Could not reach the region list.");
            return;
        }

        foreach (BackendModels.RegionInfo region in AdvantageBackend.Instance.Regions)
        {
            string ping = region.pingMs >= 0f ? $"{region.pingMs:0} ms" : "unreachable";
            string capacity = region.servers > 0 ? $"{region.servers} server(s)" : "no servers";

            HorizontalLayoutGroup row = NetUi.Row(regionList, region.id, 4f, 22f);
            NetUi.Label(row.transform, region.id, 14f, Color.white);
            NetUi.Label(row.transform, capacity, 12f, NetUi.Dim, TextAlignmentOptions.Center);
            NetUi.Label(row.transform, ping, 14f, NetUi.PingColor(region.pingMs), TextAlignmentOptions.Right);
        }

        Status("Ready.");
    }

    private void ToggleQueue()
    {
        if (busy)
        {
            return;
        }

        if (!string.IsNullOrEmpty(activeTicketId))
        {
            string cancelling = activeTicketId;
            activeTicketId = string.Empty;
            AdvantageBackend.Instance.CancelTicket(cancelling, () => Status("Search cancelled."));
            NetUi.SetButtonText(queueButton, "FIND MATCH");
            BackendSocket.Instance.SetPresence("menu");
            return;
        }

        BeginQueue();
    }

    public void BeginQueue()
    {
        if (busy || !string.IsNullOrEmpty(activeTicketId))
        {
            return;
        }

        if (!AdvantageBackend.Instance.IsSignedIn)
        {
            Status("Not signed in.");
            return;
        }

        busy = true;
        Status("Searching...");

        AdvantageBackend.Instance.QueueForMatch(Array.Empty<string>(), (ticket, error) =>
        {
            busy = false;

            if (ticket == null)
            {
                Status(error);
                return;
            }

            activeTicketId = ticket.id;
            matchEntered = false;
            NetUi.SetButtonText(queueButton, "CANCEL SEARCH");
            BackendSocket.Instance.SetPresence("queue");
            Status("Searching for a match...");
        });
    }

    private void HandleTicket(BackendModels.Ticket ticket)
    {
        if (ticket == null)
        {
            return;
        }

        switch (ticket.state)
        {
            case "matched" when ticket.match != null:
                activeTicketId = string.Empty;
                EnterMatch(ticket.match);
                break;

            case "failed":
                activeTicketId = string.Empty;
                NetUi.SetButtonText(queueButton, "FIND MATCH");
                Status(string.IsNullOrEmpty(ticket.reason) ? "No match found." : ticket.reason);
                break;

            case "searching":
                Status($"Searching... {ticket.waitSeconds}s");
                break;
        }
    }

    private void HandleMatchFound(BackendModels.MatchInfo match, int team)
    {
        activeTicketId = string.Empty;
        EnterMatch(match);
    }

    private void HandleMatchFailed(string reason)
    {
        activeTicketId = string.Empty;
        NetUi.SetButtonText(queueButton, "FIND MATCH");
        Status(string.IsNullOrEmpty(reason) ? "No match found." : reason);
    }

    private void EnterMatch(BackendModels.MatchInfo match)
    {
        if (matchEntered)
        {
            return;
        }

        if (match == null || string.IsNullOrEmpty(match.relayUrl) || string.IsNullOrEmpty(match.joinToken))
        {
            Status("The match handoff was incomplete.");
            return;
        }

        if (!Enum.TryParse(match.mode, true, out GameMode mode))
        {
            mode = GameMode.Convergence;
        }

        matchEntered = true;
        Status($"Joining {match.region}...");
        BackendSocket.Instance.SetPresence("match", match.roomId);
        NetClient.Join(match.relayUrl, match.joinToken, match.roomId, mode);
    }

    private void CreatePrivateRoom()
    {
        if (busy || !AdvantageBackend.Instance.IsSignedIn)
        {
            return;
        }

        string region = BestRegion();
        if (string.IsNullOrEmpty(region))
        {
            Status("No region available yet.");
            return;
        }

        busy = true;
        Status($"Opening a room in {region}...");

        AdvantageBackend.Instance.CreatePrivateRoom(region, MatchSettings.SelectedMode.ToString(), 4, true,
            (response, error) =>
            {
                busy = false;

                if (response == null)
                {
                    Status(error);
                    return;
                }

                Status($"Room {response.room.joinCode} - share that code.");
                NetContext.LocalPlayerIsHost = true;

                if (!Enum.TryParse(response.room.mode, true, out GameMode mode))
                {
                    mode = GameMode.Convergence;
                }

                NetClient.Join(response.relayUrl, response.joinToken, response.room.id, mode);
            });
    }

    private void JoinByCode()
    {
        string code = codeField != null ? codeField.text.Trim() : string.Empty;

        if (string.IsNullOrEmpty(code))
        {
            Status("Enter a room code first.");
            return;
        }

        busy = true;
        Status($"Joining {code.ToUpperInvariant()}...");

        AdvantageBackend.Instance.JoinRoomByCode(code, (response, error) =>
        {
            busy = false;

            if (response == null)
            {
                Status(error);
                return;
            }

            if (!Enum.TryParse(response.room.mode, true, out GameMode mode))
            {
                mode = GameMode.Convergence;
            }

            NetClient.Join(response.relayUrl, response.joinToken, response.room.id, mode);
        });
    }

    private void HandleRoomInvite(string code, BackendModels.Player from)
    {
        if (codeField != null)
        {
            codeField.text = code;
        }

        Status($"{from?.name ?? "A friend"} invited you - code {code}.");
    }

    private string BestRegion()
    {
        foreach (BackendModels.RegionInfo region in AdvantageBackend.Instance.Regions)
        {
            if (region.servers > 0)
            {
                return region.id;
            }
        }

        List<BackendModels.RegionInfo> regions = AdvantageBackend.Instance.Regions;
        return regions.Count > 0 ? regions[0].id : string.Empty;
    }

    private void AddFriend()
    {
        string handle = friendField != null ? friendField.text.Trim() : string.Empty;

        if (string.IsNullOrEmpty(handle) || !handle.Contains("#"))
        {
            Status("Friends are added by handle, like Nova#0417.");
            return;
        }

        AdvantageBackend.Instance.AddFriend(handle, (ok, error) =>
        {
            Status(ok ? $"Request sent to {handle}." : error);

            if (ok)
            {
                friendField.text = string.Empty;
                RefreshFriends();
            }
        });
    }

    private void RefreshFriends()
    {
        if (!AdvantageBackend.Instance.IsSignedIn)
        {
            return;
        }

        AdvantageBackend.Instance.FetchFriends(BuildFriendList);
    }

    private void BuildFriendList(BackendModels.FriendsResponse response)
    {
        if (friendList == null)
        {
            return;
        }

        NetUi.ClearChildren(friendList);

        if (response == null)
        {
            NetUi.Label(friendList, "could not load friends", 13f, NetUi.Bad);
            return;
        }

        if (response.incoming != null && response.incoming.Length > 0)
        {
            NetUi.Label(friendList, "REQUESTS", 12f, NetUi.Warn);

            foreach (BackendModels.FriendEntry entry in response.incoming)
            {
                BackendModels.FriendEntry captured = entry;
                HorizontalLayoutGroup row = NetUi.Row(friendList, "Incoming", 4f, 26f);
                NetUi.Label(row.transform, entry.player.handle, 13f, Color.white);
                NetUi.Button(row.transform, "ACCEPT", () => Accept(captured), NetUi.Good * 0.5f, 24f);
                NetUi.Button(row.transform, "X", () => Remove(captured), null, 24f);
            }
        }

        if (response.friends == null || response.friends.Length == 0)
        {
            NetUi.Label(friendList, "no friends yet - add one by handle", 13f, NetUi.Dim);
        }
        else
        {
            NetUi.Label(friendList, "FRIENDS", 12f, NetUi.Dim);

            foreach (BackendModels.FriendEntry entry in response.friends)
            {
                BackendModels.FriendEntry captured = entry;

                HorizontalLayoutGroup row = NetUi.Row(friendList, entry.player.name, 4f, 26f);
                NetUi.Label(row.transform, entry.player.handle, 13f, Color.white);
                NetUi.Label(row.transform, entry.presence ?? "offline", 12f, PresenceColor(entry.presence), TextAlignmentOptions.Center);

                if (entry.presence == "menu" || entry.presence == "queue")
                {
                    NetUi.Button(row.transform, "INVITE", () => Invite(captured), null, 24f);
                }

                NetUi.Button(row.transform, "X", () => Remove(captured), null, 24f);
            }
        }

        if (response.outgoing != null && response.outgoing.Length > 0)
        {
            NetUi.Label(friendList, "SENT", 12f, NetUi.Dim);

            foreach (BackendModels.FriendEntry entry in response.outgoing)
            {
                NetUi.Label(friendList, $"{entry.player.handle} - pending", 12f, NetUi.Dim);
            }
        }
    }

    private static Color PresenceColor(string presence)
    {
        switch (presence)
        {
            case "match": return NetUi.Accent;
            case "queue": return NetUi.Warn;
            case "menu": return NetUi.Good;
            default: return NetUi.Dim;
        }
    }

    private void Accept(BackendModels.FriendEntry entry)
    {
        AdvantageBackend.Instance.AcceptFriend(entry.player.id, _ => RefreshFriends());
    }

    private void Remove(BackendModels.FriendEntry entry)
    {
        AdvantageBackend.Instance.RemoveFriend(entry.player.id, _ => RefreshFriends());
    }

    private void Invite(BackendModels.FriendEntry entry)
    {
        AdvantageBackend.Instance.InviteToParty(entry.player.id,
            ok => Status(ok ? $"Invited {entry.player.name}." : "Could not send that invite."));
    }

    private void PromptRename()
    {

        string wanted = codeField != null ? codeField.text.Trim() : string.Empty;

        if (string.IsNullOrEmpty(wanted))
        {
            Status("Type a new name in the code box, then press CHANGE NAME.");
            return;
        }

        AdvantageBackend.Instance.Rename(wanted, (ok, error) =>
        {
            if (ok)
            {
                identityLabel.text = AdvantageBackend.Instance.Player.handle;
                codeField.text = string.Empty;
                Status("Name changed.");
            }
            else
            {
                Status(error);
            }
        });
    }

    private void Status(string message)
    {
        if (statusLabel != null)
        {
            statusLabel.text = message ?? string.Empty;
        }
    }

    private void Subscribe(BackendSocket socket)
    {
        if (socket == null || subscribed)
        {
            return;
        }

        subscribed = true;
        socket.MatchFound += HandleMatchFound;
        socket.MatchFailed += HandleMatchFailed;
        socket.FriendRequestReceived += HandleFriendChanged;
        socket.FriendAccepted += HandleFriendChanged;
        socket.FriendRemoved += HandleFriendRemoved;
        socket.FriendPresenceChanged += HandlePresenceChanged;
        socket.RoomInvited += HandleRoomInvite;
        socket.Connect();
    }

    private void HandleFriendChanged(BackendModels.Player player)
    {
        RefreshFriends();
    }

    private void HandleFriendRemoved(string playerId)
    {
        RefreshFriends();
    }

    private void HandlePresenceChanged(string playerId, string presence)
    {
        RefreshFriends();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (!subscribed)
        {
            return;
        }

        BackendSocket socket = BackendSocket.Instance;
        if (socket == null)
        {
            return;
        }

        socket.MatchFound -= HandleMatchFound;
        socket.MatchFailed -= HandleMatchFailed;
        socket.FriendRequestReceived -= HandleFriendChanged;
        socket.FriendAccepted -= HandleFriendChanged;
        socket.FriendRemoved -= HandleFriendRemoved;
        socket.FriendPresenceChanged -= HandlePresenceChanged;
        socket.RoomInvited -= HandleRoomInvite;
    }
}
