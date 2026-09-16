using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Health))]
[RequireComponent(typeof(NavMeshAgent))]
public class BotBrain : MonoBehaviour
{
    private static readonly List<BotBrain> all = new List<BotBrain>();

    public static readonly List<Transform> ObjectiveAnchors = new List<Transform>();

    private enum State
    {
        Advance,
        Engage,
        Dead
    }

    [Header("Role")]
    [SerializeField] private LoadoutRole role = LoadoutRole.Support;

    [Header("Perception")]
    [SerializeField] private float sightRange = 45f;
    [SerializeField] private float fieldOfView = 130f;
    [SerializeField] private LayerMask sightBlockers = ~0;
    [SerializeField] private Transform eyes;

    [Header("Aim")]
    [SerializeField] private float reactionSeconds = 0.32f;
    [SerializeField] private float aimTurnSpeed = 7f;
    [SerializeField] private float aimErrorDegrees = 2.6f;
    [SerializeField] private float burstSeconds = 0.55f;
    [SerializeField] private float burstRestSeconds = 0.4f;

    [Header("Combat")]
    [SerializeField] private WeaponDefinition weapon;
    [SerializeField] private float preferredRange = 14f;

    [Header("Movement")]
    [SerializeField] private float strafeChangeSeconds = 1.4f;

    [SerializeField] private Health health;
    [SerializeField] private Transform visuals;

    private NavMeshAgent agent;
    private StatusEffects status;
    private BotOperative botOp;
    private float baseAgentSpeed = 5.5f;
    private float stunnedUntil = -1f;
    private Vector3 knockback;
    private float lastHurtTime = -999f;
    private float lastCombatTime = -999f;
    private float nextAbilityThink;
    private float ultLockoutUntil;
    private Vector3 dashVelocity;
    private float dashUntil;
    private State state = State.Advance;
    private Health target;
    private float nextShotTime;
    private float targetVisibleSince = -1f;
    private float burstEndTime;
    private float burstRestUntil;
    private float nextStrafeTime;
    private float strafeSign = 1f;
    private Vector3 patrolTarget;
    private float nextRepathTime;
    private Vector3 aimDirection = Vector3.forward;

    public Health Health => health;

    public OperativeId Operative => botOp != null ? botOp.Id : OperativeId.Bulwark;
    public LoadoutRole Role => role;
    public static IReadOnlyList<BotBrain> All => all;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        if (health == null)
        {
            health = GetComponent<Health>();
        }

        if (eyes == null)
        {
            eyes = transform;
        }

        if (botOp == null)
        {
            botOp = GetComponent<BotOperative>();
        }

