using UnityEngine;
using UnityEngine.InputSystem;

public static class MouseWorldInput
{
    private static bool hasLastScreenPosition;
    private static Vector2 lastScreenPosition;

    public static bool TryGetScreenPosition(InputAction fallbackAction, out Vector2 screenPosition)
    {
        if (TryGetLegacyMousePosition(out screenPosition))
        {
            CacheScreenPosition(screenPosition);
            return true;
        }

        if (Mouse.current != null)
        {
            screenPosition = Mouse.current.position.ReadValue();
            if (IsInsideScreen(screenPosition))
            {
                CacheScreenPosition(screenPosition);
                return true;
            }
        }

        if (fallbackAction != null)
        {
            screenPosition = fallbackAction.ReadValue<Vector2>();
            if (IsInsideScreen(screenPosition))
            {
                CacheScreenPosition(screenPosition);
                return true;
            }
        }

        if (hasLastScreenPosition)
        {
            screenPosition = lastScreenPosition;
            return true;
        }

        screenPosition = Vector2.zero;
        return false;
    }

    public static bool TryGetWorldPosition(Camera targetCamera, float planeZ, InputAction fallbackAction, out Vector2 worldPosition)
    {
        worldPosition = Vector2.zero;
        if (targetCamera == null || !TryGetScreenPosition(fallbackAction, out Vector2 screenPosition))
        {
            return false;
        }

        float depth = Mathf.Abs(targetCamera.transform.position.z - planeZ);
        Vector3 nextWorld = targetCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, depth));
        worldPosition = nextWorld;
        return true;
    }

    public static bool WasPrimaryPressedThisFrame()
    {
        if (TryGetLegacyMouseButtonDown(0))
        {
            return true;
        }

        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    }

    public static bool WasSecondaryPressedThisFrame()
    {
        if (TryGetLegacyMouseButtonDown(1))
        {
            return true;
        }

        return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
    }

    private static bool TryGetLegacyMousePosition(out Vector2 screenPosition)
    {
        screenPosition = Vector2.zero;
        try
        {
            Vector3 mousePosition = UnityEngine.Input.mousePosition;
            Vector2 candidate = new Vector2(mousePosition.x, mousePosition.y);
            if (!IsInsideScreen(candidate))
            {
                return false;
            }

            screenPosition = candidate;
            return true;
        }
        catch (System.InvalidOperationException)
        {
            return false;
        }
    }

    private static bool TryGetLegacyMouseButtonDown(int button)
    {
        try
        {
            return UnityEngine.Input.GetMouseButtonDown(button);
        }
        catch (System.InvalidOperationException)
        {
            return false;
        }
    }

    private static bool IsInsideScreen(Vector2 screenPosition)
    {
        return screenPosition.x >= 0f
            && screenPosition.y >= 0f
            && screenPosition.x <= Screen.width
            && screenPosition.y <= Screen.height
            && screenPosition.sqrMagnitude > 0.01f;
    }

    private static void CacheScreenPosition(Vector2 screenPosition)
    {
        lastScreenPosition = screenPosition;
        hasLastScreenPosition = true;
    }
}
