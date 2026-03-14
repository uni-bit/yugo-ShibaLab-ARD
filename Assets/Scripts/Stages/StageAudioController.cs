using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[AddComponentMenu("Stages/Stage Audio Controller")]
public class StageAudioController : MonoBehaviour
{
    private const string Stage1AmbientPath = "Assets/assets/sounds/環境音/stage1/stage1environment.mp3";
    private const string Stage2AmbientPath = "Assets/assets/sounds/環境音/stage2/stage2environment.MP3";
    private const string Stage3AmbientPath = "Assets/assets/sounds/環境音/stage3/stage3environment.MP3";
    private const string Stage1AnimalMovePath = "Assets/assets/sounds/環境音/stage1/animal-move-gimmick.MP3";
    private const string Stage1LeafMovePath = "Assets/assets/sounds/環境音/stage1/tree-move-reaf-gimmick.mp3";
    private const string Stage1SoilMovePath = "Assets/assets/sounds/環境音/stage1/tree-move-soil-gimmick.mp3";
    private const string Stage2DestroyPath = "Assets/assets/sounds/環境音/stage2/gimmick壁破壊候補/破壊音.mp3";
    private const string Stage2DestroyPath2 = "Assets/assets/sounds/環境音/stage2/gimmick壁破壊候補/破壊音2.mp3";
    private const string Stage2ExplosionPath = "Assets/assets/sounds/環境音/stage2/gimmick壁破壊候補/爆発.mp3";
    private const string Stage2WaterDrop1Path = "Assets/assets/sounds/環境音/stage2/素材/Water_Drop03-1(Low-Reverb).mp3";
    private const string Stage2WaterDrop2Path = "Assets/assets/sounds/環境音/stage2/素材/Water_Drop03-2(High-Reverb).mp3";
    private const string Stage2WaterDrop3Path = "Assets/assets/sounds/環境音/stage2/素材/Water_Drop03-3(Low-Dry).mp3";
    private const string Stage2WaterDrop4Path = "Assets/assets/sounds/環境音/stage2/素材/Water_Drop03-4(High-Dry).mp3";
    private const string Stage3Glow1Path = "Assets/assets/sounds/環境音/stage3/gimmick光る音（それぞれ違うの割り当ててほしい）/hikari1.mp3";
    private const string Stage3Glow2Path = "Assets/assets/sounds/環境音/stage3/gimmick光る音（それぞれ違うの割り当ててほしい）/hikari2.mp3";
    private const string Stage3Glow3Path = "Assets/assets/sounds/環境音/stage3/gimmick光る音（それぞれ違うの割り当ててほしい）/hikari3.mp3";
    private const string Stage3StoneFallPath = "Assets/assets/sounds/環境音/stage3/素材/砂や小石が落ちる音2.mp3";
    private const string Stage3RockRisePath = "Assets/assets/sounds/環境音/stage3/素材/岩が浮く瞬間の振動音1.mp3";
    private const string CommonSuccessPath = "Assets/assets/sounds/環境音/共通/succes.mp3";

    [Header("Sources")]
    [SerializeField] private AudioSource ambientSource;
    [SerializeField] private AudioSource oneShotSource;

    [Header("Master Volume")]
    [SerializeField] private KeyCode volumeUpKey = KeyCode.UpArrow;
    [SerializeField] private KeyCode volumeDownKey = KeyCode.DownArrow;
    [SerializeField] private float masterVolumeStep = 0.05f;

    [Header("Mix")]
    [SerializeField] private float ambientVolume = 0.65f;
    [SerializeField] private float oneShotVolume = 0.95f;

    [Header("Clips")]
    [SerializeField] private AudioClip stage1Ambient;
    [SerializeField] private AudioClip stage2Ambient;
    [SerializeField] private AudioClip stage3Ambient;
    [SerializeField] private AudioClip commonSuccess;
    [SerializeField] private AudioClip stage1AnimalMove;
    [SerializeField] private AudioClip stage1LeafMove;
    [SerializeField] private AudioClip stage1SoilMove;
    [SerializeField] private AudioClip stage2Destroy;
    [SerializeField] private AudioClip stage2DestroySecondary;
    [SerializeField] private AudioClip stage2Explosion;
    [SerializeField] private AudioClip stage3RockRise;
    [SerializeField] private AudioClip stage3StoneFall;
    [SerializeField] private AudioClip[] stage2WaterDrops = new AudioClip[0];
    [SerializeField] private AudioClip[] stage3GlowClips = new AudioClip[0];

