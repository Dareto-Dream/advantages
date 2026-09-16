using System.Collections.Generic;
using UnityEngine;

public class StatusEffects : MonoBehaviour
{
    private class Mod
    {
        public string tag;
        public float expireAt;
        public float move = 1f;
        public float incoming = 1f;
        public float outgoing = 1f;
        public bool lockAbilities;
        public bool outlined;
        public bool knockbackImmune;
        public bool debuff;
    }

    public enum OutlineScope
    {
        Team,
        Caster
    }

    private readonly List<Mod> mods = new List<Mod>();
    private GameObject outlineMarker;
    private MeshRenderer outlineRenderer;

    private float outlineUntil = -1f;
    private OutlineScope outlineScope;
    private Health outlineCaster;
    private bool outlineShowHealth;

    private Health ownHealth;

    private const float HitBarSeconds = 5f;
    private float hitBarUntil = -1f;
    private Health hitBarAttacker;
    private GameObject hitBarRoot;
    private Transform hitBarFill;
    private MeshRenderer hitBarFillRenderer;
    private int hitBarStep = -1;
    private Camera hitBarCamera;

    public float MoveMultiplier { get; private set; } = 1f;
    public float IncomingDamageMultiplier { get; private set; } = 1f;
    public float OutgoingDamageMultiplier { get; private set; } = 1f;
    public bool AbilitiesLocked { get; private set; }
    public bool Outlined { get; private set; }
    public bool KnockbackImmune { get; private set; }

    public static StatusEffects For(Health health)
    {
        if (health == null)
        {
            return null;
        }

        StatusEffects existing = health.GetComponent<StatusEffects>();
        return existing != null ? existing : health.gameObject.AddComponent<StatusEffects>();
    }

    private void Awake()
    {
        ownHealth = GetComponent<Health>();
    }

    public void FlashHealthBar(Health attacker)
    {
        if (attacker == null || attacker == ownHealth)
        {
            return;
        }

        hitBarAttacker = attacker;
        hitBarUntil = Time.time + HitBarSeconds;
    }

    public void Apply(
        string tag,
        float duration,
        float move = 1f,
        float incoming = 1f,
        float outgoing = 1f,
        bool lockAbilities = false,
        bool outlined = false,
        bool knockbackImmune = false)
    {
        Mod mod = mods.Find(m => m.tag == tag);
        if (mod == null)
        {
            mod = new Mod { tag = tag };
            mods.Add(mod);
        }

        mod.expireAt = Time.time + duration;
        mod.move = move;
        mod.incoming = incoming;
        mod.outgoing = outgoing;
        mod.lockAbilities = lockAbilities;
        mod.outlined = outlined;
        mod.knockbackImmune = knockbackImmune;
        mod.debuff = move < 1f || incoming > 1f || lockAbilities || outlined;

        Recompute();
    }

    public void ApplyStun(float duration)
    {
        Apply("stun", duration, move: 0.05f, lockAbilities: true);
    }

    public void ApplySilence(float duration)
    {
        Apply("silence", duration, lockAbilities: true);
    }

    public void ApplyOutline(float duration, OutlineScope scope, Health caster, bool showHealth = false)
    {
        outlineUntil = Mathf.Max(outlineUntil, Time.time + duration);
        outlineScope = scope;
        outlineCaster = caster;
        outlineShowHealth = showHealth;
    }

    public bool Has(string tag)
    {
        return mods.Exists(m => m.tag == tag && Time.time < m.expireAt);
    }

    public void Clear(string tag)
    {
        if (mods.RemoveAll(m => m.tag == tag) > 0)
        {
            Recompute();
        }
    }

    public void CleanseDebuffs()
    {
        if (mods.RemoveAll(m => m.debuff) > 0)
        {
            Recompute();
        }
    }

    public void ClearAll()
    {
        mods.Clear();
        Recompute();
    }

    private void Update()
    {
        if (mods.Count > 0 && mods.RemoveAll(m => Time.time >= m.expireAt) > 0)
        {
            Recompute();
        }

        UpdateOutlineMarker();
        UpdateHitBar();
    }

