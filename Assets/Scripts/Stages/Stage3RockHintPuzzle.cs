using UnityEngine;

/// <summary>
/// Stage 3 / Stage 4 のカラーヒントロックパズルコンポーネント（両ステージ共通）。
/// <para>
/// 緑ヒント岩と青ヒント岩を、それぞれ <c>hintHoldSeconds</c> 秒間スポットライトの中心ゾーンで照射すると<br/>
/// 該当色の台座岩が発光し、両方活性化するとフィナーレに移行する。
/// </para>
/// <para>
/// フィナーレフェーズ: LocalReveal → PanoramaReveal（全体明転）→ HoldBeforeTransition → Complete<br/>
/// Complete 時、<c>advanceToNextStageOnComplete = true</c> なら <see cref="StageSequenceController.FadeToStage"/> で次ステージへ遷移する。
/// </para>
/// <para>
/// Stage 3 は次ステージあり（Stage 4 へ）、Stage 4 は最終ステージ（遷移なし）として使用されている。
/// </para>
/// </summary>
[AddComponentMenu("Stages/Stage 3 Rock Hint Puzzle")]
public class Stage3RockHintPuzzle : MonoBehaviour
{
    private const string EmissiveShellName = "Stage3 Emissive Shell";
    private const string DefaultBurstShaderName = "Sprites/Default";



    private enum FinalePhase
    {
        None,
        LocalReveal,
        PanoramaReveal,
        HoldBeforeTransition,
        Complete
    }

    [SerializeField] private Transform redPedestalRock;
    [SerializeField] private Transform greenPedestalRock;
    [SerializeField] private Transform bluePedestalRock;
    [SerializeField] private Transform greenHintRock;
    [SerializeField] private Transform blueHintRock;
    [SerializeField] private Color redGlowColor = new Color(1f, 0.28f, 0.22f, 1f);
    [SerializeField] private Color greenGlowColor = new Color(0.28f, 0.95f, 0.42f, 1f);
    [SerializeField] private Color blueGlowColor = new Color(0.24f, 0.62f, 1f, 1f);
    [SerializeField] private float inactiveColorMultiplier = 0.28f;
    [SerializeField] private float pedestalEmissionIntensity = 1.9f;
    [SerializeField] private float hintRockEmissionIntensity = 2.6f;
    [SerializeField] private float redPedestalEmissionIntensity = 2.2f;
    [SerializeField] private float inactiveEmissionIntensity = 0f;
    [SerializeField] private float pedestalGlowLightIntensity = 0.2f;
    [SerializeField] private float hintGlowLightIntensity = 0.26f;
    [SerializeField] private float redPedestalGlowLightIntensity = 0.22f;
    [SerializeField] private float glowLightRange = 1.8f;
    [SerializeField] private float emissiveShellScale = 1.07f;
    [SerializeField] private float emissiveShellAlpha = 0.48f;
    [SerializeField] private float maxRockEmissionIntensity = 2.7f;
    [SerializeField] private float hintHoldSeconds = 2f;
    [SerializeField] private float hintActivationExposureThreshold = 0.045f;
    [SerializeField, Range(0.05f, 1f)] private float hintCenterZoneNormalizedRadius = 0.35f;
    [SerializeField] private bool requireHintCenterZone = false;
    [SerializeField] private float localRevealDuration = 2f;
    [SerializeField] private float brightenDuration = 1.1f;
    [SerializeField] private float holdBeforeStageTransitionSeconds = 1f;
    [SerializeField] private Color finalBrightnessColor = new Color(1f, 0.96f, 0.84f, 1f);
    [SerializeField] private float emissionBoost = 0f;
    [SerializeField] private float hiddenBrightenAlpha = 0.92f;
    [SerializeField] private Color ambientTargetColor = new Color(0.22f, 0.22f, 0.24f, 1f);
    [SerializeField] private float localRevealLightIntensity = 1.8f;
    [SerializeField] private float localRevealLightRange = 2.8f;
    [SerializeField] private float finalLightIntensity = 1.2f;
    [SerializeField] private float finalLightRange = 12f;
    [SerializeField] private float revealLightHeightOffset = 0.35f;
    [SerializeField] private bool advanceToNextStageOnComplete = true;
    [SerializeField] private int nextStageIndex = 3;
    [SerializeField] private bool useBrightTransitionToNextStage = false;
    [SerializeField] private int burstParticleCount = 22;
    [SerializeField] private float burstSpeed = 2.8f;
    [SerializeField] private float burstLifetime = 0.85f;
    [Header("Looping Lift")]
    [SerializeField] private Transform[] loopingLiftTargets = new Transform[0];
    [SerializeField] private float loopingLiftMinY = 0f;
    [SerializeField] private float loopingLiftMaxY = 2f;
    [SerializeField] private float loopingLiftUpSpeed = 0.5f;
    [Header("Obstacle Rocks")]
    [SerializeField] private Transform[] obstacleRocks = new Transform[0];
    [SerializeField] private float obstacleRockResetY = 0f;
    [SerializeField] private float obstacleRockMaxY = 3f;
    [SerializeField] private float obstacleRockRiseSpeed = 0.4f;
    [SerializeField] private float obstacleGlowIntensity = 0.8f;
    [SerializeField] private Color obstacleGlowColor = new Color(0.85f, 0.55f, 0.2f, 1f);
    [SerializeField] private LayerMask obstacleOcclusionMask = ~0;
    [Header("Rise Randomization")]
    [SerializeField] private float speedRandomRange = 0.3f;
    [SerializeField] private float swayAmplitude = 0.08f;
    [SerializeField] private float swayFrequency = 1.2f;
    [Header("Puzzle Activation")]
    [Tooltip("ステージ有効化直後にヒント岩への照射を無視する秒数。フェードイン中の誤検知を防ぐ。")]
    [SerializeField] private float puzzleActivationDelay = 1.5f;