    private StageSequenceController stageSequenceController;
    private StageLightCreatureTarget[] stage1Targets = new StageLightCreatureTarget[0];
    private StageLightOrderedPuzzle stage1Puzzle;
    private StageSymbolNumberRevealTarget[] stage2RevealTargets = new StageSymbolNumberRevealTarget[0];
    private StageLightCodeDialColumn[] stage2Columns = new StageLightCodeDialColumn[0];
    private StageLightCodeLockPuzzle stage2CodeLock;
    private Stage2CompletionSequence stage2CompletionSequence;
    private Stage3RockHintPuzzle stage3Puzzle;
    private Stage4SequenceController stage4Sequence;

    private readonly Dictionary<int, bool> creatureSolvedStates = new Dictionary<int, bool>();
    private readonly Dictionary<int, bool> revealStates = new Dictionary<int, bool>();
    private readonly Dictionary<int, int> dialDigitStates = new Dictionary<int, int>();
    private int lastStageIndex = -1;
    private bool lastStage1Solved;
    private bool lastStage2Solved;
    private bool lastStage2CompletionPlaying;
    private bool lastStage3Green;
    private bool lastStage3Blue;
    private bool lastStage3Complete;
    private int lastStage4ActivationCount;
    private int lastStage4MessageShownCount;
    private int lastStage4ReturnTriggeredCount;
    private int waterDropIndex;

    private void Reset()
    {
        EnsureAudioSources();
#if UNITY_EDITOR
        AutoAssignClips();
#endif
    }

    private void Awake()
    {
        EnsureAudioSources();
        ResolveReferences(true);
#if UNITY_EDITOR
        AutoAssignClips();
#endif
        ApplyAmbientForCurrentStage();
    }

    private void OnEnable()
    {
        ResolveReferences(true);
        ApplyAmbientForCurrentStage();
    }

    private void OnValidate()
    {
        EnsureAudioSources();
#if UNITY_EDITOR
        AutoAssignClips();
#endif
    }

    private void Update()
    {
        HandleMasterVolumeInput();
        ResolveReferences(false);

        if (stageSequenceController == null)
        {
            return;
        }

        int currentStageIndex = stageSequenceController.CurrentStageIndex;
        if (currentStageIndex != lastStageIndex)
        {
            lastStageIndex = currentStageIndex;
            ApplyAmbientForCurrentStage();
            if (currentStageIndex == 2)
            {
                PlayOneShot(stage3RockRise, 0.8f);
            }
        }

        PollStage1Audio(currentStageIndex == 0);
        PollStage2Audio(currentStageIndex == 1);
        PollHintPuzzleAudio(stage3Puzzle, 2, currentStageIndex == 2, ref lastStage3Green, ref lastStage3Blue, ref lastStage3Complete);
        PollStage4Audio(currentStageIndex == 3);
    }

    private void HandleMasterVolumeInput()
    {
        if (Input.GetKeyDown(volumeUpKey))
        {
            AudioListener.volume = Mathf.Clamp01(AudioListener.volume + masterVolumeStep);
        }
        else if (Input.GetKeyDown(volumeDownKey))
        {
            AudioListener.volume = Mathf.Clamp01(AudioListener.volume - masterVolumeStep);
        }
    }

    private void EnsureAudioSources()
    {
        if (ambientSource == null)
        {
            ambientSource = GetOrCreateSource("Ambient Source");
            ambientSource.loop = true;
            ambientSource.playOnAwake = true;
        }

        if (oneShotSource == null)
        {
            oneShotSource = GetOrCreateSource("One Shot Source");
            oneShotSource.loop = false;
            oneShotSource.playOnAwake = false;
        }

        ambientSource.spatialBlend = 0f;
        ambientSource.volume = ambientVolume;
        oneShotSource.spatialBlend = 0f;
        oneShotSource.volume = oneShotVolume;
    }

    private AudioSource GetOrCreateSource(string childName)
    {
        Transform child = transform.Find(childName);
        if (child == null)
        {
            child = new GameObject(childName).transform;
            child.SetParent(transform, false);
        }

        AudioSource source = child.GetComponent<AudioSource>();
        if (source == null)
        {
            source = child.gameObject.AddComponent<AudioSource>();
        }

        return source;
    }

