using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class AbilityProjectile : MonoBehaviour
{
    private Rigidbody body;
    private float dieAt;
    private float ignoreOwnerUntil;
    private Transform owner;
    private bool done;
    private Action<Vector3, Collider> onImpact;

    public static AbilityProjectile Spawn(
        AbilityContext ctx,
        Vector3 origin,
        Vector3 velocity,
        float gravityScale,
        float radius,
        float life,
        Color color,
        Action<Vector3, Collider> onImpact)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "AbilityProjectile";
        go.transform.position = origin;
        go.transform.localScale = Vector3.one * Mathf.Max(0.15f, radius * 0.5f);
        go.GetComponent<MeshRenderer>().sharedMaterial = AbilityVisuals.SolidUnlit(color);

        SphereCollider col = go.GetComponent<SphereCollider>();
        col.isTrigger = true;

        Rigidbody rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.linearVelocity = velocity;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        AbilityProjectile projectile = go.AddComponent<AbilityProjectile>();
        projectile.body = rb;
        projectile.dieAt = Time.time + life;
        projectile.ignoreOwnerUntil = Time.time + 0.08f;
        projectile.owner = ctx.Transform;
        projectile.onImpact = onImpact;
        projectile.gravity = gravityScale;

        return projectile;
    }

    private float gravity;

    private void FixedUpdate()
    {
        if (gravity != 0f)
        {
            body.linearVelocity += Physics.gravity * (gravity * Time.fixedDeltaTime);
        }

        if (body.linearVelocity.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(body.linearVelocity);
        }
    }

    private void Update()
    {
        if (!done && Time.time >= dieAt)
        {
            Impact(transform.position, null);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (done || Time.time < ignoreOwnerUntil)
        {
            return;
        }

        if (owner != null && (other.transform == owner || other.transform.IsChildOf(owner)))
        {
            return;
        }

        Impact(transform.position, other);
    }

    private void Impact(Vector3 point, Collider hit)
    {
        if (done)
        {
            return;
        }

        done = true;
        onImpact?.Invoke(point, hit);
        Destroy(gameObject);
    }
}
