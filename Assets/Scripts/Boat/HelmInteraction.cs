using UnityEngine;

/// <summary>
/// The helm station. Walk into the trigger near the wheel, press Interact to take control of the ship,
/// press it again to let go. While at the helm the player's walking controls are off and the Input
/// System's Ship/Drive action steers the ship.
/// </summary>
public class HelmInteraction : BoatStationTrigger
{
    [Header("Helm")]
    [Tooltip("Leave empty to use the BoatController on a parent object.")]
    [SerializeField] private BoatController boat;

    private PlayerInputActions inputs;

    private void Awake()
    {
        if (boat == null) boat = GetComponentInParent<BoatController>();
    }

    protected override void OnStationEntered(PlayerController occupant)
    {
        inputs = occupant.InputActions;
        inputs.Boat.Enable();
        boat.AttachDriver(inputs.Boat.Move);
    }

    protected override void OnStationExited(PlayerController occupant)
    {
        boat.DetachDriver();

        if (inputs != null)
        {
            inputs.Boat.Disable();
            inputs = null;
        }
    }
}