using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class NetClient : MonoBehaviour
{

    private const int InputRedundancy = 3;

    private const float EntityTimeout = 3f;

    public static NetClient Instance { get; private set; }

    private WebSocketTransport transport;
    private readonly NetWriter writer = new NetWriter(2048);
    private readonly NetReader reader = new NetReader();

    private readonly Dictionary<ushort, NetAvatar> avatars = new Dictionary<ushort, NetAvatar>();
    private readonly Dictionary<ushort, float> lastSeen = new Dictionary<ushort, float>();
    private readonly List<NetInput> inputHistory = new List<NetInput>(InputRedundancy);
    private readonly List<ushort> despawnScratch = new List<ushort>();

    private readonly Dictionary<uint, Vector3> predictedPositions = new Dictionary<uint, Vector3>();
    private readonly List<uint> predictionPruneScratch = new List<uint>();

    private string relayUrl = string.Empty;
    private string joinToken = string.Empty;
    private string sceneName = string.Empty;

    private ushort localEntityId;
    private uint inputTick;
    private uint lastAckedTick;
    private float nextInputAt;

    private bool welcomed;
    private bool sceneLoaded;
    private bool localSpawned;

    private PlayerController localAvatar;
    private Rigidbody localBody;
    private Vector3 pendingCorrection;

    private bool reloadLatched;
    private byte abilityLatched;
    private byte weaponSlotLatched;

    public float Ping { get; private set; }

    public float PredictionError { get; private set; }

    public uint AcknowledgedInputTick => lastAckedTick;

    public bool Connected => transport != null && transport.State == TransportState.Open;

    public string DisconnectReason { get; private set; } = string.Empty;

    public GameMode Mode { get; private set; }

    public int TeamSize { get; private set; } = 4;

    public bool BotsEnabled { get; private set; } = true;

    public bool IsHost { get; private set; }

    public Team LocalTeam { get; private set; } = Team.Attackers;

    public readonly Dictionary<ushort, RosterEntry> Roster = new Dictionary<ushort, RosterEntry>();

    public static NetClient Join(string relayUrl, string joinToken, string roomId, GameMode mode)
    {
        if (Instance == null)
        {
            GameObject host = new GameObject("~NetClient");
            Instance = host.AddComponent<NetClient>();
            DontDestroyOnLoad(host);
        }

        Instance.Begin(relayUrl, joinToken, roomId, mode);
        return Instance;
    }

    private void Begin(string relay, string token, string roomId, GameMode mode)
    {
        relayUrl = relay;
        joinToken = token;
        Mode = mode;

        NetContext.SetRole(NetRole.Client);
        NetContext.SetSession(roomId, AdvantageBackend.Instance.Player?.id ?? string.Empty);

        MatchSettings.SelectedMode = mode;
        sceneName = GameModeInfo.SceneFor(mode);
        sceneLoaded = false;
        localSpawned = false;
        welcomed = false;

        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.LoadScene(sceneName);

        string url = $"{relayUrl}/play?token={System.Uri.EscapeDataString(token)}";
        transport = new WebSocketTransport();
        transport.Connect(url);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == sceneName)
        {
            sceneLoaded = true;
        }
    }

    private void Update()
    {
        if (transport == null)
        {
            return;
        }

        LatchEdges();
        Pump();

        if (transport.State == TransportState.Open)
        {
            if (!welcomed)
            {
                SendHello();
            }

            EnsureLocalAvatar();
            SendInputs();
            ApplyCorrection();
        }
        else if (transport.State == TransportState.Failed || transport.State == TransportState.Closed)
        {
            if (string.IsNullOrEmpty(DisconnectReason))
            {
                DisconnectReason = string.IsNullOrEmpty(transport.LastError)
                    ? "connection to the match was lost"
                    : transport.LastError;

                Debug.LogWarning($"[Client] disconnected: {DisconnectReason}");
            }
        }

        ExpireEntities();
    }

    private void SendHello()
    {
        welcomed = true;

        writer.Reset();
        writer.WriteByte((byte)ClientOp.Hello);
        writer.WriteByte(NetConfig.ProtocolVersion);
        writer.WriteString(joinToken);
        transport.SendBinary(writer.Segment);
    }

    private void LatchEdges()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (keyboard == null)
        {
            return;
        }

        if (keyboard.rKey.wasPressedThisFrame)
        {
            reloadLatched = true;
        }

        if (keyboard.eKey.wasPressedThisFrame)
        {
            abilityLatched |= InputAbility.Primary;
        }

        if (keyboard.leftShiftKey.wasPressedThisFrame || keyboard.rightShiftKey.wasPressedThisFrame)
        {
            abilityLatched |= InputAbility.Secondary;
        }

        if (keyboard.fKey.wasPressedThisFrame
            || keyboard.spaceKey.wasPressedThisFrame
            || (mouse != null && mouse.rightButton.wasPressedThisFrame))
        {
            abilityLatched |= InputAbility.Special;
        }

        if (keyboard.qKey.wasPressedThisFrame)
        {
            abilityLatched |= InputAbility.Ultimate;
        }

        if (keyboard.digit1Key.wasPressedThisFrame) weaponSlotLatched = 1;
        else if (keyboard.digit2Key.wasPressedThisFrame) weaponSlotLatched = 2;
        else if (keyboard.digit3Key.wasPressedThisFrame) weaponSlotLatched = 3;
    }

    private void SendInputs()
    {
        if (Time.unscaledTime < nextInputAt || localAvatar == null)
        {
            return;
        }

        nextInputAt = Time.unscaledTime + (1f / NetConfig.InputSendRate);
        inputTick++;

        NetInput input = SampleInput();
        predictedPositions[input.tick] = input.predictedPosition;

        inputHistory.Add(input);
        while (inputHistory.Count > InputRedundancy)
        {
            inputHistory.RemoveAt(0);
        }

        writer.Reset();
        writer.WriteByte((byte)ClientOp.Input);
        writer.WriteByte((byte)inputHistory.Count);

        foreach (NetInput historic in inputHistory)
        {
            historic.Write(writer);
        }

        transport.SendBinary(writer.Segment);
    }

    private NetInput SampleInput()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        Gamepad gamepad = Gamepad.current;

        Vector2 move = Vector2.zero;
        byte buttons = 0;

        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move.y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move.y -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move.x += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move.x -= 1f;

            if (keyboard.spaceKey.isPressed) buttons |= InputButton.Jump;
            if (keyboard.leftCtrlKey.isPressed || keyboard.cKey.isPressed) buttons |= InputButton.Descend;
            if (keyboard.leftShiftKey.isPressed) buttons |= InputButton.Crouch;
            if (keyboard.eKey.isPressed) buttons |= InputButton.Interact;
        }

        if (gamepad != null)
        {
            Vector2 stick = gamepad.leftStick.ReadValue();
            if (stick.sqrMagnitude > move.sqrMagnitude)
            {
                move = stick;
            }

            if (gamepad.buttonSouth.isPressed) buttons |= InputButton.Jump;
            if (gamepad.rightTrigger.ReadValue() > 0.5f) buttons |= InputButton.Fire;
            if (gamepad.leftTrigger.ReadValue() > 0.5f) buttons |= InputButton.Aim;
        }

        if (mouse != null)
        {
            if (mouse.leftButton.isPressed) buttons |= InputButton.Fire;
            if (mouse.rightButton.isPressed) buttons |= InputButton.Aim;
        }

        if (reloadLatched)
        {
            buttons |= InputButton.Reload;
            reloadLatched = false;
        }

        NetInput input = new NetInput
        {
            tick = inputTick,
            yaw = localAvatar.Look != null ? localAvatar.Look.Yaw : localAvatar.transform.eulerAngles.y,
            pitch = localAvatar.Look != null ? localAvatar.Look.Pitch : 0f,
            moveX = Mathf.Clamp(move.x, -1f, 1f),
            moveY = Mathf.Clamp(move.y, -1f, 1f),
            buttons = buttons,
            abilities = abilityLatched,
            weaponSlot = weaponSlotLatched,
            predictedPosition = localAvatar.transform.position,
        };

        abilityLatched = 0;
        weaponSlotLatched = 0;

        return input;
    }

    private void Pump()
    {
        while (transport.TryReceive(out TransportMessage message))
        {
            if (message.isText || message.length == 0)
            {
                continue;
            }

            reader.Reset(message.data, 0, message.length);
            ServerOp op = (ServerOp)reader.ReadByte();

            switch (op)
            {
                case ServerOp.Welcome: ReadWelcome(); break;
                case ServerOp.Snapshot: ReadSnapshot(); break;
                case ServerOp.MatchState: ReadMatchState(); break;
                case ServerOp.Roster: ReadRoster(); break;
                case ServerOp.Event: ReadEvent(); break;
                case ServerOp.Ping: ReadPing(); break;
                case ServerOp.Reject: ReadReject(); break;
            }
        }
    }

    private void ReadWelcome()
    {
        byte version = reader.ReadByte();
        localEntityId = reader.ReadUShort();
        reader.ReadUInt();
        reader.ReadByte();

        Mode = (GameMode)reader.ReadByte();
        TeamSize = reader.ReadByte();
        LocalTeam = (Team)reader.ReadByte();
        IsHost = reader.ReadBool();
        string roomId = reader.ReadString();

        if (version != NetConfig.ProtocolVersion)
        {
            DisconnectReason = $"the server speaks protocol {version}, this build speaks {NetConfig.ProtocolVersion}";
            Debug.LogError($"[Client] {DisconnectReason}");
            Leave();
            return;
        }

        NetContext.LocalPlayerIsHost = IsHost;
        NetContext.SetSession(roomId, NetContext.LocalPlayerId);
        MatchSettings.PlayerTeam = LocalTeam;
        MatchSettings.TeamSize = TeamSize;

        Debug.Log($"[Client] welcomed into {roomId} as entity {localEntityId} on {LocalTeam}");
    }

    private void ReadSnapshot()
    {
        reader.ReadUInt();
        int count = reader.ReadByte();

        EntityState ownState = default;
        bool hasOwnState = false;

        for (int i = 0; i < count; i++)
        {
            EntityState state = EntityState.Read(reader);
            if (!reader.Ok)
            {
                return;
            }

            lastSeen[state.id] = Time.time;

            if (state.id == localEntityId)
            {
                ownState = state;
                hasOwnState = true;
                continue;
            }

            NetAvatar avatar = EnsureAvatar(state);
            avatar?.Push(state);
        }

        lastAckedTick = reader.ReadUInt();

        if (hasOwnState)
        {
            Reconcile(ownState);
        }
    }

    private void Reconcile(EntityState state)
    {
        if (localAvatar == null)
        {
            return;
        }

        Vector3 error;

        if (predictedPositions.TryGetValue(lastAckedTick, out Vector3 predictedAtAck))
        {
            error = state.position - predictedAtAck;
        }
        else
        {
            error = state.position - localAvatar.transform.position;
        }

        PredictionError = error.magnitude;

        if (PredictionError > NetConfig.HardCorrectionDistance)
        {
            pendingCorrection = Vector3.zero;
            localAvatar.transform.position = state.position;

            if (localBody != null)
            {
                localBody.position = state.position;
                localBody.linearVelocity = state.velocity;
            }
        }
        else
        {
            pendingCorrection = error;
        }

        localAvatar.Health.SetNetworkPools(state.health, state.armor, state.Has(EntityFlag.Alive));

        PrunePredictionsUpTo(lastAckedTick);
    }

    private void PrunePredictionsUpTo(uint ackedTick)
    {
        if (predictedPositions.Count == 0)
        {
            return;
        }

        predictionPruneScratch.Clear();

        foreach (uint tick in predictedPositions.Keys)
        {
            if (tick <= ackedTick)
            {
                predictionPruneScratch.Add(tick);
            }
        }

        foreach (uint tick in predictionPruneScratch)
        {
            predictedPositions.Remove(tick);
        }
    }

    private void ApplyCorrection()
    {
        if (pendingCorrection.sqrMagnitude < 0.000001f || localAvatar == null)
        {
            return;
        }

        Vector3 step = pendingCorrection * Mathf.Clamp01(NetConfig.CorrectionSmoothing * Time.deltaTime);
        pendingCorrection -= step;

        if (localBody != null)
        {
            localBody.position += step;
        }
        else
        {
            localAvatar.transform.position += step;
        }
    }

    private NetAvatar EnsureAvatar(EntityState state)
    {
        if (avatars.TryGetValue(state.id, out NetAvatar existing) && existing != null)
        {
            return existing;
        }

        MatchManager match = MatchManager.Instance;
        if (match == null || !sceneLoaded)
        {
            return null;
        }

        string name = Roster.TryGetValue(state.id, out RosterEntry entry) ? entry.name : $"Player {state.id}";

        PlayerController avatar = match.SpawnNetworkAvatar(
            (Team)state.team,
            name,
            OperativeRoster.HeroIndexOf((OperativeId)state.operative),
            state.id,
            false);

        if (avatar == null)
        {
            return null;
        }

        NetAvatar net = avatar.gameObject.AddComponent<NetAvatar>();
        net.Initialise(state.id, state.Has(EntityFlag.Bot), avatar);

        avatars[state.id] = net;
        return net;
    }

    private void EnsureLocalAvatar()
    {
        if (localSpawned || !sceneLoaded || localEntityId == 0)
        {
            return;
        }

        MatchManager match = MatchManager.Instance;
        if (match == null)
        {
            return;
        }

        localAvatar = match.SpawnNetworkAvatar(
            LocalTeam,
            AdvantageBackend.Instance.Player?.name ?? MatchSettings.PlayerName,
            MatchSettings.HeroIndex,
            0,
            true);

        if (localAvatar == null)
        {
            return;
        }

        localSpawned = true;
        localBody = localAvatar.GetComponent<Rigidbody>();
        match.SetLocalPlayer(localAvatar);

        SendHeroPick(MatchSettings.HeroIndex);
    }

    private void ExpireEntities()
    {
        if (avatars.Count == 0)
        {
            return;
        }

        despawnScratch.Clear();

        foreach (KeyValuePair<ushort, NetAvatar> entry in avatars)
        {
            if (!lastSeen.TryGetValue(entry.Key, out float seen) || Time.time - seen > EntityTimeout)
            {
                despawnScratch.Add(entry.Key);
            }
        }

        foreach (ushort id in despawnScratch)
        {
            if (avatars.TryGetValue(id, out NetAvatar avatar) && avatar != null)
            {
                MatchManager.Instance?.DespawnNetworkAvatar(avatar.GetComponent<PlayerController>());
            }

            avatars.Remove(id);
            lastSeen.Remove(id);
        }
    }

    private void ReadMatchState()
    {
        MatchStateMessage state = MatchStateMessage.Read(reader);
        if (!reader.Ok)
        {
            return;
        }

        TeamSize = state.teamSize;
        BotsEnabled = state.botsEnabled;

        MatchManager match = MatchManager.Instance;
        if (match == null)
        {
            return;
        }

        match.ApplyNetworkMatchState(
            (MatchManager.Phase)state.phase,
            state.phaseIsTimed,
            state.phaseSecondsRemaining,
            state.roundNumber,
            state.attackerScore,
            state.defenderScore);

        match.Objective?.ReadNetProgress(state.objectiveA, state.objectiveB, state.objectiveC);
    }

    private void ReadRoster()
    {
        int count = reader.ReadByte();
        Roster.Clear();

        for (int i = 0; i < count; i++)
        {
            RosterEntry entry = RosterEntry.Read(reader);
            if (!reader.Ok)
            {
                return;
            }

            Roster[entry.entityId] = entry;

            if (avatars.TryGetValue(entry.entityId, out NetAvatar avatar) && avatar != null)
            {
                avatar.SetOperative((OperativeId)entry.operative);
            }
        }
    }

    private void ReadEvent()
    {
        NetEventKind kind = (NetEventKind)reader.ReadByte();

        switch (kind)
        {
            case NetEventKind.Kill:
            {
                string killer = reader.ReadString();
                string victim = reader.ReadString();
                string weapon = reader.ReadString();
                Team team = (Team)reader.ReadByte();
                bool headshot = reader.ReadBool();

                MatchManager.Instance?.RaiseNetworkKill(killer, victim, weapon, team, headshot);
                break;
            }

            case NetEventKind.HitConfirm:
            {
                float damage = reader.ReadFloat();
                bool headshot = reader.ReadBool();
                bool killed = reader.ReadBool();

                localAvatar?.RaiseNetworkHitConfirm(damage, headshot, killed);
                break;
            }

            case NetEventKind.RoomSettings:
            {
                BotsEnabled = reader.ReadBool();
                TeamSize = reader.ReadByte();
                Mode = (GameMode)reader.ReadByte();
                reader.ReadBool();
                break;
            }
        }
    }

    private void ReadPing()
    {
        long serverTime = reader.ReadLong();

        Ping = reader.ReadUShort() / 1000f;

        writer.Reset();
        writer.WriteByte((byte)ClientOp.Pong);
        writer.WriteLong(serverTime);
        transport.SendBinary(writer.Segment);
    }

    private void ReadReject()
    {
        DisconnectReason = reader.ReadString();
        Debug.LogError($"[Client] rejected by the server: {DisconnectReason}");
        Leave();
    }

    public void SendHeroPick(int heroIndex)
    {
        if (!Connected)
        {
            return;
        }

        writer.Reset();
        writer.WriteByte((byte)ClientOp.HeroPick);
        writer.WriteByte((byte)Mathf.Clamp(heroIndex, 0, 255));
        transport.SendBinary(writer.Segment);
    }

    public void SendReady()
    {
        if (!Connected)
        {
            return;
        }

        writer.Reset();
        writer.WriteByte((byte)ClientOp.Ready);
        transport.SendBinary(writer.Segment);
    }

    public void SendRoomCommand(RoomCommand command, int argument = 0)
    {
        if (!Connected)
        {
            return;
        }

        writer.Reset();
        writer.WriteByte((byte)ClientOp.RoomCommand);
        writer.WriteByte((byte)command);
        writer.WriteInt(argument);
        transport.SendBinary(writer.Segment);
    }

    public void Leave()
    {
        if (transport != null && transport.State == TransportState.Open)
        {
            writer.Reset();
            writer.WriteByte((byte)ClientOp.Leave);
            transport.SendBinary(writer.Segment);
        }

        Shutdown();
    }

    private void Shutdown()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        transport?.Close("left the match");
        transport?.Dispose();
        transport = null;

        avatars.Clear();
        lastSeen.Clear();
        Roster.Clear();
        predictedPositions.Clear();
        pendingCorrection = Vector3.zero;
        localAvatar = null;
        localBody = null;
        localSpawned = false;

        NetContext.Reset();
    }

    private void OnDestroy()
    {
        Shutdown();

        if (Instance == this)
        {
            Instance = null;
        }
    }
}
