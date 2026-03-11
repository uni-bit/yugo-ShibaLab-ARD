using UnityEngine;

[AddComponentMenu("Pose/Test Screen Visualizer")]
public class TestScreenVisualizer : MonoBehaviour
{
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
    [SerializeField] private float targetPointSmoothing = 18f;
    [SerializeField] private float surfaceSwitchHysteresis = 0.18f;
    [SerializeField] private bool autoCalibrateOnFirstPacket = true;
    [SerializeField] private KeyCode recenterKey = KeyCode.C;
    [SerializeField] private bool useYawPitchRay = true;
    [SerializeField] private RayControlMode iPhoneRayControlMode = RayControlMode.ForwardVectorYawPitch;
    [SerializeField] private bool iPhoneInvertPitch = true;
    [SerializeField] private bool iPhoneInvertYaw;
    [SerializeField] private bool iPhoneUseUpVectorForYaw = true;
    [SerializeField] private RotationAxisSource iPhoneHorizontalAxisSource = RotationAxisSource.Y;
    [SerializeField] private RotationAxisSource iPhoneVerticalAxisSource = RotationAxisSource.X;
    [SerializeField] private RayControlMode androidRayControlMode = RayControlMode.AxisMappedEuler;
    [SerializeField] private bool androidInvertPitch;
    [SerializeField] private bool androidInvertYaw;
    [SerializeField] private RotationAxisSource androidHorizontalAxisSource = RotationAxisSource.Z;
    [SerializeField] private RotationAxisSource androidVerticalAxisSource = RotationAxisSource.X;

    private bool hasReference;
    private int lastHandledRecenterRequestCount;
    private bool hasSmoothedTargetPoint;
    private ProjectionTargetSurface lastSurface = ProjectionTargetSurface.Front;

    public string CurrentSurfaceName { get; private set; } = "Front";
    public Vector3 CurrentTargetPoint { get; private set; }

    private void Reset()
    {
        receiver = GetComponent<UdpQuaternionReceiver>();
        sourceLight = GetComponentInChildren<Light>();
    }

    private void Awake()
    {
        if (receiver == null)
        {
            receiver = GetComponent<UdpQuaternionReceiver>();
        }

        if (sourceLight == null)
        {
            sourceLight = GetComponentInChildren<Light>();
        }

        lastHandledRecenterRequestCount = receiver != null ? receiver.RecenterRequestCount : 0;
    }

    private void Update()
    {
        if (Input.GetKeyDown(recenterKey))
        {
            ResetReference();
        }

        if (receiver != null && receiver.RecenterRequestCount != lastHandledRecenterRequestCount)
        {
            lastHandledRecenterRequestCount = receiver.RecenterRequestCount;
            ResetReference();
        }

        UpdateSpotlightTarget();
    }