    private void ResolveReferences(bool forceRefresh)
    {
        if (stageSequenceController == null)
        {
            stageSequenceController = GetComponent<StageSequenceController>();
        }

        if (stageSequenceController == null)
        {
            return;
        }

        if (!forceRefresh
            && stage1Targets.Length > 0
            && stage2RevealTargets.Length > 0
            && stage3Puzzle != null
            && stage4Sequence != null)
        {
            return;
        }

        Dictionary<int, Transform> stageRoots = CollectStageRoots();
        stage1Targets = stageRoots.TryGetValue(0, out Transform stage1Root)
            ? stage1Root.GetComponentsInChildren<StageLightCreatureTarget>(true)
            : new StageLightCreatureTarget[0];
        stage1Puzzle = stage1Root != null ? stage1Root.GetComponentInChildren<StageLightOrderedPuzzle>(true) : null;

        stage2RevealTargets = stageRoots.TryGetValue(1, out Transform stage2Root)
            ? stage2Root.GetComponentsInChildren<StageSymbolNumberRevealTarget>(true)
            : new StageSymbolNumberRevealTarget[0];
        stage2Columns = stage2Root != null ? stage2Root.GetComponentsInChildren<StageLightCodeDialColumn>(true) : new StageLightCodeDialColumn[0];
        stage2CodeLock = stage2Root != null ? stage2Root.GetComponentInChildren<StageLightCodeLockPuzzle>(true) : null;
        stage2CompletionSequence = stage2Root != null ? stage2Root.GetComponentInChildren<Stage2CompletionSequence>(true) : null;

        stage3Puzzle = stageRoots.TryGetValue(2, out Transform stage3Root)
            ? stage3Root.GetComponentInChildren<Stage3RockHintPuzzle>(true)
            : null;
        stage4Sequence = stageRoots.TryGetValue(3, out Transform stage4Root)
            ? stage4Root.GetComponentInChildren<Stage4SequenceController>(true)
            : null;

        InitializeStateCache();
    }

    private Dictionary<int, Transform> CollectStageRoots()
    {
        Dictionary<int, Transform> roots = new Dictionary<int, Transform>();
        StageRootMarker[] markers = GetComponentsInChildren<StageRootMarker>(true);
        for (int index = 0; index < markers.Length; index++)
        {
            StageRootMarker marker = markers[index];
            if (marker == null)
            {
                continue;
            }

            roots[marker.StageIndex] = marker.transform;
        }

        return roots;
    }

    private void InitializeStateCache()
    {
        creatureSolvedStates.Clear();
        for (int index = 0; index < stage1Targets.Length; index++)
        {
            StageLightCreatureTarget target = stage1Targets[index];
            if (target != null)
            {
                creatureSolvedStates[target.GetInstanceID()] = target.IsSolved;
            }
        }

        revealStates.Clear();
        for (int index = 0; index < stage2RevealTargets.Length; index++)
        {
            StageSymbolNumberRevealTarget target = stage2RevealTargets[index];
            if (target != null)
            {
                revealStates[target.GetInstanceID()] = target.HasBeenRevealed;
            }
        }

        dialDigitStates.Clear();
        for (int index = 0; index < stage2Columns.Length; index++)
        {
            StageLightCodeDialColumn column = stage2Columns[index];
            if (column != null)
            {
                dialDigitStates[column.GetInstanceID()] = column.CurrentDigit;
            }
        }

        lastStage1Solved = stage1Puzzle != null && stage1Puzzle.IsSolved;
        lastStage2Solved = stage2CodeLock != null && stage2CodeLock.IsSolved;
        lastStage2CompletionPlaying = stage2CompletionSequence != null && stage2CompletionSequence.IsPlaying;
        lastStage3Green = stage3Puzzle != null && stage3Puzzle.GreenActivated;
        lastStage3Blue = stage3Puzzle != null && stage3Puzzle.BlueActivated;
        lastStage3Complete = stage3Puzzle != null && stage3Puzzle.IsComplete;
        lastStage4ActivationCount = stage4Sequence != null ? stage4Sequence.ActivationCount : 0;
        lastStage4MessageShownCount = stage4Sequence != null ? stage4Sequence.MessageShownCount : 0;
        lastStage4ReturnTriggeredCount = stage4Sequence != null ? stage4Sequence.ReturnTriggeredCount : 0;
    }

