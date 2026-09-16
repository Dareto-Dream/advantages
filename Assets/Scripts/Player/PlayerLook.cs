using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerLook : MonoBehaviour
{
    [Header("Sensitivity")]
    [SerializeField] private float mouseDegreesPerCount = 0.12f;
    [SerializeField] private float gamepadDegreesPerSecond = 220f;

    [Header("Pitch Limits")]
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 85f;

    [Header("Rig")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private float eyeHeight = 0.7f;
    [SerializeField] private bool autoAttachMainCamera = true;

    private Rigidbody rb;
    private float yaw;
    private float pitch;
    private Vector2 recoilAngles;
    private float recoilRecovery = 9f;
    private bool inputEnabled = true;
    private float sensitivityScale = 1f;

    public Transform CameraPivot => cameraPivot;
    public float Yaw => yaw + recoilAngles.y;
    public float Pitch => pitch - recoilAngles.x;
    public Vector3 AimForward => cameraPivot != null ? cameraPivot.forward : transform.forward;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        yaw = transform.eulerAngles.y;
        pitch = 0f;

        EnsureRig();
    }

    private void Update()
    {
        if (inputEnabled && CursorService.IsLocked)
        {
            Vector2 lookDelta = ReadLookDelta();
            yaw = Mathf.Repeat(yaw + lookDelta.x, 360f);
            pitch = Mathf.Clamp(pitch - lookDelta.y, minPitch, maxPitch);
        }

        RecoverRecoil();
    }

    private void LateUpdate()
    {
        if (cameraPivot != null)
        {
            cameraPivot.rotation = Quaternion.Euler(Pitch, Yaw, 0f);
        }

        if (rb == null)
        {
            transform.rotation = Quaternion.Euler(0f, Yaw, 0f);
        }
    }

    private void FixedUpdate()
    {
        if (rb != null)
        {
            rb.MoveRotation(Quaternion.Euler(0f, Yaw, 0f));
        }
    }

    public void SetInputEnabled(bool value)
    {
        inputEnabled = value;
    }

    public void SetSensitivityScale(float value)
    {
        sensitivityScale = Mathf.Clamp(value, 0.05f, 10f);
    }

    public void SetEyeHeight(float value)
    {
        eyeHeight = Mathf.Max(0.2f, value);
        if (cameraPivot != null)
        {
            cameraPivot.localPosition = new Vector3(0f, eyeHeight, 0f);
        }
    }

    public void AddRecoil(float up, float side, float recovery)
    {
        recoilAngles += new Vector2(up, side);
        recoilRecovery = Mathf.Max(0.5f, recovery);
    }

    public void SetAim(float newYaw, float newPitch)
    {
        yaw = Mathf.Repeat(newYaw, 360f);
        pitch = Mathf.Clamp(newPitch, minPitch, maxPitch);
        recoilAngles = Vector2.zero;
    }

    private void RecoverRecoil()
    {
        if (recoilAngles.sqrMagnitude < 0.000001f)
        {
            recoilAngles = Vector2.zero;
            return;
        }

        recoilAngles = Vector2.Lerp(recoilAngles, Vector2.zero, recoilRecovery * Time.deltaTime);
    }

    private Vector2 ReadLookDelta()
    {
        Vector2 delta = Vector2.zero;

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            delta += mouse.delta.ReadValue() * (mouseDegreesPerCount * sensitivityScale);
        }

        Gamepad gamepad = Gamepad.current;
        if (gamepad != null)
        {
            Vector2 stick = gamepad.rightStick.ReadValue();
            delta += stick * (gamepadDegreesPerSecond * sensitivityScale * Time.deltaTime);
        }

        return delta;
    }

    private void EnsureRig()
    {
        if (cameraPivot == null)
        {
            Transform existing = transform.Find("CameraPivot");
            if (existing != null)
            {
                cameraPivot = existing;
            }
            else
            {
                GameObject pivot = new GameObject("CameraPivot");
                cameraPivot = pivot.transform;
                cameraPivot.SetParent(transform, false);
            }
        }

        cameraPivot.localPosition = new Vector3(0f, eyeHeight, 0f);

        if (!autoAttachMainCamera)
        {
            return;
        }

        if (cameraPivot.GetComponentInChildren<Camera>() != null)
        {
            return;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        Transform cameraTransform = camera.transform;
        cameraTransform.SetParent(cameraPivot, false);
        cameraTransform.localPosition = Vector3.zero;
        cameraTransform.localRotation = Quaternion.identity;
    }
}