    public void Configure(UdpQuaternionReceiver receiverReference, Light lightSource)
    {
        receiver = receiverReference;
        sourceLight = lightSource;
        hasReference = false;
        lastHandledRecenterRequestCount = receiver != null ? receiver.RecenterRequestCount : 0;
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

        float blend = targetPointSmoothing <= 0f ? 1f : 1f - Mathf.Exp(-targetPointSmoothing * Time.deltaTime);
        if (!hasSmoothedTargetPoint)
        {
            CurrentTargetPoint = worldTargetPoint;
            hasSmoothedTargetPoint = true;
        }
        else
        {
            CurrentTargetPoint = Vector3.Lerp(CurrentTargetPoint, worldTargetPoint, blend);
        }

        CurrentSurfaceName = surface.ToString();
        lastSurface = surface;
        Vector3 targetDirection = CurrentTargetPoint - sourceLight.transform.position;

        if (targetDirection.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        sourceLight.transform.rotation = Quaternion.LookRotation(targetDirection.normalized, Vector3.up);
    }

    private Vector3 GetRayDirection(Transform pointerTransform)
    {
        if (pointerTransform == null)
        {
            return sourceLight != null ? sourceLight.transform.forward : transform.forward;
        }

        if (receiver != null && receiver.CoordinatePreset == QuaternionCoordinatePreset.IPhoneCoreMotion)
        {
            Transform iPhoneBasisTransform = pointerTransform.parent != null ? pointerTransform.parent : transform;
            Vector3 localDirection = iPhoneBasisTransform != null
                ? iPhoneBasisTransform.InverseTransformDirection(pointerTransform.forward)
                : pointerTransform.forward;

            if (iPhoneInvertPitch)
            {
                localDirection.y = -localDirection.y;
            }

            if (iPhoneInvertYaw)
            {
                localDirection.x = -localDirection.x;
            }

            if (localDirection.sqrMagnitude <= 0.000001f)
            {
                return pointerTransform.forward;
            }

            localDirection.Normalize();
            return (iPhoneBasisTransform != null ? iPhoneBasisTransform.rotation : Quaternion.identity) * localDirection;
        }

        if (!useYawPitchRay)
        {
            return pointerTransform.forward;
        }

        Transform basisTransform = pointerTransform.parent != null ? pointerTransform.parent : transform;
        Quaternion localRotation = basisTransform != null
            ? Quaternion.Inverse(basisTransform.rotation) * pointerTransform.rotation
            : pointerTransform.localRotation;

        ResolveActiveRaySettings(
            out RayControlMode rayControlMode,
            out bool shouldInvertPitch,
            out bool shouldInvertYaw,
            out RotationAxisSource horizontalAxisSource,
            out RotationAxisSource verticalAxisSource);

        float yaw;
        float pitch;

        if (rayControlMode == RayControlMode.ForwardVectorYawPitch)
        {
            Vector3 localForward = basisTransform != null
                ? basisTransform.InverseTransformDirection(pointerTransform.forward)
                : pointerTransform.localRotation * Vector3.forward;

            if (localForward.sqrMagnitude <= 0.000001f)
            {
                return pointerTransform.forward;
            }

            localForward.Normalize();

            if (receiver != null
                && receiver.CoordinatePreset == QuaternionCoordinatePreset.IPhoneCoreMotion
                && iPhoneUseUpVectorForYaw)
            {
                Vector3 localUp = basisTransform != null
                    ? basisTransform.InverseTransformDirection(pointerTransform.up)
                    : pointerTransform.localRotation * Vector3.up;

                localUp.Normalize();
                yaw = Mathf.Atan2(localUp.x, localUp.y) * Mathf.Rad2Deg;
            }
            else
            {
                yaw = Mathf.Atan2(localForward.x, localForward.z) * Mathf.Rad2Deg;
            }

            pitch = Mathf.Atan2(localForward.y, Mathf.Sqrt((localForward.x * localForward.x) + (localForward.z * localForward.z))) * Mathf.Rad2Deg;
        }
        else
        {
            yaw = GetSignedAxisAngle(localRotation, horizontalAxisSource);
            pitch = GetSignedAxisAngle(localRotation, verticalAxisSource);
        }

        if (shouldInvertPitch)
        {
            pitch = -pitch;
        }

        if (shouldInvertYaw)
        {
            yaw = -yaw;
        }

        Quaternion yawPitchRotation = Quaternion.Euler(pitch, yaw, 0f);
        return (basisTransform != null ? basisTransform.rotation : Quaternion.identity) * (yawPitchRotation * Vector3.forward);
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
        local.x = Mathf.Clamp(local.x, -halfWidth, halfWidth);
        local.y = Mathf.Clamp(local.y, -halfHeight, halfHeight);
        local.z = 0f;
        hitPoint = surface.transform.TransformPoint(local);
        return true;
    }

    private Vector3 GetFallbackSurfacePoint(ProjectionSurface surface, Vector3 rayDirection)
    {
        if (surface != null)
        {
            Vector3 localDirection = surface.transform.InverseTransformDirection(rayDirection.normalized);
            float normalizedX = Mathf.Clamp01((localDirection.x * 0.5f) + 0.5f);
            float normalizedY = Mathf.Clamp01((localDirection.y * 0.5f) + 0.5f);
            return surface.GetPoint(normalizedX, normalizedY);
        }

        float aspect = (float)referenceResolution.x / referenceResolution.y;
        float screenWidthWorld = screenHeightWorld * aspect;
        float halfHeight = screenHeightWorld * 0.5f;

        if (rayDirection.x < 0f)
        {
            return transform.TransformPoint(new Vector3(-screenWidthWorld * 0.5f, Mathf.Clamp(rayDirection.y, -1f, 1f) * halfHeight, screenDistance));
        }

        return transform.TransformPoint(new Vector3(Mathf.Clamp(rayDirection.x, -1f, 1f) * screenWidthWorld * 0.5f, Mathf.Clamp(rayDirection.y, -1f, 1f) * halfHeight, screenDistance));
    }
}
