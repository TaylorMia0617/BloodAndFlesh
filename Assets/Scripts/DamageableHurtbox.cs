using UnityEngine;

[DisallowMultipleComponent]
public sealed class DamageableHurtbox : MonoBehaviour
{
    private IDamageable owner;
    private int ownerId;

    public IDamageable Owner
    {
        get
        {
            if (owner == null)
            {
                ResolveOwner();
            }

            return owner;
        }
    }

    public int OwnerId
    {
        get
        {
            if (ownerId == 0)
            {
                ResolveOwner();
            }

            return ownerId;
        }
    }

    private void Awake()
    {
        ResolveOwner();
    }

    private void ResolveOwner()
    {
        owner = GetComponentInParent<IDamageable>();
        Component ownerComponent = owner as Component;
        ownerId = ownerComponent != null ? ownerComponent.gameObject.GetInstanceID() : gameObject.GetInstanceID();
    }
}
