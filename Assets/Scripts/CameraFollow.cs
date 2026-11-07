using UnityEngine;

// Camera that follows behind a target (the boat).
// Attach to Main Camera.
public class CameraFollow : MonoBehaviour
{
    public Transform target;                           // drag the Boat here
    public Vector3 offset = new Vector3(0f, 6f, -16f); // behind and above the boat
    public float followSpeed = 4f;
    public float turnSpeed = 3f;

    float yaw;
    float focusY;

    void Start()
    {
        if (target != null)
        {
            yaw = target.eulerAngles.y;
            focusY = target.position.y;
        }
    }

    // LateUpdate runs after the boat has moved, so the camera doesn't jitter
    void LateUpdate()
    {
        if (target == null) return;

        // Follow the boat's heading smoothly
        yaw = Mathf.LerpAngle(yaw, target.eulerAngles.y, turnSpeed * Time.deltaTime);

        // Follow height slowly, so the camera ignores small wave bobbing
        focusY = Mathf.Lerp(focusY, target.position.y, 1.5f * Time.deltaTime);
        Vector3 focus = new Vector3(target.position.x, focusY, target.position.z);

        Vector3 wanted = focus + Quaternion.Euler(0f, yaw, 0f) * offset;
        transform.position = Vector3.Lerp(transform.position, wanted, followSpeed * Time.deltaTime);
        transform.LookAt(focus + Vector3.up * 1.5f);
    }
}