using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[RequireComponent(typeof(UdpQuaternionReceiver))]
[RequireComponent(typeof(PoseCalibrationCoordinator))]
[RequireComponent(typeof(PoseRotationDriver))]
[RequireComponent(typeof(PoseDebugOverlay))]
[RequireComponent(typeof(TestScreenVisualizer))]
[AddComponentMenu("Pose/Pose Test Bootstrap")]
public class PoseTestBootstrap : MonoBehaviour
{
    private const string RigRootName = "Pose Rig";
    private const string ViewerOriginName = "Viewer Origin";
    private const string RotationPivotName = "Rotation Pivot";
    private static readonly string[] PointerVisualNames = { "Pointer Shaft", "Pointer Head", "Pointer Tail" };
    private const float ScreenDistance = 3f;
    private const float ScreenHeightWorld = 3f;
    private const float ViewerHeight = 0.4f;
    private const float FrontScreenWidth = ScreenHeightWorld * (1920f / 1080f);
    private const int WindowedDefaultWidth = 1920;
    private const int WindowedDefaultHeight = 1080;

    [SerializeField] private bool buildOnStart = true;
    [SerializeField] private bool buildPreviewInEditMode = true;
    [SerializeField] private bool forceBlackEnvironment = true;
    [SerializeField] private bool disableOtherSceneLights = true;
    [SerializeField] private bool forceBlackEnvironmentInEditMode;
    [SerializeField] private bool disableOtherSceneLightsInEditMode;
    [SerializeField] private bool showDebugPointerVisuals;
    [SerializeField] private bool startInFullscreen = true;
    [SerializeField] private FullScreenMode fullscreenMode = FullScreenMode.FullScreenWindow;
    [SerializeField] private bool allowFullscreenToggle = true;
    [SerializeField] private KeyCode fullscreenToggleKey = KeyCode.F11;
    [SerializeField] private int windowedWidth = WindowedDefaultWidth;
    [SerializeField] private int windowedHeight = WindowedDefaultHeight;
    private bool hasBuilt;
    private bool editorPreviewQueued;

    private struct SceneLightState
    {
        public Light Light;
        public bool WasEnabled;
    }

    private readonly System.Collections.Generic.List<SceneLightState> managedSceneLights = new System.Collections.Generic.List<SceneLightState>();
    private static readonly int StageSpotlightPositionId = Shader.PropertyToID("_StageSpotlightPosition");
    private static readonly int StageSpotlightDirectionId = Shader.PropertyToID("_StageSpotlightDirection");
    private static readonly int StageSpotlightRangeId = Shader.PropertyToID("_StageSpotlightRange");
    private static readonly int StageSpotlightCosOuterId = Shader.PropertyToID("_StageSpotlightCosOuter");
    private static readonly int StageSpotlightCosInnerId = Shader.PropertyToID("_StageSpotlightCosInner");
    private static readonly int StageSpotlightEnabledId = Shader.PropertyToID("_StageSpotlightEnabled");

    public Light ActiveSpotLight { get; private set; }
    public Transform ViewerOriginTransform { get; private set; }

    private void Reset()
    {
        EnsureRuntimeComponentsAttached();
    }

    private void OnEnable()
    {
        EnsureRuntimeComponentsAttached();

        if (!Application.isPlaying)
        {
            RefreshEditorPreview();
        }
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            ClearExistingRig();
            RestoreManagedSceneLights();
        }

