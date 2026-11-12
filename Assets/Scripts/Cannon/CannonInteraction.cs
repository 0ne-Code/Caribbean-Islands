using UnityEngine;

/// <summary>
/// The cannon station. Walk into the trigger, press Interact to take the cannon, press it again to leave.
/// All the enter/exit/carry logic lives in ShipStation; this only starts and stops the cannon.
/// </summary>
public class CannonInteraction : BoatStationTrigger
{
    [Header("Cannon")]
    [SerializeField] private CannonController cannon;

    // Can't leave while the fuse is burning (remove this override to allow it).
    protected override bool CanExit => !cannon.IsFiring;

    protected override void OnStationEntered(PlayerController occupant)
    {
        cannon.BeginAiming(occupant);
    }

    protected override void OnStationExited(PlayerController occupant)
    {
        cannon.EndAiming();
    }
}