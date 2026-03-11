using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[AddComponentMenu("Stages/Stage Sequence Controller")]
public class StageSequenceController : MonoBehaviour
{
    private const int DefaultStartingStageIndex = 1;

    [SerializeField] private GameObject[] stageRoots = new GameObject[3];
    [SerializeField] private bool autoCreateStageSetupIfMissing = true;
    [SerializeField] private bool autoSyncStageSetup = true;
    [SerializeField] private int startingStageIndex = DefaultStartingStageIndex;
    [SerializeField, HideInInspector] private bool migratedStartingStageDefault;
    [SerializeField] private bool allowKeyboardShortcuts = true;
    [SerializeField] private KeyCode previousStageKey = KeyCode.LeftBracket;
    [SerializeField] private KeyCode nextStageKey = KeyCode.RightBracket;
    [SerializeField] private KeyCode stage1Key = KeyCode.Alpha1;
    [SerializeField] private KeyCode stage2Key = KeyCode.Alpha2;
    [SerializeField] private KeyCode stage3Key = KeyCode.Alpha3;

#if UNITY_EDITOR
    private bool pendingEditorSync;
#endif

    private bool pendingRuntimeStageRefresh;

    public int CurrentStageIndex { get; private set; }

    private void Reset()
    {
        MigrateStartingStageDefault();
        DiscoverExistingStageRoots();
        SyncStageSetupIfEnabled();
        ApplyStageVisibility(Mathf.Clamp(startingStageIndex, 0, Mathf.Max(0, stageRoots.Length - 1)));
    }

    private void Awake()
    {
        MigrateStartingStageDefault();
        DiscoverExistingStageRoots();
        SyncStageSetupIfEnabled();
        ApplyStageVisibility(Mathf.Clamp(startingStageIndex, 0, Mathf.Max(0, stageRoots.Length - 1)));
        pendingRuntimeStageRefresh = Application.isPlaying;
    }

    private void OnValidate()
    {
        MigrateStartingStageDefault();
        DiscoverExistingStageRoots();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            ScheduleEditorSync();
        }
        else
#endif
        {
            SyncStageSetupIfEnabled();
        }