    private void Recompute()
    {
        float move = 1f;
        float incoming = 1f;
        float outgoing = 1f;
        bool locked = false;
        bool outlined = false;
        bool knockbackImmune = false;

        foreach (Mod mod in mods)
        {
            move *= mod.move;
            incoming *= mod.incoming;
            outgoing *= mod.outgoing;
            locked |= mod.lockAbilities;
            outlined |= mod.outlined;
            knockbackImmune |= mod.knockbackImmune;
        }

        MoveMultiplier = Mathf.Clamp(move, 0.05f, 3f);
        IncomingDamageMultiplier = Mathf.Clamp(incoming, 0.1f, 3f);
        OutgoingDamageMultiplier = Mathf.Clamp(outgoing, 0.1f, 3f);
        AbilitiesLocked = locked;
        Outlined = outlined;
        KnockbackImmune = knockbackImmune;
    }

    private void UpdateOutlineMarker()
    {
        bool teamReveal = (Time.time < outlineUntil && outlineScope == OutlineScope.Team) || Outlined;
        if (teamReveal && LocalPlayerSeesOutline(OutlineScope.Team, outlineCaster))
        {
            float until = Time.time < outlineUntil && outlineScope == OutlineScope.Team
                ? outlineUntil
                : Time.time + 0.25f;
            SeeThroughOutline.For(this)?.Show(new Color(1f, 0.36f, 0.14f), until);
        }

        bool casterHealthReveal = Time.time < outlineUntil
            && outlineScope == OutlineScope.Caster
            && outlineShowHealth
            && LocalPlayerSeesOutline(OutlineScope.Caster, outlineCaster);

        UpdateCasterHealthMarker(casterHealthReveal);
    }

    private void UpdateCasterHealthMarker(bool active)
    {
        if (active && outlineMarker == null)
        {
            outlineMarker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            outlineMarker.name = "ReconMarker";
            Object.Destroy(outlineMarker.GetComponent<Collider>());
            outlineMarker.transform.SetParent(transform, false);
            outlineMarker.transform.localPosition = new Vector3(0f, 2.35f, 0f);
            outlineRenderer = outlineMarker.GetComponent<MeshRenderer>();
        }
        else if (!active && outlineMarker != null)
        {
            Object.Destroy(outlineMarker);
            outlineMarker = null;
            outlineRenderer = null;
        }

        if (outlineMarker == null)
        {
            return;
        }

        if (ownHealth == null)
        {
            ownHealth = GetComponent<Health>();
        }

        float frac = ownHealth != null ? ownHealth.HealthFraction : 1f;
        outlineMarker.transform.localScale = new Vector3(0.06f + 0.5f * frac, 0.12f, 0.12f);
        if (outlineRenderer != null)
        {
            outlineRenderer.sharedMaterial = AbilityVisuals.SolidUnlit(
                Color.Lerp(new Color(0.95f, 0.25f, 0.2f), new Color(0.3f, 0.95f, 0.4f), frac));
        }
    }

    private void UpdateHitBar()
    {
        bool alive = ownHealth != null && ownHealth.IsAlive;
        bool hitView = alive && Time.time < hitBarUntil && hitBarAttacker != null && LocalViewerAlliedWith(hitBarAttacker);
        bool supportAllyView = alive && SupportSeesThisAlly();
        bool active = hitView || supportAllyView;

        if (active && hitBarRoot == null)
        {
            BuildHitBar();
        }
        else if (!active && hitBarRoot != null)
        {
            Object.Destroy(hitBarRoot);
            hitBarRoot = null;
            hitBarFill = null;
            hitBarFillRenderer = null;
            hitBarStep = -1;
        }

        if (hitBarRoot == null)
        {
            return;
        }

        hitBarRoot.SetActive(true);

        float frac = Mathf.Clamp01(ownHealth.HealthFraction);
        hitBarFill.localScale = new Vector3(Mathf.Max(0.002f, frac), 0.14f, 1f);
        hitBarFill.localPosition = new Vector3(-(1f - frac) * 0.5f, 0f, -0.02f);

        int step = Mathf.RoundToInt(frac * 20f);
        if (step != hitBarStep && hitBarFillRenderer != null)
        {
            hitBarStep = step;
            float q = step / 20f;
            hitBarFillRenderer.sharedMaterial = AbilityVisuals.SolidUnlit(
                Color.Lerp(new Color(0.90f, 0.20f, 0.15f), new Color(0.30f, 0.90f, 0.35f), q));
        }

        if (hitBarCamera == null)
        {
            hitBarCamera = Camera.main;
        }

        if (hitBarCamera != null)
        {
            hitBarRoot.transform.rotation = Quaternion.LookRotation(
                hitBarRoot.transform.position - hitBarCamera.transform.position);
        }
    }

