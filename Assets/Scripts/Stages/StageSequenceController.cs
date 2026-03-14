using System.Collections;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Stage 1〜4 の root を管理し、表示ステージを切り替えるコントローラー。
/// <para>
/// <see cref="SetStage(int)"/>: 即座にステージを切り替える。<br/>
/// <see cref="FadeToStage(int)"/>: <see cref="StageTransitionFader"/> によるフェード付き遷移（Play Mode のみ）。
/// </para>
/// <para>
/// <see cref="StageSequenceDebugBuilder.EnsureStageSetup"/> で不足ステージを自動補完する。<br/>
/// <c>autoSyncStageSetup = true</c> のとき、OnValidate / Awake のたびに自動同期が走る。
/// </para>
/// </summary>
public interface IStageActivationHandler
{
    void OnStageActivated();
}

[AddComponentMenu("Stages/Stage Sequence Controller")]
public class StageSequenceController : MonoBehaviour
{
    private const int DefaultStartingStageIndex = 1;

    [SerializeField] private GameObject[] stageRoots = new GameObject[4];
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
    [SerializeField] private KeyCode stage4Key = KeyCode.Alpha4;

#if UNITY_EDITOR
    private bool pendingEditorSync;
#endif

    private bool pendingRuntimeStageRefresh;
    private StageTransitionFader transitionFader;
    private PoseTestBootstrap poseBootstrap;

    public int CurrentStageIndex { get; private set; }
    public int StageCount => stageRoots != null ? stageRoots.Length : 0;

    private void Reset()
    {
        EnsureAudioController();
        MigrateStartingStageDefault();
        DiscoverExistingStageRoots();
        SyncStageSetupIfEnabled();
        ApplyStageVisibility(GetPreferredEditModeStageIndex());
    }

    private void Awake()
    {
        EnsureAudioController();
        MigrateStartingStageDefault();
        DiscoverExistingStageRoots();
        ResolveRuntimeReferences();
        SyncStageSetupIfEnabled();
        ApplyStageVisibility(Mathf.Clamp(startingStageIndex, 0, Mathf.Max(0, stageRoots.Length - 1)));
        pendingRuntimeStageRefresh = Application.isPlaying;
    }

