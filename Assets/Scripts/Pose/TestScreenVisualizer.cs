using UnityEngine;

[AddComponentMenu("Pose/Test Screen Visualizer")]
public class TestScreenVisualizer : MonoBehaviour
{
    private enum ProjectionTargetSurface
    {
        Front,
        Left
    }

    [SerializeField] private UdpQuaternionReceiver receiver;
    [SerializeField] private Light sourceLight;
    [SerializeField] private ProjectionSurface frontSurface;
    [SerializeField] private ProjectionSurface leftSurface;
    [SerializeField] private Vector2Int referenceResolution = new Vector2Int(1920, 1080);
    [SerializeField] private float screenDistance = 3f;
    [SerializeField] private float screenHeightWorld = 3f;
    [SerializeField] private float leftScreenWidthMultiplier = 1f;
    [SerializeField] private float zRangeForFullWidth = 0.35f;
    [SerializeField] private float xRangeForFullHeight = 0.35f;
    [SerializeField] private bool autoCalibrateOnFirstPacket = true;
    [SerializeField] private KeyCode recenterKey = KeyCode.C;

    private Quaternion referenceRawQuaternion = Quaternion.identity;
    private bool hasReference;
    private int lastHandledRecenterRequestCount;

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

        Quaternion rawQuaternion = receiver.LatestRawRotation;
        if (receiver.ReceivedPacketCount <= 0)
        {
            sourceLight.enabled = false;
            return;
        }

        sourceLight.enabled = true;

        if (autoCalibrateOnFirstPacket && !hasReference)
        {
            referenceRawQuaternion = rawQuaternion;
            hasReference = true;
        }

        Quaternion reference = hasReference ? referenceRawQuaternion : Quaternion.identity;

        float deltaZ = rawQuaternion.z - reference.z;
        float deltaX = rawQuaternion.x - reference.x;

        float normalizedX = Mathf.Clamp(-deltaZ / Mathf.Max(0.0001f, zRangeForFullWidth), -1f, 1f);
        float normalizedY = Mathf.Clamp(deltaX / Mathf.Max(0.0001f, xRangeForFullHeight), -1f, 1f);

        float aspect = (float)referenceResolution.x / referenceResolution.y;
        float screenWidthWorld = frontSurface != null ? frontSurface.Width : screenHeightWorld * aspect;
        float activeScreenHeight = frontSurface != null ? frontSurface.Height : screenHeightWorld;
        float leftScreenWidth = leftSurface != null
            ? leftSurface.Width
            : screenWidthWorld * Mathf.Max(0f, leftScreenWidthMultiplier);
        float wrappedHorizontal = Mathf.Lerp(-leftScreenWidth, screenWidthWorld, (normalizedX + 1f) * 0.5f);
        float normalizedVertical = Mathf.Clamp01((normalizedY + 1f) * 0.5f);
        float vertical = normalizedY * activeScreenHeight * 0.5f;

        Vector3 worldTargetPoint;
        ProjectionTargetSurface surface;

        if (wrappedHorizontal >= 0f)
        {
            surface = ProjectionTargetSurface.Front;

            if (frontSurface != null)
            {
                float frontU = Mathf.Clamp01(wrappedHorizontal / Mathf.Max(0.0001f, screenWidthWorld));
                worldTargetPoint = frontSurface.GetPoint(frontU, normalizedVertical);
            }
            else
            {
                Vector3 localTargetPoint = new Vector3(
                    (-screenWidthWorld * 0.5f) + wrappedHorizontal,
                    vertical,
                    screenDistance);
                worldTargetPoint = transform.TransformPoint(localTargetPoint);
            }
        }
        else
        {
            surface = ProjectionTargetSurface.Left;

            if (leftSurface != null)
            {
                float leftU = Mathf.Clamp01((wrappedHorizontal + leftScreenWidth) / Mathf.Max(0.0001f, leftScreenWidth));
                worldTargetPoint = leftSurface.GetPoint(leftU, normalizedVertical);
            }
            else
            {
                Vector3 localTargetPoint = new Vector3(
                    -screenWidthWorld * 0.5f,
                    vertical,
                    screenDistance + wrappedHorizontal);
                worldTargetPoint = transform.TransformPoint(localTargetPoint);
            }
        }

        Vector3 targetDirection = worldTargetPoint - sourceLight.transform.position;

        if (targetDirection.sqrMagnitude <= 0.000001f)
        {
            return; 
        }

        CurrentSurfaceName = surface.ToString();
        CurrentTargetPoint = worldTargetPoint;
        sourceLight.transform.rotation = Quaternion.LookRotation(targetDirection.normalized, Vector3.up);
    }
}
