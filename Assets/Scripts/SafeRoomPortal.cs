using UnityEngine;

public class SafeRoomPortal : MonoBehaviour
{
    public enum PortalMode
    {
        DisabledVisualDoor,
        DungeonExitToSafeRoom,
        SafeRoomExitToDungeon
    }

    [SerializeField] private PortalMode mode = PortalMode.DisabledVisualDoor;

    public PortalMode Mode => mode;

    public void Configure(PortalMode nextMode)
    {
        mode = nextMode;
    }

    [System.Obsolete("Use Configure(PortalMode) instead.")]
    public void Configure(bool isExit)
    {
        mode = isExit ? PortalMode.SafeRoomExitToDungeon : PortalMode.DungeonExitToSafeRoom;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerInputManager>() == null)
        {
            return;
        }

        SafeRoomManager manager = SafeRoomManager.Instance;
        if (manager == null)
        {
            return;
        }

        switch (mode)
        {
            case PortalMode.DungeonExitToSafeRoom:
                manager.EnterSafeRoom(other.transform, transform);
                break;
            case PortalMode.SafeRoomExitToDungeon:
                manager.ExitSafeRoom(other.transform);
                break;
        }
    }
}
