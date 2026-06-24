using UnityEngine;

public static class EnemyMovementMotor
{
    public static void Move(Rigidbody2D body, Vector2 current, Vector2 direction, float speed)
    {
        if (body == null || direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        body.MovePosition(current + direction.normalized * (Mathf.Max(0f, speed) * Time.fixedDeltaTime));
    }

    public static void Face(Transform transform, Vector2 direction)
    {
        if (transform == null || direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }
}