    private void BuildHitBar()
    {
        hitBarRoot = new GameObject("HitHealthBar");
        hitBarRoot.transform.SetParent(transform, false);

        float top = 2.05f;
        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule != null)
        {
            top = capsule.height + 0.28f;
        }

        hitBarRoot.transform.localPosition = new Vector3(0f, top, 0f);

        GameObject backing = GameObject.CreatePrimitive(PrimitiveType.Quad);
        backing.name = "Backing";
        Object.Destroy(backing.GetComponent<Collider>());
        backing.transform.SetParent(hitBarRoot.transform, false);
        backing.transform.localScale = new Vector3(1.04f, 0.18f, 1f);
        backing.GetComponent<MeshRenderer>().sharedMaterial = AbilityVisuals.SolidUnlit(new Color(0.04f, 0.04f, 0.05f, 1f));

        GameObject fill = GameObject.CreatePrimitive(PrimitiveType.Quad);
        fill.name = "Fill";
        Object.Destroy(fill.GetComponent<Collider>());
        fill.transform.SetParent(hitBarRoot.transform, false);
        fill.transform.localPosition = new Vector3(0f, 0f, -0.02f);
        fill.transform.localScale = new Vector3(1f, 0.14f, 1f);
        hitBarFill = fill.transform;
        hitBarFillRenderer = fill.GetComponent<MeshRenderer>();
    }

    private bool SupportSeesThisAlly()
    {
        MatchManager match = MatchManager.Instance;
        PlayerController localPlayer = match != null ? match.Player : null;
        if (localPlayer == null || ownHealth == null)
        {
            return false;
        }

        Health local = localPlayer.Health;
        if (local == null || local == ownHealth || local.Team.IsHostileTo(ownHealth.Team))
        {
            return false;
        }

        OperativeController op = localPlayer.Operative;
        return op != null && op.Definition.role == HeroRole.Support;
    }

    private bool LocalViewerAlliedWith(Health attacker)
    {
        if (attacker == null)
        {
            return false;
        }

        MatchManager match = MatchManager.Instance;
        Health local = match != null && match.Player != null ? match.Player.Health : null;
        if (local == null)
        {
            return true;
        }

        return local == attacker || !local.Team.IsHostileTo(attacker.Team);
    }

    private bool LocalPlayerSeesOutline(OutlineScope scope, Health caster)
    {
        MatchManager match = MatchManager.Instance;
        Health local = match != null && match.Player != null ? match.Player.Health : null;
        if (local == null)
        {
            return true;
        }

        if (scope == OutlineScope.Caster)
        {
            return caster != null && local == caster;
        }

        return caster == null || !local.Team.IsHostileTo(caster.Team);
    }

    private void OnDisable()
    {
        if (outlineMarker != null)
        {
            Object.Destroy(outlineMarker);
            outlineMarker = null;
        }

        if (hitBarRoot != null)
        {
            Object.Destroy(hitBarRoot);
            hitBarRoot = null;
            hitBarFill = null;
            hitBarFillRenderer = null;
            hitBarStep = -1;
        }

        hitBarUntil = -1f;
    }
}
