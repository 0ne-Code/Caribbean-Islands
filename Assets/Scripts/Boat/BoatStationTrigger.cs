using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

/// <summary>
/// Base class for anything on the ship the player can "use" (cannon, helm).
///
/// Flow: walk into the trigger zone, press Interact (Player/Interact in your Input Actions) to enter,
/// press Interact again to leave. While using the station the player is locked to <c>standPosition</c>
/// and carried along with the ship.
///
/// Entering and leaving go through ONE Interact callback, so a single key press can never both leave
/// and instantly re-enter. Put a trigger Collider on the same GameObject.
/// Keep the trigger zones of different stations apart so they don't overlap.
/// </summary>
[RequireComponent(typeof(Collider))]
public abstract class BoatStationTrigger : MonoBehaviour
{
    [Header("Station")]
    [Tooltip("Where the player stands while using this station (a child of the ship).")]
    [FormerlySerializedAs("shootPosition")]
    [SerializeField] private Transform standPosition;
    [SerializeField] private float lerpDuration = 0.75f;

    private enum State { Idle, Entering, Occupied }

    private State state = State.Idle;
    private PlayerController playerInZone;
    private PlayerController user;
    private Rigidbody userBody;
    private InputAction interact;
    private int lastTransitionFrame = -1;

    /// <summary>Called once the player has arrived and the station should start working.</summary>
    protected abstract void OnStationEntered(PlayerController occupant);

    /// <summary>Called when the player leaves (or the station is disabled while in use).</summary>
    protected abstract void OnStationExited(PlayerController occupant);

    /// <summary>Override to stop the player leaving at certain moments (e.g. while the cannon fuse is burning).</summary>
    protected virtual bool CanExit => true;

    protected virtual void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnDisable()
    {
        if (user != null) Release();
        Unsubscribe();
    }

    // The occupant is carried by the station every physics step, so the ship can move,
    // turn and rock without leaving them behind.
    private void FixedUpdate()
    {
        if (state == State.Occupied)
            userBody.MovePosition(standPosition.position);
    }

    // ------------------------------------------------------------ Trigger zone

    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();

        // Online: only the local player has an enabled PlayerController, so remote players are skipped.
        if (player == null || !player.isActiveAndEnabled) return;

        playerInZone = player;
        if (state == State.Idle) Subscribe(player);
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || player != playerInZone) return;

        playerInZone = null;

        // Stay subscribed while in use, otherwise the player could never press Interact to leave.
        if (state == State.Idle) Unsubscribe();
    }

    // ------------------------------------------------------------ Interact input

    private void Subscribe(PlayerController player)
    {
        if (interact != null) return;

        interact = player.InputActions.Player.Interact;
        interact.performed += OnInteractPerformed;
    }

    private void Unsubscribe()
    {
        if (interact == null) return;

        interact.performed -= OnInteractPerformed;
        interact = null;
    }

    private void OnInteractPerformed(InputAction.CallbackContext ctx)
    {
        // Safety net: never handle two transitions in the same frame.
        if (Time.frameCount == lastTransitionFrame) return;

        switch (state)
        {
            case State.Idle:
                if (playerInZone != null) StartCoroutine(Enter(playerInZone));
                break;

            case State.Occupied:
                if (CanExit) Exit();
                break;
        }
    }

    // ------------------------------------------------------------ Enter / Exit

    private IEnumerator Enter(PlayerController entering)
    {
        lastTransitionFrame = Time.frameCount;

        state = State.Entering;
        user = entering;
        userBody = user.GetComponent<Rigidbody>();

        // Disabling PlayerController also disables the whole Player input map (including Interact),
        // so switch Interact back on to be able to leave again.
        user.enabled = false;
        interact.Enable();
        FreezeUserBody(true);

        // Blend in the station's local space, so the target keeps up with the moving ship.
        Vector3 startLocal = standPosition.InverseTransformPoint(userBody.position);
        float t = 0f;

        while (t < 1f)
        {
            yield return new WaitForFixedUpdate();

            t += Time.fixedDeltaTime / Mathf.Max(0.01f, lerpDuration);
            float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            Vector3 local = Vector3.Lerp(startLocal, Vector3.zero, eased);
            userBody.MovePosition(standPosition.TransformPoint(local));
        }

        state = State.Occupied;
        lastTransitionFrame = Time.frameCount;
        OnStationEntered(user);
    }

    private void Exit()
    {
        lastTransitionFrame = Time.frameCount;
        Release();
    }

    // Gives the player back their controls. Also used if the station is disabled while in use.
    private void Release()
    {
        PlayerController leaving = user;

        if (state == State.Occupied) OnStationExited(leaving);

        FreezeUserBody(false);
        leaving.enabled = true;

        // They are still standing in the zone, so a later Interact press can enter again.
        playerInZone = leaving;

        user = null;
        userBody = null;
        state = State.Idle;
    }

    // PlayerController drives the Rigidbody velocity, and gravity is handled manually. Once it's disabled
    // the body would keep its last velocity, so zero it and make it kinematic while using a station.
    private void FreezeUserBody(bool freeze)
    {
        if (userBody == null) return;

        if (freeze)
        {
            userBody.linearVelocity = Vector3.zero;
            userBody.angularVelocity = Vector3.zero;
            userBody.isKinematic = true;
        }
        else
        {
            userBody.isKinematic = false;
        }
    }
}