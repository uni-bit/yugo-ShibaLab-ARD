using UnityEngine;

[AddComponentMenu("Pose/Pose Test Bootstrap")]
public class PoseTestBootstrap : MonoBehaviour
{
    private const string RigRootName = "Pose Rig";
    private const string ViewerOriginName = "Viewer Origin";
    private const float ScreenDistance = 3f;
    private const float ScreenHeightWorld = 3f;
    private const float ViewerHeight = 0.4f;
    private const float FrontScreenWidth = ScreenHeightWorld * (1920f / 1080f);

    [SerializeField] private bool buildOnStart = true;
    private bool hasBuilt;

    private void Start()
    {
        if (!Application.isPlaying || !buildOnStart || hasBuilt)
        {
            return;
        }

        BuildDemo();
    }

    [ContextMenu("Build Demo")]
    public void BuildDemo()
    {
        ClearExistingRig();
        Screen.SetResolution(1920, 1080, false);
        RenderSettings.ambientLight = Color.black;
        SetupDisplays();
        ProjectionSurface frontSurface;
        ProjectionSurface leftSurface;
        Transform viewerOrigin;
        SetupDemoRig(out frontSurface, out leftSurface, out viewerOrigin);
        SetupCameras(frontSurface, leftSurface, viewerOrigin);
        hasBuilt = true;
    }
 
    private void SetupDisplays()
    {
        if (Display.displays.Length > 1)
        {
            Display.displays[1].Activate();
        }
    }

    private void SetupCameras(ProjectionSurface frontSurface, ProjectionSurface leftSurface, Transform viewerOrigin)
    {
        Camera frontCamera = EnsureCamera("Main Camera", "MainCamera");
        ConfigureCamera(frontCamera, 0, 40f, frontSurface, viewerOrigin, false, false);

        Camera leftCamera = EnsureCamera("Left Projection Camera", null);
        ConfigureCamera(leftCamera, 1, 40f, leftSurface, viewerOrigin, false, false);
    }

    private static Camera EnsureCamera(string cameraName, string tagName)
    {
        GameObject cameraObject = GameObject.Find(cameraName);
        Camera sceneCamera = cameraObject != null ? cameraObject.GetComponent<Camera>() : null;

        if (sceneCamera == null)
        {
            cameraObject = new GameObject(cameraName);
            sceneCamera = cameraObject.AddComponent<Camera>();
        }

        if (!string.IsNullOrEmpty(tagName))
        {
            cameraObject.tag = tagName;
        }

        return sceneCamera;
    }

    private static void ConfigureCamera(Camera sceneCamera, int targetDisplay, float fieldOfView, ProjectionSurface surface, Transform eyePoint, bool flipHorizontally, bool flipVertically)
    {
        sceneCamera.transform.position = eyePoint != null ? eyePoint.position : Vector3.zero;
        sceneCamera.transform.rotation = eyePoint != null ? eyePoint.rotation : Quaternion.identity;
        sceneCamera.clearFlags = CameraClearFlags.SolidColor;
        sceneCamera.backgroundColor = Color.black;
        sceneCamera.fieldOfView = fieldOfView;
        sceneCamera.aspect = 1920f / 1080f;
        sceneCamera.targetDisplay = targetDisplay;
        sceneCamera.rect = new Rect(0f, 0f, 1f, 1f);

        OffAxisProjectionCamera offAxisCamera = sceneCamera.GetComponent<OffAxisProjectionCamera>();
        if (offAxisCamera == null)
        {
            offAxisCamera = sceneCamera.gameObject.AddComponent<OffAxisProjectionCamera>();
        }

        offAxisCamera.Configure(surface, eyePoint, flipHorizontally, flipVertically);
    }

