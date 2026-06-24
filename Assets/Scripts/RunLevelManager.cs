using UnityEngine;

public class RunLevelManager : MonoBehaviour
{
    public enum RunPhase
    {
        RunMap,
        SafeRoom
    }

    private static RunLevelManager instance;

    [SerializeField] private int chapter = 1;
    [SerializeField] private int stage = 1;
    [SerializeField] private int stagesPerChapter = 3;
    [SerializeField] private int baseEnemyBudget = 20;
    [SerializeField] private int baseInitialEnemySpawnCount = 6;
    [SerializeField] private bool startEntranceCanReturnToSafeRoom;

    private RunPhase phase = RunPhase.RunMap;
    private Vector3 lastMapReturnPosition;

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
    public RunPhase Phase => phase;
    public StageConfig CurrentStageConfig => StageConfigDatabase.Get(chapter, stage);
    public int CurrentEnemyBudget => Mathf.Max(1, CurrentStageConfig != null ? CurrentStageConfig.enemyBudget : baseEnemyBudget + (chapter - 1) * 4 + (stage - 1) * 2);
    public int CurrentInitialEnemySpawnCount => Mathf.Max(0, CurrentStageConfig != null ? CurrentStageConfig.initialEnemySpawnCount : baseInitialEnemySpawnCount + (chapter - 1) * 2 + (stage - 1));
    public bool IsInSafeRoom => phase == RunPhase.SafeRoom;
    public bool StartEntranceCanReturnToSafeRoom => startEntranceCanReturnToSafeRoom;
    public Vector3 LastMapReturnPosition => lastMapReturnPosition;

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
        phase = RunPhase.RunMap;
        startEntranceCanReturnToSafeRoom = false;
        lastMapReturnPosition = Vector3.zero;
    }

    public void AdvanceStage()
    {
        stage++;
        if (stage > Mathf.Max(1, stagesPerChapter))
        {
            chapter++;
            stage = 1;
        }

        phase = RunPhase.RunMap;
        startEntranceCanReturnToSafeRoom = false;
    }

    public void EnterSafeRoom(Vector3 mapReturnPosition)
    {
        lastMapReturnPosition = mapReturnPosition;
        phase = RunPhase.SafeRoom;
    }

    public void ExitSafeRoom()
    {
        phase = RunPhase.RunMap;
    }
}
