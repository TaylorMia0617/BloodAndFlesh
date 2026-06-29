using UnityEngine;

public sealed class WorldHostilityDirectorRunner : MonoBehaviour
{
    private static WorldHostilityDirectorRunner instance;

    public static WorldHostilityDirectorRunner Ensure()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindObjectOfType<WorldHostilityDirectorRunner>();
        if (instance != null)
        {
            return instance;
        }

        GameObject runnerObject = new GameObject("WorldHostilityDirectorRunner");
        instance = runnerObject.AddComponent<WorldHostilityDirectorRunner>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void Update()
    {
        WorldHostilityDirector.Current.Tick(Time.deltaTime);
    }
}
