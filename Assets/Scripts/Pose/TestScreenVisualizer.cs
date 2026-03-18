using UnityEngine;

[AddComponentMenu("Pose/Test Screen Visualizer")]
public class TestScreenVisualizer : MonoBehaviour
{
    private const bool DefaultIPhoneInvertPitch = false;

    private enum ProjectionTargetSurface
    {
        Front,
        Left
    }

    private enum RotationAxisSource
    {
        X,
        Y,
        Z
    }

    private enum RayControlMode
    {
        ForwardVectorYawPitch,
        AxisMappedEuler
    }

    [SerializeField] private UdpQuaternionReceiver receiver;
    [SerializeField] private Light sourceLight;
    [SerializeField] private ProjectionSurface frontSurface;
    [SerializeField] private ProjectionSurface leftSurface;

    [Header("Standing Position Simulation")]
    [Tooltip("立ち位置の自動補正の有効無効")]
    [SerializeField] private bool simulateStandingPosition = true;
    [Tooltip("スクリーンの横幅を1としたときの、プレイヤーの立ち位置の距離比率。例: 4/5=0.8。")]
    [SerializeField] private float standingDistanceRatio = 0.8f;
    [Tooltip("自動計算された立ち位置からの微調整オフセット（X:右, Y:上, Z:前）")]
    [SerializeField] private Vector3 simulatedOriginOffset = Vector3.zero;

    [Header("Fallback Configurations")]
    [SerializeField] private Vector2Int referenceResolution = new Vector2Int(1920, 1080);
    [Tooltip("仮想スクリーンへの距離(Front/Leftが無い場合のフォールバック用)")]
    [SerializeField] private float screenDistance = 3f;
    [SerializeField] private float screenHeightWorld = 3f;
    [SerializeField] private float leftScreenWidthMultiplier = 1f;

    [Header("Smoothing")]
    [SerializeField] private float targetPointSmoothing = 0f;
    [SerializeField] private float surfaceSwitchHysteresis = 0.18f;
    [SerializeField] private bool autoCalibrateOnFirstPacket = true;
    [SerializeField] private bool useYawPitchRay = true;
    [SerializeField] private RayControlMode iPhoneRayControlMode = RayControlMode.ForwardVectorYawPitch;
    [SerializeField] private bool iPhoneInvertPitch;
    [SerializeField, HideInInspector] private bool migratedIPhoneInvertPitchDefault;
    [SerializeField] private bool iPhoneInvertYaw;
    [SerializeField] private bool iPhoneUseUpVectorForYaw = true;
    [SerializeField] private RotationAxisSource iPhoneHorizontalAxisSource = RotationAxisSource.Y;
    [SerializeField] private RotationAxisSource iPhoneVerticalAxisSource = RotationAxisSource.X;
    [SerializeField] private RayControlMode androidRayControlMode = RayControlMode.AxisMappedEuler;
    [SerializeField] private bool androidInvertPitch;
    [SerializeField] private bool androidInvertYaw;
    [SerializeField] private RotationAxisSource androidHorizontalAxisSource = RotationAxisSource.Z;
    [SerializeField] private RotationAxisSource androidVerticalAxisSource = RotationAxisSource.X;
    [Header("Aim Correction")]
    [Tooltip("立ち位置に応じた画面内ライト位置の補正。各面の右/上方向に対する正規化オフセット。")]
    [SerializeField] private Vector2 screenAimOffsetNormalized = Vector2.zero;
    [Tooltip("立ち位置に応じた感度補正。1,1 が等倍。中心基準で各面の横/縦の振れ幅を調整する。")]
    [SerializeField] private Vector2 screenAimScale = Vector2.one;
    [Header("Touch Calibration")]
    [Tooltip("ZigSimのYが画面下向き正のとき true（iOS標準）。ワールド上方向と合わせるため反転する。")]
    [SerializeField] private bool invertTouchY = true;

    private bool hasReference;
    private bool hasSmoothedTargetPoint;
    private ProjectionTargetSurface lastSurface = ProjectionTargetSurface.Front;
    private Vector2 calibrationOffset;

    public string CurrentSurfaceName { get; private set; } = "Front";
    public Vector3 CurrentTargetPoint { get; private set; }

