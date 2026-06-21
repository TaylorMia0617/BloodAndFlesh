using UnityEngine;

public class PlayerCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float cameraHeight = -10f;
    [SerializeField] private float orthographicSize = 8f;

    private Camera followCamera;

    private void Awake()
    {
        followCamera = GetComponent<Camera>();
        if (followCamera != null)
        {
            followCamera.orthographic = true;
            followCamera.orthographicSize = orthographicSize;
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        transform.position = new Vector3(target.position.x, target.position.y, cameraHeight);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        LateUpdate();
    }
}
