using UnityEngine;

public class RunLevelManager : MonoBehaviour
{
    private static RunLevelManager instance;

    [SerializeField] private int chapter = 1;
    [SerializeField] private int stage = 1;
    [SerializeField] private int stagesPerChapter = 3;

    public static RunLevelManager Instance
    {
        get
        {
            if (instance != null)
            {
                return instance;
            }

            instance = FindObjectOfType<RunLevelManager>();
            if (instance != null)
            {
                return instance;
            }

            GameObject managerObject = new GameObject("RunLevelManager");
            instance = managerObject.AddComponent<RunLevelManager>();
            return instance;
        }
    }

    public int Chapter => chapter;
    public int Stage => stage;
    public string StageLabel => $"{chapter}-{stage}";

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    public void StartNewRun()
    {
        chapter = 1;
        stage = 1;
    }

    public void AdvanceStage()
    {
        stage++;
        if (stage > Mathf.Max(1, stagesPerChapter))
        {
            chapter++;
            stage = 1;
        }
    }
}
