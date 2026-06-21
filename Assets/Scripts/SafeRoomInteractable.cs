using UnityEngine;

public class SafeRoomInteractable : MonoBehaviour
{
    [SerializeField] private string description;
    [SerializeField] private bool exitsSafeRoom;

    public void Configure(string nextDescription, bool isExit)
    {
        description = nextDescription;
        exitsSafeRoom = isExit;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerInputManager player = other.GetComponent<PlayerInputManager>();
        if (player == null)
        {
            return;
        }

        if (exitsSafeRoom)
        {
            SafeRoomManager.Instance.ExitSafeRoom(player.transform);
            return;
        }

        Debug.Log(description);
    }
}
