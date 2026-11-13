using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

/// <summary>
/// Handles everything the cannon itself does: aiming at the screen centre and firing.
/// It does nothing until BeginAiming() is called (by CannonInteraction).
/// Lives under the ship, so it is part of the ship prefab. The player is passed in when aiming starts.
/// </summary>
public class CannonController : MonoBehaviour
{
    public enum LocalAxis
    {
        Forward,    // +Z (blue)
        Back,       // -Z
        Right,      // +X (red)
        Left,       // -X
        Up,         // +Y (green)
        Down        // -Y
    }

    [Header("References")]
    [SerializeField] private Transform barrel;      // Rotating part
    [FormerlySerializedAs("muzzle")]
    [SerializeField] private Transform firePoint;      // Empty at barrel tip: where balls spawn

    [Header("Axes")]
    [Tooltip("Which local axis of the BARREL points out of the cannon mouth. The barrel is rotated so this axis aims at the crosshair.")]
    [SerializeField] private LocalAxis barrelAxis = LocalAxis.Forward;

    [Tooltip("Which local axis of the FIRE POINT the cannonball is launched along.")]
    [FormerlySerializedAs("muzzleAxis")]
    [SerializeField] private LocalAxis fireAxis = LocalAxis.Forward;

    [Header("Aiming")]
    [SerializeField] private float aimDistance = 100f;
    [SerializeField] private float barrelTurnSpeed = 10f;
    [SerializeField] private LayerMask aimMask = ~0;

    [SerializeField] private bool drawDebug = true;     // Red = where it should aim, Green = launch direction (Scene view)

    [Header("Rotation Limits")]
    [Tooltip("The frame the limits are measured in. Its blue (Z) arrow is the centre of the firing arc and its green (Y) arrow is up. Leave empty to use this object. Keep it under the ship so the arc turns with the ship.")]
    [SerializeField] private Transform mount;
    [Tooltip("Sideways limits in degrees. Negative = left of centre, positive = right.")]
    [SerializeField] private float minYaw = -45f;
    [SerializeField] private float maxYaw = 45f;
    [Tooltip("Up/down limits in degrees. Negative = below horizontal, positive = above.")]
    [SerializeField] private float minPitch = -10f;
    [SerializeField] private float maxPitch = 45f;

    [Header("Shooting")]
    [SerializeField] private GameObject cannonballPrefab;
    [SerializeField] private float shootDelay = 2.5f;
    [SerializeField] private float shootForce = 30f;
    [SerializeField] private float ballLifetime = 10f;

    private BoatController ship;
    private PlayerController user;
    private Camera playerCamera;

    private InputAction attack;
    private bool isAiming;
    private bool isFiring;

    public bool IsAiming => isAiming;
    public bool IsFiring => isFiring;

    private void Awake()
    {
        ship = GetComponentInParent<BoatController>();
    }

    private void OnDisable()
    {
        // Safety: never leave the callback subscribed if this object is disabled mid-aim.
        if (isAiming) EndAiming();
    }

    // ------------------------------------------------------------ Axis helpers

    private static Vector3 ToVector(LocalAxis axis)
    {
        switch (axis)
        {
            case LocalAxis.Back: return Vector3.back;
            case LocalAxis.Right: return Vector3.right;
            case LocalAxis.Left: return Vector3.left;
            case LocalAxis.Up: return Vector3.up;
            case LocalAxis.Down: return Vector3.down;
            default: return Vector3.forward;
        }
    }

    private Vector3 FireDirection => firePoint.TransformDirection(ToVector(fireAxis));

    // ------------------------------------------------------------ Public API

    public void BeginAiming(PlayerController shooter)
    {
        if (isAiming) return;

        user = shooter;
        playerCamera = shooter.PlayerCamera;
        isAiming = true;

        attack = shooter.InputActions.Player.Attack;
        attack.performed += OnFirePerformed;

        // PlayerController.OnDisable() disables the whole Player map (including Attack) when it is
        // turned off for shoot mode, so enable just this one action explicitly.
        attack.Enable();
    }

    public void EndAiming()
    {
        isAiming = false;

        if (attack != null)
        {
            attack.performed -= OnFirePerformed;
            attack = null;
        }

        user = null;
        playerCamera = null;
    }

    public void TryFire()
    {
        if (!isAiming || isFiring) return;
        StartCoroutine(FireRoutine());
    }

    // ------------------------------------------------------------ Aiming

