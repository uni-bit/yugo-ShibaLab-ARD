using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stage 2 完了時に再生される演出シーケンスコンポーネント。
/// <para>
/// <see cref="Play"/> を呼ぶと以下のフェーズを順次実行する:<br/>
/// Waiting → DelayBeforeCollapse → Collapsing（パネル崩落）→ DelayAfterCollapse<br/>
/// → ExpandingLight（ライト拡張・全体明転）→ DelayAfterLight → SelfMove（stage root 自走）→ Complete
/// </para>
/// <para>
/// Complete フェーズで <see cref="StageSequenceController.FadeToStage"/> を呼び Stage 3 へ遷移する。<br/>
/// <c>advanceToStage3OnComplete = false</c> にすると遷移をスキップできる。
/// </para>
/// </summary>
[AddComponentMenu("Stages/Stage 2 Completion Sequence")]
public class Stage2CompletionSequence : MonoBehaviour, IStageActivationHandler
{
    private enum SequencePhase
    {
        Waiting,
        DelayBeforeCollapse,
        Collapsing,
        DelayAfterCollapse,
        ExpandingLight,
        DelayAfterLight,
        SelfMove,
        Complete
    }

    private sealed class CollapsePiece
    {
        public Transform Transform;
        public Vector3 Velocity;
        public Vector3 AngularVelocity;
        public Vector3 BaseScale;
    }

    private sealed class RendererState
    {
        public Renderer Renderer;
        public MaterialPropertyBlock PropertyBlock;
        public Color BaseColor = Color.white;
        public bool HasBaseColor;
        public Color AlbedoColor = Color.white;
        public bool HasAlbedoColor;
        public Color LitColor = Color.white;
        public bool HasLitColor;
        public Color HiddenColor = new Color(1f, 1f, 1f, 0f);
        public bool HasHiddenColor;
        public Color EmissionColor = Color.black;
        public bool HasEmissionColor;
    }

    [SerializeField] private Transform collapseRoot;
    [SerializeField] private Transform[] collapseTargets = new Transform[0];
    [SerializeField] private Transform panelTransform;
    [SerializeField] private Transform stageRoot;
    [SerializeField] private float solvedPauseSeconds = 3.25f;
    [SerializeField] private float lightExpandDuration = 0.75f;
    [SerializeField] private float collapseDuration = 3.1f;
    [SerializeField] private Vector2 collapseHorizontalSpeedRange = new Vector2(0.9f, 2.6f);
    [SerializeField] private Vector2 collapseVerticalSpeedRange = new Vector2(0.8f, 2.1f);
    [SerializeField] private float collapseGravity = 4.8f;
    [SerializeField] private float collapseRotationSpeed = 240f;
    [SerializeField] private int collapseColumns = 4;
    [SerializeField] private int collapseRows = 3;
    [SerializeField] private float brightenDuration = 1.1f;
    [SerializeField] private float delayAfterCollapseSeconds = 1.8f;
    [SerializeField] private float delayAfterLightSeconds = 2f;
    [SerializeField] private float selfMoveDuration = 3.6f;
    [SerializeField] private float selfMoveDistance = 6f;
    [SerializeField] private float selfMoveHeight = 0f;
    [SerializeField] private bool keepCollapsedDebrisUntilTransition = true;
    [SerializeField] private Color finalBrightnessColor = new Color(1f, 0.96f, 0.84f, 1f);
    [SerializeField] private float emissionBoost = 2.4f;
    [SerializeField] private float finalLightIntensity = 6f;
    [SerializeField] private float finalLightRange = 34f;
    [SerializeField] private float hiddenBrightenAlpha = 0.92f;
    [SerializeField] private float expandedSpotAngle = 135f;
    [SerializeField] private float expandedSpotRange = 110f;
    [SerializeField] private float expandedSpotIntensity = 28f;
    [SerializeField] private Color ambientTargetColor = new Color(0.85f, 0.8f, 0.68f, 1f);
    [SerializeField] private float moveEndDarkenStartNormalized = 0.72f;
    [SerializeField] private bool disableLightsDuringSelfMove = true;
    [SerializeField] private bool advanceToStage3OnComplete = true;
    [SerializeField] private int nextStageIndex = 2;

    private readonly List<CollapsePiece> collapsePieces = new List<CollapsePiece>();
    private readonly List<RendererState> rendererStates = new List<RendererState>();

    private SequencePhase currentPhase = SequencePhase.Waiting;
    private float phaseElapsed;
    private Vector3 selfMoveStartPosition;
    private Vector3 selfMoveTargetPosition;
    private Vector3 travelDirection = Vector3.forward;
    private Light finalGlowLight;
    private Renderer panelRenderer;
    private Collider panelCollider;
    private Light activeSpotLight;
    private float initialSpotAngle;
    private float initialSpotRange;
    private float initialSpotIntensity;
    private Color initialAmbientColor;
    private Transform[] configuredCollapseTargets = new Transform[0];
    private StageLightCodeLockPuzzle codeLockPuzzle;
    private Vector3 initialStageRootLocalPosition;
    private Quaternion initialStageRootLocalRotation;
    private bool hasInitialStageRootTransform;

