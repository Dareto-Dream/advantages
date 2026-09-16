// im certain this file wont get over-bloated
// *laughs in ignorance*
// working on this file again
// bye bye sleep schedule
// OH GOD WHY
// well i needed to study for a test but procrastination is pretty
// dude idk why i chose this game idea, its hell
// *sisyphus intensifies*
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MatchManager : MonoBehaviour
{
    // predefine phases for matches (this is not stolen from marvel rivals, i swear)
    public enum Phase
    {

        HeroSelect,

        Staging,
        Live,
        Overtime,
        RoundOver,
        MatchOver
    }

    public static MatchManager Instance { get; private set; }

    // buncha settings for the match, these are like edited every 3s btw
    [Header("Prefabs")]
    [SerializeField] private PlayerController playerPrefab;
    [SerializeField] private BotBrain botPrefab;

    [Header("Rules")]
    [Tooltip("Once per match: seconds allowed to pick a hero. The round won't start until the local player has picked, even if this runs out.")]
    [SerializeField] private float heroSelectSeconds = 10f;
    [Tooltip("Spawn-room countdown at the start of every round - the countdown to Live.")]
    [SerializeField] private float stagingSeconds = 30f;
    [SerializeField] private float roundOverSeconds = 4f;
    [Tooltip("Objective modes only: seconds spent dead before respawning back in your spawn room.")]
    [SerializeField] private float respawnSeconds = 9f;

    private const float LegacyArenaSeconds = 120f;
    [SerializeField] private float comebackArmor = 25f;

    [Header("Materials")]
    [SerializeField] private Material attackerMaterial;
    [SerializeField] private Material defenderMaterial;

    private readonly List<BotBrain> bots = new List<BotBrain>();
    private readonly Dictionary<Team, int> roundWins = new Dictionary<Team, int>
    {
        { Team.Attackers, 0 },
        { Team.Defenders, 0 }
    };

    private PlayerController player;

    private Phase phase = Phase.HeroSelect;
    private float phaseEndTime = float.MaxValue;

    private Team comebackTeam = Team.None;
    private int roundNumber;

    public bool PlayerReady { get; private set; }

    private readonly Dictionary<Health, float> pendingRespawns = new Dictionary<Health, float>();
    private readonly List<Health> respawnScratch = new List<Health>();

    private ModeObjective objective;

    public event Action<Phase> PhaseChanged;
    public event Action<int, int> ScoreChanged;
    public event Action<Team> RoundEnded;
    public event Action<Team> MatchEnded;

    public event Action<string, string, string, Team, bool> KillLogged;

    public Phase CurrentPhase => phase;
    public float PhaseSecondsRemaining => Mathf.Max(0f, phaseEndTime - Time.time);

    public bool PhaseIsTimed => !float.IsInfinity(phaseEndTime) && phaseEndTime != float.MaxValue;
    public int RoundNumber => roundNumber;
    public int AttackerScore => roundWins[Team.Attackers];
    public int DefenderScore => roundWins[Team.Defenders];
    public Team ComebackTeam => comebackTeam;
    public PlayerController Player => player;
    public IReadOnlyList<BotBrain> Bots => bots;
    public ModeObjective Objective => objective;

    // pretty important function if the match doesnt exist atp then the match is cooked
    private void Awake()
    {
        Instance = this;
        MatchSettings.Load();
        objective = FindAnyObjectByType<ModeObjective>();

        MatchStats.EnsureExists();
    }

    // ensure proper cleanup of the singleton when the object is destroyed, cause memory leak
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // i just rewrote this like an hour ago and im blanking
    // this is the big boss it acts as the entry point for the match, it also handles the client v server ideology,
    // which is pretty important for the match to work, so future me specifically, stop touching this
    private IEnumerator Start()
    {

        if (NetContext.IsClient)
        {
            yield break;
        }

        if (NetContext.IsServer)
        {
            yield break;
        }

        SpawnPlayer();
        SpawnBots();

        yield return null;

        EnterPhase(Phase.HeroSelect, heroSelectSeconds);
    }

    // lit just confirm player lulz gotz to make sure we are the client
    public void MarkPlayerReady()
    {
        PlayerReady = true;

        if (NetContext.IsClient && NetClient.Instance != null)
        {
            NetClient.Instance.SendHeroPick(MatchSettings.HeroIndex);
            NetClient.Instance.SendReady();
        }
    }

    // yumm 🤤 tick loops
    private void Update()
    {

        // client aint authorative so we just kinda slap its wrist
        if (NetContext.IsClient)
        {
            return;
        }

        // switching between those phases defined earlier
        switch (phase)
        {
            // probs gunna change in the future, but it waits for the timer to be over and everyonne having had select their operative
            case Phase.HeroSelect:

                if (Time.time >= phaseEndTime && EveryoneReady())
                {
                    StartRound();
                }

                break;

            // this is the like prep phase, time for defender based games to reach point first
            // also could be used to coordinate with team as you cna still change operatives in this phase
            case Phase.Staging:
                if (Time.time >= phaseEndTime)
                {
                    EnterPhase(Phase.Live, LivePhaseSeconds());
                }

                break;

            // when you are in game we check respawns and do all live computation (this would get hella big if it wasnt a function)
            case Phase.Live:
                TickRespawns();
                EvaluateLiveRound();
                break;

            // overtime system defined in PRD, similar to live phase
            case Phase.Overtime:
                TickRespawns();
                EvaluateOvertimeRound();
                break;

            // this is also subject to change but once the current round ends, we just start a new one
            case Phase.RoundOver:
                if (Time.time >= phaseEndTime)
                {
                    StartRound();
                }

                break;
        }
    }

    public Func<bool> ReadyGate { get; set; }

    private readonly List<PlayerController> networkAvatars = new List<PlayerController>();

    public IReadOnlyList<PlayerController> NetworkAvatars => networkAvatars;

    private bool EveryoneReady()
    {
        return ReadyGate != null ? ReadyGate() : PlayerReady;
    }

    public PlayerController SpawnNetworkAvatar(Team team, string displayName, int heroIndex, int spawnIndex, bool localControl)
    {
        if (playerPrefab == null)
        {
            Debug.LogError("MatchManager has no player prefab assigned.");
            return null;
        }

        SpawnPoint spawn = PickSpawn(team, spawnIndex);
        Vector3 position = spawn != null ? spawn.transform.position : Vector3.up;
        Quaternion rotation = spawn != null ? spawn.transform.rotation : Quaternion.identity;

        PlayerController avatar = Instantiate(playerPrefab, position, rotation);
        avatar.name = localControl ? "Player" : $"Net_{displayName}";
        avatar.Health.Team = team;
        avatar.Health.SetDisplayName(displayName);
        avatar.SetOperativeByHeroIndex(Mathf.Max(0, heroIndex));
        avatar.Health.Died += info => HandleCombatantDied(info, avatar.Health);

        ApplyTeamColor(avatar.gameObject, team);

        if (localControl)
        {
            player = avatar;
            avatar.Look.SetSensitivityScale(MatchSettings.MouseSensitivity);
            avatar.ConfigureAsLocalPlayer();
        }
        else
        {

            avatar.ConfigureAsNetworkAvatar(NetContext.IsServer);
        }

        networkAvatars.Add(avatar);
        return avatar;
    }

    public void DespawnNetworkAvatar(PlayerController avatar)
    {
        if (avatar == null)
        {
            return;
        }

        networkAvatars.Remove(avatar);
        pendingRespawns.Remove(avatar.Health);

        if (player == avatar)
        {
            player = null;
        }

        Destroy(avatar.gameObject);
    }

    public void RefreshBots(int teamSize, bool botsEnabled)
    {
        if (NetContext.IsClient)
        {
            return;
        }

        if (!botsEnabled)
        {
            ClearAllBots();
            return;
        }

        foreach (Team team in new[] { Team.Attackers, Team.Defenders })
        {
            int humans = CountHumans(team);
            int currentBots = CountBots(team);
            int wanted = Mathf.Max(0, teamSize - humans);

            for (int i = currentBots; i < wanted; i++)
            {
                SpawnOneBot(team, humans + i);
            }

            for (int i = currentBots; i > wanted; i--)
            {
                RemoveOneBot(team);
            }
        }
    }

    public void ClearAllBots()
    {
        for (int i = bots.Count - 1; i >= 0; i--)
        {
            BotBrain bot = bots[i];
            bots.RemoveAt(i);

            if (bot != null)
            {
                pendingRespawns.Remove(bot.Health);
                Destroy(bot.gameObject);
            }
        }
    }

    public void ServerAddBot(Team team)
    {
        if (team == Team.None)
        {
            team = CountBots(Team.Attackers) + CountHumans(Team.Attackers)
                <= CountBots(Team.Defenders) + CountHumans(Team.Defenders)
                ? Team.Attackers
                : Team.Defenders;
        }

        SpawnOneBot(team, CountHumans(team) + CountBots(team));
    }

    public void ServerRemoveBot(Team team)
    {
        if (team == Team.None)
        {
            team = CountBots(Team.Attackers) >= CountBots(Team.Defenders) ? Team.Attackers : Team.Defenders;
        }

        RemoveOneBot(team);
    }

    private int CountHumans(Team team)
    {
        int count = 0;

        foreach (PlayerController avatar in networkAvatars)
        {
            if (avatar != null && avatar.Health.Team == team)
            {
                count++;
            }
        }

        if (player != null && player.Health.Team == team && !networkAvatars.Contains(player))
        {
            count++;
        }

        return count;
    }

    private int CountBots(Team team)
    {
        int count = 0;

        foreach (BotBrain bot in bots)
        {
            if (bot != null && bot.Health.Team == team)
            {
                count++;
            }
        }

        return count;
    }

    private void RemoveOneBot(Team team)
    {
        for (int i = bots.Count - 1; i >= 0; i--)
        {
            BotBrain bot = bots[i];
            if (bot == null || bot.Health.Team != team)
            {
                continue;
            }

            bots.RemoveAt(i);
            pendingRespawns.Remove(bot.Health);
            Destroy(bot.gameObject);
            return;
        }
    }

    private void SpawnOneBot(Team team, int spawnIndex)
    {
        if (botPrefab == null)
        {
            return;
        }

        HashSet<OperativeId> taken = new HashSet<OperativeId>();

        foreach (BotBrain existing in bots)
        {
            if (existing != null && existing.Health.Team == team)
            {
                taken.Add(existing.Operative);
            }
        }

        HeroRole[] composition = { HeroRole.Tank, HeroRole.Support, HeroRole.Dps, HeroRole.Dps };

        SpawnPoint spawn = PickSpawn(team, spawnIndex + 1);
        Vector3 position = spawn != null ? spawn.transform.position : Vector3.up;
        Quaternion rotation = spawn != null ? spawn.transform.rotation : Quaternion.identity;

        BotBrain bot = Instantiate(botPrefab, position, rotation);
        string prefix = team == MatchSettings.PlayerTeam ? "Ally" : "Enemy";
        bot.name = $"{prefix}_{spawnIndex + 1}";

        OperativeId pick = PickBotOperative(composition[spawnIndex % composition.Length], taken);
        bot.Configure(team, pick, $"{prefix} {spawnIndex + 1}");
        ApplyTeamColor(bot.gameObject, team);

        bot.Health.Died += info => HandleCombatantDied(info, bot.Health);
        bots.Add(bot);

        bot.enabled = phase == Phase.Live || phase == Phase.Overtime;
    }

    public void ServerBeginMatch(int teamSize, bool botsEnabled)
    {
        if (!NetContext.IsServer)
        {
            return;
        }

        RefreshBots(teamSize, botsEnabled);
        EnterPhase(Phase.HeroSelect, heroSelectSeconds);
    }

    public void ForceEndPhase()
    {
        phaseEndTime = Time.time;

        switch (phase)
        {
            case Phase.HeroSelect:
                StartRound();
                break;

            case Phase.Staging:
                EnterPhase(Phase.Live, LivePhaseSeconds());
                break;

            case Phase.Live:
            case Phase.Overtime:
                FinishRound(Team.None);
                break;

            case Phase.RoundOver:
                StartRound();
                break;
        }
    }

    public void ApplyNetworkMatchState(Phase networkPhase, bool timed, float secondsRemaining, int round, int attackers, int defenders)
    {
        roundNumber = round;
        roundWins[Team.Attackers] = attackers;
        roundWins[Team.Defenders] = defenders;
        phaseEndTime = timed ? Time.time + secondsRemaining : float.PositiveInfinity;

        if (phase != networkPhase)
        {
            EnterPhase(networkPhase, timed ? secondsRemaining : float.PositiveInfinity);
        }

        ScoreChanged?.Invoke(AttackerScore, DefenderScore);
    }

    public void SetLocalPlayer(PlayerController avatar)
    {
        player = avatar;
    }

    public void RaiseNetworkKill(string killerName, string victimName, string weapon, Team killerTeam, bool headshot)
    {
        KillLogged?.Invoke(killerName, victimName, weapon, killerTeam, headshot);
    }

    private void SpawnPlayer()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("MatchManager has no player prefab assigned.");
            return;
        }

        SpawnPoint spawn = PickSpawn(MatchSettings.PlayerTeam, 0);
        Vector3 position = spawn != null ? spawn.transform.position : Vector3.up;
        Quaternion rotation = spawn != null ? spawn.transform.rotation : Quaternion.identity;

        player = Instantiate(playerPrefab, position, rotation);
        player.name = "Player";
        player.Health.Team = MatchSettings.PlayerTeam;
        player.Health.SetDisplayName(MatchSettings.PlayerName);
        player.SetRole(MatchSettings.PlayerRole);

        player.SetOperativeByHeroIndex(MatchSettings.HeroIndex);
        player.Look.SetSensitivityScale(MatchSettings.MouseSensitivity);
        player.ConfigureAsLocalPlayer();
        player.Health.Died += info => HandleCombatantDied(info, player.Health);
    }

    private void SpawnBots()
    {
        if (botPrefab == null)
        {
            Debug.LogError("MatchManager has no bot prefab assigned.");
            return;
        }

        SpawnTeamBots(MatchSettings.PlayerTeam, MatchSettings.TeamSize - 1, "Ally");
        SpawnTeamBots(MatchSettings.EnemyTeam, MatchSettings.TeamSize, "Enemy");
    }

    private void SpawnTeamBots(Team team, int count, string prefix)
    {

        HeroRole[] composition = { HeroRole.Tank, HeroRole.Support, HeroRole.Dps, HeroRole.Dps };
        HashSet<OperativeId> taken = new HashSet<OperativeId>();

        if (team == MatchSettings.PlayerTeam)
        {
            taken.Add(OperativeRoster.FromHeroIndex(MatchSettings.HeroIndex).id);
        }

        for (int i = 0; i < count; i++)
        {
            SpawnPoint spawn = PickSpawn(team, i + 1);
            Vector3 position = spawn != null ? spawn.transform.position : Vector3.up;
            Quaternion rotation2 = spawn != null ? spawn.transform.rotation : Quaternion.identity;

            BotBrain bot = Instantiate(botPrefab, position, rotation2);
            bot.name = $"{prefix}_{i + 1}";

            OperativeId pick = PickBotOperative(composition[i % composition.Length], taken);
            taken.Add(pick);

            bot.Configure(team, pick, $"{prefix} {i + 1}");
            ApplyTeamColor(bot.gameObject, team);

            bot.Health.Died += info => HandleCombatantDied(info, bot.Health);
            bots.Add(bot);
        }
    }

    private static OperativeId PickBotOperative(HeroRole want, HashSet<OperativeId> taken)
    {
        List<OperativeId> pool = new List<OperativeId>();

        foreach (OperativeDefinition def in OperativeRoster.All)
        {
            if (def.role == want && !taken.Contains(def.id))
            {
                pool.Add(def.id);
            }
        }

        if (pool.Count == 0)
        {
            foreach (OperativeDefinition def in OperativeRoster.All)
            {
                if (!taken.Contains(def.id))
                {
                    pool.Add(def.id);
                }
            }
        }

        return pool.Count > 0 ? pool[UnityEngine.Random.Range(0, pool.Count)] : OperativeId.Bulwark;
    }

    private void ApplyTeamColor(GameObject root, Team team)
    {
        Material material = team == Team.Attackers ? attackerMaterial : defenderMaterial;
        if (material == null)
        {
            return;
        }

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {

            if (renderer.gameObject.name == "Body" || renderer.gameObject.name == "Head")
            {
                renderer.sharedMaterial = material;
            }
        }
    }

    private SpawnPoint PickSpawn(Team team, int index)
    {
        List<SpawnPoint> points = SpawnPoint.For(team);
        if (points.Count == 0)
        {
            return null;
        }

        return points[index % points.Count];
    }

    public void StartRound()
    {
        roundNumber++;

        player?.SetOperativeByHeroIndex(MatchSettings.HeroIndex);

        if (roundNumber > 1)
        {
            ResetCombatants();
        }

        pendingRespawns.Clear();
        objective?.ResetForRound();
        EnterPhase(Phase.Staging, stagingSeconds);
    }

    private void HandleCombatantDied(DamageInfo info, Health victim)
    {
        LogKill(info, victim);

        if (NetContext.IsClient)
        {
            return;
        }

        if (objective == null || victim == null || !objective.AllowsRespawns)
        {
            return;
        }

        if (phase == Phase.Live || phase == Phase.Overtime)
        {
            pendingRespawns[victim] = Time.time + respawnSeconds;
        }
    }

    private void TickRespawns()
    {
        if (pendingRespawns.Count == 0)
        {
            return;
        }

        respawnScratch.Clear();
        foreach (KeyValuePair<Health, float> entry in pendingRespawns)
        {
            if (Time.time >= entry.Value)
            {
                respawnScratch.Add(entry.Key);
            }
        }

        foreach (Health victim in respawnScratch)
        {
            pendingRespawns.Remove(victim);
            RespawnCombatant(victim);
        }

        if (respawnScratch.Count > 0)
        {

            SpawnPanel.RefreshAllPassage();
        }
    }

    private void RespawnCombatant(Health victim)
    {
        if (victim == null)
        {
            return;
        }

        SpawnPoint spawn = PickSpawn(victim.Team, UnityEngine.Random.Range(0, 4));
        Vector3 position = spawn != null ? spawn.transform.position : Vector3.up;
        float yaw = spawn != null ? spawn.transform.eulerAngles.y : 0f;
        RespawnCombatantAt(victim, position, yaw);
    }

    private void RespawnCombatantAt(Health victim, Vector3 position, float yaw)
    {
        if (player != null && victim == player.Health)
        {
            player.Respawn(position, yaw);
            return;
        }

        foreach (BotBrain bot in bots)
        {
            if (bot.Health == victim)
            {
                bot.ResetForRound(position, Quaternion.Euler(0f, yaw, 0f));
                return;
            }
        }
    }

    public bool HasPendingRevive(Team team)
    {
        foreach (KeyValuePair<Health, float> entry in pendingRespawns)
        {
            if (entry.Key != null && entry.Key.Team == team)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryReviveNearest(Vector3 position, Team team)
    {
        Health best = null;
        float latest = float.MinValue;

        foreach (KeyValuePair<Health, float> entry in pendingRespawns)
        {
            if (entry.Key == null || entry.Key.Team != team)
            {
                continue;
            }

            if (entry.Value > latest)
            {
                latest = entry.Value;
                best = entry.Key;
            }
        }

        if (best == null)
        {
            return false;
        }

        pendingRespawns.Remove(best);
        RespawnCombatantAt(best, position + Vector3.up * 0.1f, best.transform.eulerAngles.y);
        SpawnPanel.RefreshAllPassage();
        return true;
    }

    private void ResetCombatants()
    {
        int allyIndex = 1;
        int enemyIndex = 1;

        if (player != null)
        {
            SpawnPoint spawn = PickSpawn(MatchSettings.PlayerTeam, 0);
            Vector3 position = spawn != null ? spawn.transform.position : Vector3.up;
            float yaw = spawn != null ? spawn.transform.eulerAngles.y : 0f;
            player.Respawn(position, yaw);
            ApplyComebackBuff(player.Health);
        }

        foreach (BotBrain bot in bots)
        {
            Team team = bot.Health.Team;
            int index = team == MatchSettings.PlayerTeam ? allyIndex++ : enemyIndex++;
            SpawnPoint spawn = PickSpawn(team, index);
            Vector3 position = spawn != null ? spawn.transform.position : Vector3.up;
            Quaternion rotation = spawn != null ? spawn.transform.rotation : Quaternion.identity;

            bot.ResetForRound(position, rotation);
            ApplyComebackBuff(bot.Health);
        }
    }

    private void ApplyComebackBuff(Health health)
    {
        if (!MatchSettings.ComebackBuffEnabled || comebackTeam == Team.None)
        {
            return;
        }

        if (health.Team == comebackTeam)
        {
            health.AddArmor(comebackArmor);
        }
    }

    private float LivePhaseSeconds()
    {
        if (objective == null)
        {
            return LegacyArenaSeconds;
        }

        return objective.UsesMasterTimer ? objective.RoundSeconds : float.PositiveInfinity;
    }

    // dude im writing this function right now and its pain im at 752 lines of code i might be fried
    // time to give up?
    // maybe i should just go make mobile game slop

    // lulz bro wuz strugglin ^
    // this is the live round ticker, it essentially does a bunch of checks and big boy calculations during the match to see if the round is over yet
    private void EvaluateLiveRound()
    {
        if (objective != null) // lit all the new modes are objective, so this just checks for completion in relation to timer
        {
            Team? winner = objective.TickLive(Time.deltaTime);
            if (winner.HasValue)
            {
                FinishRound(winner.Value);
                return;
            }

            if (objective.WantsImmediateOvertime())
            {
                EnterOvertimePhase();
                return;
            }

            if (objective.UsesMasterTimer && Time.time >= phaseEndTime)
            {
                if (objective.ShouldEnterOvertime())
                {
                    EnterOvertimePhase();
                }
                else
                {
                    FinishRound(objective.ResolveTimerExpiry());
                }
            }

            return;
        }

        // legacy code from arena mode
        int attackersAlive = CombatantRegistry.AliveCount(Team.Attackers);
        int defendersAlive = CombatantRegistry.AliveCount(Team.Defenders);

        if (attackersAlive == 0 && defendersAlive == 0)
        {
            FinishRound(Team.None);
            return;
        }

        if (attackersAlive == 0)
        {
            FinishRound(Team.Defenders);
            return;
        }

        if (defendersAlive == 0)
        {
            FinishRound(Team.Attackers);
            return;
        }

        if (Time.time >= phaseEndTime)
        {

            FinishRound(Team.Defenders);
        }
    }

    private void EvaluateOvertimeRound()
    {
        // legacy arena, there shouldnt even be overtime
        // but I had a bug and this was the easiest fix
        if (objective == null) 
        {
            FinishRound(Team.None);
            return;
        }

        Team? winner = objective.TickOvertime(Time.deltaTime);
        if (winner.HasValue)
        {
            FinishRound(winner.Value);
            return;
        }

        if (objective.OvertimeExpired)
        {
            FinishRound(objective.ResolveOvertimeExpiry());
        }
    }

    private void EnterOvertimePhase()
    {

        EnterPhase(Phase.Overtime, 3600f);
        objective.EnterOvertime();
    }

    public void ExtendPhaseTimer(float seconds)
    {
        phaseEndTime += seconds;
    }

    private void FinishRound(Team winner)
    {
        if (winner != Team.None)
        {
            roundWins[winner]++;
            comebackTeam = winner == Team.Attackers ? Team.Defenders : Team.Attackers;
        }

        ScoreChanged?.Invoke(AttackerScore, DefenderScore);
        RoundEnded?.Invoke(winner);

        if (winner != Team.None && roundWins[winner] >= MatchSettings.RoundsToWin)
        {
            EnterPhase(Phase.MatchOver, 0f);
            MatchEnded?.Invoke(winner);
            CursorService.Unlock();
            return;
        }

        EnterPhase(Phase.RoundOver, roundOverSeconds);
    }

    private void EnterPhase(Phase next, float duration)
    {
        phase = next;
        phaseEndTime = Time.time + duration;

        bool combatActive = next == Phase.Live || next == Phase.Overtime;

        if (!combatActive)
        {
            AbilityWorldReset.ClearAll();
        }

        if (player != null)
        {

            player.SetControlEnabled(combatActive || next == Phase.Staging);
        }

        Team headStartTeam = objective != null ? objective.HeadStartTeam : Team.None;

        foreach (BotBrain bot in bots)
        {

            bool stagingHeadStart = next == Phase.Staging
                && headStartTeam != Team.None
                && bot.Health.Team == headStartTeam;

            bot.enabled = combatActive || stagingHeadStart;
        }

        PhaseChanged?.Invoke(next);
    }

    private void LogKill(DamageInfo info, Health victim)
    {
        Health killer = info.instigator != null ? info.instigator.GetComponentInParent<Health>() : null;
        string killerName = killer != null ? killer.DisplayName : "The world";
        Team killerTeam = killer != null ? killer.Team : Team.None;
        string weapon = string.IsNullOrEmpty(info.sourceName) ? "Impact" : info.sourceName;

        KillLogged?.Invoke(killerName, victim.DisplayName, weapon, killerTeam, info.isHeadshot);
    }

    public void RestartMatch()
    {
        roundWins[Team.Attackers] = 0;
        roundWins[Team.Defenders] = 0;
        comebackTeam = Team.None;
        roundNumber = 0;
        pendingRespawns.Clear();
        MatchStats.Instance?.ResetAll();
        ScoreChanged?.Invoke(0, 0);
        ResetCombatants();
        StartRound();
    }
}
