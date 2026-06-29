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
    [Header("World Hostility Debug")]
    [SerializeField] private bool overrideWorldHostility;
    [SerializeField] private float worldHostilityOverride = 1f;
    [SerializeField] private float currentWorldHostilityPreview = 1f;

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
    public float CurrentWorldHostility => overrideWorldHostility ? Mathf.Max(0f, worldHostilityOverride) : Mathf.Max(0f, CurrentStageConfig != null ? CurrentStageConfig.worldHostility : 1f);
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
        RefreshWorldHostilityPreview();
    }

    private void OnValidate()
    {
        worldHostilityOverride = Mathf.Max(0f, worldHostilityOverride);
        RefreshWorldHostilityPreview();
    }

    public void StartNewRun()
    {
        chapter = 1;
        stage = 1;
        phase = RunPhase.RunMap;
        startEntranceCanReturnToSafeRoom = false;
        lastMapReturnPosition = Vector3.zero;
        RefreshWorldHostilityPreview();
    }

    public void AdvanceStage()
    {
        TaskRunState.Existing?.NotifyStageEnded(lastMapReturnPosition);
        stage++;
        if (stage > Mathf.Max(1, stagesPerChapter))
        {
            chapter++;
            stage = 1;
        }

        phase = RunPhase.RunMap;
        startEntranceCanReturnToSafeRoom = false;
        RefreshWorldHostilityPreview();
    }

    public void EnterSafeRoom(Vector3 mapReturnPosition)
    {
        lastMapReturnPosition = mapReturnPosition;
        phase = RunPhase.SafeRoom;
        WorldHostilityDirector.Current.Notify(new DirectorEvent(DirectorEventType.PlayerEnteredSafehouse, mapReturnPosition));
    }

    public void ExitSafeRoom()
    {
        phase = RunPhase.RunMap;
        RefreshWorldHostilityPreview();
        WorldHostilityDirector.Current.Notify(new DirectorEvent(DirectorEventType.PlayerExitedSafehouse, lastMapReturnPosition));
    }

    private void RefreshWorldHostilityPreview()
    {
        currentWorldHostilityPreview = CurrentWorldHostility;
    }
}
