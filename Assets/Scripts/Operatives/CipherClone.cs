using System.Collections.Generic;
using UnityEngine;

public class CipherClone : MonoBehaviour
{
    private const float Life = 8f;
    private const float SideOffset = 2.2f;

    private Transform source;
    private IAbilityOwner sourceOwner;
    private Vector3 lastSourcePos;
    private float dieAt;

    private readonly List<Ability> abilities = new List<Ability>();
    private AbilityContext cloneContext;

    public static CipherClone Spawn(AbilityContext ctx)
    {
        Vector3 side = ctx.Transform.right * SideOffset;
        Vector3 spawn = ctx.Transform.position + side;

        GameObject go = new GameObject("CipherClone");
        go.transform.position = spawn;
        go.transform.rotation = ctx.Transform.rotation;

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.transform.SetParent(go.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        body.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
        Object.Destroy(body.GetComponent<Collider>());
        body.GetComponent<MeshRenderer>().sharedMaterial = AbilityVisuals.SolidUnlit(new Color(0.56f, 0.46f, 0.95f));

        CapsuleCollider col = go.AddComponent<CapsuleCollider>();
        col.height = 1.8f;
        col.radius = 0.35f;
        col.center = new Vector3(0f, 0.9f, 0f);

        Health health = go.AddComponent<Health>();
        health.Team = ctx.Team;
        health.SetDisplayName("Decoy");
        health.IsDecoy = true;
        health.ConfigurePools(150f, 0f);

        Hitbox hitbox = go.AddComponent<Hitbox>();
        hitbox.Configure(Hitbox.Zone.Body, health);

        health.Died += _ => Object.Destroy(go, 0.1f);

        CipherClone clone = go.AddComponent<CipherClone>();
        clone.source = ctx.Transform;
        clone.sourceOwner = ctx.AbilityOwner;
        clone.lastSourcePos = ctx.Transform.position;
        clone.dieAt = Time.time + Life;

        clone.cloneContext = new AbilityContext
        {
            Owner = go,
            Transform = go.transform,
            Health = health,
            Status = go.AddComponent<StatusEffects>(),
            AbilityOwner = null,
            RequestLaunch = (_, __) => { },
            RequestTeleport = (_, __) => { }
        };

        foreach (OperativeDefinition.AbilitySlot def in OperativeRoster.Get(OperativeId.Cipher).abilities)
        {
            if (def.slot == Ability.Slot.Ultimate)
            {
                continue;
            }

            Ability ability = (Ability)go.AddComponent(def.type);
            ability.slot = def.slot;
            ability.KeyLabel = def.key;
            ability.Bind(clone.cloneContext);
            clone.abilities.Add(ability);
        }

        if (clone.sourceOwner != null)
        {
            clone.sourceOwner.AbilityCast += clone.MirrorCast;
        }

        return clone;
    }

    private void Update()
    {
        if (source == null || Time.time >= dieAt)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 delta = source.position - lastSourcePos;
        lastSourcePos = source.position;
        transform.position += delta;
        transform.rotation = source.rotation;
    }

    private void MirrorCast(Ability.Slot slot)
    {
        if (slot == Ability.Slot.Secondary)
        {
            return;
        }

        foreach (Ability ability in abilities)
        {
            if (ability != null && ability.slot == slot)
            {
                ability.TryActivate();
                return;
            }
        }
    }

    private void OnDestroy()
    {
        if (sourceOwner != null)
        {
            sourceOwner.AbilityCast -= MirrorCast;
        }
    }
}