    private void LateUpdate()
    {
        if (!isAiming) return;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 targetPoint = GetAimPoint(ray);

        Vector3 dir = targetPoint - firePoint.position;
        if (dir.sqrMagnitude < 0.001f) return;

        // Keep the aim inside the allowed arc.
        dir = ClampToArc(dir);

        // Rotate so the barrel's chosen local axis points along dir.
        Quaternion axisFix = Quaternion.FromToRotation(ToVector(barrelAxis), Vector3.forward);
        Quaternion targetRot = Quaternion.LookRotation(dir) * axisFix;

        barrel.rotation = Quaternion.Slerp(barrel.rotation, targetRot, barrelTurnSpeed * Time.deltaTime);

        if (drawDebug)
        {
            Debug.DrawLine(firePoint.position, targetPoint, Color.red);
            Debug.DrawRay(firePoint.position, FireDirection * 10f, Color.green);
        }
    }

    // Limits a world-space aim direction to the allowed yaw/pitch arc, measured in the mount's frame.
    private Vector3 ClampToArc(Vector3 worldDir)
    {
        Transform frame = mount != null ? mount : transform;
        Vector3 local = frame.InverseTransformDirection(worldDir).normalized;

        float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
        float pitch = Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg;

        yaw = Mathf.Clamp(yaw, minYaw, maxYaw);
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        return frame.TransformDirection(ArcDirection(yaw, pitch));
    }

    // Local direction for a yaw (sideways) and pitch (up is positive) in degrees.
    private static Vector3 ArcDirection(float yaw, float pitch)
    {
        return Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;
    }

    // Select the cannon in the Scene view to see the allowed arc (yellow) and its centre (green).
    private void OnDrawGizmosSelected()
    {
        Transform frame = mount != null ? mount : transform;
        Vector3 origin = firePoint != null ? firePoint.position : frame.position;
        const float length = 6f;

        Gizmos.color = Color.green;
        Gizmos.DrawRay(origin, frame.forward * length);

        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(origin, frame.TransformDirection(ArcDirection(minYaw, minPitch)) * length);
        Gizmos.DrawRay(origin, frame.TransformDirection(ArcDirection(minYaw, maxPitch)) * length);
        Gizmos.DrawRay(origin, frame.TransformDirection(ArcDirection(maxYaw, minPitch)) * length);
        Gizmos.DrawRay(origin, frame.TransformDirection(ArcDirection(maxYaw, maxPitch)) * length);
    }

    private Vector3 GetAimPoint(Ray ray)
    {
        Vector3 point = ray.GetPoint(aimDistance);
        float best = float.MaxValue;

        RaycastHit[] hits = Physics.RaycastAll(ray, aimDistance, aimMask, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            if (IsIgnored(hit.collider.transform)) continue;
            if (hit.distance < best)
            {
                best = hit.distance;
                point = hit.point;
            }
        }
        return point;
    }

    // The aim ray skips the shooter and this cannon's own ship, so railings and masts don't block it.
    private bool IsIgnored(Transform t)
    {
        if (user != null && t.IsChildOf(user.transform)) return true;

        Transform ownRoot = ship != null ? ship.transform : transform;
        return t.IsChildOf(ownRoot);
    }

    // ------------------------------------------------------------ Shooting

    private void OnFirePerformed(InputAction.CallbackContext ctx) => TryFire();

    private IEnumerator FireRoutine()
    {
        isFiring = true;

        // Fuse sound / VFX / animation can go here.
        yield return new WaitForSeconds(shootDelay);

        Vector3 launchDir = FireDirection;
        GameObject ball = Instantiate(cannonballPrefab, firePoint.position, Quaternion.LookRotation(launchDir));

        // Don't let the ball collide with the ship that fired it, or the person firing.
        IgnoreCollisions(ball, ship != null ? ship.transform : null);
        IgnoreCollisions(ball, user != null ? user.transform : null);

        if (ball.TryGetComponent(out Rigidbody rb))
        {
            // The ball starts with the ship's own velocity, otherwise it would fall behind a moving ship.
            Vector3 shipVelocity = ship != null ? ship.GetPointVelocity(firePoint.position) : Vector3.zero;
            rb.AddForce(shipVelocity + launchDir * shootForce, ForceMode.VelocityChange);
        }

        Destroy(ball, ballLifetime);

        // Muzzle flash / recoil / camera shake can go here.
        isFiring = false;
    }

    private static void IgnoreCollisions(GameObject ball, Transform root)
    {
        if (root == null) return;

        Collider[] others = root.GetComponentsInChildren<Collider>();
        foreach (Collider ballCollider in ball.GetComponentsInChildren<Collider>())
            foreach (Collider other in others)
                Physics.IgnoreCollision(ballCollider, other);
    }
}