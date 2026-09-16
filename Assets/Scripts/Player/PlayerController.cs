using System;
using UnityEngine;

[RequireComponent(typeof(Health))]
public class PlayerController : MonoBehaviour
{
    [Header("Parts")]
    [SerializeField] private movement mover;
    [SerializeField] private PlayerLook look;
    [SerializeField] private WeaponController weapons;
    [SerializeField] private Loadout loadout;
    [SerializeField] private OperativeController operative;
    [SerializeField] private Health health;
    [SerializeField] private Transform visuals;

    [Header("Death")]
    [SerializeField] private float deathCameraDrop = 0.45f;

    private bool isDead;
    private bool controlEnabled = true;
    private Vector3 cameraHome;

    public event Action<DamageInfo> Died;
    public event Action Respawned;

    public event Action<float, bool, bool> HitConfirmed;

    public Health Health => health;
    public WeaponController Weapons => weapons;
    public movement Mover => mover;
    public PlayerLook Look => look;
    public Loadout Loadout => loadout;
    public OperativeController Operative => operative;
    public bool IsDead => isDead;

    private void Awake()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }

        if (mover == null)
        {
            mover = GetComponent<movement>();
        }

        if (look == null)
        {
            look = GetComponent<PlayerLook>();
        }

        if (weapons == null)
        {
            weapons = GetComponentInChildren<WeaponController>();
        }

        if (loadout == null)
        {
            loadout = GetComponent<Loadout>();
        }

        if (operative == null)
        {
            operative = GetComponent<OperativeController>();
        }

        if (look != null && look.CameraPivot != null)
        {
            cameraHome = look.CameraPivot.localPosition;
        }
    }

    private void OnEnable()
    {
        health.Died += HandleDied;
        health.Revived += HandleRevived;

        if (weapons != null)
        {
            weapons.HitConfirmed += HandleHitConfirmed;
        }
    }

    private void OnDisable()
    {
        health.Died -= HandleDied;
        health.Revived -= HandleRevived;

        if (weapons != null)
        {
            weapons.HitConfirmed -= HandleHitConfirmed;
        }
    }

    private void Update()
    {
        if (mover != null && weapons != null && !isDead)
        {
            mover.SetSpeedMultiplier(weapons.MoveSpeedMultiplier);
        }
    }

    public void SetControlEnabled(bool value)
    {
        controlEnabled = value;
        ApplyControlState();
    }

    public bool IsNetworkAvatar { get; private set; }

    public void ConfigureAsLocalPlayer()
    {
        operative?.ConfigureOwnership(true);

        int localBodyLayer = LayerMask.NameToLayer("LocalBody");
        if (localBodyLayer < 0)
        {
            return;
        }

        int mask = ~(1 << localBodyLayer);
        foreach (Camera camera in GetComponentsInChildren<Camera>(true))
        {
            camera.cullingMask &= mask;
        }
    }

    public void RaiseNetworkHitConfirm(float damage, bool headshot, bool killed)
    {
        HitConfirmed?.Invoke(damage, headshot, killed);
    }

    public void ConfigureAsNetworkAvatar(bool simulateFromInput)
    {
        IsNetworkAvatar = true;

        operative?.ConfigureOwnership(false);

        foreach (Camera camera in GetComponentsInChildren<Camera>(true))
        {
            camera.gameObject.SetActive(false);
        }

        foreach (AudioListener listener in GetComponentsInChildren<AudioListener>(true))
        {
            listener.enabled = false;
        }

        if (look != null)
        {
            look.SetInputEnabled(false);
        }

        if (simulateFromInput)
        {
            mover?.SetNetDriven(true);
            weapons?.SetNetDriven(true);
            operative?.SetNetDriven(true);
            return;
        }

        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
        }

        if (mover != null)
        {
            mover.enabled = false;
        }

        if (weapons != null)
        {
            weapons.SetInputEnabled(false);
        }

        if (operative != null)
        {
            operative.SetInputEnabled(false);
        }
    }

    public void SetRole(LoadoutRole role)
    {
        if (loadout != null)
        {
            loadout.SetRole(role);
        }
    }

    public void SetOperativeByHeroIndex(int heroIndex)
    {
        if (operative != null)
        {
            operative.SetOperativeByHeroIndex(heroIndex);
        }
    }

    public void Respawn(Vector3 position, float yaw)
    {
        if (mover != null)
        {
            mover.Teleport(position, yaw);
        }
        else
        {
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        }

        if (look != null)
        {
            look.SetAim(yaw, 0f);
        }

        health.ResetHealth();
        ClearDeadState();
    }

    private void HandleDied(DamageInfo info)
    {
        isDead = true;
        ApplyControlState();

        if (look != null && look.CameraPivot != null)
        {
            Vector3 local = look.CameraPivot.localPosition;
            look.CameraPivot.localPosition = new Vector3(local.x, local.y - deathCameraDrop, local.z);
        }

        Died?.Invoke(info);
    }

    private void HandleRevived()
    {
        ClearDeadState();
    }

    private void ClearDeadState()
    {
        isDead = false;

        if (look != null && look.CameraPivot != null)
        {
            look.CameraPivot.localPosition = cameraHome;
        }

        if (weapons != null)
        {
            weapons.RefillAll();
        }

        if (operative != null)
        {
            operative.ResetForRespawn();
        }

        if (visuals != null)
        {
            visuals.gameObject.SetActive(true);
        }

        ApplyControlState();
        Respawned?.Invoke();
    }

    private void HandleHitConfirmed(float damage, bool headshot, bool kill)
    {
        HitConfirmed?.Invoke(damage, headshot, kill);
    }

    private void ApplyControlState()
    {
        bool active = controlEnabled && !isDead;

        if (mover != null)
        {
            mover.SetInputEnabled(active);
        }

        if (look != null)
        {

            look.SetInputEnabled(controlEnabled);
        }

        if (weapons != null)
        {
            weapons.SetInputEnabled(active);
        }

        if (operative != null)
        {
            operative.SetInputEnabled(active);
        }
    }
}
