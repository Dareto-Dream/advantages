using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class NetRoom
{
    private const float StateBroadcastInterval = 0.5f;
    private const float PingInterval = 1f;
    private const float GatewaySyncInterval = 5f;
    private const float JoinGraceSeconds = 8f;

    private readonly NetServer server;
    private readonly NetWriter writer = new NetWriter(8192);
    private readonly NetReader reader = new NetReader();
    private readonly List<NetInput> inputScratch = new List<NetInput>(16);
    private readonly List<EntityState> entityScratch = new List<EntityState>(32);

    private float nextSnapshotAt;
    private float nextStateAt;
    private float nextPingAt;
    private float nextGatewaySyncAt;
    private float matchStartDeadline;

    private ushort nextEntityId = 1;
    private uint tick;

    private bool sceneRequested;
    private bool matchStarted;
    private bool rosterDirty;

    private int lastBotCount = -1;

    public NetRoom(NetServer server)
    {
        this.server = server;
    }

    public string RoomId { get; private set; } = string.Empty;

    public GameMode Mode { get; private set; } = GameMode.Convergence;

    public int TeamSize { get; private set; } = 4;

    public bool BotsEnabled { get; private set; } = true;

    public string HostPlayerId { get; private set; } = string.Empty;

    public bool IsActive => matchStarted;

    public bool Paused { get; private set; }

    public void SetPausedExternal(bool paused)
    {
        Paused = paused;
        Time.timeScale = paused ? 0f : 1f;
    }

    public void HandlePacket(NetConnection connection, TransportMessage message)
    {
        reader.Reset(message.data, 0, message.length);
        ClientOp op = (ClientOp)reader.ReadByte();

        if (!connection.Authenticated && op != ClientOp.Hello)
        {

            server.Disconnect(connection, "handshake expected");
            return;
        }

        switch (op)
        {
            case ClientOp.Hello:
                HandleHello(connection);
                break;

            case ClientOp.Input:
                HandleInput(connection);
                break;

            case ClientOp.HeroPick:
                connection.HeroIndex = reader.ReadByte();
                if (connection.Avatar != null)
                {
                    connection.Avatar.SetOperativeByHeroIndex(connection.HeroIndex);
                }

                rosterDirty = true;
                break;

            case ClientOp.Ready:
                connection.Ready = true;
                break;

            case ClientOp.RoomCommand:
                HandleRoomCommand(connection);
                break;

            case ClientOp.Pong:
            {
                long sentAt = reader.ReadLong();
                float rtt = Mathf.Clamp((float)(Now() - sentAt) / 1000f, 0f, 2f);

                connection.RoundTripTime = connection.RoundTripTime <= 0f
                    ? rtt
                    : Mathf.Lerp(connection.RoundTripTime, rtt, 0.25f);
                break;
            }

            case ClientOp.Leave:
                server.Disconnect(connection, "left the match");
                break;
        }
    }

    private void HandleHello(NetConnection connection)
    {
        byte version = reader.ReadByte();
        string token = reader.ReadString();

        if (version != NetConfig.ProtocolVersion)
        {
            Reject(connection, $"this build speaks protocol {NetConfig.ProtocolVersion}, you sent {version}");
            return;
        }

        NetToken.JoinClaims claims = NetToken.Verify(token, NetConfig.InternalSecret);

        if (!claims.valid)
        {
            Reject(connection, "your join token was not accepted");
            return;
        }

        if (string.IsNullOrEmpty(RoomId))
        {
            AdoptRoom(claims.roomId);
        }
        else if (RoomId != claims.roomId)
        {

            Reject(connection, "this server is running a different match");
            return;
        }

        foreach (NetConnection existing in server.Connections)
        {
            if (existing != connection && existing.Authenticated && existing.PlayerId == claims.playerId)
            {
                Debug.Log($"[Room] {claims.name} reconnected; dropping the stale socket");
                server.Disconnect(existing, "replaced by a newer connection");
                break;
            }
        }

        connection.MarkAuthenticated(claims.playerId, claims.name, claims.team, claims.host);
        connection.EntityId = nextEntityId++;

        if (claims.host && string.IsNullOrEmpty(HostPlayerId))
        {
            HostPlayerId = claims.playerId;
        }

        SendWelcome(connection);
        rosterDirty = true;

        Debug.Log($"[Room] {connection.DisplayName} joined room {RoomId} on {connection.Team}");
    }

    private void HandleInput(NetConnection connection)
    {
        byte count = reader.ReadByte();

        for (int i = 0; i < count; i++)
        {
            NetInput input = NetInput.Read(reader);
            if (!reader.Ok)
            {
                return;
            }

            connection.ReceiveInput(input);
        }
    }

    private void HandleRoomCommand(NetConnection connection)
    {
        RoomCommand command = (RoomCommand)reader.ReadByte();
        int argument = reader.ReadInt();

        if (!connection.IsHost && connection.PlayerId != HostPlayerId)
        {
            Debug.LogWarning($"[Room] {connection.DisplayName} tried a host command without being host");
            return;
        }

        MatchManager match = MatchManager.Instance;

        switch (command)
        {
            case RoomCommand.SetBotsEnabled:
                BotsEnabled = argument != 0;
                match?.RefreshBots(TeamSize, BotsEnabled);
                break;

            case RoomCommand.AddBot:
                match?.ServerAddBot(TeamFromArgument(argument));
                break;

            case RoomCommand.RemoveBot:
                match?.ServerRemoveBot(TeamFromArgument(argument));
                break;

            case RoomCommand.ClearBots:
                BotsEnabled = false;
                match?.ClearAllBots();
                break;

            case RoomCommand.SkipPhase:
                match?.ForceEndPhase();
                break;

            case RoomCommand.RestartMatch:
                match?.RestartMatch();
                break;

            case RoomCommand.SetTeamSize:
                TeamSize = Mathf.Clamp(argument, 1, 8);
                MatchSettings.TeamSize = TeamSize;
                match?.RefreshBots(TeamSize, BotsEnabled);
                break;

            case RoomCommand.HealAll:
                HealEveryone();
                break;

            case RoomCommand.SetMode:
                SetMode((GameMode)Mathf.Clamp(argument, 0, Enum.GetValues(typeof(GameMode)).Length - 1));
                break;

            case RoomCommand.KickPlayer:
                KickEntity((ushort)argument);
                break;

            case RoomCommand.SetPaused:
                SetPausedExternal(argument != 0);
                break;
        }

        BroadcastRoomSettings();
        rosterDirty = true;
    }

    private static Team TeamFromArgument(int argument)
    {
        switch (argument)
        {
            case 1: return Team.Attackers;
            case 2: return Team.Defenders;
            default: return Team.None;
        }
    }

    private void HealEveryone()
    {
        foreach (Health health in CombatantRegistry.All)
        {
            if (health != null && health.IsAlive)
            {
                health.RestorePools(health.MaxHealth, health.MaxArmor);
            }
        }

        foreach (NetConnection connection in server.Connections)
        {
            connection.Avatar?.Weapons?.RefillAll();
        }
    }

    private void KickEntity(ushort entityId)
    {
        foreach (NetConnection connection in server.Connections)
        {
            if (connection.EntityId == entityId)
            {
                Reject(connection, "removed from the match by the host");
                server.Disconnect(connection, "kicked");
                return;
            }
        }
    }

    private void SetMode(GameMode mode)
    {
        if (Mode == mode)
        {
            return;
        }

        Mode = mode;
        MatchSettings.SelectedMode = mode;

        matchStarted = false;
        sceneRequested = false;

        foreach (NetConnection connection in server.Connections)
        {
            connection.Avatar = null;
        }
    }

    private void AdoptRoom(string roomId)
    {
        RoomId = roomId;
        matchStartDeadline = Time.time + JoinGraceSeconds;
        server.ClaimRooms();
        server.StartCoroutine(FetchRoomConfig());
    }

    private IEnumerator FetchRoomConfig()
    {
        yield return server.Internal("GET", $"/internal/rooms/{RoomId}", null, (ok, body) =>
        {
            if (!ok)
            {
                Debug.LogWarning("[Room] could not read the room config; running on defaults");
                return;
            }

            RoomConfigResponse parsed = null;

            try
            {
                parsed = JsonUtility.FromJson<RoomConfigResponse>(body);
            }
            catch (Exception err)
            {
                Debug.LogWarning($"[Room] unreadable room config: {err.Message}");
            }

            if (parsed?.room == null)
            {
                return;
            }

            if (Enum.TryParse(parsed.room.mode, true, out GameMode parsedMode))
            {
                Mode = parsedMode;
            }

            TeamSize = Mathf.Clamp(parsed.room.teamSize, 1, 8);
            BotsEnabled = parsed.room.botsEnabled;
            HostPlayerId = parsed.room.hostId ?? string.Empty;
        });
    }

#pragma warning disable 0649
    [Serializable]
    private class RoomConfigResponse
    {
        public RoomConfigBody room;
    }
#pragma warning restore 0649

#pragma warning disable 0649
    [Serializable]
    private class RoomConfigBody
    {
        public string id;
        public string mode;
        public string state;
        public int teamSize;
        public bool botsEnabled;
        public string hostId;
    }
#pragma warning restore 0649

    public void Tick()
    {
        if (string.IsNullOrEmpty(RoomId))
        {
            return;
        }

        EnsureScene();
        EnsureAvatars();
        EnsureMatchStarted();

        float now = Time.unscaledTime;

        MatchManager match = MatchManager.Instance;
        int botCount = match != null ? match.Bots.Count : 0;
        if (botCount != lastBotCount)
        {
            lastBotCount = botCount;
            rosterDirty = true;
        }

        if (now >= nextSnapshotAt)
        {

            nextSnapshotAt += NetConfig.TickInterval;
            if (nextSnapshotAt < now - NetConfig.TickInterval * 3f)
            {
                nextSnapshotAt = now + NetConfig.TickInterval;
            }

            tick++;

            LagCompensation.Capture(Time.time);
            BroadcastSnapshot();
        }

        if (now >= nextStateAt)
        {
            nextStateAt = now + StateBroadcastInterval;
            BroadcastMatchState();
        }

        if (rosterDirty)
        {
            rosterDirty = false;
            BroadcastRoster();
        }

        if (now >= nextPingAt)
        {
            nextPingAt = now + PingInterval;
            BroadcastPing();
        }

        if (now >= nextGatewaySyncAt)
        {
            nextGatewaySyncAt = now + GatewaySyncInterval;
            server.StartCoroutine(SyncToGateway());
        }
    }

    private void EnsureScene()
    {
        if (sceneRequested)
        {
            return;
        }

        sceneRequested = true;
        MatchSettings.SelectedMode = Mode;
        MatchSettings.TeamSize = TeamSize;

        string sceneName = GameModeInfo.SceneFor(Mode);
        Debug.Log($"[Room] loading {sceneName} for room {RoomId}");
        SceneManager.LoadScene(sceneName);
    }

    private void EnsureAvatars()
    {
        MatchManager match = MatchManager.Instance;
        if (match == null)
        {
            return;
        }

        int spawnIndex = 0;

        foreach (NetConnection connection in server.Connections)
        {
            if (!connection.Authenticated)
            {
                continue;
            }

            if (connection.Avatar == null)
            {
                connection.Avatar = match.SpawnNetworkAvatar(
                    connection.Team,
                    connection.DisplayName,
                    Mathf.Max(0, connection.HeroIndex),
                    spawnIndex,
                    false);

                if (connection.Avatar != null)
                {

                    NetConnection owner = connection;
                    connection.Avatar.HitConfirmed += (damage, headshot, killed) =>
                        SendHitConfirm(owner.Avatar, damage, headshot, killed);
                }

                rosterDirty = true;
            }

            spawnIndex++;
        }
    }

    private void EnsureMatchStarted()
    {
        if (matchStarted)
        {
            return;
        }

        MatchManager match = MatchManager.Instance;
        if (match == null)
        {
            return;
        }

        int connected = 0;
        foreach (NetConnection connection in server.Connections)
        {
            if (connection.Authenticated && connection.Avatar != null)
            {
                connected++;
            }
        }

        if (connected == 0)
        {
            return;
        }

        bool everyoneHere = connected >= TeamSize * 2;
        if (!everyoneHere && Time.time < matchStartDeadline)
        {
            return;
        }

        matchStarted = true;
        match.ReadyGate = AllPlayersReady;
        match.KillLogged += BroadcastKill;
        match.PhaseChanged += _ => BroadcastMatchState();
        match.ScoreChanged += (_, __) => BroadcastMatchState();
        match.ServerBeginMatch(TeamSize, BotsEnabled);

        server.StartCoroutine(ReportRoomState("live"));
        Debug.Log($"[Room] match started with {connected} players, bots {(BotsEnabled ? "on" : "off")}");
    }

    private bool AllPlayersReady()
    {
        foreach (NetConnection connection in server.Connections)
        {
            if (connection.Authenticated && !connection.Ready)
            {
                return false;
            }
        }

        return true;
    }

    public void ApplyInputs()
    {
        if (Paused)
        {
            return;
        }

        foreach (NetConnection connection in server.Connections)
        {
            if (!connection.Authenticated || connection.Avatar == null)
            {
                continue;
            }

            inputScratch.Clear();

            if (connection.Drain(inputScratch))
            {
                foreach (NetInput input in inputScratch)
                {
                    ApplyOne(connection, input);
                }
            }
            else if (connection.Coast(out NetInput coasted))
            {
                ApplyOne(connection, coasted);
            }

            if (connection.Avatar.Weapons != null)
            {
                connection.Avatar.Weapons.NetShooterLatency = connection.RoundTripTime;
            }
        }
    }

    private void ApplyOne(NetConnection connection, NetInput input)
    {
        PlayerController avatar = connection.Avatar;
        if (avatar == null)
        {
            return;
        }

        connection.ResolveEdges(input, out bool jumpPressed, out bool firePressed);

        bool jumpHeld = input.Held(InputButton.Jump);

        avatar.Look?.SetAim(input.yaw, input.pitch);
        avatar.Mover?.ApplyNetInput(
            new Vector2(input.moveX, input.moveY),
            jumpPressed,
            jumpHeld,
            input.Held(InputButton.Descend));

        avatar.Weapons?.ApplyNetInput(
            input.Held(InputButton.Fire),
            firePressed,
            input.Held(InputButton.Aim),
            input.Held(InputButton.Reload),
            input.weaponSlot);

        avatar.Operative?.ApplyNetAbilityInput(input.abilities);
    }

    private void SendWelcome(NetConnection connection)
    {
        writer.Reset();
        writer.WriteByte((byte)ServerOp.Welcome);
        writer.WriteByte(NetConfig.ProtocolVersion);
        writer.WriteUShort(connection.EntityId);
        writer.WriteUInt(tick);
        writer.WriteByte((byte)NetConfig.TickRate);
        writer.WriteByte((byte)Mode);
        writer.WriteByte((byte)TeamSize);
        writer.WriteByte((byte)connection.Team);
        writer.WriteBool(connection.IsHost || connection.PlayerId == HostPlayerId);
        writer.WriteString(RoomId);

        connection.Transport.SendBinary(writer.Segment);
    }

    private void Reject(NetConnection connection, string reason)
    {
        writer.Reset();
        writer.WriteByte((byte)ServerOp.Reject);
        writer.WriteString(reason);
        connection.Transport.SendBinary(writer.Segment);

        Debug.LogWarning($"[Room] rejected a connection: {reason}");
    }

    private void BroadcastSnapshot()
    {
        entityScratch.Clear();
        CollectEntities(entityScratch);

        writer.Reset();
        writer.WriteByte((byte)ServerOp.Snapshot);
        writer.WriteUInt(tick);
        writer.WriteByte((byte)Mathf.Min(entityScratch.Count, byte.MaxValue));

        foreach (EntityState state in entityScratch)
        {
            state.Write(writer);
        }

        int bodyLength = writer.Length;

        foreach (NetConnection connection in server.Connections)
        {
            if (!connection.Authenticated)
            {
                continue;
            }

            writer.WriteUInt(connection.LastAppliedTick);
            connection.Transport.SendBinary(writer.Segment);

            writer.Truncate(bodyLength);
        }
    }

    private void CollectEntities(List<EntityState> into)
    {
        foreach (NetConnection connection in server.Connections)
        {
            if (!connection.Authenticated || connection.Avatar == null)
            {
                continue;
            }

            into.Add(StateFor(connection.EntityId, connection.Avatar));
        }

        MatchManager match = MatchManager.Instance;
        if (match == null)
        {
            return;
        }

        ushort botId = 1000;

        foreach (BotBrain bot in match.Bots)
        {
            if (bot == null || bot.Health == null)
            {
                continue;
            }

            into.Add(StateForBot(botId++, bot));
        }
    }

    private static EntityState StateFor(ushort id, PlayerController avatar)
    {
        Health health = avatar.Health;
        Rigidbody body = avatar.GetComponent<Rigidbody>();

        byte flags = 0;
        if (health.IsAlive) flags |= EntityFlag.Alive;
        if (avatar.Mover != null && avatar.Mover.IsGrounded) flags |= EntityFlag.Grounded;
        if (avatar.Weapons != null && avatar.Weapons.IsAiming) flags |= EntityFlag.Aiming;
        if (avatar.Weapons != null && avatar.Weapons.IsReloading) flags |= EntityFlag.Reloading;

        return new EntityState
        {
            id = id,
            flags = flags,
            team = (byte)health.Team,
            operative = (byte)(avatar.Operative != null ? avatar.Operative.Id : OperativeId.Bulwark),
            position = avatar.transform.position,
            velocity = body != null ? body.linearVelocity : Vector3.zero,
            yaw = avatar.Look != null ? avatar.Look.Yaw : avatar.transform.eulerAngles.y,
            pitch = avatar.Look != null ? avatar.Look.Pitch : 0f,
            health = health.CurrentHealth,
            armor = health.CurrentArmor,
        };
    }

    private static EntityState StateForBot(ushort id, BotBrain bot)
    {
        Health health = bot.Health;
        Rigidbody body = bot.GetComponent<Rigidbody>();

        byte flags = EntityFlag.Bot;
        if (health.IsAlive) flags |= EntityFlag.Alive;
        flags |= EntityFlag.Grounded;

        return new EntityState
        {
            id = id,
            flags = flags,
            team = (byte)health.Team,
            operative = (byte)bot.Operative,
            position = bot.transform.position,
            velocity = body != null ? body.linearVelocity : Vector3.zero,
            yaw = bot.transform.eulerAngles.y,
            pitch = 0f,
            health = health.CurrentHealth,
            armor = health.CurrentArmor,
        };
    }

    private void BroadcastMatchState()
    {
        MatchManager match = MatchManager.Instance;
        if (match == null)
        {
            return;
        }

        MatchStateMessage state = new MatchStateMessage
        {
            phase = (byte)match.CurrentPhase,
            phaseIsTimed = match.PhaseIsTimed,
            phaseSecondsRemaining = match.PhaseSecondsRemaining,
            roundNumber = (byte)Mathf.Clamp(match.RoundNumber, 0, 255),
            attackerScore = (byte)Mathf.Clamp(match.AttackerScore, 0, 255),
            defenderScore = (byte)Mathf.Clamp(match.DefenderScore, 0, 255),
            mode = (byte)Mode,
            teamSize = (byte)TeamSize,
            botsEnabled = BotsEnabled,
            paused = Paused,
        };

        ModeObjective objective = match.Objective;
        if (objective != null)
        {
            objective.WriteNetProgress(out state.objectiveA, out state.objectiveB, out state.objectiveC);
        }

        writer.Reset();
        writer.WriteByte((byte)ServerOp.MatchState);
        state.Write(writer);
        server.Broadcast(writer);
    }

    private void BroadcastRoster()
    {
        MatchManager match = MatchManager.Instance;

        List<RosterEntry> entries = new List<RosterEntry>();

        foreach (NetConnection connection in server.Connections)
        {
            if (!connection.Authenticated)
            {
                continue;
            }

            entries.Add(new RosterEntry
            {
                entityId = connection.EntityId,
                playerId = connection.PlayerId,
                name = connection.DisplayName,
                team = (byte)connection.Team,
                operative = (byte)(connection.Avatar?.Operative != null
                    ? connection.Avatar.Operative.Id
                    : OperativeId.Bulwark),
                bot = false,
            });
        }

        if (match != null)
        {
            ushort botId = 1000;

            foreach (BotBrain bot in match.Bots)
            {
                if (bot == null || bot.Health == null)
                {
                    continue;
                }

                entries.Add(new RosterEntry
                {
                    entityId = botId++,
                    playerId = string.Empty,
                    name = bot.Health.DisplayName,
                    team = (byte)bot.Health.Team,
                    operative = (byte)bot.Operative,
                    bot = true,
                });
            }
        }

        writer.Reset();
        writer.WriteByte((byte)ServerOp.Roster);
        writer.WriteByte((byte)Mathf.Min(entries.Count, byte.MaxValue));

        foreach (RosterEntry entry in entries)
        {
            entry.Write(writer);
        }

        server.Broadcast(writer);
    }

    private void BroadcastPing()
    {
        long now = Now();

        foreach (NetConnection connection in server.Connections)
        {
            if (!connection.Authenticated)
            {
                continue;
            }

            writer.Reset();
            writer.WriteByte((byte)ServerOp.Ping);
            writer.WriteLong(now);
            writer.WriteUShort((ushort)Mathf.Clamp(Mathf.RoundToInt(connection.RoundTripTime * 1000f), 0, 65535));
            connection.Transport.SendBinary(writer.Segment);
        }
    }

    private void BroadcastRoomSettings()
    {
        writer.Reset();
        writer.WriteByte((byte)ServerOp.Event);
        writer.WriteByte((byte)NetEventKind.RoomSettings);
        writer.WriteBool(BotsEnabled);
        writer.WriteByte((byte)TeamSize);
        writer.WriteByte((byte)Mode);
        writer.WriteBool(Paused);
        server.Broadcast(writer);
    }

    public void BroadcastKill(string killer, string victim, string weapon, Team killerTeam, bool headshot)
    {
        writer.Reset();
        writer.WriteByte((byte)ServerOp.Event);
        writer.WriteByte((byte)NetEventKind.Kill);
        writer.WriteString(killer);
        writer.WriteString(victim);
        writer.WriteString(weapon);
        writer.WriteByte((byte)killerTeam);
        writer.WriteBool(headshot);
        server.Broadcast(writer);
    }

    public void SendHitConfirm(PlayerController shooter, float damage, bool headshot, bool killed)
    {
        foreach (NetConnection connection in server.Connections)
        {
            if (connection.Avatar != shooter || !connection.Authenticated)
            {
                continue;
            }

            writer.Reset();
            writer.WriteByte((byte)ServerOp.Event);
            writer.WriteByte((byte)NetEventKind.HitConfirm);
            writer.WriteFloat(damage);
            writer.WriteBool(headshot);
            writer.WriteBool(killed);
            connection.Transport.SendBinary(writer.Segment);
            return;
        }
    }

    private IEnumerator SyncToGateway()
    {
        if (string.IsNullOrEmpty(RoomId))
        {
            yield break;
        }

        System.Text.StringBuilder payload = new System.Text.StringBuilder("{\"players\":[");
        bool first = true;

        foreach (NetConnection connection in server.Connections)
        {
            if (!connection.Authenticated)
            {
                continue;
            }

            if (!first)
            {
                payload.Append(',');
            }

            int team = connection.Team == Team.Defenders ? 1 : 0;
            payload.Append($"{{\"id\":\"{connection.PlayerId}\",\"team\":{team}}}");
            first = false;
        }

        payload.Append($"],\"botsEnabled\":{(BotsEnabled ? "true" : "false")}}}");

        yield return server.Internal("POST", $"/internal/rooms/{RoomId}/players", payload.ToString(), null);
    }

    private IEnumerator ReportRoomState(string state)
    {
        yield return server.Internal("POST", $"/internal/rooms/{RoomId}/state", $"{{\"state\":\"{state}\"}}", null);
    }

    public void RemovePlayer(NetConnection connection)
    {
        if (connection.Avatar != null)
        {
            MatchManager.Instance?.DespawnNetworkAvatar(connection.Avatar);
            connection.Avatar = null;
        }

        rosterDirty = true;

        int remaining = 0;
        foreach (NetConnection other in server.Connections)
        {
            if (other != connection && other.Authenticated)
            {
                remaining++;
            }
        }

        if (remaining == 0 && !string.IsNullOrEmpty(RoomId))
        {
            Debug.Log($"[Room] last player left; closing {RoomId}");
            server.StartCoroutine(ReportRoomState("over"));
            server.ReleaseRoom(RoomId);
            Reset();
        }
        else
        {
            MatchManager.Instance?.RefreshBots(TeamSize, BotsEnabled);
        }
    }

    private void Reset()
    {
        RoomId = string.Empty;
        matchStarted = false;
        sceneRequested = false;
        Paused = false;
        Time.timeScale = 1f;
        nextEntityId = 1;
        LagCompensation.Clear();

        SceneManager.LoadScene(SceneFlow.MainMenu);
    }

    private static long Now()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