        if (!Application.isPlaying)
        {
            ApplyStageVisibility(Mathf.Clamp(startingStageIndex, 0, Mathf.Max(0, stageRoots.Length - 1)));
        }
    }

    private void Update()
    {
        if (pendingRuntimeStageRefresh && Application.isPlaying)
        {
            pendingRuntimeStageRefresh = false;
            RefreshActiveStageState();
        }

        if (!allowKeyboardShortcuts)
        {
            return;
        }

        if (Input.GetKeyDown(previousStageKey))
        {
            PreviousStage();
        }
        else if (Input.GetKeyDown(nextStageKey))
        {
            NextStage();
        }
        else if (Input.GetKeyDown(stage1Key))
        {
            SetStage(0);
        }
        else if (Input.GetKeyDown(stage2Key))
        {
            SetStage(1);
        }
        else if (Input.GetKeyDown(stage3Key))
        {
            SetStage(2);
        }
    }

    public void SetStage(int stageIndex)
    {
        ApplyStageVisibility(stageIndex);
        RefreshActiveStageState();
    }

    public void NextStage()
    {
        if (stageRoots == null || stageRoots.Length == 0)
        {
            return;
        }

        SetStage((CurrentStageIndex + 1) % stageRoots.Length);
    }

    public void PreviousStage()
    {
        if (stageRoots == null || stageRoots.Length == 0)
        {
            return;
        }

        int previousIndex = CurrentStageIndex - 1;
        if (previousIndex < 0)
        {
            previousIndex = stageRoots.Length - 1;
        }

        SetStage(previousIndex);
    }

    [ContextMenu("Create Missing Stage Setup")]
    public void CreateMissingStageSetup()
    {
        DiscoverExistingStageRoots();
        stageRoots = StageSequenceDebugBuilder.EnsureStageSetup(transform, stageRoots);
        ApplyStageVisibility(Mathf.Clamp(startingStageIndex, 0, Mathf.Max(0, stageRoots.Length - 1)));
    }

    [ContextMenu("Sync Stage Setup")]
    public void SyncStageSetup()
    {
        DiscoverExistingStageRoots();
        stageRoots = StageSequenceDebugBuilder.EnsureStageSetup(transform, stageRoots);
        ApplyStageVisibility(Mathf.Clamp(startingStageIndex, 0, Mathf.Max(0, stageRoots.Length - 1)));
    }

    public void CreateDebugStageSetup()
    {
        CreateMissingStageSetup();
    }

    public void ConfigureStageRoots(GameObject[] roots)
    {
        stageRoots = roots;
        ApplyStageVisibility(Mathf.Clamp(startingStageIndex, 0, Mathf.Max(0, stageRoots.Length - 1)));
    }

    private void ApplyStageVisibility(int activeIndex)
    {
        if (stageRoots == null || stageRoots.Length == 0)
        {
            CurrentStageIndex = 0;
            return;
        }

        int clampedIndex = Mathf.Clamp(activeIndex, 0, stageRoots.Length - 1);
        CurrentStageIndex = clampedIndex;

        for (int index = 0; index < stageRoots.Length; index++)
        {
            GameObject stageRoot = stageRoots[index];
            if (stageRoot == null)
            {
                continue;
            }

            stageRoot.SetActive(index == clampedIndex);
        }
    }

    private void RefreshActiveStageState()
    {
        if (stageRoots == null || CurrentStageIndex < 0 || CurrentStageIndex >= stageRoots.Length)
        {
            return;
        }

        GameObject activeRoot = stageRoots[CurrentStageIndex];
        if (activeRoot == null)
        {
            return;
        }

        SpotlightSensor[] sensors = activeRoot.GetComponentsInChildren<SpotlightSensor>(true);
        for (int index = 0; index < sensors.Length; index++)
        {
            if (sensors[index] != null)
            {
                sensors[index].RefreshState();
            }
        }

        StageSymbolMappingDisplay[] displays = activeRoot.GetComponentsInChildren<StageSymbolMappingDisplay>(true);
        for (int index = 0; index < displays.Length; index++)
        {
            if (displays[index] != null)
            {
                displays[index].RefreshState();
            }
        }

    }

    private void CreateStageSetupIfNeeded()
    {
        if (!autoCreateStageSetupIfMissing)
        {
            return;
        }

        if (!AreAnyStageRootsMissing())
        {
            return;
        }

        stageRoots = StageSequenceDebugBuilder.EnsureStageSetup(transform, stageRoots);
    }

    private void SyncStageSetupIfEnabled()
    {
        if (autoSyncStageSetup)
        {
            stageRoots = StageSequenceDebugBuilder.EnsureStageSetup(transform, stageRoots);
            return;
        }

        CreateStageSetupIfNeeded();
    }

    private bool AreAnyStageRootsMissing()
    {
        if (stageRoots == null || stageRoots.Length < 3)
        {
            return true;
        }

        for (int index = 0; index < stageRoots.Length; index++)
        {
            if (stageRoots[index] == null)
            {
                return true;
            }
        }

        return false;
    }

    private void DiscoverExistingStageRoots()
    {
        if (stageRoots == null || stageRoots.Length != 3)
        {
            stageRoots = new GameObject[3];
        }

        StageRootMarker[] markers = GetComponentsInChildren<StageRootMarker>(true);
        for (int index = 0; index < markers.Length; index++)
        {
            StageRootMarker marker = markers[index];
            if (marker == null)
            {
                continue;
            }

            int stageIndex = marker.StageIndex;
            if (stageIndex < 0 || stageIndex >= stageRoots.Length)
            {
                continue;
            }

            if (stageRoots[stageIndex] == null)
            {
                stageRoots[stageIndex] = marker.gameObject;
            }
        }
    }

    private void MigrateStartingStageDefault()
    {
        if (migratedStartingStageDefault)
        {
            return;
        }

        startingStageIndex = DefaultStartingStageIndex;
        migratedStartingStageDefault = true;
    }

#if UNITY_EDITOR
    private void ScheduleEditorSync()
    {
        if (pendingEditorSync)
        {
            return;
        }

        pendingEditorSync = true;
        EditorApplication.delayCall += RunDelayedEditorSync;
    }

    private void RunDelayedEditorSync()
    {
        EditorApplication.delayCall -= RunDelayedEditorSync;
        pendingEditorSync = false;

        if (this == null || gameObject == null || Application.isPlaying)
        {
            return;
        }

        CreateStageSetupIfNeeded();
        ApplyStageVisibility(Mathf.Clamp(startingStageIndex, 0, Mathf.Max(0, stageRoots.Length - 1)));
        EditorUtility.SetDirty(this);
    }
#endif
}