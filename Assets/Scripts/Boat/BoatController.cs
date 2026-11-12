using UnityEngine;
using UnityEngine.InputSystem;

 //< summary >
 //A ship that sails on the OceanWaves surface.

 //- Moves a KINEMATIC Rigidbody in FixedUpdate, so the walking player and cannonballs interact with it
 //  through normal physics.
 //- Has no input of its own. HelmInteraction hands it the Input System's Ship/Drive action when a player
 //  takes the wheel (AttachDriver) and takes it back when they let go (DetachDriver).
 //- Exposes its motion through IMovingPlatform so the player is carried along while standing on deck.
 //</summary>
[RequireComponent(typeof(Rigidbody))]
public class BoatController : MonoBehaviour, IMovingPlatform
{
    [Header("References")]
    [Tooltip("Leave empty to use the OceanWaves in the scene (needed when this ship is a prefab).")]
    [SerializeField] private OceanWaves ocean;

    [Header("Movement")]
    [SerializeField] private float maxSpeed = 8f;          // top forward speed (m/s)
    [SerializeField] private float acceleration = 4f;      // how fast it speeds up
    [SerializeField] private float deceleration = 2f;      // how fast it slows when nobody is steering
    [SerializeField] private float turnSpeed = 50f;        // degrees per second at full authority
    [SerializeField] private float reverseFactor = 0.4f;   // reverse is slower than forward

    [Header("Floating")]
    [SerializeField] private float floatOffset = 0.3f;     // raise/lower the ship in the water
    [SerializeField] private float halfLength = 4f;        // centre to bow/stern sample points
    [SerializeField] private float halfWidth = 1.5f;       // centre to port/starboard sample points
    [SerializeField] private float tiltSmoothing = 2f;     // lower = calmer rocking
    [SerializeField] private float heightSmoothing = 8f;

    [Header("Ocean limit")]
    [SerializeField] private float limit = 140f;           // can't go farther than this from the origin

    private Rigidbody body;
    private InputAction driveAction;    // Vector2: x = steer, y = throttle. Null when nobody is at the helm.

    private float throttleInput;
    private float steerInput;

    private float speed;
    private float yaw;
    private float yawRate;
    private float smoothPitch;
    private float smoothRoll;

    // Motion of the last physics step, handed to anything standing on the ship.
    private Vector3 velocity;
    private Vector3 angularVelocity;

    public float Speed => speed;
    public float YawRate => yawRate;
    public bool HasDriver => driveAction != null;

    // ------------------------------------------------------------ Unity

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        yaw = transform.eulerAngles.y;
    }

    private void Start()
    {
        // Start (not Awake) so the ocean has already registered itself.
        if (ocean == null) ocean = OceanWaves.Instance;
    }

    private void FixedUpdate()
    {
        if (ocean == null) return;

        if (driveAction != null)
        {
            Vector2 drive = driveAction.ReadValue<Vector2>();
            SetInput(drive.y, drive.x);
        }

        float dt = Time.fixedDeltaTime;
        UpdateSpeedAndHeading(dt);

        Quaternion heading = Quaternion.Euler(0f, yaw, 0f);
        Vector3 forward = heading * Vector3.forward;
        Vector3 right = heading * Vector3.right;

        Vector3 position = body.position + forward * (speed * dt);
        position.x = Mathf.Clamp(position.x, -limit, limit);
        position.z = Mathf.Clamp(position.z, -limit, limit);

        SampleWaves(position, forward, right, out float waterHeight, out float pitch, out float roll);

        position.y = Mathf.Lerp(body.position.y, waterHeight + floatOffset, heightSmoothing * dt);

        // Smooth only the tilt. Heading is applied directly so the hull always faces the way it sails.
        float tilt = tiltSmoothing * dt;
        smoothPitch = Mathf.Lerp(smoothPitch, pitch, tilt);
        smoothRoll = Mathf.Lerp(smoothRoll, roll, tilt);
        Quaternion rotation = Quaternion.Euler(smoothPitch, yaw, smoothRoll);

        TrackMotion(position, rotation, dt);
        body.Move(position, rotation);
    }

    // ------------------------------------------------------------ Public API

    /// <summary>Start reading steering from an Input System Vector2 action (x = steer, y = throttle).</summary>
    public void AttachDriver(InputAction drive)
    {
        driveAction = drive;
    }

    /// <summary>Stop reading input. The ship coasts to a stop.</summary>
    public void DetachDriver()
    {
        driveAction = null;
        SetInput(0f, 0f);
    }

    /// <summary>Drive the ship from outside (network layer, AI, tests). Both values are -1..1.</summary>
    public void SetInput(float throttle, float steer)
    {
        throttleInput = Mathf.Clamp(throttle, -1f, 1f);
        steerInput = Mathf.Clamp(steer, -1f, 1f);
    }

    public Vector3 GetPointVelocity(Vector3 worldPoint)
    {
        return velocity + Vector3.Cross(angularVelocity, worldPoint - body.position);
    }

    // ------------------------------------------------------------ Simulation

    private void UpdateSpeedAndHeading(float dt)
    {
        float targetSpeed = throttleInput * maxSpeed;
        if (throttleInput < 0f) targetSpeed *= reverseFactor;

        float rate = Mathf.Approximately(throttleInput, 0f) ? deceleration : acceleration;
        speed = Mathf.MoveTowards(speed, targetSpeed, rate * dt);

        // Ships can only turn while moving.
        float turnAuthority = Mathf.Clamp(speed / (maxSpeed * 0.5f), -1f, 1f);
        yawRate = steerInput * turnSpeed * turnAuthority;
        yaw += yawRate * dt;
    }

    private void SampleWaves(Vector3 position, Vector3 forward, Vector3 right,
                             out float centerHeight, out float pitch, out float roll)
    {
        centerHeight = WaterHeightAt(position);

        float bow = WaterHeightAt(position + forward * halfLength);
        float stern = WaterHeightAt(position - forward * halfLength);
        float starboard = WaterHeightAt(position + right * halfWidth);
        float port = WaterHeightAt(position - right * halfWidth);

        pitch = -Mathf.Atan2(bow - stern, 2f * halfLength) * Mathf.Rad2Deg;
        roll = Mathf.Atan2(starboard - port, 2f * halfWidth) * Mathf.Rad2Deg;
    }

    private float WaterHeightAt(Vector3 worldPoint)
    {
        return ocean.GetHeight(worldPoint.x, worldPoint.z);
    }

    private void TrackMotion(Vector3 newPosition, Quaternion newRotation, float dt)
    {
        velocity = (newPosition - body.position) / dt;

        Quaternion delta = newRotation * Quaternion.Inverse(body.rotation);
        delta.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;

        angularVelocity = Mathf.Abs(angle) > 0.0001f
            ? axis * (angle * Mathf.Deg2Rad / dt)
            : Vector3.zero;
    }
}