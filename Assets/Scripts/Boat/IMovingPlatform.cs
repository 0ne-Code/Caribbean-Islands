using UnityEngine;

/// <summary>
/// Implemented by anything the player can stand on that moves (e.g. the ship).
/// PlayerController reads this to carry the player along with the platform.
/// </summary>
public interface IMovingPlatform
{
    /// <summary>World-space velocity of the platform surface at the given world point (includes rotation).</summary>
    Vector3 GetPointVelocity(Vector3 worldPoint);

    /// <summary>How fast the platform is turning around the world Y axis, in degrees per second.</summary>
    float YawRate { get; }
}