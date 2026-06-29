using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class SemanticResourcePickup : MonoBehaviour
{
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private string catalogId;
    [SerializeField] private int amountMin = 1;
    [SerializeField] private int amountMax = 1;
    [SerializeField] private string sourceBuildingId;

    private ResourceNode node;
    private bool collected;

    public void Configure(ResourceNode resourceNode)
    {
        node = resourceNode;
        resourceType = resourceNode.Type;
        catalogId = resourceNode.CatalogId;
        amountMin = Mathf.Max(1, resourceNode.AmountMin);
        amountMax = Mathf.Max(amountMin, resourceNode.AmountMax);
        sourceBuildingId = resourceNode.SourceBuildingId;
        collected = false;
    }

    public bool TryCollect(PlayerInventory inventory)
    {
        if (collected || inventory == null)
        {
            return false;
        }

        ResourceNode pickupNode = node;
        if (string.IsNullOrEmpty(pickupNode.CatalogId))
        {
            pickupNode.Type = resourceType;
            pickupNode.CatalogId = catalogId;
            pickupNode.AmountMin = amountMin;
            pickupNode.AmountMax = amountMax;
            pickupNode.SourceBuildingId = sourceBuildingId;
        }

        int quantity = Random.Range(Mathf.Max(1, pickupNode.AmountMin), Mathf.Max(Mathf.Max(1, pickupNode.AmountMin), pickupNode.AmountMax) + 1);
        bool accepted;
        if (pickupNode.Type == ResourceType.Currency)
        {
            inventory.AddGold(quantity);
            accepted = true;
        }
        else
        {
            accepted = inventory.TryAddItem(SemanticResourceResolver.CreateInventoryItem(pickupNode, quantity));
        }

        if (!accepted)
        {
            return false;
        }

        collected = true;
        TaskRunState.Existing?.NotifyItemCollected(pickupNode.CatalogId, quantity, transform.position);
        NotifyDirector(pickupNode, quantity);
        gameObject.SetActive(false);
        return true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerInventory inventory = other != null ? other.GetComponentInParent<PlayerInventory>() : null;
        TryCollect(inventory);
    }

    private void NotifyDirector(ResourceNode pickupNode, int quantity)
    {
        float magnitude = EstimateValueMagnitude(pickupNode, quantity);
        if (magnitude <= 0f)
        {
            return;
        }

        WorldHostilityDirector.Current.Notify(new DirectorEvent(
            DirectorEventType.ValuableLootPicked,
            transform.position,
            magnitude,
            pickupNode.CatalogId));
    }

    private static float EstimateValueMagnitude(ResourceNode pickupNode, int quantity)
    {
        switch (pickupNode.Type)
        {
            case ResourceType.RareCore:
            case ResourceType.TaskItem:
            case ResourceType.ExtractionUpgrade:
                return 1f + Mathf.Max(0, quantity - 1) * 0.25f;
            case ResourceType.Material:
                return pickupNode.CatalogId != null && pickupNode.CatalogId.Contains("arcane") ? 0.8f : 0.35f;
            case ResourceType.Currency:
                return quantity >= 10 ? 0.3f : 0.1f;
            case ResourceType.Medical:
            default:
                return 0.2f;
        }
    }
}
