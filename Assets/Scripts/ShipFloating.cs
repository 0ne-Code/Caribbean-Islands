
using UnityEngine;

public class ShipFloating : MonoBehaviour
{
    [Header("Water Reference")]
    public OceanWaves ocean;

    [Header("Floating Settings")]
    public float waterOffset = 0.2f;
    public float floatSmoothness = 3f;

    [Header("Ship Bobbing")]
    public float bobAmount = 0.15f;
    public float bobSpeed = 1.5f;

    [Header("Ship Tilting")]
    public float tiltAmount = 3f;
    public float tiltSpeed = 1f;

    float startY;

    void Start()
    {
        startY = transform.position.y;
    }

    void Update()
    {
        if (ocean == null) return;

        // Get the water height underneath the ship
        float waterHeight = ocean.GetHeight(
            transform.position.x,
            transform.position.z
        );

        // Add gentle bobbing
        float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmount;

        // Calculate target height
        float targetY = waterHeight + waterOffset + bob;

        // Smoothly move the ship vertically
        Vector3 position = transform.position;

        position.y = Mathf.Lerp(
            position.y,
            targetY,
            Time.deltaTime * floatSmoothness
        );

        transform.position = position;

        // Gentle tilting
        float tiltX = Mathf.Sin(Time.time * tiltSpeed) * tiltAmount;
        float tiltZ = Mathf.Sin(Time.time * tiltSpeed * 0.7f) * tiltAmount;

        Quaternion targetRotation = Quaternion.Euler(
            tiltX,
            transform.eulerAngles.y,
            tiltZ
        );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            Time.deltaTime * 2f
        );
    }
}
