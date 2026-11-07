using UnityEngine;
using UnityEngine.InputSystem;   // Unity's newer input system (installed in your project)

// WASD boat that floats on the OceanWaves water.
// Attach to the Boat object. No Rigidbody needed.
public class BoatController : MonoBehaviour
{
    [Header("Drag the OceanWaves object here")]
    public OceanWaves ocean;

    [Header("Movement")]
    public float maxSpeed = 8f;        // top forward speed
    public float acceleration = 4f;    // how fast it speeds up
    public float deceleration = 2f;    // how fast it slows when you let go
    public float turnSpeed = 50f;      // degrees per second
    public float reverseFactor = 0.4f; // reverse is slower than forward

    [Header("Floating")]
    public float floatOffset = 0.3f;   // raise/lower the boat in the water
    public float halfLength = 4f;      // distance from center to front/back sample points
    public float halfWidth = 1.5f;     // distance from center to left/right sample points
    public float tiltSmoothing = 2f;   // lower = calmer rocking
    public float heightSmoothing = 8f;

    [Header("Ocean limit")]
    public float limit = 140f;         // boat can't go farther than this from the center

    float speed;
    float yaw;

    void Start()
    {
        yaw = transform.eulerAngles.y;
    }

    void Update()
    {
        if (ocean == null) return;   // safety: do nothing if ocean isn't assigned

        // 1. Read keys (W/S = forward/back, A/D = turn)
        float throttle = 0f;
        float steer = 0f;
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed) throttle += 1f;
            if (kb.sKey.isPressed) throttle -= 1f;
            if (kb.dKey.isPressed) steer += 1f;
            if (kb.aKey.isPressed) steer -= 1f;
        }

        // 2. Speed and turning
        float targetSpeed = throttle * maxSpeed;
        if (throttle < 0f) targetSpeed *= reverseFactor;
        float rate = (throttle != 0f) ? acceleration : deceleration;
        speed = Mathf.MoveTowards(speed, targetSpeed, rate * Time.deltaTime);

        // Boats can only turn while moving
        float turnAmount = Mathf.Clamp(speed / (maxSpeed * 0.5f), -1f, 1f);
        yaw += steer * turnSpeed * turnAmount * Time.deltaTime;

        // 3. Move forward along the flat heading
        Quaternion flatRot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 forward = flatRot * Vector3.forward;
        Vector3 right = flatRot * Vector3.right;

        Vector3 pos = transform.position + forward * speed * Time.deltaTime;
        pos.x = Mathf.Clamp(pos.x, -limit, limit);
        pos.z = Mathf.Clamp(pos.z, -limit, limit);

        // 4. Ask the ocean how high the water is at 5 spots
        float hCenter = ocean.GetHeight(pos.x, pos.z);
        Vector3 f = pos + forward * halfLength;
        Vector3 b = pos - forward * halfLength;
        Vector3 r = pos + right * halfWidth;
        Vector3 l = pos - right * halfWidth;
        float hFront = ocean.GetHeight(f.x, f.z);
        float hBack = ocean.GetHeight(b.x, b.z);
        float hRight = ocean.GetHeight(r.x, r.z);
        float hLeft = ocean.GetHeight(l.x, l.z);

        // 5. Float: sit on the water
        float targetY = hCenter + floatOffset;
        pos.y = Mathf.Lerp(transform.position.y, targetY, heightSmoothing * Time.deltaTime);
        transform.position = pos;

        // 6. Tilt to match the wave slope
        float pitch = -Mathf.Atan2(hFront - hBack, 2f * halfLength) * Mathf.Rad2Deg;
        float roll = Mathf.Atan2(hRight - hLeft, 2f * halfWidth) * Mathf.Rad2Deg;

        Quaternion targetRot = Quaternion.Euler(pitch, yaw, roll);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, tiltSmoothing * Time.deltaTime);
    }
}