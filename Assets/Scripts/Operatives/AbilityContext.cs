using System;
using UnityEngine;

public sealed class AbilityContext
{
    public GameObject Owner;
    public Transform Transform;
    public Health Health;
    public movement Movement;
    public Rigidbody Body;
    public PlayerLook Look;
    public WeaponController Weapons;
    public StatusEffects Status;

    public IAbilityOwner AbilityOwner;

    public Action<Vector3, float> RequestLaunch;

    public Action<Vector3, float> RequestTeleport;

    public Func<float, Health> PreferredAllyTarget;

    public Team Team => Health != null ? Health.Team : Team.None;

    public Vector3 EyePosition => Look != null && Look.CameraPivot != null
        ? Look.CameraPivot.position
        : Transform.position + Vector3.up * 1.6f;

    public Vector3 AimDirection => Look != null ? Look.AimForward : Transform.forward;
}
