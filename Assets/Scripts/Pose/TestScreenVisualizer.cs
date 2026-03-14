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
    [SerializeField] private Vector2Int referenceResolution = new Vector2Int(1920, 1080);
    [SerializeField] private float screenDistance = 3f;
    [SerializeField] private float screenHeightWorld = 3f;
    [SerializeField] private float leftScreenWidthMultiplier = 1f;
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
        Vector3 rayOrigin = pointerTransform != null ? pointerTransform.position : transform.position;
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

        normalizedX = Mathf.Clamp(normalizedX, -0.5f, 0.5f);
        normalizedY = Mathf.Clamp(normalizedY, -0.5f, 0.5f);

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
        bool hasFrontCandidate = TryGetClosestPointOnSurfaceForRay(
            rayOrigin,
            rayDirection,
            frontSurface,
            out Vector3 frontPoint,
            out float frontScore);

        bool hasLeftCandidate = TryGetClosestPointOnSurfaceForRay(
            rayOrigin,
            rayDirection,
            leftSurface,
            out Vector3 leftPoint,
            out float leftScore);

        if (hasFrontCandidate && (!hasLeftCandidate || frontScore <= leftScore))
        {
            surface = ProjectionTargetSurface.Front;
            worldTargetPoint = frontPoint;
            return;
        }

        if (hasLeftCandidate)
        {
            surface = ProjectionTargetSurface.Left;
            worldTargetPoint = leftPoint;
            return;
        }

        surface = rayDirection.x < 0f ? ProjectionTargetSurface.Left : ProjectionTargetSurface.Front;
        worldTargetPoint = surface == ProjectionTargetSurface.Left
            ? GetFallbackSurfacePoint(leftSurface, rayDirection)
            : GetFallbackSurfacePoint(frontSurface, rayDirection);
    }

    private bool TryGetClosestPointOnSurfaceForRay(
        Vector3 rayOrigin,
        Vector3 rayDirection,
        ProjectionSurface surface,
        out Vector3 closestPoint,
        out float score)
    {
        closestPoint = Vector3.zero;
        score = float.PositiveInfinity;

        if (surface == null)
        {
            return false;
        }

        bool hasPlaneHit = TryIntersectSurface(rayOrigin, rayDirection, surface, out Vector3 surfacePoint, out _, out bool isInsideSurface);
        closestPoint = hasPlaneHit ? surfacePoint : GetFallbackSurfacePoint(surface, rayDirection);

        Vector3 normalizedDirection = rayDirection.normalized;
        Vector3 toPoint = closestPoint - rayOrigin;
        float alongRay = Vector3.Dot(toPoint, normalizedDirection);
        Vector3 closestPointOnRay = rayOrigin + normalizedDirection * Mathf.Max(0f, alongRay);
        float perpendicularDistanceSqr = (closestPoint - closestPointOnRay).sqrMagnitude;
        float behindPenalty = alongRay < 0f ? (100f + (-alongRay * 10f)) : 0f;
        float outsidePenalty = hasPlaneHit && !isInsideSurface ? 0.01f : 0f;

        score = perpendicularDistanceSqr + behindPenalty + outsidePenalty;
        return true;
    }

    private static bool TryIntersectSurface(Vector3 rayOrigin, Vector3 rayDirection, ProjectionSurface surface, out Vector3 hitPoint, out float hitDistance, out bool isInsideSurface)
    {
        hitPoint = Vector3.zero;
        hitDistance = float.PositiveInfinity;
        isInsideSurface = false;

        if (surface == null)
        {
            return false;
        }

        Plane plane = new Plane(surface.transform.forward, surface.transform.position);
        Ray ray = new Ray(rayOrigin, rayDirection);
        if (!plane.Raycast(ray, out hitDistance) || hitDistance <= 0f)
        {
            return false;
        }

        Vector3 planeHit = ray.GetPoint(hitDistance);
        Vector3 local = surface.transform.InverseTransformPoint(planeHit);
        float halfWidth = surface.Width * 0.5f;
        float halfHeight = surface.Height * 0.5f;
        isInsideSurface = Mathf.Abs(local.x) <= halfWidth && Mathf.Abs(local.y) <= halfHeight;
        local.z = 0f;
        hitPoint = surface.transform.TransformPoint(local);
        return true;
    }

    /// <summary>現在スポットライトが向いているサーフェスを返す。</summary>
    private ProjectionSurface GetActiveSurface()
    {
        return lastSurface == ProjectionTargetSurface.Left ? leftSurface : frontSurface;
    }

    private Vector3 GetFallbackSurfacePoint(ProjectionSurface surface, Vector3 rayDirection)    {
        if (surface != null)
        {
            Vector3 localDirection = surface.transform.InverseTransformDirection(rayDirection.normalized);
            float nx = (localDirection.x * 0.5f) + 0.5f;
            float ny = (localDirection.y * 0.5f) + 0.5f;
            return surface.transform.position
                + surface.transform.right * ((nx - 0.5f) * surface.Width)
                + surface.transform.up * ((ny - 0.5f) * surface.Height);
        }

        float aspect = (float)referenceResolution.x / referenceResolution.y;
        float screenWidthWorld = screenHeightWorld * aspect;
        float halfHeight = screenHeightWorld * 0.5f;

        if (rayDirection.x < 0f)
        {
            return transform.TransformPoint(new Vector3(-screenWidthWorld * 0.5f, rayDirection.y * halfHeight, screenDistance));
        }

        return transform.TransformPoint(new Vector3(rayDirection.x * screenWidthWorld * 0.5f, rayDirection.y * halfHeight, screenDistance));
    }
}
