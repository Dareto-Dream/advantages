using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class movement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float groundAcceleration = 28f;
    [SerializeField] private float airAcceleration = 10f;
    [SerializeField] private float groundFriction = 7f;

    [Header("Movement Tech")]
    [Tooltip("Wish-speed cap used by the air acceleration model. Low values are what let strafe-jumping build speed.")]
    [SerializeField] private float airSpeedCap = 1.1f;
    [Tooltip("Hard ceiling on horizontal speed, as a multiple of base move speed.")]
    [SerializeField] private float maxSpeedMultiplier = 2.4f;
    [Tooltip("Jumping again within this window after landing skips ground friction - the bunnyhop.")]
    [SerializeField] private float hopFrictionWindow = 0.12f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 6f;
    [SerializeField] private float groundCheckDistance = 0.15f;
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private float jumpBufferTime = 0.14f;
    [SerializeField] private LayerMask groundLayers = ~0;

    private Rigidbody rb;
    private CapsuleCollider capsule;
    private StatusEffects status;
    private Vector2 moveInput;
    private bool isGrounded;
    private bool inputEnabled = true;
    private float speedMultiplier = 1f;
    private float lastGroundedTime;
    private float lastJumpPressedTime = -999f;
    private float lastJumpTime = -999f;
    private float speedClampReleasedUntil = -999f;
    private bool flightMode;

    private bool netDriven;
    private Vector2 netMove;
    private bool netJumpQueued;
    private bool netAscend;
    private bool netDescend;

    public event System.Action Jumped;

    public bool IsGrounded => isGrounded;

    public float MoveSpeed => moveSpeed * speedMultiplier * (status != null ? status.MoveMultiplier : 1f);
    public float HorizontalSpeed => new Vector2(rb.linearVelocity.x, rb.linearVelocity.z).magnitude;
    public Vector2 MoveInput => moveInput;

    public float SpeedExcess01 => Mathf.InverseLerp(MoveSpeed, MoveSpeed * maxSpeedMultiplier, HorizontalSpeed);

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        status = GetComponent<StatusEffects>();

        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void Update()
    {
        if (!inputEnabled)
        {
            moveInput = Vector2.zero;
            return;
        }

        if (netDriven)
        {
            moveInput = netMove;

            if (netJumpQueued)
            {
                netJumpQueued = false;
                lastJumpPressedTime = Time.time;
            }

            return;
        }

        ReadMoveInput();

        if (JumpPressedThisFrame())
        {
            lastJumpPressedTime = Time.time;
        }
    }

    public void SetNetDriven(bool value)
    {
        netDriven = value;

        if (value)
        {
            netMove = Vector2.zero;
            netJumpQueued = false;
        }
    }

    public void ApplyNetInput(Vector2 move, bool jumpPressed, bool ascend, bool descend)
    {
        netMove = Vector2.ClampMagnitude(move, 1f);
        netAscend = ascend;
        netDescend = descend;

        if (jumpPressed)
        {
            netJumpQueued = true;
        }
    }

    private void FixedUpdate()
    {
        isGrounded = CheckGrounded();

        if (isGrounded)
        {
            lastGroundedTime = Time.time;
        }

        if (flightMode)
        {
            FlyMovement();
            return;
        }

        bool jumpQueued = Time.time - lastJumpPressedTime <= jumpBufferTime;
        bool canJump = jumpQueued && Time.time - lastGroundedTime <= coyoteTime && Time.time - lastJumpTime > 0.08f;

        if (isGrounded && !canJump)
        {
            ApplyFriction();
        }

        Vector3 wishDirection = GetMoveDirection();

        if (isGrounded)
        {
            Accelerate(wishDirection, MoveSpeed, groundAcceleration);
        }
        else
        {
            Accelerate(wishDirection, Mathf.Min(MoveSpeed, airSpeedCap), airAcceleration);
        }

        if (canJump)
        {
            DoJump();
        }

        ClampHorizontalSpeed();
    }

    public void ApplyLoadout(LoadoutStats stats)
    {
        moveSpeed = stats.moveSpeed;
        groundAcceleration = stats.groundAcceleration;
        airAcceleration = stats.airAcceleration;
        jumpForce = stats.jumpForce;
    }

    public void SetOperativeStats(float newMoveSpeed, float newJumpForce)
    {
        moveSpeed = newMoveSpeed;
        jumpForce = newJumpForce;
    }

    public void SetBodyScale(float scale)
    {
        if (capsule == null)
        {
            capsule = GetComponent<CapsuleCollider>();
        }

        OperativeBody.ApplyCapsule(capsule, scale);
    }

    public void AirJump()
    {
        Vector3 velocity = rb.linearVelocity;
        velocity.y = Mathf.Max(velocity.y, 0f);
        rb.linearVelocity = velocity;
        rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
        lastJumpTime = Time.time;
    }

    public void SetFlightMode(bool value)
    {
        flightMode = value;
    }

    private void FlyMovement()
    {
        if (!inputEnabled)
        {
            Vector3 idle = rb.linearVelocity;
            idle.y = Mathf.MoveTowards(idle.y, 0f, 20f * Time.fixedDeltaTime);
            rb.linearVelocity = new Vector3(idle.x * 0.9f, idle.y, idle.z * 0.9f);
            return;
        }

        Accelerate(GetMoveDirection(), MoveSpeed, 20f);

        bool ascend;
        bool descend;

        if (netDriven)
        {
            ascend = netAscend;
            descend = netDescend;
        }
        else
        {
            Keyboard keyboard = Keyboard.current;
            ascend = keyboard != null && keyboard.spaceKey.isPressed;
            descend = keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.cKey.isPressed);
        }

        Vector3 velocity = rb.linearVelocity;
        float targetY = ascend ? 6.5f : descend ? -7f : -1.4f;
        velocity.y = Mathf.MoveTowards(velocity.y, targetY, 26f * Time.fixedDeltaTime);
        rb.linearVelocity = velocity;

        ClampHorizontalSpeed();
    }

    public void SetInputEnabled(bool value)
    {
        inputEnabled = value;

        if (!value)
        {
            moveInput = Vector2.zero;
        }
    }

    public void SetSpeedMultiplier(float value)
    {
        speedMultiplier = Mathf.Clamp(value, 0.1f, 3f);
    }

    public void AddLaunch(Vector3 velocity, float unclampedSeconds = 0f)
    {
        rb.linearVelocity += velocity;

        if (unclampedSeconds > 0f)
        {
            speedClampReleasedUntil = Time.time + unclampedSeconds;
        }
    }

    public void Teleport(Vector3 position, float yaw)
    {
        rb.position = position;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
    }

    private void ReadMoveInput()
    {
        moveInput = Vector2.zero;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            {
                moveInput.y += 1f;
            }

            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            {
                moveInput.y -= 1f;
            }

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            {
                moveInput.x += 1f;
            }

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            {
                moveInput.x -= 1f;
            }
        }

        Gamepad gamepad = Gamepad.current;
        if (gamepad != null)
        {
            Vector2 stickInput = gamepad.leftStick.ReadValue();
            if (stickInput.sqrMagnitude > moveInput.sqrMagnitude)
            {
                moveInput = stickInput;
            }
        }

        moveInput = Vector2.ClampMagnitude(moveInput, 1f);
    }

    private bool JumpPressedThisFrame()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
        {
            return true;
        }

        Gamepad gamepad = Gamepad.current;
        return gamepad != null && gamepad.buttonSouth.wasPressedThisFrame;
    }

    private void Accelerate(Vector3 wishDirection, float wishSpeed, float acceleration)
    {
        if (wishDirection.sqrMagnitude < 0.0001f || wishSpeed <= 0f)
        {
            return;
        }

        Vector3 velocity = rb.linearVelocity;
        float currentSpeed = Vector3.Dot(new Vector3(velocity.x, 0f, velocity.z), wishDirection);
        float addSpeed = wishSpeed - currentSpeed;

        if (addSpeed <= 0f)
        {
            return;
        }

        float accelSpeed = Mathf.Min(acceleration * wishSpeed * Time.fixedDeltaTime, addSpeed);
        velocity += wishDirection * accelSpeed;
        rb.linearVelocity = velocity;
    }

    private void ApplyFriction()
    {

        if (Time.time - lastJumpTime <= hopFrictionWindow)
        {
            return;
        }

        Vector3 velocity = rb.linearVelocity;
        Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
        float speed = horizontal.magnitude;

        if (speed < 0.01f)
        {
            rb.linearVelocity = new Vector3(0f, velocity.y, 0f);
            return;
        }

        float control = Mathf.Max(speed, MoveSpeed * 0.4f);
        float drop = control * groundFriction * Time.fixedDeltaTime;
        float newSpeed = Mathf.Max(0f, speed - drop) / speed;

        rb.linearVelocity = new Vector3(horizontal.x * newSpeed, velocity.y, horizontal.z * newSpeed);
    }

    private void ClampHorizontalSpeed()
    {

        if (Time.time < speedClampReleasedUntil)
        {
            return;
        }

        float max = MoveSpeed * maxSpeedMultiplier;
        Vector3 velocity = rb.linearVelocity;
        Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);

        if (horizontal.sqrMagnitude <= max * max)
        {
            return;
        }

        horizontal = horizontal.normalized * max;
        rb.linearVelocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
    }

    private Vector3 GetMoveDirection()
    {
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;

        Vector3 direction = forward * moveInput.y + right * moveInput.x;
        if (direction.sqrMagnitude > 1f)
        {
            direction.Normalize();
        }

        return direction;
    }

    private void DoJump()
    {
        Vector3 velocity = rb.linearVelocity;
        velocity.y = Mathf.Max(velocity.y, 0f);
        rb.linearVelocity = velocity;
        rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);

        lastJumpTime = Time.time;
        lastJumpPressedTime = -999f;
        lastGroundedTime = -999f;
        Jumped?.Invoke();
    }

    private bool CheckGrounded()
    {
        Vector3 up = transform.up;
        Vector3 center = transform.TransformPoint(capsule.center);
        Vector3 scale = transform.lossyScale;
        float radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) * 0.95f;
        float bottomSphereOffset = Mathf.Max((capsule.height * 0.5f - capsule.radius) * Mathf.Abs(scale.y), 0f);
        Vector3 origin = center - up * bottomSphereOffset + up * 0.03f;
        float castDistance = groundCheckDistance + 0.03f;

        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            radius,
            -up,
            castDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore
        );

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider != capsule && !hit.collider.transform.IsChildOf(transform))
            {
                return true;
            }
        }

        return false;
    }
}