    private void ApplyAmbientForCurrentStage()
    {
        if (ambientSource == null || stageSequenceController == null)
        {
            return;
        }

        AudioClip ambientClip = GetAmbientClip(stageSequenceController.CurrentStageIndex);
        ambientSource.volume = ambientVolume;

        if (ambientClip == null)
        {
            ambientSource.Stop();
            ambientSource.clip = null;
            return;
        }

        if (ambientSource.clip != ambientClip)
        {
            ambientSource.clip = ambientClip;
        }

        if (!ambientSource.isPlaying)
        {
            ambientSource.Play();
        }
    }

    private AudioClip GetAmbientClip(int stageIndex)
    {
        switch (stageIndex)
        {
            case 0:
                return stage1Ambient;
            case 1:
                return stage2Ambient;
            case 2:
            case 3:
                return stage3Ambient;
            default:
                return null;
        }
    }

    private void PollStage1Audio(bool isActiveStage)
    {
        if (!isActiveStage)
        {
            return;
        }

        for (int index = 0; index < stage1Targets.Length; index++)
        {
            StageLightCreatureTarget target = stage1Targets[index];
            if (target == null)
            {
                continue;
            }

            int id = target.GetInstanceID();
            bool previousSolved = creatureSolvedStates.TryGetValue(id, out bool cachedSolved) && cachedSolved;
            if (!previousSolved && target.IsSolved)
            {
                PlayStage1ReactionClip(target);
            }

            creatureSolvedStates[id] = target.IsSolved;
        }

        bool stageSolved = stage1Puzzle != null && stage1Puzzle.IsSolved;
        if (!lastStage1Solved && stageSolved)
        {
            PlayOneShot(commonSuccess, 1f);
        }
        lastStage1Solved = stageSolved;
    }

    private void PlayStage1ReactionClip(StageLightCreatureTarget target)
    {
        AudioClip clip = target.CurrentReactionMode == StageLightCreatureTarget.ReactionMode.Hide
            ? (stage1LeafMove != null ? stage1LeafMove : stage1SoilMove)
            : stage1AnimalMove;
        PlayOneShot(clip, 0.95f);
    }

    private void PollStage2Audio(bool isActiveStage)
    {
        for (int index = 0; index < stage2RevealTargets.Length; index++)
        {
            StageSymbolNumberRevealTarget target = stage2RevealTargets[index];
            if (target == null)
            {
                continue;
            }

            int id = target.GetInstanceID();
            bool previousRevealed = revealStates.TryGetValue(id, out bool cachedReveal) && cachedReveal;
            if (isActiveStage && !previousRevealed && target.HasBeenRevealed)
            {
                PlayNextWaterDrop();
            }

            revealStates[id] = target.HasBeenRevealed;
        }

        for (int index = 0; index < stage2Columns.Length; index++)
        {
            StageLightCodeDialColumn column = stage2Columns[index];
            if (column == null)
            {
                continue;
            }

            int id = column.GetInstanceID();
            int previousDigit = dialDigitStates.TryGetValue(id, out int cachedDigit) ? cachedDigit : column.CurrentDigit;
            if (isActiveStage && previousDigit != column.CurrentDigit)
            {
                PlayNextWaterDrop();
            }

            dialDigitStates[id] = column.CurrentDigit;
        }

        bool codeSolved = stage2CodeLock != null && stage2CodeLock.IsSolved;
        if (isActiveStage && !lastStage2Solved && codeSolved)
        {
            PlayOneShot(commonSuccess, 1f);
        }
        lastStage2Solved = codeSolved;

        bool completionPlaying = stage2CompletionSequence != null && stage2CompletionSequence.IsPlaying;
        if (isActiveStage && !lastStage2CompletionPlaying && completionPlaying)
        {
            PlayOneShot(stage2Explosion, 1f);
            PlayOneShot(stage2Destroy, 0.92f);
            PlayOneShot(stage2DestroySecondary, 0.88f);
        }
        lastStage2CompletionPlaying = completionPlaying;
    }

    private void PlayNextWaterDrop()
    {
        if (stage2WaterDrops == null || stage2WaterDrops.Length == 0)
        {
            return;
        }

        AudioClip clip = stage2WaterDrops[waterDropIndex % stage2WaterDrops.Length];
        waterDropIndex++;
        PlayOneShot(clip, 0.9f);
    }