    private void Reset()
    {
        receiver = GetComponent<UdpQuaternionReceiver>();
        sourceLight = GetComponentInChildren<Light>();
        iPhoneInvertPitch = DefaultIPhoneInvertPitch;
        migratedIPhoneInvertPitchDefault = true;
    }

    private void Awake()
    {
        MigrateIPhoneInvertPitchDefault();

        if (receiver == null)
        {
            receiver = GetComponent<UdpQuaternionReceiver>();
        }

        if (sourceLight == null)
        {
            sourceLight = GetComponentInChildren<Light>();
        }

    }

    private void Update()
    {
        UpdateSpotlightTarget();
    }

    public void Configure(UdpQuaternionReceiver receiverReference, Light lightSource)
    {
        receiver = receiverReference;
        sourceLight = lightSource;
        MigrateIPhoneInvertPitchDefault();
        hasReference = false;
    }

    public void ConfigureSurfaces(ProjectionSurface frontSurfaceReference, ProjectionSurface leftSurfaceReference)
    {
        frontSurface = frontSurfaceReference;
        leftSurface = leftSurfaceReference;
    }

    public void ResetReference()
    {
        hasReference = false;
        hasSmoothedTargetPoint = false;
        calibrationOffset = Vector2.zero;
    }

    public void SetCalibrationPoint(Vector2 normalizedTouchPosition)
    {
        // 後方互換用。直接 calibrationOffset をセットする旧 API。
        // 新しいコードは SetCalibrationFromTouch を使用すること。
        calibrationOffset = normalizedTouchPosition;
    }

    /// <summary>
    /// ZigSim の touch0 座標 ([-1,1] 範囲) とキャリブレーション前のスポットライト
    /// ターゲット位置からオフセットを計算してキャリブレーションを適用する。
    /// <para>
    /// タップ位置がワールド空間のどこに対応するかを算出し、現在のIMUターゲットとの
    /// 差分をサーフェス UV スケールで表現して <c>calibrationOffset</c> に保存する。
    /// </para>
    /// </summary>
    public void SetCalibrationFromTouch(Vector2 zigSimTouch, Vector3 previousTargetWorldPos)
    {
        ProjectionSurface activeSurface = GetActiveSurface();
        if (activeSurface == null)
        {
            // サーフェス情報がない場合はフォールバックとして旧来の直接代入
            calibrationOffset = zigSimTouch;
            return;
        }

        // ZigSim [-1,1] → サーフェス UV [0,1]
        float u = (zigSimTouch.x + 1f) * 0.5f;
        float v = invertTouchY
            ? 1f - (zigSimTouch.y + 1f) * 0.5f   // iOS: Y下向き正 → ワールド上方向へ反転
            : (zigSimTouch.y + 1f) * 0.5f;

        // タッチ位置をワールド座標に変換
        Vector3 touchWorldPos = activeSurface.transform.position
            + activeSurface.transform.right  * ((u - 0.5f) * activeSurface.Width)
            + activeSurface.transform.up     * ((v - 0.5f) * activeSurface.Height);

        // 現在のターゲットからタッチ位置へのベクトルを
        // サーフェス右・上軸に投影し、Width/Height で正規化してオフセットにする
        Vector3 diff = touchWorldPos - previousTargetWorldPos;
        calibrationOffset = new Vector2(
            Vector3.Dot(diff, activeSurface.transform.right) / Mathf.Max(0.001f, activeSurface.Width),
            Vector3.Dot(diff, activeSurface.transform.up)    / Mathf.Max(0.001f, activeSurface.Height));
    }

    private void OnValidate()
    {
        MigrateIPhoneInvertPitchDefault();
    }

    private void MigrateIPhoneInvertPitchDefault()
    {
        if (migratedIPhoneInvertPitchDefault)
        {
            return;
        }

        iPhoneInvertPitch = DefaultIPhoneInvertPitch;
        migratedIPhoneInvertPitchDefault = true;
    }

