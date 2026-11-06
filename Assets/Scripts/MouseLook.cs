using UnityEngine;

public class MouseLook : MonoBehaviour
{
    [SerializeField] private float mouseSens = 0.1f;

    [SerializeField] private Transform player;
    [SerializeField] private float transitionSpeed = 20f;

    public float currentYaw;
    private float targetYaw;
    private float currentPitch;
    private float targetPitch;

    private PlayerInputActions playerInput;
    void Awake()
    {
        playerInput = new PlayerInputActions();

        Cursor.lockState = CursorLockMode.Locked; ;
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

        float mouseX = (lookInput.x * mouseSens);
        float mouseY = (lookInput.y * mouseSens);

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
}
