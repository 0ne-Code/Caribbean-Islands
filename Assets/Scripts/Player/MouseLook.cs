using UnityEngine;

/// <summary>
/// First-person look. Put on the camera (a child of the player).
/// This is the only camera-rotation script: the old CameraFollow chase camera is no longer used.
/// </summary>
public class MouseLook : MonoBehaviour
{
    [SerializeField] private float mouseSens = 0.1f;
    [SerializeField] private float transitionSpeed = 20f;

    private float currentYaw;
    private float targetYaw;
    private float currentPitch;
    private float targetPitch;

    private PlayerInputActions playerInput;

    /// <summary>World yaw the player is looking along; PlayerController uses it as the walking direction.</summary>
    public float CurrentYaw => currentYaw;

    void Awake()
    {
        playerInput = new PlayerInputActions();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnEnable()
    {
        playerInput.Player.Enable();
    }

    void OnDisable()
    {
        playerInput.Player.Disable();
    }

    void Update()
    {
        Vector2 lookInput = playerInput.Player.Look.ReadValue<Vector2>();

        float mouseX = lookInput.x * mouseSens;
        float mouseY = lookInput.y * mouseSens;

        targetYaw += mouseX;
        currentYaw = Mathf.Lerp(currentYaw, targetYaw, transitionSpeed * Time.deltaTime);

        targetPitch -= mouseY;
        targetPitch = Mathf.Clamp(targetPitch, -90f, 90f);
        currentPitch = Mathf.Lerp(currentPitch, targetPitch, transitionSpeed * Time.deltaTime);
    }

    void LateUpdate()
    {
        transform.localRotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
    }

    /// <summary>Turns the view by an amount without disturbing the smoothing (used when standing on a turning ship).</summary>
    public void AddYaw(float degrees)
    {
        targetYaw += degrees;
        currentYaw += degrees;
    }
}