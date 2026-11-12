using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float sprintMultiplier = 2f;
    [SerializeField] private float currentSpeed;
    [SerializeField] private float sprintTransitionSpeed = 10f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private float verticalVel;

    [Header("Ground Check")]
    [SerializeField] private float castPosOffset = 0.8f;
    [SerializeField] private float sphereRadius = 0.3f;
    [SerializeField] private bool isGrounded;
    [SerializeField] private LayerMask layerMask;

    [Header("Moving Platforms (ships)")]
    [Tooltip("Turn the view with the ship while standing on it.")]
    [SerializeField] private bool inheritPlatformYaw = true;
    [Tooltip("How long (seconds) you keep the ship's motion after leaving the deck, e.g. mid-jump.")]
    [SerializeField] private float platformDetachTime = 0.5f;

    [Header("References")]
    [SerializeField] private MouseLook mouseLook;
    [SerializeField] private Camera playerCamera;

    private const float gravity = -19.62f;
    private const int GroundBufferSize = 8;

    private readonly Collider[] groundHits = new Collider[GroundBufferSize];

    private Rigidbody rb;
    private PlayerInputActions playerInput;

    private IMovingPlatform platform;
    private Vector3 appliedPlatformVelocity;
    private float airTime;

    private bool justJumped;
    private bool justLanded;
    private bool hasGroundedState;

    public bool IsGrounded => isGrounded;
    public bool IsSprinting => playerInput.Player.Sprint.IsPressed();
    public PlayerInputActions InputActions => playerInput;
    public Camera PlayerCamera => playerCamera;

    /// <summary>Horizontal speed relative to whatever the player stands on (ignores the ship's own movement).</summary>
    public float HorizontalSpeed
    {
        get
        {
            Vector3 relative = rb.linearVelocity - appliedPlatformVelocity;
            relative.y = 0f;
            return relative.magnitude;
        }
    }

    void Awake()
    {
        playerInput = new PlayerInputActions();
        rb = GetComponent<Rigidbody>();

        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        if (mouseLook == null) mouseLook = GetComponentInChildren<MouseLook>();

        currentSpeed = moveSpeed;
    }

    void OnEnable()
    {
        playerInput.Player.Enable();
        playerInput.Player.Jump.performed += HandleJump;
    }

    void OnDisable()
    {
        playerInput.Player.Jump.performed -= HandleJump;
        playerInput.Player.Disable();
    }

    void HandleJump(InputAction.CallbackContext ctx)
    {
        if (isGrounded)
        {
            verticalVel = jumpForce;
            justJumped = true;
        }
    }

    void Update()
    {
        // Rotate the view with the ship so turning the ship feels like turning with it.
        if (inheritPlatformYaw && platform != null)
            mouseLook.AddYaw(platform.YawRate * Time.deltaTime);
    }

    void FixedUpdate()
    {
        bool wasGrounded = isGrounded;

        CheckGrounded();

        if (hasGroundedState && !wasGrounded && isGrounded)
        {
            justLanded = true;
        }

        hasGroundedState = true;

        HandleGravity();
        HandleMovement();
    }

    void HandleMovement()
    {
        Vector2 moveInput = playerInput.Player.Move.ReadValue<Vector2>();

        bool sprinting = IsSprinting;

        float targetSpeed = sprinting
            ? moveSpeed * sprintMultiplier
            : moveSpeed;

        currentSpeed = Mathf.Lerp(
            currentSpeed,
            targetSpeed,
            sprintTransitionSpeed * Time.fixedDeltaTime
        );

        float airControl = isGrounded ? 1f : 0.7f;

        float inputX = moveInput.x * currentSpeed * airControl;
        float inputY = moveInput.y * currentSpeed * airControl;

        // Everything below works relative to the platform, then the platform's motion is added back on top.
        Vector3 platformVel = platform != null
            ? platform.GetPointVelocity(rb.position)
            : Vector3.zero;
        appliedPlatformVelocity = platformVel;

        Vector3 currentRelativeVel = new Vector3(
            rb.linearVelocity.x - platformVel.x,
            verticalVel,
            rb.linearVelocity.z - platformVel.z
        );

        Vector3 desiredRelativeVel = Quaternion.Euler(
            0f,
            mouseLook.CurrentYaw,
            0f
        ) * new Vector3(inputX, 0f, inputY);

        desiredRelativeVel.y = verticalVel;

        Vector3 relativeVel = isGrounded
            ? desiredRelativeVel
            : Vector3.Lerp(currentRelativeVel, desiredRelativeVel, 3f * Time.fixedDeltaTime);

        rb.linearVelocity = relativeVel + platformVel;
    }

    void HandleGravity()
    {
        if (isGrounded && verticalVel < 0f)
            verticalVel = -1f;
        else
            verticalVel += gravity * Time.fixedDeltaTime;
    }

    void CheckGrounded()
    {
        Vector3 castPos = transform.position +
                          (Vector3.down * castPosOffset);

        int hitCount = Physics.OverlapSphereNonAlloc(
            castPos,
            sphereRadius,
            groundHits,
            layerMask,
            QueryTriggerInteraction.Ignore
        );

        isGrounded = hitCount > 0;

        UpdatePlatform(FindPlatform(hitCount));
    }

    // Looks through whatever the ground check touched for a moving platform (a ship's Rigidbody).
    IMovingPlatform FindPlatform(int hitCount)
    {
        for (int i = 0; i < hitCount; i++)
        {
            Rigidbody body = groundHits[i].attachedRigidbody;
            if (body != null && body.TryGetComponent(out IMovingPlatform found))
                return found;
        }
        return null;
    }

    void UpdatePlatform(IMovingPlatform groundPlatform)
    {
        if (isGrounded)
        {
            platform = groundPlatform;   // null when standing on static ground (island)
            airTime = 0f;
            return;
        }

        // Airborne: keep the ship's motion briefly so jumping on deck feels right, but not forever
        // (otherwise jumping overboard would carry you along with the ship).
        airTime += Time.fixedDeltaTime;
        if (airTime > platformDetachTime) platform = null;
    }

    public bool ConsumeJustJumped()
    {
        bool value = justJumped;
        justJumped = false;
        return value;
    }

    public bool ConsumeJustLanded()
    {
        bool value = justLanded;
        justLanded = false;
        return value;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = isGrounded ? Color.blue : Color.red;

        Vector3 drawPos = transform.position +
                          (Vector3.down * castPosOffset);

        Gizmos.DrawWireSphere(drawPos, sphereRadius);
    }
}