        ActiveSpotLight = null;
        ClearSpotlightShaderGlobals();
    }

    private void OnDestroy()
    {
        if (!Application.isPlaying)
        {
            RestoreManagedSceneLights();
        }
    }

    private void OnValidate()
    {
        EnsureRuntimeComponentsAttached();

        if (!Application.isPlaying)
        {
            RefreshEditorPreview();
        }
    }

    private void Start()
    {
        if (!Application.isPlaying || !buildOnStart || hasBuilt)
        {
            return;
        }

        BuildDemo();
    }

    private void Update()
    {
        if (Application.isPlaying && allowFullscreenToggle && Input.GetKeyDown(fullscreenToggleKey))
        {
            ToggleFullscreenMode();
        }

        UpdateSpotlightShaderGlobals();
    }

    [ContextMenu("Build Demo")]
    public void BuildDemo()
    {
        BuildDemoInternal(Application.isPlaying);
    }

    private void BuildDemoInternal(bool isRuntimeBuild)
    {
        ClearExistingRig();

        if (isRuntimeBuild)
        {
            ApplyInitialWindowMode();
            SetupDisplays();
        }

        ApplyBlackEnvironment(isRuntimeBuild);

        ProjectionSurface frontSurface;
        ProjectionSurface leftSurface;
        ProjectionSurface rightSurface;
        Transform viewerOrigin;
        SetupDemoRig(out frontSurface, out leftSurface, out rightSurface, out viewerOrigin);
        SetupCameras(frontSurface, leftSurface, rightSurface, viewerOrigin);
        hasBuilt = isRuntimeBuild;
    }

    private void ApplyInitialWindowMode()
    {
        if (startInFullscreen)
        {
            Screen.fullScreenMode = fullscreenMode;
            Screen.fullScreen = true;
            return;
        }

        int targetWidth = Mathf.Max(640, windowedWidth);
        int targetHeight = Mathf.Max(360, windowedHeight);
        Screen.SetResolution(targetWidth, targetHeight, FullScreenMode.Windowed);
        Screen.fullScreen = false;
    }

    private void ToggleFullscreenMode()
    {
        bool switchingToWindowed = Screen.fullScreen && Screen.fullScreenMode != FullScreenMode.Windowed;
        if (switchingToWindowed)
        {
            int targetWidth = Mathf.Max(640, windowedWidth);
            int targetHeight = Mathf.Max(360, windowedHeight);
            Screen.SetResolution(targetWidth, targetHeight, FullScreenMode.Windowed);
            Screen.fullScreen = false;
            return;
        }

        Screen.fullScreenMode = fullscreenMode;
        Screen.fullScreen = true;
    }
 
    private void SetupDisplays()
    {
        if (Display.displays.Length > 1)
        {
            Display.displays[1].Activate();
        }

        if (Display.displays.Length > 2)
        {
            Display.displays[2].Activate();
        }
    }

    private void SetupCameras(ProjectionSurface frontSurface, ProjectionSurface leftSurface, ProjectionSurface rightSurface, Transform viewerOrigin)
    {
        Camera frontCamera = EnsureCamera("Main Camera", "MainCamera");
        ConfigureCamera(frontCamera, 0, 40f, frontSurface, viewerOrigin, false, false);

        Camera leftCamera = EnsureCamera("Left Projection Camera", null);
        ConfigureCamera(leftCamera, 1, 40f, leftSurface, viewerOrigin, false, false);

        Camera rightCamera = EnsureCamera("Right Projection Camera", null);
        ConfigureCamera(rightCamera, 2, 40f, rightSurface, viewerOrigin, false, false);
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

    private void SetupDemoRig(out ProjectionSurface frontSurface, out ProjectionSurface leftSurface, out ProjectionSurface rightSurface, out Transform viewerOrigin)
    {
        GameObject rigRoot = new GameObject(RigRootName);
        rigRoot.transform.SetParent(transform, false);
        rigRoot.transform.position = Vector3.zero;

        frontSurface = CreateFrontSurface(rigRoot.transform);
        leftSurface = CreateLeftSurface(rigRoot.transform, frontSurface.Width);
        rightSurface = CreateRightSurface(rigRoot.transform, frontSurface.Width);
        viewerOrigin = CreateViewerOrigin(rigRoot.transform);
        ViewerOriginTransform = viewerOrigin;

        GameObject rotationPivot = new GameObject(RotationPivotName);
        rotationPivot.transform.SetParent(rigRoot.transform, false);
        rotationPivot.transform.localPosition = Vector3.zero;
        rotationPivot.transform.localRotation = Quaternion.identity;

        UdpQuaternionReceiver receiver = EnsureComponent<UdpQuaternionReceiver>();
        PoseCalibrationCoordinator calibrationCoordinator = EnsureComponent<PoseCalibrationCoordinator>();
        PoseRotationDriver driver = EnsureComponent<PoseRotationDriver>();
        PoseDebugOverlay overlay = EnsureComponent<PoseDebugOverlay>();
        TestScreenVisualizer visualizer = EnsureComponent<TestScreenVisualizer>();

        SyncDebugPointerVisuals(rotationPivot.transform);

        GameObject tipLightObject = new GameObject("Tip Light");
        tipLightObject.transform.SetParent(rotationPivot.transform, false);
        tipLightObject.transform.localPosition = new Vector3(0f, 0f, 0.72f);
        tipLightObject.transform.localRotation = Quaternion.identity;

        Light tipLight = tipLightObject.AddComponent<Light>();
        tipLight.type = LightType.Spot;
        tipLight.spotAngle = 18f;
        tipLight.range = 60f;
        tipLight.intensity = 16f;
        tipLight.color = Color.white;
        ActiveSpotLight = tipLight;
        UpdateSpotlightShaderGlobals();

        ApplySpotlightOnlyLighting(tipLight, Application.isPlaying);

        driver.Configure(receiver, rotationPivot.transform, tipLight, 0.72f);
        driver.SetTipLightAlignment(false);
        visualizer.Configure(receiver, tipLight);
        visualizer.ConfigureSurfaces(frontSurface, leftSurface);
        calibrationCoordinator.Configure(receiver, driver, visualizer);
        overlay.Configure(receiver, driver, visualizer);
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

    private static ProjectionSurface CreateRightSurface(Transform parent, float frontSurfaceWidth)
    {
        GameObject rightSurfaceObject = new GameObject("Right Surface");
        rightSurfaceObject.name = "Right Surface";
        rightSurfaceObject.transform.SetParent(parent, false);
        rightSurfaceObject.transform.localPosition = new Vector3(frontSurfaceWidth * 0.5f, 0f, ScreenDistance - frontSurfaceWidth * 0.5f);
        rightSurfaceObject.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

        ProjectionSurface surface = rightSurfaceObject.AddComponent<ProjectionSurface>();
        surface.Configure(FrontScreenWidth, ScreenHeightWorld);
        return surface;
    }

    private void ClearExistingRig()
    {
        ActiveSpotLight = null;
        ViewerOriginTransform = null;
        ClearSpotlightShaderGlobals();

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

    private void SyncDebugPointerVisuals(Transform parent)
    {
        if (parent == null)
        {
            return;
        }

        RemovePointerVisuals(parent);
        if (!showDebugPointerVisuals)
        {
            return;
        }

        CreatePointerModel(parent);
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

    private static void RemovePointerVisuals(Transform parent)
    {
        for (int index = 0; index < PointerVisualNames.Length; index++)
        {
            Transform child = parent.Find(PointerVisualNames[index]);
            if (child == null)
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

    private static void SetRendererColor(GameObject target, Color color)
    {
        MeshRenderer renderer = target.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            Material sharedMaterial = renderer.sharedMaterial;
            if (sharedMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader == null)
                {
                    return;
                }

                sharedMaterial = new Material(shader);
                renderer.sharedMaterial = sharedMaterial;
            }

            if (sharedMaterial.HasProperty("_BaseColor"))
            {
                sharedMaterial.SetColor("_BaseColor", color);
            }

            sharedMaterial.color = color;
        }
    }

    private void EnsureRuntimeComponentsAttached()
    {
        EnsureComponent<UdpQuaternionReceiver>();
        EnsureComponent<PoseCalibrationCoordinator>();
        EnsureComponent<PoseRotationDriver>();
        EnsureComponent<PoseDebugOverlay>();
        EnsureComponent<TestScreenVisualizer>();
    }

    private void ApplyBlackEnvironment(bool isRuntimeBuild)
    {
        bool shouldForceBlackEnvironment = isRuntimeBuild ? forceBlackEnvironment : forceBlackEnvironmentInEditMode;
        if (!shouldForceBlackEnvironment)
        {
            return;
        }

        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.black;
        RenderSettings.ambientIntensity = 0f;
        RenderSettings.reflectionIntensity = 0f;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
#endif
    }

    private void ApplySpotlightOnlyLighting(Light spotlight, bool isRuntimeBuild)
    {
        RestoreManagedSceneLights();

        bool shouldDisableOtherSceneLights = isRuntimeBuild ? disableOtherSceneLights : disableOtherSceneLightsInEditMode;
        if (!shouldDisableOtherSceneLights)
        {
            return;
        }

        Light[] allLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (Light sceneLight in allLights)
        {
            if (sceneLight == null || sceneLight == spotlight)
            {
                continue;
            }

            managedSceneLights.Add(new SceneLightState
            {
                Light = sceneLight,
                WasEnabled = sceneLight.enabled
            });

            sceneLight.enabled = false;
        }
    }

    private void RestoreManagedSceneLights()
    {
        for (int index = 0; index < managedSceneLights.Count; index++)
        {
            SceneLightState state = managedSceneLights[index];
            if (state.Light != null)
            {
                state.Light.enabled = state.WasEnabled;
            }
        }

        managedSceneLights.Clear();
    }

    private void RefreshEditorPreview()
    {
        if (buildPreviewInEditMode)
        {
            QueueEditorPreviewBuild();
            return;
        }

        ClearExistingRig();
        RestoreManagedSceneLights();
    }

    private void QueueEditorPreviewBuild()
    {
#if UNITY_EDITOR
        if (editorPreviewQueued || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        editorPreviewQueued = true;
        EditorApplication.delayCall += RebuildEditorPreview;
#endif
    }

    private void RebuildEditorPreview()
    {
#if UNITY_EDITOR
        editorPreviewQueued = false;

        if (this == null || Application.isPlaying || !buildPreviewInEditMode)
        {
            return;
        }

        BuildDemoInternal(false);
 #endif
    }

    private T EnsureComponent<T>() where T : Component
    {
        T component = GetComponent<T>();
        if (component == null)
        {
            component = gameObject.AddComponent<T>();
        }

        return component;
    }

    private void UpdateSpotlightShaderGlobals()
    {
        if (ActiveSpotLight == null || !ActiveSpotLight.enabled || ActiveSpotLight.type != LightType.Spot)
        {
            ClearSpotlightShaderGlobals();
            return;
        }

        float halfAngleRadians = ActiveSpotLight.spotAngle * 0.5f * Mathf.Deg2Rad;
        float innerAngleRadians = halfAngleRadians * 0.82f;

        Shader.SetGlobalVector(StageSpotlightPositionId, ActiveSpotLight.transform.position);
        Shader.SetGlobalVector(StageSpotlightDirectionId, ActiveSpotLight.transform.forward);
        Shader.SetGlobalFloat(StageSpotlightRangeId, ActiveSpotLight.range);
        Shader.SetGlobalFloat(StageSpotlightCosOuterId, Mathf.Cos(halfAngleRadians));
        Shader.SetGlobalFloat(StageSpotlightCosInnerId, Mathf.Cos(innerAngleRadians));
        Shader.SetGlobalFloat(StageSpotlightEnabledId, 1f);
    }

    private static void ClearSpotlightShaderGlobals()
    {
        Shader.SetGlobalVector(StageSpotlightPositionId, Vector4.zero);
        Shader.SetGlobalVector(StageSpotlightDirectionId, Vector3.forward);
        Shader.SetGlobalFloat(StageSpotlightRangeId, 0f);
        Shader.SetGlobalFloat(StageSpotlightCosOuterId, -1f);
        Shader.SetGlobalFloat(StageSpotlightCosInnerId, -1f);
        Shader.SetGlobalFloat(StageSpotlightEnabledId, 0f);
    }
}
