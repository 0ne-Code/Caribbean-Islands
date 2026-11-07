using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles everything the cannon itself does: aiming at the screen centre and firing.
/// It does nothing until BeginAiming() is called (by CannonInteraction).
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
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform barrel;      // Rotating part
    [SerializeField] private Transform muzzle;      // Empty at barrel tip

    [Header("Axes")]
    [Tooltip("Which local axis of the BARREL points out of the cannon mouth. The barrel is rotated so this axis aims at the crosshair.")]
    [SerializeField] private LocalAxis barrelAxis = LocalAxis.Forward;

    [Tooltip("Which local axis of the MUZZLE the cannonball is launched along.")]
    [SerializeField] private LocalAxis muzzleAxis = LocalAxis.Forward;

    [Header("Aiming")]
    [SerializeField] private float aimDistance = 100f;
    [SerializeField] private float barrelTurnSpeed = 10f;
    [SerializeField] private LayerMask aimMask = ~0;

    [Tooltip("The aim ray ignores colliders under these (add the Player here). This cannon is ignored automatically.")]
    [SerializeField] private Transform[] ignoreRoots;

    [SerializeField] private bool drawDebug = true;     // Red = where it should aim, Green = launch direction (Scene view)

    [Header("Shooting")]
    [SerializeField] private GameObject cannonballPrefab;
    [SerializeField] private float shootDelay = 2.5f;
    [SerializeField] private float shootForce = 30f;
    [SerializeField] private float ballLifetime = 10f;

    [Header("Input")]
    [Tooltip("Your PlayerController. Its PlayerInputActions instance is reused for the Attack action.")]
    [SerializeField] private PlayerController playerController;

    private InputAction attack;
    private bool isAiming;
    private bool isFiring;

    public bool IsAiming => isAiming;
    public bool IsFiring => isFiring;

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

    private Vector3 MuzzleDirection => muzzle.TransformDirection(ToVector(muzzleAxis));

    // ------------------------------------------------------------ Public API

    public void BeginAiming()
    {
        isAiming = true;

        attack = playerController.InputActions.Player.Attack;
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

        Vector3 dir = targetPoint - muzzle.position;
        if (dir.sqrMagnitude < 0.001f) return;

        // Rotate so the barrel's chosen local axis points along dir.
        Quaternion axisFix = Quaternion.FromToRotation(ToVector(barrelAxis), Vector3.forward);
        Quaternion targetRot = Quaternion.LookRotation(dir) * axisFix;

        barrel.rotation = Quaternion.Slerp(barrel.rotation, targetRot, barrelTurnSpeed * Time.deltaTime);

        if (drawDebug)
        {
            Debug.DrawLine(muzzle.position, targetPoint, Color.red);
            Debug.DrawRay(muzzle.position, MuzzleDirection * 10f, Color.green);
        }
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

    private bool IsIgnored(Transform t)
    {
        if (t.IsChildOf(transform)) return true;
        if (ignoreRoots == null) return false;

        foreach (Transform root in ignoreRoots)
            if (root != null && t.IsChildOf(root)) return true;

        return false;
    }

    // ------------------------------------------------------------ Shooting

    private void OnFirePerformed(InputAction.CallbackContext ctx) => TryFire();

    private IEnumerator FireRoutine()
    {
        isFiring = true;

        // Fuse sound / VFX / animation can go here.
        yield return new WaitForSeconds(shootDelay);

        Vector3 launchDir = MuzzleDirection;
        GameObject ball = Instantiate(cannonballPrefab, muzzle.position, Quaternion.LookRotation(launchDir));

        if (ball.TryGetComponent(out Rigidbody rb))
            rb.AddForce(launchDir * shootForce, ForceMode.VelocityChange);

        Destroy(ball, ballLifetime);

        // Muzzle flash / recoil / camera shake can go here.
        isFiring = false;
    }
}