    private SpotlightSensor greenHintSensor;
    private SpotlightSensor blueHintSensor;
    private MaterialPropertyBlock propertyBlock;
    private float greenHintHoldTimer;
    private float blueHintHoldTimer;
    private bool greenActivated;
    private bool blueActivated;
    private bool greenPedestalBurstPlayed;
    private bool bluePedestalBurstPlayed;
    private bool greenHintBurstPlayed;
    private bool blueHintBurstPlayed;
    private FinalePhase finalePhase;
    private float finaleTimer;
    private Color initialAmbientColor;
    private Light revealLight;
    private float[] liftSpeedMultipliers;
    private float[] liftSwayPhases;
    private Vector3[] liftInitialPositions;
    private float[] obstacleSpeedMultipliers;
    private float[] obstacleSwayPhases;
    private Vector3[] obstacleInitialPositions;
    // ステージ有効化からの経過秒数（puzzleActivationDelay 超過まではヒント判定を行わない）
    private float activationElapsed;

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
        public bool HasEmissionColor;
    }

    private sealed class RockVisualCache
    {
        public Renderer[] Renderers;
        public Light GlowLight;
        public Renderer[] EmissiveShellRenderers;
    }

    private readonly System.Collections.Generic.List<RendererState> rendererStates = new System.Collections.Generic.List<RendererState>();
    private readonly System.Collections.Generic.Dictionary<Transform, RockVisualCache> rockVisualCaches = new System.Collections.Generic.Dictionary<Transform, RockVisualCache>();

    public bool IsComplete => finalePhase == FinalePhase.Complete;
    public bool IsFinaleStarted => finalePhase != FinalePhase.None;
    public bool GreenActivated => greenActivated;
    public bool BlueActivated => blueActivated;

    private void Awake()
    {
        ResolveSensors();
        CacheRockVisuals();
        CacheLightState();
        RefreshVisuals();
    }

    private void OnEnable()
    {
        ResetPuzzleState();
        ResolveSensors();
        CacheRockVisuals();
        CacheLightState();
        RefreshVisuals();
        InitRiseRandomization();
        EnsureObstacleSensors();
        EnsureObstacleColliders();
    }

    private void OnDisable()
    {
        bool preserveAmbientForStage4Transition = Application.isPlaying
            && finalePhase == FinalePhase.Complete
            && advanceToNextStageOnComplete
            && nextStageIndex == 3;

        // Stage3 完了→Stage4 の遷移だけは、Stage4 側のフェード明転へ繋ぐため ambient を維持する。
        // それ以外の無効化では必ず初期 ambient に戻して、Stage4→Stage2 復帰時の明るさ残りを防ぐ。
        if (Application.isPlaying && !preserveAmbientForStage4Transition)
        {
            RenderSettings.ambientLight = initialAmbientColor;
        }
    }

    private void OnValidate()
    {
        ResolveSensors();
        CacheRockVisuals();
        CacheLightState();
        RefreshVisuals();
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        // obstacleとリフトは常に動かす
        UpdateLoopingLiftTargets();
        UpdateObstacleRocks();

        // ステージ開始直後のクールダウン中はヒント判定をスキップ（フェードイン中の誤検知がパズルを進行させないように）
        if (activationElapsed < puzzleActivationDelay)
        {
            activationElapsed += Time.deltaTime;
            return;
        }

        UpdateHintActivation(greenHintSensor, ref greenHintHoldTimer, ref greenActivated, greenHintRock, greenGlowColor, ref greenHintBurstPlayed, greenPedestalRock, ref greenPedestalBurstPlayed);
        UpdateHintActivation(blueHintSensor, ref blueHintHoldTimer, ref blueActivated, blueHintRock, blueGlowColor, ref blueHintBurstPlayed, bluePedestalRock, ref bluePedestalBurstPlayed);

        if (greenActivated && blueActivated)
        {
            UpdateFinale();
        }
    }

    public void ConfigureDefaults(
        Transform redPedestalRockReference,
        Transform greenPedestalRockReference,
        Transform bluePedestalRockReference,
        Transform greenHintRockReference,
        Transform blueHintRockReference)
    {
        if (redPedestalRock == null)
        {
            redPedestalRock = redPedestalRockReference;
        }

        if (greenPedestalRock == null)
        {
            greenPedestalRock = greenPedestalRockReference;
        }

        if (bluePedestalRock == null)
        {
            bluePedestalRock = bluePedestalRockReference;
        }

        if (greenHintRock == null)
        {
            greenHintRock = greenHintRockReference;
        }

        if (blueHintRock == null)
        {
            blueHintRock = blueHintRockReference;
        }

        ResolveSensors();
        CacheRockVisuals();
        RefreshVisuals();
    }

    public void ConfigureTransition(bool shouldAdvanceToNextStage, int nextStage)
    {
        advanceToNextStageOnComplete = shouldAdvanceToNextStage;
        nextStageIndex = Mathf.Max(0, nextStage);
    }

    public void ConfigureBrightTransition(bool useBright)
    {
        useBrightTransitionToNextStage = useBright;
    }

    public void ConfigureDynamicStageObjects(Transform[] liftTargets, Transform[] obstacleTargets)
    {
        if (liftTargets != null)
        {
            loopingLiftTargets = liftTargets;
        }

        if (obstacleTargets != null)
        {
            obstacleRocks = obstacleTargets;
        }

        InitRiseRandomization();
        EnsureObstacleSensors();
        EnsureObstacleColliders();
    }

    public void RefreshVisuals()
    {
        ApplyRockState(redPedestalRock, redGlowColor, true, redPedestalEmissionIntensity);
        ApplyRockGlowLight(redPedestalRock, redGlowColor, true, redPedestalGlowLightIntensity);
        ApplyRockState(greenPedestalRock, greenGlowColor, greenActivated, pedestalEmissionIntensity);
        ApplyRockGlowLight(greenPedestalRock, greenGlowColor, greenActivated, pedestalGlowLightIntensity);
        ApplyRockState(bluePedestalRock, blueGlowColor, blueActivated, pedestalEmissionIntensity);
        ApplyRockGlowLight(bluePedestalRock, blueGlowColor, blueActivated, pedestalGlowLightIntensity);
        ApplyRockState(greenHintRock, greenGlowColor, greenActivated, hintRockEmissionIntensity);
        ApplyRockGlowLight(greenHintRock, greenGlowColor, greenActivated, hintGlowLightIntensity);
        ApplyRockState(blueHintRock, blueGlowColor, blueActivated, hintRockEmissionIntensity);
        ApplyRockGlowLight(blueHintRock, blueGlowColor, blueActivated, hintGlowLightIntensity);
    }

    private void CacheRockVisuals()
    {
        CacheRockVisual(redPedestalRock);
        CacheRockVisual(greenPedestalRock);
        CacheRockVisual(bluePedestalRock);
        CacheRockVisual(greenHintRock);
        CacheRockVisual(blueHintRock);
    }

    private void ResolveSensors()
    {
        greenHintSensor = EnsureHintSensor(greenHintRock);
        blueHintSensor = EnsureHintSensor(blueHintRock);
    }

    private void ResetPuzzleState()
    {
        greenHintHoldTimer = 0f;
        blueHintHoldTimer = 0f;
        greenActivated = false;
        blueActivated = false;
        greenPedestalBurstPlayed = false;
        bluePedestalBurstPlayed = false;
        greenHintBurstPlayed = false;
        blueHintBurstPlayed = false;
        finalePhase = FinalePhase.None;
        finaleTimer = 0f;
        rendererStates.Clear();
        activationElapsed = 0f;

        if (revealLight != null)
        {
            revealLight.enabled = false;
            revealLight.intensity = 0f;
        }

        RestoreInitialPositions(loopingLiftTargets, liftInitialPositions);
        RestoreInitialPositions(obstacleRocks, obstacleInitialPositions);
    }

    private void InitRiseRandomization()
    {
        liftSpeedMultipliers = CreateSpeedMultipliers(loopingLiftTargets);
        liftSwayPhases = CreateSwayPhases(loopingLiftTargets);
        liftInitialPositions = CapturePositions(loopingLiftTargets);
        obstacleSpeedMultipliers = CreateSpeedMultipliers(obstacleRocks);
        obstacleSwayPhases = CreateSwayPhases(obstacleRocks);
        obstacleInitialPositions = CapturePositions(obstacleRocks);
    }

    private float[] CreateSpeedMultipliers(Transform[] targets)
    {
        if (targets == null || targets.Length == 0)
        {
            return null;
        }

        float[] result = new float[targets.Length];
        for (int i = 0; i < targets.Length; i++)
        {
            result[i] = 1f + Random.Range(-speedRandomRange, speedRandomRange);
        }

        return result;
    }

    private static float[] CreateSwayPhases(Transform[] targets)
    {
        if (targets == null || targets.Length == 0)
        {
            return null;
        }

        float[] result = new float[targets.Length];
        for (int i = 0; i < targets.Length; i++)
        {
            result[i] = Random.Range(0f, Mathf.PI * 2f);
        }

        return result;
    }

    private static Vector3[] CapturePositions(Transform[] targets)
    {
        if (targets == null || targets.Length == 0)
        {
            return null;
        }

        Vector3[] result = new Vector3[targets.Length];
        for (int i = 0; i < targets.Length; i++)
        {
            result[i] = targets[i] != null ? targets[i].position : Vector3.zero;
        }

        return result;
    }

    private static void RestoreInitialPositions(Transform[] targets, Vector3[] initialPositions)
    {
        if (targets == null || initialPositions == null)
        {
            return;
        }

        for (int i = 0; i < targets.Length && i < initialPositions.Length; i++)
        {
            if (targets[i] == null)
            {
                continue;
            }

            targets[i].position = initialPositions[i];
        }
    }

    private SpotlightSensor EnsureHintSensor(Transform rockRoot)
    {
        if (rockRoot == null)
        {
            return null;
        }

        SpotlightSensor sensor = rockRoot.GetComponent<SpotlightSensor>();
        if (sensor == null)
        {
            sensor = rockRoot.gameObject.AddComponent<SpotlightSensor>();
        }

        Renderer sampleRenderer = rockRoot.GetComponentInChildren<Renderer>(true);
        Collider sampleCollider = rockRoot.GetComponentInChildren<Collider>(true);
        if (sampleCollider == null)
        {
            MeshFilter meshFilter = rockRoot.GetComponentInChildren<MeshFilter>(true);
            if (meshFilter != null)
            {
                sampleCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
            }
        }

        sensor.Configure(null, null, null, sampleRenderer, sampleCollider);
        sensor.SetLineOfSight(true);
        return sensor;
    }

    private void UpdateHintActivation(
        SpotlightSensor sensor,
        ref float holdTimer,
        ref bool activated,
        Transform hintRock,
        Color glowColor,
        ref bool hintBurstPlayed,
        Transform pedestalRock,
        ref bool pedestalBurstPlayed)
    {
        if (activated)
        {
            return;
        }

        bool isLit = IsHintIlluminated(sensor);
        if (isLit)
        {
            holdTimer += Time.deltaTime;
        }
        else
        {
            if (holdTimer > 0f)
            {
                holdTimer = 0f;
                ApplyGradualGlow(hintRock, glowColor, 0f);
            }
            return;
        }

        float progress = Mathf.Clamp01(holdTimer / Mathf.Max(0.001f, hintHoldSeconds));
        ApplyGradualGlow(hintRock, glowColor, progress);

        if (holdTimer >= hintHoldSeconds)
        {
            activated = true;
            RefreshVisuals();

            if (!hintBurstPlayed)
            {
                SpawnBurst(hintRock);
                hintBurstPlayed = true;
            }

            if (!pedestalBurstPlayed)
            {
                SpawnBurst(pedestalRock);
                pedestalBurstPlayed = true;
            }
        }
    }

    private void ApplyGradualGlow(Transform rockRoot, Color glowColor, float progress)
    {
        if (rockRoot == null)
        {
            return;
        }

        float lerpedEmission = Mathf.Lerp(inactiveEmissionIntensity, hintRockEmissionIntensity, progress);
        float lerpedLightIntensity = Mathf.Lerp(0f, hintGlowLightIntensity, progress);
        ApplyRockState(rockRoot, glowColor, progress > 0.01f, lerpedEmission);
        ApplyRockGlowLight(rockRoot, glowColor, progress > 0.01f, lerpedLightIntensity);
    }

    private bool IsHintIlluminated(SpotlightSensor sensor)
    {
        if (sensor == null)
        {
            return false;
        }

        if (!(sensor.IsLit || sensor.Exposure01 >= hintActivationExposureThreshold))
        {
            return false;
        }

        if (!requireHintCenterZone)
        {
            return !IsBlockedByObstacle(sensor);
        }

        Light sourceLight = sensor.SourceLight;
        if (sourceLight == null || sourceLight.type != LightType.Spot || sourceLight.spotAngle <= 0.001f)
        {
            return false;
        }

        Vector3 samplePosition = sensor.SampleWorldPosition;
        Vector3 toTarget = samplePosition - sourceLight.transform.position;
        if (toTarget.sqrMagnitude <= 0.000001f)
        {
            return false;
        }

        float angleToTarget = Vector3.Angle(sourceLight.transform.forward, toTarget.normalized);
        float halfAngle = sourceLight.spotAngle * 0.5f;
        float normalized = angleToTarget / Mathf.Max(halfAngle, 0.0001f);
        return normalized <= hintCenterZoneNormalizedRadius && !IsBlockedByObstacle(sensor);
    }

    private bool IsBlockedByObstacle(SpotlightSensor sensor)
    {
        if (sensor == null)
        {
            return false;
        }

        Light sourceLight = sensor.SourceLight;
        if (sourceLight == null)
        {
            return false;
        }

        Vector3 lightPosition = sourceLight.transform.position;
        Vector3 sensorPosition = sensor.SampleWorldPosition;
        Vector3 direction = sensorPosition - lightPosition;
        float maxDistance = direction.magnitude;
        if (maxDistance <= 0.001f)
        {
            return false;
        }

        direction /= maxDistance;

        RaycastHit[] hits = Physics.RaycastAll(new Ray(lightPosition, direction), maxDistance, obstacleOcclusionMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
        {
            return false;
        }

        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
        for (int index = 0; index < hits.Length; index++)
        {
            Transform hitTransform = hits[index].transform;
            if (hitTransform == null)
            {
                continue;
            }

            if (hitTransform == sensor.transform || hitTransform.IsChildOf(sensor.transform))
            {
                return false;
            }

            if (IsObstacleTransform(hitTransform))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsObstacleTransform(Transform target)
    {
        if (target == null)
        {
            return false;
        }

        if (obstacleRocks != null)
        {
            for (int index = 0; index < obstacleRocks.Length; index++)
            {
                Transform obstacle = obstacleRocks[index];
                if (obstacle == null) continue;
                if (target == obstacle || target.IsChildOf(obstacle)) return true;
            }
        }

        if (loopingLiftTargets != null)
        {
            for (int index = 0; index < loopingLiftTargets.Length; index++)
            {
                Transform lift = loopingLiftTargets[index];
                if (lift == null) continue;
                if (target == lift || target.IsChildOf(lift)) return true;
            }
        }

        return false;
    }

    private void UpdateLoopingLiftTargets()
    {
        if (loopingLiftTargets == null || loopingLiftTargets.Length == 0)
        {
            return;
        }

        float maxY = Mathf.Max(loopingLiftMinY, loopingLiftMaxY);
        float minY = Mathf.Min(loopingLiftMinY, loopingLiftMaxY);
        float baseSpeed = Mathf.Max(0f, loopingLiftUpSpeed);

        for (int index = 0; index < loopingLiftTargets.Length; index++)
        {
            Transform target = loopingLiftTargets[index];
            if (target == null)
            {
                continue;
            }

            float speedMul = liftSpeedMultipliers != null && index < liftSpeedMultipliers.Length ? liftSpeedMultipliers[index] : 1f;
            Vector3 position = target.position;
            position.y += baseSpeed * speedMul * Time.deltaTime;
            if (position.y >= maxY)
            {
                position.y = minY;
            }

            if (liftSwayPhases != null && index < liftSwayPhases.Length && liftInitialPositions != null && index < liftInitialPositions.Length)
            {
                float sway = Mathf.Sin(Time.time * swayFrequency + liftSwayPhases[index]) * swayAmplitude;
                position.x = liftInitialPositions[index].x + sway;
            }

            target.position = position;
        }
    }

    /// <summary>
    /// 障害物岩に非トリガーのコライダーがない場合は、描画メッシュに合わせてコライダーを付与する。
    /// SpotlightSensor の LineOfSight レイキャストが障害物岩で正しくブロックされるために必要。
    /// </summary>
    private void EnsureObstacleColliders()
    {
        if (obstacleRocks == null)
        {
            return;
        }

        for (int i = 0; i < obstacleRocks.Length; i++)
        {
            Transform rock = obstacleRocks[i];
            if (rock == null)
            {
                continue;
            }

            Collider[] existing = rock.GetComponentsInChildren<Collider>(true);
            bool hasNonTrigger = false;
            for (int j = 0; j < existing.Length; j++)
            {
                if (existing[j] != null && !existing[j].isTrigger)
                {
                    hasNonTrigger = true;
                    break;
                }
            }

            if (!hasNonTrigger)
            {
                Renderer[] childRenderers = rock.GetComponentsInChildren<Renderer>(true);
                if (childRenderers.Length > 0)
                {
                    foreach (var cr in childRenderers)
                    {
                        if (cr.name == EmissiveShellName) continue;
                        if (cr.GetComponent<Collider>() != null) continue;
                        
                        MeshFilter mf = cr.GetComponent<MeshFilter>();
                        if (mf != null && mf.sharedMesh != null)
                        {
                            MeshCollider mc = cr.gameObject.AddComponent<MeshCollider>();
                            mc.convex = true;
                            mc.isTrigger = false;
                        }
                        else
                        {
                            BoxCollider box = cr.gameObject.AddComponent<BoxCollider>();
                            box.isTrigger = false;
                        }
                    }
                }
                else
                {
                    BoxCollider box = rock.gameObject.AddComponent<BoxCollider>();
                    box.isTrigger = false;
                }
            }
        }
    }

    private void EnsureObstacleSensors()
    {
        if (obstacleRocks == null)
        {
            return;
        }

        for (int index = 0; index < obstacleRocks.Length; index++)
        {
            Transform obstacle = obstacleRocks[index];
            if (obstacle == null)
            {
                continue;
            }

            SpotlightSensor sensor = obstacle.GetComponent<SpotlightSensor>();
            if (sensor == null)
            {
                sensor = obstacle.gameObject.AddComponent<SpotlightSensor>();
            }

            Renderer sampleRenderer = obstacle.GetComponentInChildren<Renderer>(true);
            Collider sampleCollider = obstacle.GetComponentInChildren<Collider>(true);
            sensor.Configure(null, null, obstacle, sampleRenderer, sampleCollider);
            sensor.SetLineOfSight(false);
        }
    }

    private void UpdateObstacleRocks()    {
        if (obstacleRocks == null || obstacleRocks.Length == 0)
        {
            return;
        }

        float maxY = Mathf.Max(obstacleRockResetY, obstacleRockMaxY);
        float resetY = Mathf.Min(obstacleRockResetY, obstacleRockMaxY);
        float baseSpeed = Mathf.Max(0f, obstacleRockRiseSpeed);

        for (int index = 0; index < obstacleRocks.Length; index++)
        {
            Transform rock = obstacleRocks[index];
            if (rock == null)
            {
                continue;
            }

            float speedMul = obstacleSpeedMultipliers != null && index < obstacleSpeedMultipliers.Length ? obstacleSpeedMultipliers[index] : 1f;
            Vector3 position = rock.position;
            position.y += baseSpeed * speedMul * Time.deltaTime;
            if (position.y >= maxY)
            {
                position.y = resetY;
            }

            if (obstacleSwayPhases != null && index < obstacleSwayPhases.Length && obstacleInitialPositions != null && index < obstacleInitialPositions.Length)
            {
                float sway = Mathf.Sin(Time.time * swayFrequency + obstacleSwayPhases[index]) * swayAmplitude;
                position.x = obstacleInitialPositions[index].x + sway;
            }

            rock.position = position;

            UpdateObstacleGlow(rock);
        }
    }

    private void UpdateObstacleGlow(Transform obstacleRoot)
    {
        if (obstacleRoot == null)
        {
            return;
        }

        SpotlightSensor sensor = obstacleRoot.GetComponent<SpotlightSensor>();
        if (sensor == null)
        {
            return;
        }

        bool isLit = sensor.IsLit || sensor.Exposure01 >= hintActivationExposureThreshold;
        float intensity = isLit ? obstacleGlowIntensity : 0f;

        Renderer[] renderers = obstacleRoot.GetComponentsInChildren<Renderer>(false);
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        Color emissionColor = obstacleGlowColor * intensity;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer rend = renderers[i];
            if (rend == null)
            {
                continue;
            }

            rend.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_EmissionColor", emissionColor);
            rend.SetPropertyBlock(propertyBlock);
        }
    }

    private void UpdateFinale()
    {
        switch (finalePhase)
        {
            case FinalePhase.None:
                BeginLocalReveal();
                break;

            case FinalePhase.LocalReveal:
                UpdateLocalReveal();
                break;

            case FinalePhase.PanoramaReveal:
                UpdatePanoramaReveal();
                break;

            case FinalePhase.HoldBeforeTransition:
                UpdateHoldBeforeTransition();
                break;
        }
    }

    private void ApplyRockState(Transform rockRoot, Color glowColor, bool isActive, float activeEmissionIntensity)
    {
        if (rockRoot == null)
        {
            return;
        }

        RockVisualCache visualCache = GetOrCreateRockVisualCache(rockRoot);
        if (visualCache == null || visualCache.Renderers == null)
        {
            return;
        }

        Renderer[] renderers = visualCache.Renderers;
        Color appliedColor = isActive ? glowColor : glowColor * inactiveColorMultiplier;
        float emissionIntensity = isActive ? activeEmissionIntensity : inactiveEmissionIntensity;
        float clampedEmissionIntensity = Mathf.Clamp(emissionIntensity, 0f, maxRockEmissionIntensity);
        Color emissionColor = glowColor * clampedEmissionIntensity;
        Color shellColor = new Color(glowColor.r, glowColor.g, glowColor.b, isActive ? emissiveShellAlpha : 0f);

        for (int index = 0; index < renderers.Length; index++)
        {
            Renderer renderer = renderers[index];
            if (renderer == null)
            {
                continue;
            }

            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_Color", appliedColor);
            propertyBlock.SetColor("_BaseColor", appliedColor);
            propertyBlock.SetColor("_EmissionColor", emissionColor);
            renderer.SetPropertyBlock(propertyBlock);

            Material material = renderer.sharedMaterial;
            if (material == null)
            {
                continue;
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", appliedColor);
            }

            if (material.HasProperty("_Color"))
            {
                material.color = appliedColor;
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", emissionColor);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                if (isActive && emissionIntensity > 0.0001f)
                {
                    material.EnableKeyword("_EMISSION");
                }
                else
                {
                    material.DisableKeyword("_EMISSION");
                }
            }
        }

        ApplyEmissiveShellState(visualCache, shellColor, isActive);
    }

    private void ApplyRockGlowLight(Transform rockRoot, Color glowColor, bool isActive, float intensity)
    {
        if (rockRoot == null)
        {
            return;
        }

        RockVisualCache visualCache = GetOrCreateRockVisualCache(rockRoot);
        if (visualCache == null)
        {
            return;
        }

        Light glowLight = visualCache.GlowLight != null ? visualCache.GlowLight : EnsureGlowLight(rockRoot);
        if (glowLight == null)
        {
            return;
        }

        visualCache.GlowLight = glowLight;

        glowLight.color = glowColor;
        glowLight.range = glowLightRange;
        glowLight.intensity = isActive ? intensity : 0f;
        glowLight.enabled = isActive;
    }

    private void CacheRockVisual(Transform rockRoot)
    {
        if (rockRoot == null)
        {
            return;
        }

        RockVisualCache visualCache = GetOrCreateRockVisualCache(rockRoot);
        if (visualCache == null)
        {
            return;
        }

        if (visualCache.Renderers == null || visualCache.Renderers.Length == 0)
        {
            visualCache.Renderers = rockRoot.GetComponentsInChildren<Renderer>(true);
        }

        CleanupNestedEmissiveShells(rockRoot);

        if (visualCache.EmissiveShellRenderers == null || visualCache.EmissiveShellRenderers.Length == 0)
        {
            EnsureEmissiveShells(rockRoot, visualCache);
        }

        for (int index = 0; index < visualCache.Renderers.Length; index++)
        {
            Renderer renderer = visualCache.Renderers[index];
            if (renderer == null)
            {
                continue;
            }

            Material material = renderer.sharedMaterial;
            if (material == null)
            {
                continue;
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
        }

        if (visualCache.GlowLight == null)
        {
            visualCache.GlowLight = EnsureGlowLight(rockRoot);
        }
    }

    private RockVisualCache GetOrCreateRockVisualCache(Transform rockRoot)
    {
        if (rockRoot == null)
        {
            return null;
        }

        if (!rockVisualCaches.TryGetValue(rockRoot, out RockVisualCache visualCache) || visualCache == null)
        {
            visualCache = new RockVisualCache();
            rockVisualCaches[rockRoot] = visualCache;
        }

        return visualCache;
    }

    private void EnsureEmissiveShells(Transform rockRoot, RockVisualCache visualCache)
    {
        MeshFilter[] meshFilters = rockRoot.GetComponentsInChildren<MeshFilter>(true);
        var shellRenderers = new System.Collections.Generic.List<Renderer>(meshFilters.Length);

        for (int index = 0; index < meshFilters.Length; index++)
        {
            MeshFilter sourceFilter = meshFilters[index];
            if (sourceFilter == null || sourceFilter.sharedMesh == null)
            {
                continue;
            }

            Transform sourceTransform = sourceFilter.transform;
            if (sourceTransform.name == EmissiveShellName)
            {
                continue;
            }

            Transform shellTransform = sourceTransform.Find(EmissiveShellName);
            if (shellTransform == null)
            {
                shellTransform = new GameObject(EmissiveShellName).transform;
                shellTransform.SetParent(sourceTransform, false);
            }

            shellTransform.localPosition = Vector3.zero;
            shellTransform.localRotation = Quaternion.identity;
            shellTransform.localScale = Vector3.one * emissiveShellScale;

            MeshFilter shellFilter = shellTransform.GetComponent<MeshFilter>();
            if (shellFilter == null)
            {
                shellFilter = shellTransform.gameObject.AddComponent<MeshFilter>();
            }
            shellFilter.sharedMesh = sourceFilter.sharedMesh;

            MeshRenderer shellRenderer = shellTransform.GetComponent<MeshRenderer>();
            if (shellRenderer == null)
            {
                shellRenderer = shellTransform.gameObject.AddComponent<MeshRenderer>();
            }

            shellRenderer.sharedMaterial = GetOrCreateEmissiveShellMaterial(sourceTransform.name);
            shellRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shellRenderer.receiveShadows = false;
            shellRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            shellRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            shellRenderer.enabled = false;
            shellRenderers.Add(shellRenderer);
        }

        visualCache.EmissiveShellRenderers = shellRenderers.ToArray();
    }

    private void CleanupNestedEmissiveShells(Transform rockRoot)
    {
        if (rockRoot == null)
        {
            return;
        }

        Transform[] descendants = rockRoot.GetComponentsInChildren<Transform>(true);
        for (int index = 0; index < descendants.Length; index++)
        {
            Transform descendant = descendants[index];
            if (descendant == null || descendant == rockRoot || descendant.name != EmissiveShellName)
            {
                continue;
            }

            if (descendant.parent != null && descendant.parent.name == EmissiveShellName)
            {
                if (Application.isPlaying)
                {
                    Destroy(descendant.gameObject);
                }
                else
                {
                    DestroyImmediate(descendant.gameObject);
                }
            }
        }
    }

    private Material GetOrCreateEmissiveShellMaterial(string sourceName)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Unlit/Color")
            ?? Shader.Find("Sprites/Default");
        Material material = new Material(shader);
        material.name = sourceName + " Emissive Shell Material";
        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }
        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 0f);
        }
        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }
        material.renderQueue = 3000;
        return material;
    }

    private void ApplyEmissiveShellState(RockVisualCache visualCache, Color shellColor, bool isActive)
    {
        if (visualCache == null || visualCache.EmissiveShellRenderers == null)
        {
            return;
        }

        for (int index = 0; index < visualCache.EmissiveShellRenderers.Length; index++)
        {
            Renderer shellRenderer = visualCache.EmissiveShellRenderers[index];
            if (shellRenderer == null)
            {
                continue;
            }

            Material material = shellRenderer.sharedMaterial;
            if (material != null)
            {
                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", shellColor);
                }

                if (material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", shellColor);
                }
            }

            shellRenderer.enabled = isActive;
        }
    }

    private Light EnsureGlowLight(Transform rockRoot)
    {
        Transform lightTransform = rockRoot.Find("Stage3 Glow Light");
        if (lightTransform == null)
        {
            lightTransform = new GameObject("Stage3 Glow Light").transform;
            lightTransform.SetParent(rockRoot, false);
            lightTransform.localPosition = Vector3.zero;
            lightTransform.localRotation = Quaternion.identity;
        }

        Light glowLight = lightTransform.GetComponent<Light>();
        if (glowLight == null)
        {
            glowLight = lightTransform.gameObject.AddComponent<Light>();
        }

        glowLight.type = LightType.Point;
        glowLight.shadows = LightShadows.None;
        return glowLight;
    }

    private void BeginLocalReveal()
    {
        if (finalePhase != FinalePhase.None)
        {
            return;
        }

        finalePhase = FinalePhase.LocalReveal;
        finaleTimer = 0f;
        CaptureRendererStates();
        EnsureRevealLight();
        UpdateRevealLightAnchor();
        if (revealLight != null)
        {
            revealLight.enabled = true;
            revealLight.color = finalBrightnessColor;
            revealLight.range = localRevealLightRange;
            revealLight.intensity = localRevealLightIntensity;
        }
        ApplyBrightening(0f);
    }

    private void UpdateLocalReveal()
    {
        finaleTimer += Time.deltaTime;
        UpdateRevealLightAnchor();
        ApplyBrightening(0f);

        if (finaleTimer >= localRevealDuration)
        {
            BeginPanoramaReveal();
        }
    }

    private void BeginPanoramaReveal()
    {
        finalePhase = FinalePhase.PanoramaReveal;
        finaleTimer = 0f;
        UpdateRevealLightAnchor();
    }

    private void UpdatePanoramaReveal()
    {
        finaleTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(finaleTimer / Mathf.Max(0.0001f, brightenDuration));
        float eased = 1f - Mathf.Pow(1f - progress, 3f);
        UpdateRevealLightAnchor();
        ApplyBrightening(eased);
        UpdateExpandedLightState(eased);

        if (progress >= 1f)
        {
            BeginHoldBeforeTransition();
        }
    }

    private void BeginHoldBeforeTransition()
    {
        finalePhase = FinalePhase.HoldBeforeTransition;
        finaleTimer = 0f;
        ApplyBrightening(1f);
        UpdateExpandedLightState(1f);
    }

    private void UpdateHoldBeforeTransition()
    {
        finaleTimer += Time.deltaTime;
        UpdateRevealLightAnchor();
        ApplyBrightening(1f);
        UpdateExpandedLightState(1f);

        if (finaleTimer < holdBeforeStageTransitionSeconds)
        {
            return;
        }

        finalePhase = FinalePhase.Complete;
        TransitionToNextStage();
    }

    private void CacheLightState()
    {
        initialAmbientColor = RenderSettings.ambientLight;
    }

    private void CaptureRendererStates()
    {
        rendererStates.Clear();

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
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

                state.HasEmissionColor = sharedMaterial.HasProperty("_EmissionColor");
            }

            rendererStates.Add(state);
        }
    }

    private void ApplyBrightening(float progress)
    {
        RenderSettings.ambientLight = Color.Lerp(initialAmbientColor, ambientTargetColor, progress);

        for (int index = 0; index < rendererStates.Count; index++)
        {
            RendererState state = rendererStates[index];
            if (state == null || state.Renderer == null)
            {
                continue;
            }

            state.Renderer.GetPropertyBlock(state.PropertyBlock);

            if (state.HasHiddenColor)
            {
                Color revealedHiddenColor = new Color(state.HiddenColor.r, state.HiddenColor.g, state.HiddenColor.b, Mathf.Lerp(state.HiddenColor.a, hiddenBrightenAlpha, progress));
                state.PropertyBlock.SetColor("_HiddenColor", revealedHiddenColor);
            }

            state.Renderer.SetPropertyBlock(state.PropertyBlock);

            // Avoid writing shared materials every frame to reduce Stage3 CPU spikes.
        }
    }

    private void UpdateExpandedLightState(float progress)
    {
        if (revealLight != null)
        {
            revealLight.range = Mathf.Lerp(localRevealLightRange, finalLightRange, progress);
            revealLight.intensity = Mathf.Lerp(localRevealLightIntensity, finalLightIntensity, progress);
            revealLight.color = Color.Lerp(finalBrightnessColor, ambientTargetColor, progress * 0.35f);
        }
    }

    private void EnsureRevealLight()
    {
        if (revealLight != null)
        {
            return;
        }

        Transform glowTransform = transform.Find("Stage Reveal Light");
        if (glowTransform == null)
        {
            glowTransform = new GameObject("Stage Reveal Light").transform;
            glowTransform.SetParent(transform, false);
        }

        revealLight = glowTransform.GetComponent<Light>();
        if (revealLight == null)
        {
            revealLight = glowTransform.gameObject.AddComponent<Light>();
        }

        revealLight.type = LightType.Point;
        revealLight.shadows = LightShadows.None;
        revealLight.color = finalBrightnessColor;
        revealLight.range = localRevealLightRange;
        revealLight.intensity = 0f;
        revealLight.enabled = false;
    }

    private void UpdateRevealLightAnchor()
    {
        if (revealLight == null)
        {
            return;
        }

        revealLight.transform.position = ResolveRevealCenter() + (Vector3.up * revealLightHeightOffset);
        revealLight.transform.rotation = Quaternion.identity;
    }

    private Vector3 ResolveRevealCenter()
    {
        Vector3 accumulated = Vector3.zero;
        int count = 0;

        AccumulateRockCenter(redPedestalRock, ref accumulated, ref count);
        AccumulateRockCenter(greenPedestalRock, ref accumulated, ref count);
        AccumulateRockCenter(bluePedestalRock, ref accumulated, ref count);

        if (count > 0)
        {
            return accumulated / count;
        }

        return transform.position;
    }

    private static void AccumulateRockCenter(Transform rockRoot, ref Vector3 accumulated, ref int count)
    {
        if (rockRoot == null)
        {
            return;
        }

        accumulated += ResolveBurstPosition(rockRoot);
        count++;
    }

    private void TransitionToNextStage()
    {
        if (!advanceToNextStageOnComplete)
        {
            return;
        }

        StageSequenceController sequenceController = FindFirstObjectByType<StageSequenceController>();
        if (sequenceController == null)
        {
            return;
        }

        if (nextStageIndex < 0 || nextStageIndex >= sequenceController.StageCount)
        {
            Debug.LogWarning(
                string.Format(
                    "[Stage3RockHintPuzzle] nextStageIndex={0} が有効範囲外のため遷移をスキップします。",
                    nextStageIndex),
                this);
            return;
        }

        sequenceController.FadeToStageWithOptions(nextStageIndex, 0.8f, 0.9f, Color.black);
    }

    private void SpawnBurst(Transform target)
    {
        if (target == null)
        {
            return;
        }

        Vector3 burstPosition = ResolveBurstPosition(target);
        GameObject burstObject = new GameObject(target.name + " Burst " + Time.frameCount);
        burstObject.transform.SetParent(transform, true);
        burstObject.transform.position = burstPosition;
        burstObject.transform.rotation = Quaternion.identity;

        ParticleSystem particleSystem = burstObject.AddComponent<ParticleSystem>();
        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particleSystem.main;
        main.loop = false;
        main.duration = 0.35f;
        main.startLifetime = burstLifetime;
        main.startSpeed = burstSpeed;
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
        main.startColor = Color.white;
        main.maxParticles = burstParticleCount;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = particleSystem.emission;
        emission.enabled = false;

        var shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.08f;

        var colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.65f, 0.45f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        var sizeOverLifetime = particleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.2f),
            new Keyframe(0.35f, 1f),
            new Keyframe(1f, 0f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        Shader particleShader = Shader.Find(DefaultBurstShaderName)
            ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Particles/Standard Unlit")
            ?? Shader.Find("Legacy Shaders/Particles/Additive");
        if (particleShader != null)
        {
            Material particleMaterial = new Material(particleShader);
            particleMaterial.color = Color.white;
            if (particleMaterial.HasProperty("_BaseColor"))
            {
                particleMaterial.SetColor("_BaseColor", Color.white);
            }
            if (particleMaterial.HasProperty("_Color"))
            {
                particleMaterial.SetColor("_Color", Color.white);
            }
            renderer.material = particleMaterial;
        }
        renderer.alignment = ParticleSystemRenderSpace.View;

        particleSystem.Clear(true);
        particleSystem.Emit(burstParticleCount);
        particleSystem.Play();
    }

    private static Vector3 ResolveBurstPosition(Transform target)
    {
        Renderer renderer = target.GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            return renderer.bounds.center;
        }

        Collider collider = target.GetComponentInChildren<Collider>();
        if (collider != null)
        {
            return collider.bounds.center;
        }

        return target.position;
    }
}