        aimDirection = transform.forward;
    }

    private void OnEnable()
    {
        all.Add(this);
        health.Died += HandleDied;
        health.Damaged += HandleDamaged;

        nextRepathTime = Time.time + 0.3f;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }
    }

    private void OnDisable()
    {
        all.Remove(this);
        health.Died -= HandleDied;
        health.Damaged -= HandleDamaged;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }

    private void Start()
    {
        ApplyRole();
        patrolTarget = transform.position;
    }

    public void Configure(Team team, OperativeId operativeId, string name)
    {
        health.Team = team;
        health.SetDisplayName(name);

        OperativeDefinition def = OperativeRoster.Get(operativeId);
        role = def.baseRole;

        List<WeaponDefinition> kit = OperativeWeaponLibrary.WeaponsFor(operativeId);
        if (kit != null && kit.Count > 0 && kit[0] != null)
        {
            weapon = kit[0];
        }

        if (botOp == null)
        {
            botOp = GetComponent<BotOperative>() ?? gameObject.AddComponent<BotOperative>();
        }

        botOp.SetOperative(operativeId);
        ApplyRole();
    }

    public void ResetForRound(Vector3 position, Quaternion rotation)
    {
        state = State.Advance;
        target = null;
        targetVisibleSince = -1f;

        if (agent != null)
        {
            agent.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            agent.enabled = true;

            if (agent.isOnNavMesh)
            {
                agent.ResetPath();
            }
        }
        else
        {
            transform.SetPositionAndRotation(position, rotation);
        }

        health.ResetHealth();
        SetCollidersEnabled(true);
        stunnedUntil = -1f;
        knockback = Vector3.zero;
        dashVelocity = Vector3.zero;
        dashUntil = 0f;
        lastHurtTime = -999f;
        lastCombatTime = -999f;
        nextAbilityThink = Time.time + Random.Range(0.5f, 1.2f);
        ultLockoutUntil = 0f;
        botOp?.ResetForRespawn();

        if (visuals != null)
        {
            visuals.gameObject.SetActive(true);
        }

        aimDirection = transform.forward;
        nextRepathTime = 0f;
    }

    private void ApplyRole()
    {
        LoadoutPreset preset = LoadoutLibrary.Resolve(role);
        LoadoutStats stats = preset != null ? preset.ResolveStats() : LoadoutStats.For(role);

        OperativeDefinition op = botOp != null ? OperativeRoster.Get(botOp.Id) : null;
        float maxHp = op != null ? op.maxHealth : stats.maxHealth;
        float maxArmor = op != null ? op.maxArmor : stats.maxArmor;
        float moveSpeed = op != null ? op.moveSpeed : stats.moveSpeed;

        health.ConfigurePools(maxHp, maxArmor);

        if (agent != null)
        {
            agent.speed = moveSpeed * 0.85f;
            baseAgentSpeed = agent.speed;
            agent.acceleration = stats.groundAcceleration;
            agent.angularSpeed = 720f;
        }

        ApplyBodyScale(op != null ? op.role : HeroRole.Dps);

        if (weapon == null && preset != null && preset.weapons.Count > 0)
        {
            weapon = preset.weapons[0];
        }

        switch (role)
        {
            case LoadoutRole.Light:
                preferredRange = 10f;
                reactionSeconds = 0.26f;
                break;
            case LoadoutRole.Heavy:
                preferredRange = 8f;
                reactionSeconds = 0.4f;
                break;
            default:
                preferredRange = 16f;
                reactionSeconds = 0.32f;
                break;
        }
    }

    private void ApplyBodyScale(HeroRole heroRole)
    {
        float scale = OperativeBody.ScaleFor(heroRole);

        OperativeBody.ApplyCapsule(GetComponent<CapsuleCollider>(), scale);

        if (agent != null)
        {
            agent.height = OperativeBody.BaseHeight * scale;
            agent.radius = OperativeBody.BaseRadius * Mathf.Lerp(1f, scale, 0.35f);
        }

        if (visuals != null)
        {
            visuals.localScale = Vector3.one * scale;
        }

        Transform head = transform.Find("Head");
        if (head != null)
        {
            head.localPosition = new Vector3(0f, OperativeBody.BaseHeadLocalY * scale, 0f);
            head.localScale = Vector3.one * (OperativeBody.BaseHeadScale * scale);
        }
    }

    public void ApplyStun(float seconds)
    {
        stunnedUntil = Mathf.Max(stunnedUntil, Time.time + seconds);
    }

    public void ApplyKnockback(Vector3 velocity)
    {
        velocity.y = 0f;
        knockback += Vector3.ClampMagnitude(velocity, 30f);
    }

    public void NavLaunch(Vector3 velocity, float seconds)
    {
        velocity.y = 0f;
        dashVelocity = Vector3.ClampMagnitude(velocity, 25f);
        dashUntil = Time.time + Mathf.Clamp(seconds, 0.2f, 0.8f);
    }

    public void NavBlink(Vector3 worldPos, float yaw)
    {
        if (agent == null)
        {
            transform.position = worldPos;
            return;
        }

        Vector3 dest = worldPos;
        if (NavMesh.SamplePosition(worldPos, out NavMeshHit hit, 4f, NavMesh.AllAreas))
        {
            dest = hit.position;
        }

        agent.Warp(dest);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        aimDirection = transform.forward;
    }

    private void HandleDamaged(float amount, DamageInfo info)
    {
        lastHurtTime = Time.time;
        lastCombatTime = Time.time;
    }

    private void Update()
    {
        if (state == State.Dead || !health.IsAlive)
        {
            return;
        }

        if (status == null)
        {
            status = GetComponent<StatusEffects>();
        }

        if (agent != null && agent.isOnNavMesh)
        {
            agent.speed = baseAgentSpeed * (status != null ? status.MoveMultiplier : 1f);
        }

        if (Time.time < stunnedUntil)
        {
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
            }

            return;
        }

        if (knockback.sqrMagnitude > 0.04f)
        {
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.Move(knockback * Time.deltaTime);
            }

            knockback *= Mathf.Exp(-Time.deltaTime / 0.12f);
            return;
        }

        if (Time.time < dashUntil)
        {
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.Move(dashVelocity * Time.deltaTime);
            }

            FaceAim();
            return;
        }

        if (agent != null && agent.isOnNavMesh && agent.isStopped)
        {
            agent.isStopped = false;
        }

        AcquireTarget();

        if (weapon != null && weapon.healPerHit > 0f && weapon.damage <= 1f)
        {

            state = State.Engage;
            TickHealSupport();
        }
        else if (target != null)
        {
            state = State.Engage;
            TickEngage();
        }
        else
        {
            state = State.Advance;
            TickAdvance();
        }

        FaceAim();
        ConsiderAbilities();
    }

    private void AcquireTarget()
    {
        if (target != null && (!target.IsAlive || !CanSee(target)))
        {
            target = null;
            targetVisibleSince = -1f;
        }

        if (target != null)
        {
            return;
        }

        Health best = null;
        float bestDistance = float.MaxValue;

        foreach (Health candidate in CombatantRegistry.All)
        {
            if (candidate == null || candidate == health || !candidate.IsAlive)
            {
                continue;
            }

            if (!candidate.Team.IsHostileTo(health.Team))
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, candidate.transform.position);
            if (distance > sightRange || distance >= bestDistance)
            {
                continue;
            }

            if (!CanSee(candidate))
            {
                continue;
            }

            best = candidate;
            bestDistance = distance;
        }

        if (best != null)
        {
            target = best;
            targetVisibleSince = Time.time;
        }
    }

    private bool CanSee(Health other)
    {
        Vector3 origin = eyes.position;
        Vector3 targetPoint = other.transform.position + Vector3.up * 1.1f;
        Vector3 toTarget = targetPoint - origin;
        float distance = toTarget.magnitude;

        if (distance > sightRange)
        {
            return false;
        }

        if (Vector3.Angle(transform.forward, toTarget) > fieldOfView * 0.5f)
        {
            return false;
        }

        if (Physics.Raycast(origin, toTarget.normalized, out RaycastHit hit, distance, sightBlockers, QueryTriggerInteraction.Ignore))
        {
            Health blockerHealth = hit.collider.GetComponentInParent<Health>();
            if (blockerHealth != other)
            {
                return false;
            }
        }

        return true;
    }

    private void TickAdvance()
    {
        if (Time.time < nextRepathTime || agent == null || !agent.isOnNavMesh)
        {
            return;
        }

        nextRepathTime = Time.time + 1.2f;

        if (agent.remainingDistance > 1.5f && agent.hasPath)
        {
            return;
        }

        patrolTarget = PickAdvanceTarget();
        agent.isStopped = false;
        agent.SetDestination(patrolTarget);
        aimDirection = Vector3.Slerp(aimDirection, (patrolTarget - transform.position).normalized, 0.5f);
    }

    private Vector3 PickAdvanceTarget()
    {
        Vector3 anchor = PickAnchor();

        Vector2 jitter = Random.insideUnitCircle * 6f;
        Vector3 candidate = anchor + new Vector3(jitter.x, 0f, jitter.y);

        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 8f, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return anchor;
    }

    private Vector3 PickAnchor()
    {
        if (ObjectiveAnchors.Count > 0)
        {
            List<Transform> valid = new List<Transform>();

            foreach (Transform candidate in ObjectiveAnchors)
            {
                if (candidate != null)
                {
                    valid.Add(candidate);
                }
            }

            if (valid.Count > 0)
            {
                return valid[Random.Range(0, valid.Count)].position;
            }
        }

        Team enemyTeam = health.Team == Team.Attackers ? Team.Defenders : Team.Attackers;
        List<SpawnPoint> enemySpawns = SpawnPoint.For(enemyTeam);

        return enemySpawns.Count > 0
            ? enemySpawns[Random.Range(0, enemySpawns.Count)].transform.position
            : transform.position;
    }

    private void TickEngage()
    {
        lastCombatTime = Time.time;

        Vector3 toTarget = target.transform.position - transform.position;
        float distance = toTarget.magnitude;

        aimDirection = Vector3.Slerp(
            aimDirection,
            (target.transform.position + Vector3.up * 1.1f - eyes.position).normalized,
            aimTurnSpeed * Time.deltaTime
        );

        if (agent != null && agent.isOnNavMesh)
        {
            if (Time.time >= nextStrafeTime)
            {
                nextStrafeTime = Time.time + strafeChangeSeconds * Random.Range(0.6f, 1.4f);
                strafeSign = Random.value > 0.5f ? 1f : -1f;
            }

            Vector3 flatToTarget = new Vector3(toTarget.x, 0f, toTarget.z).normalized;
            Vector3 strafe = Vector3.Cross(Vector3.up, flatToTarget) * strafeSign;
            float closeIn = Mathf.Clamp(distance - preferredRange, -6f, 6f);
            Vector3 desired = transform.position + flatToTarget * closeIn + strafe * 3f;

            if (NavMesh.SamplePosition(desired, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            {
                agent.isStopped = false;
                agent.SetDestination(hit.position);
            }
        }

        TryShoot(distance);
    }

    private void TickHealSupport()
    {
        Health ally = NearestHurtVisibleAlly(weapon.range);

        if (ally == null)
        {
            TickAdvance();
            return;
        }

        lastCombatTime = Time.time;
        Vector3 to = ally.transform.position + Vector3.up * 1.1f - eyes.position;
        aimDirection = Vector3.Slerp(aimDirection, to.normalized, aimTurnSpeed * Time.deltaTime);

        if (agent != null && agent.isOnNavMesh)
        {
            bool tooFar = to.magnitude > weapon.range * 0.75f;
            agent.isStopped = false;
            agent.SetDestination(tooFar ? ally.transform.position : transform.position);
        }

        if (Time.time >= nextShotTime && Vector3.Angle(aimDirection, to.normalized) < 9f)
        {
            FireOnce(to.normalized);
            nextShotTime = Time.time + weapon.SecondsBetweenShots;
        }
    }

    private Health NearestHurtVisibleAlly(float range)
    {
        Health best = null;
        float bestScore = 0.95f;

        foreach (Health candidate in CombatantRegistry.All)
        {
            if (candidate == null || !candidate.IsAlive || candidate == health
                || candidate.Team.IsHostileTo(health.Team))
            {
                continue;
            }

            if (Vector3.Distance(transform.position, candidate.transform.position) > range)
            {
                continue;
            }

            if (candidate.HealthFraction < bestScore)
            {
                bestScore = candidate.HealthFraction;
                best = candidate;
            }
        }

        return best;
    }

    private void TryShoot(float distance)
    {
        if (weapon == null || targetVisibleSince < 0f)
        {
            return;
        }

        if (Time.time - targetVisibleSince < reactionSeconds)
        {
            return;
        }

        if (Time.time < burstRestUntil || Time.time < nextShotTime)
        {
            return;
        }

        if (distance > weapon.range)
        {
            return;
        }

        Vector3 toTarget = (target.transform.position + Vector3.up * 1.1f - eyes.position).normalized;
        if (Vector3.Angle(aimDirection, toTarget) > 6f)
        {
            return;
        }

        if (burstEndTime <= Time.time)
        {
            burstEndTime = Time.time + burstSeconds * Random.Range(0.7f, 1.3f);
        }

        FireOnce(toTarget);

        nextShotTime = Time.time + weapon.SecondsBetweenShots;

        if (Time.time >= burstEndTime)
        {
            burstRestUntil = Time.time + burstRestSeconds * Random.Range(0.7f, 1.5f);
            burstEndTime = 0f;
        }
    }

    private void FireOnce(Vector3 direction)
    {
        Vector2 disc = Random.insideUnitCircle * Mathf.Tan(aimErrorDegrees * Mathf.Deg2Rad);
        Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
        Vector3 up = Vector3.Cross(direction, right);
        Vector3 shot = (direction + right * disc.x + up * disc.y).normalized;

        Vector3 origin = eyes.position;

        if (!Physics.Raycast(origin, shot, out RaycastHit hit, weapon.range, sightBlockers, QueryTriggerInteraction.Ignore))
        {
            WeaponFx.Instance.PlayTracer(origin, origin + shot * weapon.range, weapon.tracerColor);
            return;
        }

        WeaponFx.Instance.PlayTracer(origin, hit.point, weapon.tracerColor);

        if (hit.collider.transform.IsChildOf(transform))
        {
            return;
        }

        Hitbox hitbox = hit.collider.GetComponent<Hitbox>();
        Health hitHealth = hitbox != null ? hitbox.Owner : hit.collider.GetComponentInParent<Health>();
        IDamageable damageable = hitbox != null ? (IDamageable)hitbox : hitHealth;

        if (weapon.healPerHit > 0f && hitHealth != null && hitHealth.IsAlive
            && hitHealth != health && !hitHealth.Team.IsHostileTo(health.Team) && !hitHealth.IsDecoy)
        {
            float before = hitHealth.CurrentHealth;
            hitHealth.Heal(weapon.healPerHit);
            float healed = hitHealth.CurrentHealth - before;
            if (healed > 0f)
            {
                botOp?.ReportHealingDone(healed);
                MatchStats.Instance?.RecordHeal(health, hitHealth, healed);
            }

            WeaponFx.Instance.PlayImpact(hit.point, hit.normal, new Color(0.3f, 0.95f, 0.55f));
            return;
        }

        if (damageable == null)
        {
            WeaponFx.Instance.PlayImpact(hit.point, hit.normal, new Color(0.86f, 0.86f, 0.92f));
            return;
        }

        float zone = hitbox != null ? weapon.ZoneMultiplier(hitbox.HitZone) : 1f;
        float amount = weapon.DamageAtDistance(hit.distance) * zone;

        DamageInfo info = new DamageInfo(amount, hit.point, shot, gameObject, weapon.displayName)
        {
            impactForce = weapon.impactForce
        };

        float dealt = damageable.ApplyDamage(info);
        if (dealt > 0f)
        {
            WeaponFx.Instance.PlayImpact(hit.point, hit.normal, new Color(0.92f, 0.16f, 0.22f));
            botOp?.ReportDamageDealt(dealt);
            botOp?.OnWeaponHit();
        }
    }

    private void FaceAim()
    {
        Vector3 flat = new Vector3(aimDirection.x, 0f, aimDirection.z);
        if (flat.sqrMagnitude < 0.0001f)
        {
            return;
        }

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(flat),
            aimTurnSpeed * Time.deltaTime
        );
    }

    private void ConsiderAbilities()
    {
        if (botOp == null || botOp.Abilities.Count == 0 || Time.time < nextAbilityThink)
        {
            return;
        }

        nextAbilityThink = Time.time + Random.Range(0.35f, 0.7f);

        float hp = health.HealthFraction;
        bool hurt = Time.time - lastHurtTime < 1.5f;
        bool fightLive = Time.time - lastCombatTime < 2.5f;
        float distToTarget = target != null ? Vector3.Distance(transform.position, target.transform.position) : 999f;
        Vector3 toTarget = target != null ? (target.transform.position - transform.position) : transform.forward;

        int enemiesSeen = CountVisibleEnemies(out float lowestEnemyHp);
        Health hurtAlly = FindHurtAlly(0.6f);

        foreach (Ability ability in botOp.Abilities)
        {
            if (ability == null || ability.IsUltimate || !ability.CanActivate())
            {
                continue;
            }

            if (!WantsNative(ability, hp, hurt, fightLive, distToTarget, hurtAlly))
            {
                continue;
            }

            AimForCast(ability, toTarget);
            if (botOp.TryCast(ability.slot))
            {
                nextAbilityThink = Time.time + Random.Range(0.6f, 1.1f);
                break;
            }
        }

        Ability ult = botOp.Get(Ability.Slot.Ultimate);
        if (ult != null && botOp.UltReady && !ult.IsActive && Time.time > ultLockoutUntil
            && Random.value > 0.3f
            && WantsUlt(ult, hp, fightLive, distToTarget, enemiesSeen, lowestEnemyHp, hurtAlly != null)
            && botOp.TryCast(Ability.Slot.Ultimate))
        {
            ultLockoutUntil = Time.time + 2f;
        }
    }

    private bool WantsNative(Ability ability, float hp, bool hurt, bool fightLive, float distToTarget, Health hurtAlly)
    {
        bool seeTarget = target != null && CanSee(target);

        switch (ability.Ai)
        {
            case Ability.AiHint.OffensiveBurst:
            case Ability.AiHint.OffensivePoke:
            case Ability.AiHint.Control:
                return seeTarget && distToTarget <= ability.AiRange;

            case Ability.AiHint.Deployable:
                return fightLive && distToTarget <= ability.AiRange && (hurt || Random.value < 0.5f);

            case Ability.AiHint.GapClose:
                return seeTarget && hp > 0.35f && distToTarget > 10f && distToTarget < 32f;

            case Ability.AiHint.Escape:
                return hp < 0.32f && hurt;

            case Ability.AiHint.Nova:
                return (seeTarget && distToTarget <= ability.AiRange) || (hurtAlly != null && hp < 0.7f);

            case Ability.AiHint.TeamHeal:
                return hp < 0.65f || hurtAlly != null;

            case Ability.AiHint.TeamShield:
                return hurtAlly != null || (fightLive && Random.value < 0.4f);

            case Ability.AiHint.Revive:
                return MatchManager.Instance != null && MatchManager.Instance.HasPendingRevive(health.Team);

            case Ability.AiHint.SelfMobility:
                return false;

            default:
                return false;
        }
    }

    private bool WantsUlt(Ability ult, float hp, bool fightLive, float distToTarget, int enemiesSeen, float lowestEnemyHp, bool allyLow)
    {
        bool inReach = target != null && CanSee(target) && distToTarget <= ult.AiRange;

        switch (ult.Ai)
        {
            case Ability.AiHint.UltOffensive:
                return (enemiesSeen >= 2 && inReach)
                    || (lowestEnemyHp < 0.4f && inReach)
                    || (hp > 0.6f && inReach && enemiesSeen >= 1);

            case Ability.AiHint.UltDefensive:
                return (hp < 0.4f && fightLive)
                    || (allyLow && enemiesSeen >= 1)
                    || (Time.time - lastHurtTime < 2f && hp < 0.6f && enemiesSeen >= 1);

            case Ability.AiHint.UltUtility:
                return fightLive && enemiesSeen >= 1 && (allyLow || enemiesSeen >= 2 || Random.value < 0.5f);

            default:
                return false;
        }
    }

    private void AimForCast(Ability ability, Vector3 toTarget)
    {
        if (target == null)
        {
            return;
        }

        Vector3 dir = ability.Ai == Ability.AiHint.Escape ? -toTarget : toTarget;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f)
        {
            return;
        }

        dir.Normalize();
        transform.rotation = Quaternion.LookRotation(dir);
        aimDirection = dir;
    }

    private int CountVisibleEnemies(out float lowestHpFraction)
    {
        lowestHpFraction = 1f;
        int count = 0;

        foreach (Health candidate in CombatantRegistry.All)
        {
            if (candidate == null || !candidate.IsAlive || candidate == health
                || !candidate.Team.IsHostileTo(health.Team))
            {
                continue;
            }

            if (Vector3.Distance(transform.position, candidate.transform.position) > sightRange || !CanSee(candidate))
            {
                continue;
            }

            count++;
            if (candidate.HealthFraction < lowestHpFraction)
            {
                lowestHpFraction = candidate.HealthFraction;
            }
        }

        return count;
    }

    public Health PickAllyToSupport(float range)
    {
        Health best = health;
        float bestFraction = health.HealthFraction;

        foreach (Health candidate in CombatantRegistry.All)
        {
            if (candidate == null || !candidate.IsAlive || candidate == health
                || candidate.Team.IsHostileTo(health.Team))
            {
                continue;
            }

            if (Vector3.Distance(transform.position, candidate.transform.position) > range)
            {
                continue;
            }

            if (candidate.HealthFraction < bestFraction)
            {
                bestFraction = candidate.HealthFraction;
                best = candidate;
            }
        }

        return best;
    }

    private Health FindHurtAlly(float threshold)
    {
        Health best = null;
        float bestFraction = threshold;

        foreach (Health candidate in CombatantRegistry.All)
        {
            if (candidate == null || !candidate.IsAlive || candidate == health
                || candidate.Team.IsHostileTo(health.Team))
            {
                continue;
            }

            if (Vector3.Distance(transform.position, candidate.transform.position) > 35f)
            {
                continue;
            }

            if (candidate.HealthFraction < bestFraction)
            {
                bestFraction = candidate.HealthFraction;
                best = candidate;
            }
        }

        return best;
    }

    private void HandleDied(DamageInfo info)
    {
        state = State.Dead;
        target = null;

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        if (visuals != null)
        {
            visuals.gameObject.SetActive(false);
        }

        SetCollidersEnabled(false);
    }

    private void SetCollidersEnabled(bool value)
    {
        foreach (Collider collider in GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = value;
        }
    }
}
