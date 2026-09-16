using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponController : MonoBehaviour
{
    [Header("Loadout")]
    [SerializeField] private List<WeaponDefinition> weapons = new List<WeaponDefinition>();
    [SerializeField] private int startingIndex = 0;

    [Header("Rig")]
    [SerializeField] private Transform aimOrigin;
    [SerializeField] private WeaponView view;
    [SerializeField] private Camera viewCamera;

    [Header("Targeting")]
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("Owner")]
    [SerializeField] private Health ownerHealth;
    [SerializeField] private PlayerLook look;
    [SerializeField] private movement mover;

    private StatusEffects ownerStatus;

    private readonly Dictionary<WeaponDefinition, AmmoState> ammo = new Dictionary<WeaponDefinition, AmmoState>();
    private readonly RaycastHit[] hitBuffer = new RaycastHit[16];

    private WeaponDefinition current;
    private float nextShotTime;
    private float bloom;
    private float reloadEndTime;
    private bool isReloading;
    private bool isAiming;
    private float adsBlend;
    private float baseFieldOfView = 70f;
    private bool inputEnabled = true;

    private bool netDriven;
    private bool netFireHeld;
    private bool netFirePressed;
    private bool netAim;
    private bool netReloadPressed;
    private int netSlot;

    public float NetShooterLatency { get; set; }
    private bool rmbIsAbility;

    public event Action<WeaponDefinition, int, int> AmmoChanged;

    public event Action<float, bool, bool> HitConfirmed;

    public event Action<float> AllyHealed;

    public event Action<WeaponDefinition> WeaponChanged;
    public event Action<bool> ReloadStateChanged;

    public WeaponDefinition Current => current;
    public bool IsAiming => isAiming;

    public float AdsProgress01 => adsBlend;

    public bool ScopeActive => current != null && current.optic == OpticType.Scoped && adsBlend > 0.5f;

    public bool IsReloading => isReloading;
    public float ReloadProgress => isReloading && current != null
        ? Mathf.Clamp01(1f - (reloadEndTime - Time.time) / current.reloadSeconds)
        : 1f;
    public float SpreadDegrees => CurrentSpread();
    public float MoveSpeedMultiplier => current == null ? 1f : Mathf.Lerp(1f, current.adsMoveSpeedMultiplier, adsBlend);
    public int MagazineAmmo => current != null && ammo.TryGetValue(current, out AmmoState state) ? state.magazine : 0;

    public int ReserveAmmo => int.MaxValue;

    public bool UsesMagazine => current != null && !current.infiniteAmmo;

    public IReadOnlyList<WeaponDefinition> Weapons => weapons;

    public int EquippedIndex => current == null ? -1 : weapons.IndexOf(current);

    public int MagazineFor(WeaponDefinition weapon) =>
        weapon != null && ammo.TryGetValue(weapon, out AmmoState state) ? state.magazine : 0;

    private class AmmoState
    {
        public int magazine;
        public int reserve;
    }

    private void Awake()
    {
        if (ownerHealth == null)
        {
            ownerHealth = GetComponentInParent<Health>();
        }

        ownerStatus = GetComponentInParent<StatusEffects>();

        if (look == null)
        {
            look = GetComponentInParent<PlayerLook>();
        }

        if (mover == null)
        {
            mover = GetComponentInParent<movement>();
        }

        if (aimOrigin == null && look != null)
        {
            aimOrigin = look.CameraPivot;
        }

        RefillAll();
    }

    private void Start()
    {
        if (viewCamera == null)
        {
            viewCamera = GetComponentInChildren<Camera>();
        }

        if (viewCamera == null)
        {
            viewCamera = Camera.main;
        }

        if (viewCamera != null)
        {
            baseFieldOfView = viewCamera.fieldOfView;
        }

        if (current != null)
        {

            if (view != null)
            {
                view.ShowWeapon(current);
            }

            RaiseAmmoChanged();
            WeaponChanged?.Invoke(current);
            return;
        }

        Equip(Mathf.Clamp(startingIndex, 0, Mathf.Max(0, weapons.Count - 1)));
    }

    private void Update()
    {
        if (current == null)
        {
            return;
        }

        UpdateReload();
        UpdateBloom();

        if (inputEnabled && IsOwnerAlive())
        {
            ReadSwitchInput();
            ReadAimInput();
            ReadReloadInput();
            ReadFireInput();
        }
        else
        {
            isAiming = false;
        }

        UpdateAdsBlend();
    }

    public void SetInputEnabled(bool value)
    {
        inputEnabled = value;

        if (!value)
        {
            isAiming = false;
        }
    }

    public void SetNetDriven(bool value)
    {
        netDriven = value;

        if (value)
        {
            netFireHeld = false;
            netFirePressed = false;
            netAim = false;
            netReloadPressed = false;
            netSlot = 0;
        }
    }

    public void ApplyNetInput(bool fireHeld, bool firePressed, bool aim, bool reloadPressed, int slot)
    {
        netFireHeld = fireHeld;
        netAim = aim;
        netSlot = slot;

        if (firePressed)
        {
            netFirePressed = true;
        }

        if (reloadPressed)
        {
            netReloadPressed = true;
        }
    }

    public void SetRmbAbilityMode(bool value)
    {
        rmbIsAbility = value;

        if (value)
        {
            isAiming = false;
        }
    }

    public void SetWeapons(IEnumerable<WeaponDefinition> newWeapons, int equipIndex = 0)
    {
        weapons.Clear();
        weapons.AddRange(newWeapons);
        ammo.Clear();
        RefillAll();

        current = null;
        Equip(Mathf.Clamp(equipIndex, 0, Mathf.Max(0, weapons.Count - 1)));
    }

    public void ForceReload()
    {
        if (current == null || !ammo.TryGetValue(current, out AmmoState state))
        {
            return;
        }

        CancelReload();
        state.magazine = current.magazineSize;
        bloom = 0f;
        RaiseAmmoChanged();
    }

    public void RefillAll()
    {
        foreach (WeaponDefinition weapon in weapons)
        {
            if (weapon == null)
            {
                continue;
            }

            if (!ammo.TryGetValue(weapon, out AmmoState state))
            {
                state = new AmmoState();
                ammo[weapon] = state;
            }

            state.magazine = weapon.magazineSize;
            state.reserve = weapon.reserveAmmo;
        }

        CancelReload();
        RaiseAmmoChanged();
    }

    public void Equip(int index)
    {
        if (weapons.Count == 0)
        {
            return;
        }

        index = Mathf.Clamp(index, 0, weapons.Count - 1);
        WeaponDefinition next = weapons[index];
        if (next == null || next == current)
        {
            return;
        }

        CancelReload();
        current = next;
        startingIndex = index;
        bloom = 0f;
        nextShotTime = Time.time + 0.15f;

        if (view != null)
        {
            view.ShowWeapon(current);
        }

        WeaponChanged?.Invoke(current);
        RaiseAmmoChanged();
    }

    private bool IsOwnerAlive()
    {
        return ownerHealth == null || ownerHealth.IsAlive;
    }

    private void ReadSwitchInput()
    {
        if (netDriven)
        {

            if (netSlot >= 1 && netSlot <= weapons.Count)
            {
                Equip(netSlot - 1);
            }

            netSlot = 0;
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame)
            {
                Equip(0);
            }
            else if (keyboard.digit2Key.wasPressedThisFrame)
            {
                Equip(1);
            }
            else if (keyboard.digit3Key.wasPressedThisFrame)
            {
                Equip(2);
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && weapons.Count > 1)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                int index = weapons.IndexOf(current);
                index = (index + (scroll > 0f ? 1 : -1) + weapons.Count) % weapons.Count;
                Equip(index);
            }
        }
    }

    private void ReadAimInput()
    {
        bool wantsAim;

        if (netDriven)
        {
            wantsAim = !rmbIsAbility && netAim;
        }
        else
        {
            Mouse mouse = Mouse.current;
            Gamepad gamepad = Gamepad.current;

            wantsAim = !rmbIsAbility && mouse != null && mouse.rightButton.isPressed;
            if (!wantsAim && gamepad != null)
            {
                wantsAim = gamepad.leftTrigger.ReadValue() > 0.5f;
            }
        }

        isAiming = wantsAim && !isReloading;
    }

    private void ReadReloadInput()
    {
        bool pressed;

        if (netDriven)
        {
            pressed = netReloadPressed;
            netReloadPressed = false;
        }
        else
        {
            Keyboard keyboard = Keyboard.current;
            pressed = keyboard != null && keyboard.rKey.wasPressedThisFrame;

            Gamepad gamepad = Gamepad.current;
            if (!pressed && gamepad != null)
            {
                pressed = gamepad.buttonWest.wasPressedThisFrame;
            }
        }

        if (pressed)
        {
            TryStartReload();
        }
    }

    private void ReadFireInput()
    {
        bool held;
        bool pressed;

        if (netDriven)
        {
            held = netFireHeld;
            pressed = netFirePressed;
            netFirePressed = false;
        }
        else
        {
            Mouse mouse = Mouse.current;
            Gamepad gamepad = Gamepad.current;

            held = mouse != null && mouse.leftButton.isPressed;
            pressed = mouse != null && mouse.leftButton.wasPressedThisFrame;

            if (gamepad != null)
            {
                float trigger = gamepad.rightTrigger.ReadValue();
                held = held || trigger > 0.5f;
                pressed = pressed || gamepad.rightTrigger.wasPressedThisFrame;
            }
        }

        bool wantsFire = current.automatic ? held : pressed;
        if (!wantsFire || isReloading || Time.time < nextShotTime)
        {
            return;
        }

        if (!current.infiniteAmmo && MagazineAmmo <= 0)
        {
            nextShotTime = Time.time + 0.2f;
            TryStartReload();
            return;
        }

        FireOnce();
    }

    private void FireOnce()
    {
        AmmoState state = ammo[current];
        if (!current.infiniteAmmo)
        {
            state.magazine--;
        }

        nextShotTime = Time.time + current.SecondsBetweenShots;

        Vector3 origin = aimOrigin != null ? aimOrigin.position : transform.position;
        Vector3 forward = aimOrigin != null ? aimOrigin.forward : transform.forward;
        float spread = CurrentSpread();

        float totalDamage = 0f;
        bool anyHeadshot = false;
        bool anyKill = false;

        LagCompensation.Rewind(gameObject, LagCompensation.RewindTimeFor(NetShooterLatency));

        try
        {
            for (int i = 0; i < current.pelletsPerShot; i++)
            {
                Vector3 direction = ApplySpread(forward, spread);
                ShotResult result = ResolveRay(origin, direction);

                if (result.damageDealt > 0f)
                {
                    totalDamage += result.damageDealt;
                    anyHeadshot = anyHeadshot || result.headshot;
                    anyKill = anyKill || result.killed;
                }
            }
        }
        finally
        {
            LagCompensation.Restore();
        }

        if (totalDamage > 0f)
        {
            HitConfirmed?.Invoke(totalDamage, anyHeadshot, anyKill);
        }

        bloom = Mathf.Min(current.maxBloom, bloom + current.spreadPerShot);

        if (look != null)
        {
            float sideSign = UnityEngine.Random.value > 0.5f ? 1f : -1f;
            float aimScale = Mathf.Lerp(1f, 0.7f, adsBlend);
            look.AddRecoil(
                current.recoilUp * aimScale,
                current.recoilSide * sideSign * aimScale,
                current.recoilRecovery
            );
        }

        if (view != null)
        {
            view.PlayFireKick(current);
        }

        RaiseAmmoChanged();
    }

    private struct ShotResult
    {
        public float damageDealt;
        public bool headshot;
        public bool killed;
    }

    private ShotResult ResolveRay(Vector3 origin, Vector3 direction)
    {
        ShotResult result = default;

        int count = Physics.RaycastNonAlloc(
            origin,
            direction,
            hitBuffer,
            current.range,
            hitMask,
            QueryTriggerInteraction.Ignore
        );

        RaycastHit best = default;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hitBuffer[i];
            if (IsOwnCollider(hit.collider))
            {
                continue;
            }

            SpawnPanel panel = hit.collider.GetComponent<SpawnPanel>();
            if (panel != null && !panel.BlocksProjectile(OwnerTeam))
            {
                continue;
            }

            if (!found || hit.distance < best.distance)
            {
                best = hit;
                found = true;
            }
        }

        Vector3 endPoint = found ? best.point : origin + direction * current.range;
        WeaponFx.Instance.PlayTracer(MuzzlePosition(origin), endPoint, current.tracerColor);

        if (!found)
        {
            return result;
        }

        Hitbox hitbox = best.collider.GetComponent<Hitbox>();
        Health targetHealth = hitbox != null ? hitbox.Owner : best.collider.GetComponentInParent<Health>();
        IDamageable damageable = hitbox != null
            ? (IDamageable)hitbox
            : (IDamageable)targetHealth;

        if (current.healPerHit > 0f && targetHealth != null && targetHealth.IsAlive
            && targetHealth != ownerHealth && !targetHealth.Team.IsHostileTo(OwnerTeam) && !targetHealth.IsDecoy)
        {
            float before = targetHealth.CurrentHealth;
            targetHealth.Heal(current.healPerHit);
            float healed = targetHealth.CurrentHealth - before;
            if (healed > 0f)
            {
                AllyHealed?.Invoke(healed);
                MatchStats.Instance?.RecordHeal(ownerHealth, targetHealth, healed);
            }

            WeaponFx.Instance.PlayImpact(best.point, best.normal, HealImpactColor);
            return result;
        }

        if (damageable == null || !damageable.IsAlive)
        {
            WeaponFx.Instance.PlayImpact(best.point, best.normal, WorldImpactColor);
            PushRigidbody(best, direction);
            return result;
        }

        float zoneMultiplier = hitbox != null ? current.ZoneMultiplier(hitbox.HitZone) : 1f;
        float outgoingMultiplier = ownerStatus != null ? ownerStatus.OutgoingDamageMultiplier : 1f;
        float amount = current.DamageAtDistance(best.distance) * zoneMultiplier * outgoingMultiplier;

        DamageInfo info = new DamageInfo(amount, best.point, direction, gameObject, current.displayName)
        {
            impactForce = current.impactForce,
            isHeadshot = hitbox != null && hitbox.HitZone == Hitbox.Zone.Head
        };

        float dealt = damageable.ApplyDamage(info);
        if (dealt > 0f)
        {
            result.damageDealt = dealt;
            result.headshot = info.isHeadshot;
            result.killed = !damageable.IsAlive;
            WeaponFx.Instance.PlayImpact(best.point, best.normal, FleshImpactColor);
        }
        else
        {
            WeaponFx.Instance.PlayImpact(best.point, best.normal, WorldImpactColor);
        }

        PushRigidbody(best, direction);
        return result;
    }

    private static readonly Color HealImpactColor = new Color(0.30f, 0.95f, 0.55f, 1f);
    private static readonly Color WorldImpactColor = new Color(0.86f, 0.86f, 0.92f, 1f);
    private static readonly Color FleshImpactColor = new Color(0.92f, 0.16f, 0.22f, 1f);

    private void PushRigidbody(RaycastHit hit, Vector3 direction)
    {
        if (hit.rigidbody != null && !hit.rigidbody.isKinematic)
        {
            hit.rigidbody.AddForceAtPosition(direction * current.impactForce, hit.point, ForceMode.Impulse);
        }
    }

    private Team OwnerTeam => ownerHealth != null ? ownerHealth.Team : Team.None;

    private bool IsOwnCollider(Collider collider)
    {
        Transform root = ownerHealth != null ? ownerHealth.transform : transform.root;
        return collider.transform == root || collider.transform.IsChildOf(root);
    }

    private Vector3 MuzzlePosition(Vector3 fallback)
    {
        return view != null && view.Muzzle != null ? view.Muzzle.position : fallback;
    }

    private Vector3 ApplySpread(Vector3 forward, float spreadDegrees)
    {
        if (spreadDegrees <= 0.001f)
        {
            return forward;
        }

        Vector2 disc = UnityEngine.Random.insideUnitCircle * Mathf.Tan(spreadDegrees * Mathf.Deg2Rad);
        Vector3 right = aimOrigin != null ? aimOrigin.right : transform.right;
        Vector3 up = aimOrigin != null ? aimOrigin.up : transform.up;
        return (forward + right * disc.x + up * disc.y).normalized;
    }

    private float CurrentSpread()
    {
        if (current == null)
        {
            return 0f;
        }

        float baseSpread = Mathf.Lerp(current.hipSpreadDegrees, current.adsSpreadDegrees, adsBlend);
        float total = baseSpread + bloom;

        if (mover != null && !mover.IsGrounded)
        {
            total *= current.airborneSpreadMultiplier;
        }

        return total;
    }

    private void UpdateBloom()
    {
        if (bloom > 0f)
        {
            bloom = Mathf.Max(0f, bloom - current.bloomRecovery * Time.deltaTime);
        }
    }

    private void UpdateAdsBlend()
    {
        float target = isAiming ? 1f : 0f;
        float speed = current != null ? 1f / Mathf.Max(0.02f, current.adsSeconds) : 8f;
        adsBlend = Mathf.MoveTowards(adsBlend, target, speed * Time.deltaTime);

        if (viewCamera != null && current != null)
        {
            viewCamera.fieldOfView = Mathf.Lerp(baseFieldOfView, current.adsFieldOfView, adsBlend);
        }

        if (view != null)
        {
            view.SetAimBlend(adsBlend);
        }
    }

    private void TryStartReload()
    {
        if (isReloading || current == null)
        {
            return;
        }

        AmmoState state = ammo[current];
        if (state.magazine >= current.magazineSize)
        {
            return;
        }

        isReloading = true;
        isAiming = false;
        reloadEndTime = Time.time + current.reloadSeconds;
        ReloadStateChanged?.Invoke(true);
    }

    private void UpdateReload()
    {
        if (!isReloading || Time.time < reloadEndTime)
        {
            return;
        }

        AmmoState state = ammo[current];
        state.magazine = current.magazineSize;

        isReloading = false;
        bloom = 0f;
        ReloadStateChanged?.Invoke(false);
        RaiseAmmoChanged();
    }

    private void CancelReload()
    {
        if (!isReloading)
        {
            return;
        }

        isReloading = false;
        ReloadStateChanged?.Invoke(false);
    }

    private void RaiseAmmoChanged()
    {
        AmmoChanged?.Invoke(current, MagazineAmmo, ReserveAmmo);
    }
}