    private void ResolveActiveRaySettings(
        out RayControlMode rayControlMode,
        out bool invertActivePitch,
        out bool invertActiveYaw,
        out RotationAxisSource horizontalAxisSource,
        out RotationAxisSource verticalAxisSource)
    {
        bool isAndroid = receiver != null && receiver.CoordinatePreset == QuaternionCoordinatePreset.AndroidRotationVector;
        rayControlMode = isAndroid ? androidRayControlMode : iPhoneRayControlMode;
        invertActivePitch = isAndroid ? androidInvertPitch : iPhoneInvertPitch;
        invertActiveYaw = isAndroid ? androidInvertYaw : iPhoneInvertYaw;
        horizontalAxisSource = isAndroid ? androidHorizontalAxisSource : iPhoneHorizontalAxisSource;
        verticalAxisSource = isAndroid ? androidVerticalAxisSource : iPhoneVerticalAxisSource;
    }

    private void UpdateSpotlightTarget()
    {
        if (sourceLight == null)
        {
            return;
        }

        if (receiver == null)
        {
            sourceLight.enabled = false;
            return;
        }

        if (receiver.ReceivedPacketCount <= 0)
        {
            sourceLight.enabled = false;
            return;
        }

        sourceLight.enabled = true;

        if (autoCalibrateOnFirstPacket && !hasReference)
        {
            hasReference = true;
        }

        Transform pointerTransform = sourceLight.transform.parent;
        Vector3 baseRayOrigin = pointerTransform != null ? pointerTransform.position : transform.position;
        Vector3 rayOrigin = GetCalculatedStandingPosition(baseRayOrigin);

        Vector3 rayDirection = pointerTransform != null
            ? GetRayDirection(pointerTransform)
            : sourceLight.transform.forward;
        rayDirection = rayDirection.normalized;

        if (rayDirection.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        Vector3 worldTargetPoint;
        ProjectionTargetSurface surface;
        ResolveTargetPoint(rayOrigin, rayDirection, out surface, out worldTargetPoint);
        ProjectionSurface activeSurface = surface == ProjectionTargetSurface.Left ? leftSurface : frontSurface;
        worldTargetPoint = ApplySurfaceAimCorrection(activeSurface, worldTargetPoint);

        if (calibrationOffset.sqrMagnitude > 0.00001f)
        {
            if (activeSurface != null)
            {
                worldTargetPoint += activeSurface.transform.right * (calibrationOffset.x * activeSurface.Width);
                worldTargetPoint += activeSurface.transform.up * (calibrationOffset.y * activeSurface.Height);
            }
        }

        CurrentTargetPoint = worldTargetPoint;
        hasSmoothedTargetPoint = true;

        CurrentSurfaceName = surface.ToString();
        lastSurface = surface;
        Vector3 targetDirection = CurrentTargetPoint - sourceLight.transform.position;
        
        // エディタのSceneビューでレイが見えるように可視化線を描画
        Debug.DrawLine(rayOrigin, CurrentTargetPoint, Color.red);

        if (targetDirection.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        sourceLight.transform.rotation = Quaternion.LookRotation(targetDirection.normalized, Vector3.up);
    }

    private Vector3 ApplySurfaceAimCorrection(ProjectionSurface surface, Vector3 worldTargetPoint)
    {
        if (surface == null)
        {
            return worldTargetPoint;
        }

        Vector3 local = surface.transform.InverseTransformPoint(worldTargetPoint);
        float normalizedX = local.x / Mathf.Max(0.0001f, surface.Width);
        float normalizedY = local.y / Mathf.Max(0.0001f, surface.Height);

        Vector2 clampedScale = new Vector2(
            Mathf.Max(0.01f, screenAimScale.x),
            Mathf.Max(0.01f, screenAimScale.y));

        normalizedX = (normalizedX * clampedScale.x) + screenAimOffsetNormalized.x;
        normalizedY = (normalizedY * clampedScale.y) + screenAimOffsetNormalized.y;

        // クランプ処理を完全に削除（画面外へそのままプロジェクションさせる）
        
        local.x = normalizedX * surface.Width;
        local.y = normalizedY * surface.Height;
        local.z = 0f;
        return surface.transform.TransformPoint(local);
    }

    private Vector3 GetRayDirection(Transform pointerTransform)
    {
        if (pointerTransform == null)
        {
            return sourceLight != null ? sourceLight.transform.forward : transform.forward;
        }

        // Use the rotation pivot's forward directly.
        // PoseRotationDriver already applies the calibrated relative rotation
        // to the pivot, so its forward vector faithfully represents the
        // physical pointing direction for both the front and left surfaces.
        return pointerTransform.forward;
    }

    private static float GetSignedAxisAngle(Quaternion localRotation, RotationAxisSource axisSource)
    {
        Vector3 euler = localRotation.eulerAngles;

        switch (axisSource)
        {
            case RotationAxisSource.X:
                return Mathf.DeltaAngle(0f, euler.x);
            case RotationAxisSource.Y:
                return Mathf.DeltaAngle(0f, euler.y);
            case RotationAxisSource.Z:
                return Mathf.DeltaAngle(0f, euler.z);
            default:
                return 0f;
        }
    }

    private void ResolveTargetPoint(Vector3 rayOrigin, Vector3 rayDirection, out ProjectionTargetSurface surface, out Vector3 worldTargetPoint)
    {
        // クランプや内側判定（スコア計算）を完全に削除。
        // 純粋にレイが向いている方向（XマイナスならLeft面、プラスならFront面）だけで平面を決定し、無限平面として交差させる。
        // これにより、境界での振動（フィードバックループ）や画面内への強制押し戻しが完全に解消されます。
        surface = rayDirection.x < 0f ? ProjectionTargetSurface.Left : ProjectionTargetSurface.Front;
        worldTargetPoint = surface == ProjectionTargetSurface.Left
            ? GetFallbackSurfacePoint(leftSurface, rayOrigin, rayDirection)
            : GetFallbackSurfacePoint(frontSurface, rayOrigin, rayDirection);
    }

    /// <summary>現在スポットライトが向いているサーフェスを返す。</summary>
    private ProjectionSurface GetActiveSurface()
    {
        return lastSurface == ProjectionTargetSurface.Left ? leftSurface : frontSurface;
    }

    private Vector3 GetCalculatedStandingPosition(Vector3 defaultPos)
    {
        if (!simulateStandingPosition)
        {
            return defaultPos;
        }

        float width = frontSurface != null ? frontSurface.Width : 3f;
        float dist = width * standingDistanceRatio;

        // Visualizerの基準位置(defaultPos)＝「画面の角」であると仮定し、
        // 右方向(+X)と後ろ方向(-Z)に等距離 dist 分だけ下がった位置を仮想的な立ち位置とする。
        // さらにインスペクタから manualOffset で微調整可能にする。
        Vector3 simulatedPos = defaultPos + transform.right * dist - transform.forward * dist;
        return simulatedPos + transform.TransformDirection(simulatedOriginOffset);
    }

    private Vector3 GetFallbackSurfacePoint(ProjectionSurface surface, Vector3 rayOrigin, Vector3 rayDirection)
    {
        if (surface != null)
        {
            Vector3 normal = surface.transform.forward;
            Vector3 planePos = surface.transform.position;
            float denom = Vector3.Dot(normal, rayDirection);
            
            // UnityのPlane.Raycastは面の「表裏」に依存するため、数学的な直線・平面交差判定で確実に衝突点を取得します。
            if (Mathf.Abs(denom) > 0.0001f)
            {
                float t = Vector3.Dot(planePos - rayOrigin, normal) / denom;
                if (t > 0f)
                {
                    return rayOrigin + rayDirection * t;
                }
            }
            return rayOrigin + rayDirection * 100f;
        }

        float aspect = (float)referenceResolution.x / Mathf.Max(1f, referenceResolution.y);
        float screenWidthWorld = screenHeightWorld * aspect;
        
        // ここも振動対策のためオリジナルの rayDirection.x に戻す
        bool isLeft = rayDirection.x < 0f; 

        Vector3 planeCenter;
        Vector3 planeNormal;

        if (isLeft)
        {
            planeCenter = transform.position - Vector3.right * screenDistance;
            planeNormal = Vector3.right; 
        }
        else
        {
            planeCenter = transform.position + Vector3.forward * screenDistance;
            planeNormal = -Vector3.forward; 
        }

        Plane virtualPlane = new Plane(planeNormal, planeCenter);
        float denomFallback = Vector3.Dot(planeNormal, rayDirection);
        
        if (Mathf.Abs(denomFallback) > 0.0001f)
        {
            float t = Vector3.Dot(planeCenter - rayOrigin, planeNormal) / denomFallback;
            if (t > 0f)
            {
                return rayOrigin + rayDirection * t;
            }
        }

        return planeCenter;
    }
}
