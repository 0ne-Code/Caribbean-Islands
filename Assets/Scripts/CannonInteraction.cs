using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles the player side of the cannon: trigger zone, moving the player into position,
/// toggling player scripts, and entering/exiting shoot mode.
/// Needs a Trigger collider on the same GameObject.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CannonInteraction : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CannonController cannon;
    [SerializeField] private Transform player;              // Player root (has the Rigidbody)
    [SerializeField] private Transform shootPosition;       // Where the player stands

    [Tooltip("Add your PlayerController here. Leave MouseLook off this list.")]
    [SerializeField] private Behaviour[] disableWhileShooting;

    [Header("Settings")]
    [SerializeField] private float lerpDuration = 0.75f;

    private enum State { Idle, Entering, Shooting }
    private State state = State.Idle;

    private InputAction exitAction;
    private Rigidbody playerRb;
    private bool canEnter = true;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Awake()
    {
        if (player != null)
            playerRb = player.GetComponent<Rigidbody>();

        exitAction = new InputAction("ExitCannon", InputActionType.Button, "<Keyboard>/e");
        exitAction.performed += OnExitPerformed;
    }

    private void OnDestroy()
    {
        exitAction.performed -= OnExitPerformed;
        exitAction.Dispose();
    }

    // ------------------------------------------------------------ Trigger

    private void OnTriggerEnter(Collider other)
    {
        if (state != State.Idle || !canEnter) return;
        if (!other.transform.IsChildOf(player)) return;

        StartCoroutine(EnterShootMode());
    }

    private void OnTriggerExit(Collider other)
    {
        // Prevents instantly re-entering after pressing the exit key while still inside the zone.
        if (other.transform.IsChildOf(player))
            canEnter = true;
    }

    // ------------------------------------------------------------ Enter / Exit

    private IEnumerator EnterShootMode()
    {
        state = State.Entering;
        SetPlayerControlEnabled(false);
        FreezePlayerBody(true);

        Vector3 start = player.position;
        Vector3 end = shootPosition.position;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, lerpDuration);
            float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            player.position = Vector3.Lerp(start, end, eased);
            yield return null;
        }

        state = State.Shooting;
        cannon.BeginAiming();
        exitAction.Enable();
    }

    private void ExitShootMode()
    {
        if (state != State.Shooting) return;

        exitAction.Disable();
        cannon.EndAiming();

        FreezePlayerBody(false);
        SetPlayerControlEnabled(true);

        canEnter = false;
        state = State.Idle;
    }

    private void OnExitPerformed(InputAction.CallbackContext ctx)
    {
        // Don't let the player leave mid-countdown (remove this check if you want to allow it).
        if (cannon.IsFiring) return;
        ExitShootMode();
    }

    // ------------------------------------------------------------ Helpers

    private void SetPlayerControlEnabled(bool value)
    {
        foreach (var b in disableWhileShooting)
            if (b != null) b.enabled = value;
    }

    // PlayerController drives the Rigidbody velocity, and gravity is handled manually. Once it's disabled
    // the body would keep its last velocity, so zero it and make it kinematic while in shoot mode.
    private void FreezePlayerBody(bool freeze)
    {
        if (playerRb == null) return;

        if (freeze)
        {
            playerRb.linearVelocity = Vector3.zero;
            playerRb.angularVelocity = Vector3.zero;
            playerRb.isKinematic = true;
        }
        else
        {
            playerRb.isKinematic = false;
        }
    }
}