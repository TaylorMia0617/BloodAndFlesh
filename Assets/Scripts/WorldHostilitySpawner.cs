using UnityEngine;

public class WorldHostilitySpawner : MonoBehaviour
{
    [SerializeField] private GridRouteMapGenerator mapGenerator;
    [SerializeField] private Transform playerTarget;
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private Sprite[] enemySprites;

    public void Configure(GridRouteMapGenerator generator, Transform target, Sprite[] sprites)
    {
        mapGenerator = generator;
        playerTarget = target;
        enemySprites = sprites;
    }

    public void SetSpawnInterval(float interval)
    {
        spawnInterval = Mathf.Max(0.25f, interval);
    }

    public void StartSpawning()
    {
        StopSpawning();
    }

    public void StopSpawning()
    {
    }
}