    private void OnValidate()
    {
        EnsureAudioController();
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
            ApplyStageVisibility(GetPreferredEditModeStageIndex());
        }
    }

    private void Update()
    {
        if (pendingRuntimeStageRefresh && Application.isPlaying)
        {
            pendingRuntimeStageRefresh = false;
            RefreshActiveStageState();
        }

        if (allowKeyboardShortcuts && Input.GetKeyDown(previousStageKey))
        {
            PreviousStage();
        }
        else if (allowKeyboardShortcuts && Input.GetKeyDown(nextStageKey))
        {
            NextStage();
        }

        if (Input.GetKeyDown(stage1Key) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            SetStage(0);
        }
        else if (Input.GetKeyDown(stage2Key) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            SetStage(1);
        }
        else if (Input.GetKeyDown(stage3Key) || Input.GetKeyDown(KeyCode.Keypad3))
        {
            SetStage(2);
        }
        else if (Input.GetKeyDown(stage4Key) || Input.GetKeyDown(KeyCode.Keypad4))
        {
            SetStage(3);
        }
    }

    public void SetStage(int stageIndex)
    {
        ApplyStageVisibility(stageIndex);
        RefreshActiveStageState();
        NotifyStageActivated();
    } 

    public void FadeToStage(int stageIndex)
    {
        ResolveRuntimeReferences();
        if (!Application.isPlaying || transitionFader == null || transitionFader.IsFading)
        {
            SetStage(stageIndex);
            return;
        }

        StartCoroutine(FadeToStageWithSpotlightControl(stageIndex));
    }

    private IEnumerator FadeToStageWithSpotlightControl(int stageIndex)
    {
        ResolveRuntimeReferences();

        Light activeSpotLight = poseBootstrap != null ? poseBootstrap.ActiveSpotLight : null;
        bool hadSpotlight = activeSpotLight != null;
        bool restoreEnabled = hadSpotlight && activeSpotLight.enabled;

        yield return transitionFader.FadeOutIn(
            () =>
            {
                // 全黒タイミング: スポットライトを消してステージ〜切り替え
                if (hadSpotlight)
                {
                    activeSpotLight.enabled = false;
                }
                ApplyStageVisibility(stageIndex);
                RefreshActiveStageState();
                NotifyStageActivated();
            },
            () =>
            { 
                // フェードイン開始時: スポットライトを復元 → 屠坡と共に庺なシーンが徐々に明るく
                if (hadSpotlight)
                {
                    RefreshActiveStageState();
                    activeSpotLight.enabled = restoreEnabled;
                }
            });
    }

    public void FadeToStageWithOptions(int stageIndex, float fadeOutDuration, float fadeInDuration, Color fadeOverlayColor)
    {
        FadeToStageWithOptions(stageIndex, fadeOutDuration, fadeInDuration, fadeOverlayColor, true);
    }

    public void FadeToStageWithOptions(int stageIndex, float fadeOutDuration, float fadeInDuration, Color fadeOverlayColor, bool preloadNextStage)
    {
        ResolveRuntimeReferences();
        if (!Application.isPlaying || transitionFader == null || transitionFader.IsFading)
        {
            SetStage(stageIndex);
            return;
        }

        StartCoroutine(FadeToStageWithCustomFade(stageIndex, fadeOutDuration, fadeInDuration, fadeOverlayColor, preloadNextStage));
    }

    private IEnumerator FadeToStageWithCustomFade(int stageIndex, float fadeOutDuration, float fadeInDuration, Color fadeOverlayColor, bool preloadNextStage)
    {
        ResolveRuntimeReferences();

        Light activeSpotLight = poseBootstrap != null ? poseBootstrap.ActiveSpotLight : null;
        bool hadSpotlight = activeSpotLight != null;
        bool restoreEnabled = hadSpotlight && activeSpotLight.enabled;

        yield return transitionFader.FadeOutInCustom(
            () =>
            {
                if (hadSpotlight)
                {
                    activeSpotLight.enabled = false;
                }
                ApplyStageVisibility(stageIndex);
                RefreshActiveStageState();
                NotifyStageActivated();
            },
            fadeOutDuration,
            fadeInDuration,
            fadeOverlayColor,
            () =>
            {
                if (hadSpotlight)
                {
                    RefreshActiveStageState();
                    activeSpotLight.enabled = restoreEnabled;
                }
            });
    }

    private IEnumerator PreloadStageWhenOverlayOpaque(GameObject stageRoot)
    {
        if (stageRoot == null || stageRoot.activeSelf)
        {
            yield break;
        }

        // フェードオーバーレイがほぼ不透明になるまで待機してから次ステージを有効化する。
        // transitionFader が消滅した場合や IsFading が false になった場合はタイムアウトとして抜ける。
        float waited = 0f;
        const float timeoutSeconds = 5f;
        yield return new WaitUntil(() =>
        {
            waited += Time.deltaTime;
            return transitionFader == null
                || !transitionFader.IsFading
                || transitionFader.Alpha >= 0.88f
                || waited >= timeoutSeconds;
        });

        if (stageRoot != null && !stageRoot.activeSelf)
        {
            stageRoot.SetActive(true);
        }
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
        ApplyStageVisibility(GetPreferredEditModeStageIndex());
    }

    [ContextMenu("Sync Stage Setup")]
    public void SyncStageSetup()
    {
        DiscoverExistingStageRoots();
        stageRoots = StageSequenceDebugBuilder.EnsureStageSetup(transform, stageRoots);
        ApplyStageVisibility(GetPreferredEditModeStageIndex());
    }

    public void CreateDebugStageSetup()
    {
        CreateMissingStageSetup();
    }

    public void ConfigureStageRoots(GameObject[] roots)
    {
        stageRoots = roots;
        ApplyStageVisibility(GetPreferredEditModeStageIndex());
    }

    private int GetPreferredEditModeStageIndex()
    {
        if (Application.isPlaying)
        {
            return Mathf.Clamp(startingStageIndex, 0, Mathf.Max(0, stageRoots.Length - 1));
        }

        int activeStageIndex = GetCurrentlyActiveStageIndex();
        if (activeStageIndex >= 0)
        {
            return activeStageIndex;
        }

        return Mathf.Clamp(startingStageIndex, 0, Mathf.Max(0, stageRoots.Length - 1));
    }

    private void EnsureAudioController()
    {
        if (GetComponent<StageAudioController>() == null)
        {
            gameObject.AddComponent<StageAudioController>();
        }
    }

    private int GetCurrentlyActiveStageIndex()
    {
        if (stageRoots == null)
        {
            return -1;
        }

        for (int index = 0; index < stageRoots.Length; index++)
        {
            GameObject stageRoot = stageRoots[index];
            if (stageRoot != null && stageRoot.activeSelf)
            {
                return index;
            }
        }

        return -1;
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

        ApplyActiveStageSpotlightSettings(activeRoot);

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

    private void NotifyStageActivated()
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

        MonoBehaviour[] behaviours = activeRoot.GetComponentsInChildren<MonoBehaviour>(true);
        for (int index = 0; index < behaviours.Length; index++)
        {
            if (behaviours[index] is IStageActivationHandler activationHandler)
            {
                activationHandler.OnStageActivated();
            }
        }
    }

    private void ApplyActiveStageSpotlightSettings(GameObject activeRoot)
    {
        if (activeRoot == null)
        {
            return;
        }

        ResolveRuntimeReferences();
        Light activeSpotLight = poseBootstrap != null ? poseBootstrap.ActiveSpotLight : null;
        if (activeSpotLight == null)
        {
            return;
        }

        StageSpotlightSettings spotlightSettings = activeRoot.GetComponent<StageSpotlightSettings>();
        if (spotlightSettings != null)
        {
            spotlightSettings.ApplyTo(activeSpotLight);
        }
    }

    private void ResolveRuntimeReferences()
    {
        if (transitionFader == null)
        {
            transitionFader = GetComponent<StageTransitionFader>();
            if (transitionFader == null)
            {
                transitionFader = gameObject.AddComponent<StageTransitionFader>();
            }
        }

        if (poseBootstrap == null)
        {
            poseBootstrap = FindFirstObjectByType<PoseTestBootstrap>();
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
        if (stageRoots == null || stageRoots.Length < StageCount)
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
        if (stageRoots == null || stageRoots.Length != StageCount)
        {
            stageRoots = new GameObject[StageCount];
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