using UnityEngine;

public class SafeRoomNpc : MonoBehaviour
{
    [SerializeField] private Vector2 roomCenter;
    [SerializeField] private Vector2 roomHalfExtents = new Vector2(4f, 2.2f);
    [SerializeField] private float moveSpeed = 0.75f;
    [SerializeField] private float retargetInterval = 1.8f;

    private Vector2 targetPosition;
    private float nextRetargetTime;

    public void Configure(Vector2 center, Vector2 halfExtents)
    {
        roomCenter = center;
        roomHalfExtents = halfExtents;
        PickTarget();
    }

    private void Update()
    {
        if (Time.time >= nextRetargetTime || Vector2.Distance(transform.position, targetPosition) < 0.12f)
        {
            PickTarget();
        }

        Vector2 current = transform.position;
        Vector2 direction = targetPosition - current;
        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        transform.position = current + direction.normalized * (moveSpeed * Time.deltaTime);
    }

    private void PickTarget()
    {
        nextRetargetTime = Time.time + Random.Range(retargetInterval * 0.7f, retargetInterval * 1.4f);
        targetPosition = roomCenter + new Vector2(
            Random.Range(-roomHalfExtents.x, roomHalfExtents.x),
            Random.Range(-roomHalfExtents.y, roomHalfExtents.y));
    }
}
