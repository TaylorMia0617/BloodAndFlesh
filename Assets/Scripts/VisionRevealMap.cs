using UnityEngine;

public sealed class VisionRevealMap
{
    private bool[] explored;
    private bool[] visible;

    public int Width { get; private set; }
    public int Height { get; private set; }
    public int VisibleCount { get; private set; }
    public int ExploredCount { get; private set; }

    public VisionRevealMap(int width, int height)
    {
        Resize(width, height);
    }

    public void Resize(int width, int height)
    {
        Width = Mathf.Max(0, width);
        Height = Mathf.Max(0, height);
        int length = Width * Height;
        explored = new bool[length];
        visible = new bool[length];
        VisibleCount = 0;
        ExploredCount = 0;
    }

    public void BeginFrame()
    {
        if (visible == null)
        {
            return;
        }

        for (int i = 0; i < visible.Length; i++)
        {
            visible[i] = false;
        }

        VisibleCount = 0;
    }

    public void MarkVisible(Vector2Int cell)
    {
        if (!IsInside(cell))
        {
            return;
        }

        int index = Index(cell);
        if (!visible[index])
        {
            visible[index] = true;
            VisibleCount++;
        }

        if (!explored[index])
        {
            explored[index] = true;
            ExploredCount++;
        }
    }

    public bool IsVisible(Vector2Int cell)
    {
        return IsInside(cell) && visible[Index(cell)];
    }

    public bool IsExplored(Vector2Int cell)
    {
        return IsInside(cell) && explored[Index(cell)];
    }

    private bool IsInside(Vector2Int cell)
    {
        return cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Height;
    }

    private int Index(Vector2Int cell)
    {
        return cell.y * Width + cell.x;
    }
}