    private void SetupDemoRig(out ProjectionSurface frontSurface, out ProjectionSurface leftSurface, out Transform viewerOrigin)
    {
        GameObject rigRoot = new GameObject(RigRootName);
        rigRoot.transform.SetParent(transform, false);
        rigRoot.transform.position = Vector3.zero;

        frontSurface = CreateFrontSurface(rigRoot.transform);
        leftSurface = CreateLeftSurface(rigRoot.transform, frontSurface.Width);
        viewerOrigin = CreateViewerOrigin(rigRoot.transform);

        GameObject rotationPivot = new GameObject("Rotation Pivot");
        rotationPivot.transform.SetParent(rigRoot.transform, false);
        rotationPivot.transform.localPosition = Vector3.zero;
        rotationPivot.transform.localRotation = Quaternion.identity;

        UdpQuaternionReceiver receiver = rigRoot.AddComponent<UdpQuaternionReceiver>();
        PoseRotationDriver driver = rigRoot.AddComponent<PoseRotationDriver>();
        rigRoot.AddComponent<PoseDebugOverlay>();
        TestScreenVisualizer visualizer = rigRoot.AddComponent<TestScreenVisualizer>();

        CreatePointerModel(rotationPivot.transform);

        GameObject tipLightObject = new GameObject("Tip Light");
        tipLightObject.transform.SetParent(rotationPivot.transform, false);
        tipLightObject.transform.localPosition = new Vector3(0f, 0f, 0.72f);
        tipLightObject.transform.localRotation = Quaternion.identity;

        Light tipLight = tipLightObject.AddComponent<Light>();
        tipLight.type = LightType.Spot;
        tipLight.spotAngle = 10f;
        tipLight.range = 20f;
        tipLight.intensity = 16f;
        tipLight.color = Color.white;

        driver.Configure(receiver, rotationPivot.transform, tipLight, 0.72f);
        driver.SetTipLightAlignment(false);
        visualizer.Configure(receiver, tipLight);
        visualizer.ConfigureSurfaces(frontSurface, leftSurface);
    }

    private static Transform CreateViewerOrigin(Transform parent)
    {
        GameObject viewerObject = new GameObject(ViewerOriginName);
        viewerObject.transform.SetParent(parent, false);
        viewerObject.transform.localPosition = new Vector3(0f, ViewerHeight, 0f);
        viewerObject.transform.localRotation = Quaternion.identity;
        return viewerObject.transform;
    }

    private static ProjectionSurface CreateFrontSurface(Transform parent)
    {
        GameObject frontSurfaceObject = new GameObject("Front Surface");
        frontSurfaceObject.name = "Front Surface";
        frontSurfaceObject.transform.SetParent(parent, false);
        frontSurfaceObject.transform.localPosition = new Vector3(0f, 0f, ScreenDistance);
        frontSurfaceObject.transform.localRotation = Quaternion.identity;

        ProjectionSurface surface = frontSurfaceObject.AddComponent<ProjectionSurface>();
        surface.Configure(FrontScreenWidth, ScreenHeightWorld);
        return surface;
    }

    private static ProjectionSurface CreateLeftSurface(Transform parent, float frontSurfaceWidth)
    {
        GameObject leftSurfaceObject = new GameObject("Left Surface");
        leftSurfaceObject.name = "Left Surface";
        leftSurfaceObject.transform.SetParent(parent, false);
        leftSurfaceObject.transform.localPosition = new Vector3(-frontSurfaceWidth * 0.5f, 0f, ScreenDistance - frontSurfaceWidth * 0.5f);
        leftSurfaceObject.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);

        ProjectionSurface surface = leftSurfaceObject.AddComponent<ProjectionSurface>();
        surface.Configure(FrontScreenWidth, ScreenHeightWorld);
        return surface;
    }

    private void ClearExistingRig()
    {
        for (int index = transform.childCount - 1; index >= 0; index--)
        {
            Transform child = transform.GetChild(index);
            if (child.name != RigRootName)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private static void CreatePointerModel(Transform parent)
    {
        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shaft.name = "Pointer Shaft";
        shaft.transform.SetParent(parent, false);
        shaft.transform.localPosition = new Vector3(0f, 0f, 0.32f);
        shaft.transform.localScale = new Vector3(0.08f, 0.08f, 0.64f);
        SetRendererColor(shaft, new Color(0.85f, 0.85f, 0.85f, 1f));

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Pointer Head";
        head.transform.SetParent(parent, false);
        head.transform.localPosition = new Vector3(0f, 0f, 0.68f);
        head.transform.localScale = Vector3.one * 0.14f;
        SetRendererColor(head, Color.white);

        GameObject tail = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        tail.name = "Pointer Tail";
        tail.transform.SetParent(parent, false);
        tail.transform.localPosition = new Vector3(0f, 0f, 0f);
        tail.transform.localScale = Vector3.one * 0.18f;
        SetRendererColor(tail, new Color(0.3f, 0.8f, 1f, 1f));
    }

    private static void SetRendererColor(GameObject target, Color color)
    {
        MeshRenderer renderer = target.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.material.color = color;
        }
    }
}
