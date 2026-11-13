
using UnityEngine;

public class CameraEffects : MonoBehaviour
{
    [Header("Head Bob - Walking")]
    [SerializeField] private float walkFrequency = 8f;
    [SerializeField] private float walkAmplitude = 0.035f;

    [Header("Head Bob - Sprinting")]
    [SerializeField] private float sprintFrequency = 11f;
    [SerializeField] private float sprintAmplitude = 0.055f;

    [Header("Smoothing")]
    [SerializeField] private float bobSmoothSpeed = 10f;
    [SerializeField] private float breathingSmoothSpeed = 3f;
    [SerializeField] private float idleThreshold = 0.15f;

    [Header("Field Of View")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float normalFOV = 75f;
    [SerializeField] private float sprintFOV = 83f;
    [SerializeField] private float fovSmoothSpeed = 5f;

    [Header("Idle Breathing")]
    [SerializeField] private float breathingAmplitude = 0.012f;
    [SerializeField] private float breathingFrequency = 1.2f;

    [Header("Jump & Landing Feedback")]
    [SerializeField] private float jumpCameraOffset = 0.025f;
    [SerializeField] private float landingCameraOffset = 0.065f;
    [SerializeField] private float impactReturnSpeed = 12f;

    [Header("References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Rigidbody playerRb;

    private Vector3 startingPosition;

    private float bobTimer;
    private float breathingTimer;

    private float breathingOffset;
    private float impactOffset;

    private void Start()
    {
        startingPosition = transform.localPosition;
        playerCamera.fieldOfView = normalFOV;
    }

    private void Update()
    {
        HandleHeadBob();
        HandleJumpAndLanding();
        HandleFOV();

        UpdateCameraPosition();
    }

    private void HandleHeadBob()
    {
        Vector3 horizontalVelocity = new Vector3(
            playerRb.linearVelocity.x,
            0f,
            playerRb.linearVelocity.z
        );

        float horizontalSpeed = horizontalVelocity.magnitude;

        bool isMoving =
            horizontalSpeed > idleThreshold &&
            playerController.IsGrounded;

        bool isSprinting =
            isMoving && playerController.IsSprinting;

        float targetOffset;

        if (isMoving)
        {
            float frequency = isSprinting
                ? sprintFrequency
                : walkFrequency;

            float amplitude = isSprinting
                ? sprintAmplitude
                : walkAmplitude;

            bobTimer += Time.deltaTime * frequency;

            targetOffset = Mathf.Sin(bobTimer) * amplitude;

            breathingTimer = 0f;
        }
        else
        {
            breathingTimer += Time.deltaTime * breathingFrequency;

            targetOffset = Mathf.Sin(breathingTimer) *
                           breathingAmplitude;
        }

        float smoothSpeed = isMoving
            ? bobSmoothSpeed
            : breathingSmoothSpeed;

        breathingOffset = Mathf.Lerp(
            breathingOffset,
            targetOffset,
            smoothSpeed * Time.deltaTime
        );
    }

    private void HandleJumpAndLanding()
    {
        if (playerController.ConsumeJustJumped())
        {
            impactOffset = jumpCameraOffset;
        }

        if (playerController.ConsumeJustLanded())
        {
            impactOffset = -landingCameraOffset;
        }

        impactOffset = Mathf.Lerp(
            impactOffset,
            0f,
            impactReturnSpeed * Time.deltaTime
        );
    }

    private void HandleFOV()
    {
        Vector3 horizontalVelocity = new Vector3(
            playerRb.linearVelocity.x,
            0f,
            playerRb.linearVelocity.z
        );

        bool isSprinting =
            playerController.IsGrounded &&
            playerController.IsSprinting &&
            horizontalVelocity.magnitude > idleThreshold;

        float targetFOV = isSprinting
            ? sprintFOV
            : normalFOV;

        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView,
            targetFOV,
            fovSmoothSpeed * Time.deltaTime
        );
    }

    private void UpdateCameraPosition()
    {
        transform.localPosition = startingPosition +
            Vector3.up * (breathingOffset + impactOffset);
    }
}