    public bool IsPlaying => currentPhase != SequencePhase.Waiting && currentPhase != SequencePhase.Complete;
    public bool IsCollapsing => currentPhase == SequencePhase.Collapsing;
    public bool IsComplete => currentPhase == SequencePhase.Complete;

    private void Awake()
    {
        if (stageRoot == null)
        {
            stageRoot = transform;
        }

        CaptureInitialStageRootTransform();
    }

    private void OnEnable()
    {
        ResetSequenceState();
    }

    public void ResetRuntimeState()
    {
        ResetSequenceState();
    }

    public void OnStageActivated()
    {
        ResetSequenceState();
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        switch (currentPhase)
        {
            case SequencePhase.DelayBeforeCollapse:
                UpdateDelayBeforeCollapse();
                break;

            case SequencePhase.Collapsing:
                UpdateCollapse();
                break;

            case SequencePhase.DelayAfterCollapse:
                UpdateDelayAfterCollapse();
                break;

            case SequencePhase.ExpandingLight:
                UpdateExpandingLight();
                break;

            case SequencePhase.DelayAfterLight:
                UpdateDelayAfterLight();
                break;

            case SequencePhase.SelfMove:
                UpdateSelfMove();
                break;
        }
    }

    public void Configure(
        Transform collapseRootReference,
        Transform[] collapseTargetsReference,
        Transform panelReference,
        Transform stageRootReference)
    {
        collapseRoot = collapseRootReference;
        configuredCollapseTargets = collapseTargetsReference ?? new Transform[0];
        panelTransform = panelReference;
        stageRoot = stageRootReference != null ? stageRootReference : transform;
        panelRenderer = panelTransform != null ? panelTransform.GetComponent<Renderer>() : null;
        panelCollider = panelTransform != null ? panelTransform.GetComponent<Collider>() : null;
        hasInitialStageRootTransform = false;
        CaptureInitialStageRootTransform();
        ResetSequenceState();
    }

    public void ConfigureTransition(bool advanceOnComplete, int targetStageIndex)
    {
        advanceToStage3OnComplete = advanceOnComplete;
        nextStageIndex = Mathf.Max(0, targetStageIndex);
    }

    public void Play()
    {
        if (currentPhase != SequencePhase.Waiting)
        {
            return;
        }

        BeginDelayBeforeCollapse();
    }

    private void ResetSequenceState()
    {
        CaptureInitialStageRootTransform();
        RestoreInitialStageRootTransform();
        RestoreCollapsedVisualState();
        RestoreCapturedRendererStates();

        currentPhase = SequencePhase.Waiting;
        phaseElapsed = 0f;
        collapsePieces.Clear();
        rendererStates.Clear();
        PoseTestBootstrap bootstrap = FindFirstObjectByType<PoseTestBootstrap>();
        activeSpotLight = bootstrap != null ? bootstrap.ActiveSpotLight : null;
        if (activeSpotLight != null)
        {
            initialSpotAngle = activeSpotLight.spotAngle;
            initialSpotRange = activeSpotLight.range;
            initialSpotIntensity = activeSpotLight.intensity;
        }

        initialAmbientColor = RenderSettings.ambientLight;

        if (panelRenderer != null)
        {
            panelRenderer.enabled = true;
        }

        if (panelCollider != null)
        {
            panelCollider.enabled = true;
        }

        if (finalGlowLight != null)
        {
            finalGlowLight.intensity = 0f;
            finalGlowLight.enabled = false;
        }
    }

