using UnityEngine;

public sealed class StunStatusVisual : MonoBehaviour
{
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.78f, 0f);
    [SerializeField] private float verticalPadding = 0.18f;
    [SerializeField] private float rotationSpeed = 260f;
    [SerializeField] private int sortingOrder = 60;

    private SpriteRenderer iconRenderer;
    private SpriteRenderer ownerRenderer;
    private float visibleUntil;

    private void Awake()
    {
        EnsureRenderer();
        SetVisible(false);
    }

    private void Update()
    {
        if (iconRenderer == null || !iconRenderer.enabled)
        {
            return;
        }

        UpdateIconPosition();
        iconRenderer.transform.Rotate(Vector3.forward, rotationSpeed * Time.deltaTime);
        if (Time.time >= visibleUntil)
        {
            SetVisible(false);
        }
    }

    public void Show(float duration)
    {
        EnsureRenderer();
        visibleUntil = Mathf.Max(visibleUntil, Time.time + Mathf.Max(0f, duration));
        UpdateIconPosition();
        SetVisible(true);
    }

    public void Hide()
    {
        visibleUntil = 0f;
        SetVisible(false);
    }

    private void EnsureRenderer()
    {
        if (iconRenderer != null)
        {
            return;
        }

        Transform existing = transform.Find("StunStatusIcon");
        GameObject iconObject = existing != null ? existing.gameObject : new GameObject("StunStatusIcon");
        iconObject.transform.SetParent(transform);
        iconObject.transform.localPosition = localOffset;
        iconObject.transform.localRotation = Quaternion.identity;
        iconObject.transform.localScale = Vector3.one;

        iconRenderer = iconObject.GetComponent<SpriteRenderer>();
        if (iconRenderer == null)
        {
            iconRenderer = iconObject.AddComponent<SpriteRenderer>();
        }

        iconRenderer.sprite = Resources.Load<Sprite>("Arts/UI/Status/status_stun");
        iconRenderer.sortingOrder = sortingOrder;
        ownerRenderer = GetComponent<SpriteRenderer>();
    }

    private void UpdateIconPosition()
    {
        if (iconRenderer == null)
        {
            return;
        }

        if (ownerRenderer == null)
        {
            ownerRenderer = GetComponent<SpriteRenderer>();
        }

        if (ownerRenderer != null)
        {
            Bounds bounds = ownerRenderer.bounds;
            iconRenderer.transform.position = new Vector3(bounds.center.x, bounds.max.y + verticalPadding, transform.position.z);
        }
        else
        {
            iconRenderer.transform.localPosition = localOffset;
        }
    }

    private void SetVisible(bool visible)
    {
        if (iconRenderer != null)
        {
            iconRenderer.enabled = visible;
        }
    }
}
