using System;
using System.Collections.Generic;
using UnityEngine;

public class WeaponView : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        public WeaponDefinition weapon;
        public Transform model;
        public Transform muzzle;
    }

    [Header("Models")]
    [SerializeField] private List<Entry> entries = new List<Entry>();

    [Header("Poses (local to the camera pivot)")]
    [SerializeField] private Vector3 hipPosition = new Vector3(0.17f, -0.14f, 0.30f);
    [SerializeField] private Vector3 adsPosition = new Vector3(0f, -0.075f, 0.30f);
    [SerializeField] private Vector3 hipEuler = new Vector3(0f, 0f, 0f);
    [SerializeField] private Vector3 adsEuler = Vector3.zero;

    [Header("Spring")]
    [SerializeField] private float kickBack = 0.055f;
    [SerializeField] private float kickPitch = 5.5f;
    [SerializeField] private float kickRecovery = 14f;

    [Header("Sway and Bob")]
    [SerializeField] private float swayAmount = 0.012f;
    [SerializeField] private float swayMax = 0.05f;
    [SerializeField] private float swaySmoothing = 10f;
    [SerializeField] private float bobAmount = 0.018f;
    [SerializeField] private float bobFrequency = 9f;

    [SerializeField] private movement mover;
    [SerializeField] private PlayerLook look;

    private Entry activeEntry;
    private float aimBlend;
    private float kickOffset;
    private float kickAngle;
    private Vector3 swayOffset;
    private float bobPhase;
    private Vector3 lastLookAngles;

    public Transform Muzzle => activeEntry != null ? activeEntry.muzzle : transform;

    private static WeaponDefinition cipherWeapon;
    private static bool cipherWeaponResolved;

    private static WeaponDefinition CipherWeaponOverride()
    {
        if (!cipherWeaponResolved)
        {
            cipherWeaponResolved = true;
            List<WeaponDefinition> kit = OperativeWeaponLibrary.WeaponsFor(OperativeId.Cipher);
            cipherWeapon = kit != null && kit.Count > 0 ? kit[0] : null;
        }

        return cipherWeapon;
    }

    private void Awake()
    {
        if (mover == null)
        {
            mover = GetComponentInParent<movement>();
        }

        if (look == null)
        {
            look = GetComponentInParent<PlayerLook>();
        }

        foreach (Entry entry in entries)
        {
            if (entry.model != null)
            {
                entry.model.gameObject.SetActive(false);
            }
        }
    }

    public void ShowWeapon(WeaponDefinition weapon)
    {
        WeaponDefinition displayWeapon = CipherWeaponOverride() ?? weapon;
        activeEntry = null;

        foreach (Entry entry in entries)
        {
            bool isActive = entry.weapon == displayWeapon;
            if (entry.model != null)
            {
                entry.model.gameObject.SetActive(isActive);
            }

            if (isActive)
            {
                activeEntry = entry;
            }
        }
    }

    public void SetAimBlend(float blend)
    {
        aimBlend = Mathf.Clamp01(blend);
    }

    public void PlayFireKick(WeaponDefinition weapon)
    {
        kickOffset = Mathf.Min(kickOffset + kickBack, kickBack * 2.5f);
        kickAngle = Mathf.Min(kickAngle + kickPitch, kickPitch * 2.5f);
    }

    private void LateUpdate()
    {
        UpdateSway();
        UpdateBob();

        kickOffset = Mathf.Lerp(kickOffset, 0f, kickRecovery * Time.deltaTime);
        kickAngle = Mathf.Lerp(kickAngle, 0f, kickRecovery * Time.deltaTime);

        Vector3 basePosition = Vector3.Lerp(hipPosition, adsPosition, aimBlend);
        Vector3 baseEuler = Vector3.Lerp(hipEuler, adsEuler, aimBlend);

        float freedom = Mathf.Lerp(1f, 0.25f, aimBlend);

        transform.localPosition = basePosition
            + swayOffset * freedom
            + new Vector3(0f, Mathf.Sin(bobPhase * 2f) * bobAmount * freedom, 0f)
            + new Vector3(Mathf.Cos(bobPhase) * bobAmount * freedom, 0f, -kickOffset);

        transform.localRotation = Quaternion.Euler(baseEuler + new Vector3(-kickAngle, 0f, 0f));
    }

    private void UpdateSway()
    {
        if (look == null)
        {
            return;
        }

        Vector3 angles = new Vector3(look.Pitch, look.Yaw, 0f);
        Vector3 delta = new Vector3(
            Mathf.DeltaAngle(lastLookAngles.x, angles.x),
            Mathf.DeltaAngle(lastLookAngles.y, angles.y),
            0f
        );
        lastLookAngles = angles;

        Vector3 target = new Vector3(
            Mathf.Clamp(-delta.y * swayAmount, -swayMax, swayMax),
            Mathf.Clamp(delta.x * swayAmount, -swayMax, swayMax),
            0f
        );

        swayOffset = Vector3.Lerp(swayOffset, target, swaySmoothing * Time.deltaTime);
    }

    private void UpdateBob()
    {
        if (mover == null)
        {
            return;
        }

        float speed01 = mover.IsGrounded ? Mathf.Clamp01(mover.HorizontalSpeed / Mathf.Max(0.01f, mover.MoveSpeed)) : 0f;
        bobPhase += Time.deltaTime * bobFrequency * speed01;

        if (speed01 <= 0.01f)
        {
            bobPhase = Mathf.Lerp(bobPhase, Mathf.Round(bobPhase / Mathf.PI) * Mathf.PI, 6f * Time.deltaTime);
        }
    }

    public void SetEntries(List<Entry> newEntries)
    {
        entries = newEntries;
    }
}