    private void RestoreCollapsedVisualState()
    {
        for (int index = collapsePieces.Count - 1; index >= 0; index--)
        {
            CollapsePiece piece = collapsePieces[index];
            if (piece != null && piece.Transform != null)
            {
                Destroy(piece.Transform.gameObject);
            }
        }

        Transform cleanupRoot = stageRoot != null ? stageRoot : transform;
        if (cleanupRoot != null)
        {
            Transform[] descendants = cleanupRoot.GetComponentsInChildren<Transform>(true);
            for (int index = descendants.Length - 1; index >= 0; index--)
            {
                Transform descendant = descendants[index];
                if (descendant == null)
                {
                    continue;
                }

                if (descendant.name.Contains("Collapse Proxy") || descendant.name.StartsWith("Panel Shard "))
                {
                    Destroy(descendant.gameObject);
                }
            }
        }

        Transform[] targets = GetEffectiveCollapseTargets();
        for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
        {
            Transform target = targets[targetIndex];
            if (target == null)
            {
                continue;
            }

            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] != null)
                {
                    renderers[index].enabled = true;
                }
            }

            Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] != null)
                {
                    colliders[index].enabled = true;
                }
            }

            StageCodeFormulaDisplay[] formulaDisplays = target.GetComponentsInChildren<StageCodeFormulaDisplay>(true);
            for (int index = 0; index < formulaDisplays.Length; index++)
            {
                if (formulaDisplays[index] != null)
                {
                    formulaDisplays[index].enabled = true;
                }
            }
        }
    }

    private void CaptureInitialStageRootTransform()
    {
        if (stageRoot == null || hasInitialStageRootTransform)
        {
            return;
        }

        initialStageRootLocalPosition = stageRoot.localPosition;
        initialStageRootLocalRotation = stageRoot.localRotation;
        hasInitialStageRootTransform = true;
    }

    private void RestoreInitialStageRootTransform()
    {
        if (stageRoot == null || !hasInitialStageRootTransform)
        {
            return;
        }

        stageRoot.localPosition = initialStageRootLocalPosition;
        stageRoot.localRotation = initialStageRootLocalRotation;
    }

    private void BeginDelayBeforeCollapse()
    {
        currentPhase = SequencePhase.DelayBeforeCollapse;
        phaseElapsed = 0f;
        travelDirection = ResolveTravelDirection();
        CaptureRendererStates();
    }

    private void UpdateDelayBeforeCollapse()
    {
        phaseElapsed += Time.deltaTime;
        if (phaseElapsed >= solvedPauseSeconds)
        {
            BeginCollapse();
        }
    }

    private void BeginExpandingLight()
    {
        currentPhase = SequencePhase.ExpandingLight;
        phaseElapsed = 0f;
        EnsureFinalGlowLight();
        if (finalGlowLight != null)
        {
            finalGlowLight.enabled = true;
            finalGlowLight.intensity = 0f;
            finalGlowLight.range = finalLightRange;
        }
    }

    private void UpdateExpandingLight()
    {
        phaseElapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(phaseElapsed / Mathf.Max(0.0001f, lightExpandDuration));
        float eased = 1f - Mathf.Pow(1f - progress, 3f);

        ApplyBrightening(eased);
        UpdateExpandedLightState(eased);

        if (progress >= 1f)
        {
            BeginDelayAfterLight();
        }
    }

    private void BeginDelayAfterLight()
    {
        currentPhase = SequencePhase.DelayAfterLight;
        phaseElapsed = 0f;
        ApplyBrightening(1f);
        UpdateExpandedLightState(1f);
    }

    private void UpdateDelayAfterLight()
    {
        phaseElapsed += Time.deltaTime;
        ApplyBrightening(1f);
        UpdateExpandedLightState(1f);

        if (phaseElapsed >= delayAfterLightSeconds)
        {
            BeginSelfMove();
        }
    }

    private void BeginCollapse()
    {
        currentPhase = SequencePhase.Collapsing;
        phaseElapsed = 0f;
        ResolveCodeLockPuzzle();
        if (codeLockPuzzle != null)
        {
            codeLockPuzzle.DisableSolvedGlowForCollapse();
        }
        DisableCollapseTargetBehaviours();
        SpawnCollapsePieces();
    }

    private void ResolveCodeLockPuzzle()
    {
        if (codeLockPuzzle != null)
        {
            return;
        }

        codeLockPuzzle = GetComponent<StageLightCodeLockPuzzle>();
        if (codeLockPuzzle == null && stageRoot != null)
        {
            codeLockPuzzle = stageRoot.GetComponent<StageLightCodeLockPuzzle>();
        }
    }

    private void UpdateCollapse()
    {
        phaseElapsed += Time.deltaTime;

        for (int index = collapsePieces.Count - 1; index >= 0; index--)
        {
            CollapsePiece piece = collapsePieces[index];
            if (piece == null || piece.Transform == null)
            {
                collapsePieces.RemoveAt(index);
                continue;
            }

            piece.Velocity += Vector3.down * (collapseGravity * Time.deltaTime);
            piece.Transform.position += piece.Velocity * Time.deltaTime;
            piece.Transform.Rotate(piece.AngularVelocity * Time.deltaTime, Space.Self);

            float collapseProgress = Mathf.Clamp01(phaseElapsed / Mathf.Max(0.0001f, collapseDuration));
            float scaleMultiplier = Mathf.Lerp(1f, 0.35f, collapseProgress);
            piece.Transform.localScale = piece.BaseScale * scaleMultiplier;
        }

        if (phaseElapsed < collapseDuration)
        {
            return;
        }

        if (!keepCollapsedDebrisUntilTransition)
        {
            for (int index = 0; index < collapsePieces.Count; index++)
            {
                if (collapsePieces[index] != null && collapsePieces[index].Transform != null)
                {
                    Destroy(collapsePieces[index].Transform.gameObject);
                }
            }

            collapsePieces.Clear();
        }

        if (!keepCollapsedDebrisUntilTransition && collapseRoot != null)
        {
            Destroy(collapseRoot.gameObject);
        }

        BeginDelayAfterCollapse();
    }

    private void BeginDelayAfterCollapse()
    {
        currentPhase = SequencePhase.DelayAfterCollapse;
        phaseElapsed = 0f;
    }

    private void UpdateDelayAfterCollapse()
    {
        phaseElapsed += Time.deltaTime;

        if (phaseElapsed >= delayAfterCollapseSeconds)
        {
            BeginExpandingLight();
        }
    }

    private void BeginSelfMove()
    {
        currentPhase = SequencePhase.SelfMove;
        phaseElapsed = 0f;
        selfMoveStartPosition = stageRoot != null ? stageRoot.position : transform.position;
        selfMoveTargetPosition = selfMoveStartPosition - (travelDirection * selfMoveDistance) + (Vector3.up * selfMoveHeight);

        if (disableLightsDuringSelfMove)
        {
            SetMovementLightsEnabled(false);
        }
    }

    private void UpdateSelfMove()
    {
        phaseElapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(phaseElapsed / Mathf.Max(0.0001f, selfMoveDuration));
        float eased = Mathf.SmoothStep(0f, 1f, progress);
        float darkenProgress = Mathf.InverseLerp(moveEndDarkenStartNormalized, 1f, eased);
        float brightnessProgress = 1f - Mathf.SmoothStep(0f, 1f, darkenProgress);

        if (stageRoot != null)
        {
            stageRoot.position = Vector3.Lerp(selfMoveStartPosition, selfMoveTargetPosition, eased);
        }

        ApplyBrightening(brightnessProgress);
        UpdateExpandedLightState(brightnessProgress);

        if (progress < 1f)
        {
            return;
        }

        currentPhase = SequencePhase.Complete;

        if (disableLightsDuringSelfMove)
        {
            RestorePrimaryLightState();
        }

        TransitionToNextStage();
    }

    private void SetMovementLightsEnabled(bool enabled)
    {
        if (activeSpotLight != null)
        {
            activeSpotLight.enabled = enabled;
        }

        if (finalGlowLight != null)
        {
            finalGlowLight.enabled = enabled && finalGlowLight.intensity > 0.0001f;
        }
    }

    private void RestorePrimaryLightState()
    {
        if (activeSpotLight != null)
        {
            activeSpotLight.enabled = true;
            activeSpotLight.spotAngle = initialSpotAngle;
            activeSpotLight.range = initialSpotRange;
            activeSpotLight.intensity = initialSpotIntensity;
        }

        if (finalGlowLight != null)
        {
            finalGlowLight.intensity = 0f;
            finalGlowLight.enabled = false;
        }
    }

    private void TransitionToNextStage()
    {
        if (!advanceToStage3OnComplete)
        {
            return;
        }

        StageSequenceController sequenceController = FindFirstObjectByType<StageSequenceController>();
        if (sequenceController == null)
        {
            return;
        }

        // nextStageIndex が有効範囲内かチェック。範囲外の場合は遷移しない（誤設定による
        // 意図しないステージスキップを防ぐ）。
        if (nextStageIndex < 0 || nextStageIndex >= sequenceController.StageCount)
        {
            Debug.LogWarning(
                string.Format(
                    "[Stage2CompletionSequence] nextStageIndex={0} が有効範囲外 (0–{1}) のため遷移をスキップします。",
                    nextStageIndex,
                    sequenceController.StageCount - 1),
                this);
            return;
        }

        sequenceController.FadeToStage(nextStageIndex);
    }

    private Vector3 ResolveTravelDirection()
    {
        if (panelTransform != null)
        {
            Vector3 panelForward = panelTransform.forward;
            if (panelForward.sqrMagnitude > 0.0001f)
            {
                return panelForward.normalized;
            }
        }

        if (stageRoot != null)
        {
            Vector3 rootForward = stageRoot.forward;
            if (rootForward.sqrMagnitude > 0.0001f)
            {
                return rootForward.normalized;
            }
        }

        return Vector3.forward;
    }

    private void SpawnCollapsePieces()
    {
        collapsePieces.Clear();

        Transform[] targets = GetEffectiveCollapseTargets();
        if (targets.Length == 0)
        {
            return;
        }

        for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
        {
            Transform targetRoot = targets[targetIndex];
            if (targetRoot == null)
            {
                continue;
            }

            Renderer[] renderers = targetRoot.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer sourceRenderer = renderers[index];
                if (sourceRenderer == null)
                {
                    continue;
                }

                CreateCollapseProxy(sourceRenderer, targetRoot);

                sourceRenderer.enabled = false;

                Collider sourceCollider = sourceRenderer.GetComponent<Collider>();
                if (sourceCollider != null)
                {
                    sourceCollider.enabled = false;
                }
            }
        }

        if (panelRenderer != null)
        {
            panelRenderer.enabled = false;
        }

        if (panelCollider != null)
        {
            panelCollider.enabled = false;
        }

        SpawnPanelShards();
    }

    private void SpawnPanelShards()
    {
        if (panelTransform == null || panelRenderer == null)
        {
            return;
        }

        Vector3 panelScale = panelTransform.lossyScale;
        float pieceWidth = panelScale.x / Mathf.Max(1, collapseColumns);
        float pieceHeight = panelScale.y / Mathf.Max(1, collapseRows);
        float pieceDepth = Mathf.Max(0.05f, panelScale.z * 0.9f);

        Transform shardParent = stageRoot != null ? stageRoot : transform;
        Material shardMaterial = panelRenderer.sharedMaterial;
        Color shardColor = panelRenderer.sharedMaterial != null && panelRenderer.sharedMaterial.HasProperty("_BaseColor")
            ? panelRenderer.sharedMaterial.GetColor("_BaseColor")
            : panelRenderer.sharedMaterial != null && panelRenderer.sharedMaterial.HasProperty("_Color")
                ? panelRenderer.sharedMaterial.color
                : new Color(0.18f, 0.16f, 0.12f, 1f);

        for (int row = 0; row < Mathf.Max(1, collapseRows); row++)
        {
            for (int column = 0; column < Mathf.Max(1, collapseColumns); column++)
            {
                GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.name = "Panel Shard " + row + "-" + column;
                shard.transform.SetParent(shardParent, true);
                shard.transform.rotation = panelTransform.rotation;

                float horizontalOffset = ((column + 0.5f) / Mathf.Max(1, collapseColumns)) - 0.5f;
                float verticalOffset = ((row + 0.5f) / Mathf.Max(1, collapseRows)) - 0.5f;

                Vector3 localOffset = new Vector3(horizontalOffset * panelScale.x, verticalOffset * panelScale.y, 0f);
                shard.transform.position = panelTransform.TransformPoint(localOffset);
                shard.transform.localScale = new Vector3(pieceWidth * 0.94f, pieceHeight * 0.94f, pieceDepth);

                Renderer shardRenderer = shard.GetComponent<Renderer>();
                if (shardRenderer != null)
                {
                    if (shardMaterial != null)
                    {
                        shardRenderer.sharedMaterial = shardMaterial;
                    }

                    MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
                    shardRenderer.GetPropertyBlock(propertyBlock);
                    propertyBlock.SetColor("_Color", shardColor);
                    propertyBlock.SetColor("_BaseColor", shardColor);
                    shardRenderer.SetPropertyBlock(propertyBlock);
                }

                Collider shardCollider = shard.GetComponent<Collider>();
                if (shardCollider != null)
                {
                    Destroy(shardCollider);
                }

                Vector3 direction = (panelTransform.right * horizontalOffset * 1.35f)
                    + (panelTransform.up * (0.2f + (verticalOffset * 0.45f)))
                    + (panelTransform.forward * 0.7f);
                if (direction.sqrMagnitude <= 0.0001f)
                {
                    direction = panelTransform.forward;
                }

                direction.Normalize();

                CollapsePiece piece = new CollapsePiece
                {
                    Transform = shard.transform,
                    Velocity = direction * Random.Range(collapseHorizontalSpeedRange.x, collapseHorizontalSpeedRange.y)
                        + (panelTransform.up * Random.Range(collapseVerticalSpeedRange.x, collapseVerticalSpeedRange.y)),
                    AngularVelocity = new Vector3(
                        Random.Range(-collapseRotationSpeed, collapseRotationSpeed),
                        Random.Range(-collapseRotationSpeed, collapseRotationSpeed),
                        Random.Range(-collapseRotationSpeed, collapseRotationSpeed)),
                    BaseScale = shard.transform.localScale
                };

                collapsePieces.Add(piece);
            }
        }
    }

    private void CreateCollapseProxy(Renderer sourceRenderer, Transform targetRoot)
    {
        if (sourceRenderer == null)
        {
            return;
        }

        Bounds bounds = sourceRenderer.bounds;
        if (bounds.size.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        GameObject proxy = GameObject.CreatePrimitive(PrimitiveType.Cube);
        proxy.name = sourceRenderer.gameObject.name + " Collapse Proxy";

        Transform proxyTransform = proxy.transform;
        proxyTransform.SetParent(stageRoot != null ? stageRoot : transform, true);
        proxyTransform.position = bounds.center;
        proxyTransform.rotation = sourceRenderer.transform.rotation;
        proxyTransform.localScale = new Vector3(
            Mathf.Max(0.08f, bounds.size.x),
            Mathf.Max(0.08f, bounds.size.y),
            Mathf.Max(0.08f, bounds.size.z));

        Renderer proxyRenderer = proxy.GetComponent<Renderer>();
        if (proxyRenderer != null)
        {
            Material sourceMaterial = sourceRenderer.sharedMaterial;
            if (sourceMaterial != null)
            {
                proxyRenderer.sharedMaterial = sourceMaterial;
            }

            Color proxyColor = ResolveRendererColor(sourceRenderer);
            MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
            proxyRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_Color", proxyColor);
            propertyBlock.SetColor("_BaseColor", proxyColor);
            propertyBlock.SetColor("_EmissionColor", proxyColor * 0.2f);
            proxyRenderer.SetPropertyBlock(propertyBlock);
        }

        Collider proxyCollider = proxy.GetComponent<Collider>();
        if (proxyCollider != null)
        {
            Destroy(proxyCollider);
        }

        Transform directionBasis = targetRoot != null ? targetRoot : collapseRoot;
        if (directionBasis == null)
        {
            directionBasis = sourceRenderer.transform;
        }

        Vector3 centerOffset = bounds.center - directionBasis.position;
        Vector3 direction = directionBasis.forward * 0.85f;
        direction += directionBasis.right * Mathf.Sign(centerOffset.x) * Mathf.Lerp(0.25f, 1.2f, Mathf.Clamp01(Mathf.Abs(centerOffset.x) / 3f));
        direction += directionBasis.up * Mathf.Lerp(0.15f, 0.75f, Mathf.Clamp01((centerOffset.y + 2f) / 4f));

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = directionBasis.forward;
        }

        direction.Normalize();

        CollapsePiece piece = new CollapsePiece
        {
            Transform = proxyTransform,
            Velocity = direction * Random.Range(collapseHorizontalSpeedRange.x * 0.7f, collapseHorizontalSpeedRange.y * 1.15f)
                + (directionBasis.up * Random.Range(collapseVerticalSpeedRange.x * 0.8f, collapseVerticalSpeedRange.y * 1.2f)),
            AngularVelocity = new Vector3(
                Random.Range(-collapseRotationSpeed, collapseRotationSpeed),
                Random.Range(-collapseRotationSpeed, collapseRotationSpeed),
                Random.Range(-collapseRotationSpeed, collapseRotationSpeed)),
            BaseScale = proxyTransform.localScale
        };

        collapsePieces.Add(piece);
    }

    private Transform[] GetEffectiveCollapseTargets()
    {
        List<Transform> validTargets = new List<Transform>();
        AddResolvedCollapseTargets(validTargets, collapseTargets);
        AddResolvedCollapseTargets(validTargets, configuredCollapseTargets);

        if (validTargets.Count > 0)
        {
            return validTargets.ToArray();
        }

        return collapseRoot != null ? new[] { collapseRoot } : new Transform[0];
    }

    private void AddResolvedCollapseTargets(List<Transform> destination, Transform[] sourceTargets)
    {
        if (destination == null || sourceTargets == null)
        {
            return;
        }

        for (int index = 0; index < sourceTargets.Length; index++)
        {
            Transform[] resolvedTargets = ResolveCollapseTargetInstances(sourceTargets[index]);
            for (int resolvedIndex = 0; resolvedIndex < resolvedTargets.Length; resolvedIndex++)
            {
                Transform resolvedTarget = resolvedTargets[resolvedIndex];
                if (resolvedTarget != null && !destination.Contains(resolvedTarget))
                {
                    destination.Add(resolvedTarget);
                }
            }
        }
    }

    private Transform[] ResolveCollapseTargetInstances(Transform target)
    {
        if (target == null)
        {
            return new Transform[0];
        }

        if (target.gameObject.scene.IsValid())
        {
            return new[] { target };
        }

        List<string> relativePath = BuildRelativePathFromAssetRoot(target);
        if (relativePath.Count == 0)
        {
            return new Transform[0];
        }

        List<Transform> matches = new List<Transform>();
        TryResolvePathUnderRoot(matches, collapseRoot, relativePath);
        TryResolvePathUnderRoot(matches, stageRoot, relativePath);
        return matches.ToArray();
    }

    private static List<string> BuildRelativePathFromAssetRoot(Transform target)
    {
        List<string> path = new List<string>();
        Transform current = target;
        while (current != null)
        {
            path.Insert(0, current.name);
            current = current.parent;
        }

        return path;
    }

    private static void TryResolvePathUnderRoot(List<Transform> matches, Transform searchRoot, List<string> relativePath)
    {
        if (matches == null || searchRoot == null || relativePath == null || relativePath.Count == 0)
        {
            return;
        }

        Transform[] descendants = searchRoot.GetComponentsInChildren<Transform>(true);
        for (int index = 0; index < descendants.Length; index++)
        {
            Transform candidate = descendants[index];
            if (candidate == null || candidate.name != relativePath[0])
            {
                continue;
            }

            Transform resolved = TryFindDescendantByPath(candidate, relativePath, 1);
            if (resolved != null && !matches.Contains(resolved))
            {
                matches.Add(resolved);
            }
        }
    }

    private static Transform TryFindDescendantByPath(Transform current, List<string> path, int pathIndex)
    {
        if (current == null)
        {
            return null;
        }

        if (pathIndex >= path.Count)
        {
            return current;
        }

        for (int childIndex = 0; childIndex < current.childCount; childIndex++)
        {
            Transform child = current.GetChild(childIndex);
            if (child != null && child.name == path[pathIndex])
            {
                return TryFindDescendantByPath(child, path, pathIndex + 1);
            }
        }

        return null;
    }

    private void DisableCollapseTargetBehaviours()
    {
        Transform[] targets = GetEffectiveCollapseTargets();
        for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
        {
            Transform target = targets[targetIndex];
            if (target == null)
            {
                continue;
            }

            StageCodeFormulaDisplay[] formulaDisplays = target.GetComponentsInChildren<StageCodeFormulaDisplay>(true);
            for (int index = 0; index < formulaDisplays.Length; index++)
            {
                if (formulaDisplays[index] != null)
                {
                    formulaDisplays[index].enabled = false;
                }
            }
        }
    }

    private static Color ResolveRendererColor(Renderer sourceRenderer)
    {
        if (sourceRenderer == null || sourceRenderer.sharedMaterial == null)
        {
            return new Color(0.85f, 0.85f, 0.85f, 1f);
        }

        Material sourceMaterial = sourceRenderer.sharedMaterial;
        if (sourceMaterial.HasProperty("_LitColor"))
        {
            return sourceMaterial.GetColor("_LitColor");
        }

        if (sourceMaterial.HasProperty("_BaseColor"))
        {
            return sourceMaterial.GetColor("_BaseColor");
        }

        if (sourceMaterial.HasProperty("_Color"))
        {
            return sourceMaterial.color;
        }

        return new Color(0.85f, 0.85f, 0.85f, 1f);
    }

    private void CaptureRendererStates()
    {
        rendererStates.Clear();

        Transform searchRoot = stageRoot != null ? stageRoot : transform;
        Renderer[] renderers = searchRoot.GetComponentsInChildren<Renderer>(true);
        for (int index = 0; index < renderers.Length; index++)
        {
            Renderer renderer = renderers[index];
            if (renderer == null)
            {
                continue;
            }

            Material sharedMaterial = renderer.sharedMaterial;
            RendererState state = new RendererState
            {
                Renderer = renderer,
                PropertyBlock = new MaterialPropertyBlock()
            };

            if (sharedMaterial != null)
            {
                if (sharedMaterial.HasProperty("_BaseColor"))
                {
                    state.BaseColor = sharedMaterial.GetColor("_BaseColor");
                    state.HasBaseColor = true;
                }

                if (sharedMaterial.HasProperty("_Color"))
                {
                    state.AlbedoColor = sharedMaterial.color;
                    state.HasAlbedoColor = true;
                }

                if (sharedMaterial.HasProperty("_LitColor"))
                {
                    state.LitColor = sharedMaterial.GetColor("_LitColor");
                    state.HasLitColor = true;
                }

                if (sharedMaterial.HasProperty("_HiddenColor"))
                {
                    state.HiddenColor = sharedMaterial.GetColor("_HiddenColor");
                    state.HasHiddenColor = true;
                }

                if (sharedMaterial.HasProperty("_EmissionColor"))
                {
                    state.EmissionColor = sharedMaterial.GetColor("_EmissionColor");
                    state.HasEmissionColor = true;
                }
            }

            rendererStates.Add(state);
        }
    }

    private void RestoreCapturedRendererStates()
    {
        for (int index = 0; index < rendererStates.Count; index++)
        {
            RendererState state = rendererStates[index];
            if (state == null || state.Renderer == null)
            {
                continue;
            }

            state.Renderer.GetPropertyBlock(state.PropertyBlock);

            if (state.HasBaseColor)
            {
                state.PropertyBlock.SetColor("_BaseColor", state.BaseColor);
            }

            if (state.HasAlbedoColor)
            {
                state.PropertyBlock.SetColor("_Color", state.AlbedoColor);
            }

            if (state.HasLitColor)
            {
                state.PropertyBlock.SetColor("_LitColor", state.LitColor);
            }

            if (state.HasHiddenColor)
            {
                state.PropertyBlock.SetColor("_HiddenColor", state.HiddenColor);
            }

            if (state.HasEmissionColor)
            {
                state.PropertyBlock.SetColor("_EmissionColor", state.EmissionColor);
            }

            state.Renderer.SetPropertyBlock(state.PropertyBlock);

            Material sharedMaterial = state.Renderer.sharedMaterial;
            if (sharedMaterial == null)
            {
                continue;
            }

            if (state.HasBaseColor)
            {
                sharedMaterial.SetColor("_BaseColor", state.BaseColor);
            }

            if (state.HasAlbedoColor)
            {
                sharedMaterial.color = state.AlbedoColor;
            }

            if (state.HasLitColor)
            {
                sharedMaterial.SetColor("_LitColor", state.LitColor);
            }

            if (state.HasHiddenColor)
            {
                sharedMaterial.SetColor("_HiddenColor", state.HiddenColor);
            }

            if (state.HasEmissionColor)
            {
                sharedMaterial.SetColor("_EmissionColor", state.EmissionColor);
                if (state.EmissionColor.maxColorComponent > 0.0001f)
                {
                    sharedMaterial.EnableKeyword("_EMISSION");
                }
                else
                {
                    sharedMaterial.DisableKeyword("_EMISSION");
                }
            }
        }
    }

    private void ApplyBrightening(float progress)
    {
        Color revealedHiddenColor = new Color(finalBrightnessColor.r, finalBrightnessColor.g, finalBrightnessColor.b, hiddenBrightenAlpha);
        RenderSettings.ambientLight = Color.Lerp(initialAmbientColor, ambientTargetColor, progress);

        for (int index = 0; index < rendererStates.Count; index++)
        {
            RendererState state = rendererStates[index];
            if (state == null || state.Renderer == null)
            {
                continue;
            }

            state.Renderer.GetPropertyBlock(state.PropertyBlock);

            if (state.HasBaseColor)
            {
                state.PropertyBlock.SetColor("_BaseColor", Color.Lerp(state.BaseColor, finalBrightnessColor, progress));
            }

            if (state.HasAlbedoColor)
            {
                state.PropertyBlock.SetColor("_Color", Color.Lerp(state.AlbedoColor, finalBrightnessColor, progress));
            }

            if (state.HasLitColor)
            {
                state.PropertyBlock.SetColor("_LitColor", Color.Lerp(state.LitColor, finalBrightnessColor, progress));
            }

            if (state.HasHiddenColor)
            {
                state.PropertyBlock.SetColor("_HiddenColor", Color.Lerp(state.HiddenColor, revealedHiddenColor, progress));
            }

            if (state.HasEmissionColor)
            {
                state.PropertyBlock.SetColor("_EmissionColor", finalBrightnessColor * (progress * emissionBoost));
            }

            state.Renderer.SetPropertyBlock(state.PropertyBlock);

            Material sharedMaterial = state.Renderer.sharedMaterial;
            if (sharedMaterial == null)
            {
                continue;
            }

            if (state.HasBaseColor)
            {
                sharedMaterial.SetColor("_BaseColor", Color.Lerp(state.BaseColor, finalBrightnessColor, progress));
            }

            if (state.HasAlbedoColor)
            {
                sharedMaterial.color = Color.Lerp(state.AlbedoColor, finalBrightnessColor, progress);
            }

            if (state.HasLitColor)
            {
                sharedMaterial.SetColor("_LitColor", Color.Lerp(state.LitColor, finalBrightnessColor, progress));
            }

            if (state.HasHiddenColor)
            {
                sharedMaterial.SetColor("_HiddenColor", Color.Lerp(state.HiddenColor, revealedHiddenColor, progress));
            }

            if (state.HasEmissionColor)
            {
                sharedMaterial.SetColor("_EmissionColor", finalBrightnessColor * (progress * emissionBoost));
                sharedMaterial.EnableKeyword("_EMISSION");
            }
        }
    }

    private void UpdateExpandedLightState(float progress)
    {
        if (activeSpotLight != null)
        {
            activeSpotLight.spotAngle = Mathf.Lerp(initialSpotAngle, expandedSpotAngle, progress);
            activeSpotLight.range = Mathf.Lerp(initialSpotRange, expandedSpotRange, progress);
            activeSpotLight.intensity = Mathf.Lerp(initialSpotIntensity, expandedSpotIntensity, progress);
        }

        if (finalGlowLight != null)
        {
            finalGlowLight.intensity = Mathf.Lerp(0f, finalLightIntensity, progress);
        }
    }

    private void EnsureFinalGlowLight()
    {
        if (stageRoot == null)
        {
            stageRoot = transform;
        }

        if (finalGlowLight != null)
        {
            return;
        }

        Transform glowTransform = stageRoot.Find("Final Glow Light");
        if (glowTransform == null)
        {
            glowTransform = new GameObject("Final Glow Light").transform;
            glowTransform.SetParent(stageRoot, false);
        }

        glowTransform.localPosition = new Vector3(0f, 2.8f, 5.5f);
        glowTransform.localRotation = Quaternion.identity;

        finalGlowLight = glowTransform.GetComponent<Light>();
        if (finalGlowLight == null)
        {
            finalGlowLight = glowTransform.gameObject.AddComponent<Light>();
        }

        finalGlowLight.type = LightType.Point;
        finalGlowLight.color = finalBrightnessColor;
        finalGlowLight.range = finalLightRange;
        finalGlowLight.intensity = 0f;
        finalGlowLight.enabled = false;
    }
}