    private void PollHintPuzzleAudio(Stage3RockHintPuzzle puzzle, int stageIndex, bool isActiveStage, ref bool lastGreenState, ref bool lastBlueState, ref bool lastCompleteState)
    {
        if (puzzle == null)
        {
            return;
        }

        bool green = puzzle.GreenActivated;
        bool blue = puzzle.BlueActivated;
        bool complete = puzzle.IsComplete;

        if (isActiveStage && !lastGreenState && green)
        {
            PlayGlowClip(0);
        }

        if (isActiveStage && !lastBlueState && blue)
        {
            PlayGlowClip(1);
        }

        if (isActiveStage && !lastCompleteState && complete)
        {
            PlayGlowClip(2);
            PlayOneShot(stage3StoneFall, 0.9f);
            if (stageIndex == 2)
            {
                PlayOneShot(commonSuccess, 0.95f);
            }
        }

        lastGreenState = green;
        lastBlueState = blue;
        lastCompleteState = complete;
    }

    private void PlayGlowClip(int index)
    {
        if (stage3GlowClips == null || index < 0 || index >= stage3GlowClips.Length)
        {
            return;
        }

        PlayOneShot(stage3GlowClips[index], 0.95f);
    }

    private void PollStage4Audio(bool isActiveStage)
    {
        if (stage4Sequence == null)
        {
            return;
        }

        if (isActiveStage && stage4Sequence.ActivationCount != lastStage4ActivationCount)
        {
            lastStage4ActivationCount = stage4Sequence.ActivationCount;
            PlayOneShot(stage3RockRise, 0.82f);
        }

        if (stage4Sequence.MessageShownCount != lastStage4MessageShownCount)
        {
            lastStage4MessageShownCount = stage4Sequence.MessageShownCount;
            PlayOneShot(commonSuccess, 1f);
        }

        if (stage4Sequence.ReturnTriggeredCount != lastStage4ReturnTriggeredCount)
        {
            lastStage4ReturnTriggeredCount = stage4Sequence.ReturnTriggeredCount;
            PlayOneShot(stage3StoneFall, 0.72f);
        }
    }

    private void PlayOneShot(AudioClip clip, float volumeScale)
    {
        if (clip == null || oneShotSource == null)
        {
            return;
        }

        oneShotSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }

#if UNITY_EDITOR
    public void AutoAssignClips()
    {
        stage1Ambient = stage1Ambient != null ? stage1Ambient : LoadClip(Stage1AmbientPath);
        stage2Ambient = stage2Ambient != null ? stage2Ambient : LoadClip(Stage2AmbientPath);
        stage3Ambient = stage3Ambient != null ? stage3Ambient : LoadClip(Stage3AmbientPath);
        commonSuccess = commonSuccess != null ? commonSuccess : LoadClip(CommonSuccessPath);
        stage1AnimalMove = stage1AnimalMove != null ? stage1AnimalMove : LoadClip(Stage1AnimalMovePath);
        stage1LeafMove = stage1LeafMove != null ? stage1LeafMove : LoadClip(Stage1LeafMovePath);
        stage1SoilMove = stage1SoilMove != null ? stage1SoilMove : LoadClip(Stage1SoilMovePath);
        stage2Destroy = stage2Destroy != null ? stage2Destroy : LoadClip(Stage2DestroyPath);
        stage2DestroySecondary = stage2DestroySecondary != null ? stage2DestroySecondary : LoadClip(Stage2DestroyPath2);
        stage2Explosion = stage2Explosion != null ? stage2Explosion : LoadClip(Stage2ExplosionPath);
        stage3RockRise = stage3RockRise != null ? stage3RockRise : LoadClip(Stage3RockRisePath);
        stage3StoneFall = stage3StoneFall != null ? stage3StoneFall : LoadClip(Stage3StoneFallPath);

        if (stage2WaterDrops == null || stage2WaterDrops.Length == 0)
        {
            stage2WaterDrops = new[]
            {
                LoadClip(Stage2WaterDrop1Path),
                LoadClip(Stage2WaterDrop2Path),
                LoadClip(Stage2WaterDrop3Path),
                LoadClip(Stage2WaterDrop4Path)
            };
        }

        if (stage3GlowClips == null || stage3GlowClips.Length == 0)
        {
            stage3GlowClips = new[]
            {
                LoadClip(Stage3Glow1Path),
                LoadClip(Stage3Glow2Path),
                LoadClip(Stage3Glow3Path)
            };
        }
    }

    private static AudioClip LoadClip(string assetPath)
    {
        return AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
    }
#endif
}
