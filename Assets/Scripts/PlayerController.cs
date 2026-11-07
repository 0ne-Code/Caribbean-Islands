
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed;
    [SerializeField] private float sprintMultiplier;
    [SerializeField] private float currentSpeed;
    [SerializeField] private float sprintTransitionSpeed = 10f;

    [Header("Jump")]
    [SerializeField] private float jumpForce;
    [SerializeField] private float verticalVel;

    [Header("Ground Check")]
    [SerializeField] private bool isGrounded;
    [SerializeField] private float castPosOffset;
    [SerializeField] private float sphereRadius;
    [SerializeField] private LayerMask layerMask;

    private const float gravity = -19.62f;

    [SerializeField] private MouseLook mouseLook;

    private Rigidbody rb;
    private PlayerInputActions playerInput;

    private bool justJumped;
    private bool justLanded;
    private bool hasGroundedState;

    public bool IsGrounded => isGrounded;
    public bool IsSprinting => playerInput.Player.Sprint.IsPressed();

    void Awake()
    {
        playerInput = new PlayerInputActions();
        rb = GetComponent<Rigidbody>();

        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

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

        Vector3 currentLocalVel = new Vector3(
            rb.linearVelocity.x,
            verticalVel,
            rb.linearVelocity.z
        );

        Vector3 localVel = Quaternion.Euler(
            0f,
            mouseLook.currentYaw,
            0f
        ) * new Vector3(inputX, 0f, inputY);

        localVel.y = verticalVel;

        if (!isGrounded)
        {
            rb.linearVelocity = Vector3.Lerp(
                currentLocalVel,
                localVel,
                3f * Time.fixedDeltaTime
            );
        }
        else
        {
            rb.linearVelocity = localVel;
        }
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

        isGrounded = Physics.CheckSphere(
            castPos,
            sphereRadius,
            layerMask
        );
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