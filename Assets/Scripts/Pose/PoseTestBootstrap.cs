using UnityEngine;
using UnityEngine.Rendering;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 投影リグを実行時に自動構築するブートストラップコンポーネント。
/// <para>
/// 主な役割:
/// <list type="bullet">
/// <item>Front/Left の 2 面投影サーフェスと OffAxisProjection カメラの生成</item>
/// <item>ZIG SIM 由来の姿勢入力を受けるスポットライトの生成と <see cref="ActiveSpotLight"/> としての公開</item>
/// <item>実行時の黒背景化・他ライト無効化</item>
/// <item>シェーダーグローバル変数 (_StageSpotlightPosition 等) の毎フレーム更新</item>
/// </list>
/// </para>
/// <para>
/// ステージ側は <see cref="ActiveSpotLight"/> を参照して <see cref="Stages.SpotlightSensor"/> のライト源とします。
/// <see cref="StageSequenceController"/> も <c>FindFirstObjectByType</c> でこのコンポーネントを取得します。
/// </para>
/// </summary>
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
    private const float FrontScreenWidth = 2.30f;
    private const float ScreenDistance = FrontScreenWidth * 0.5f;
    private const float ScreenHeightWorld = FrontScreenWidth * (1080f / 1920f);
    private const float DefaultViewerDistanceFromScreens = 2.0f;
    private const float ViewerHeight = 0f;
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
    [SerializeField] private KeyCode fullscreenToggleKey = KeyCode.F11;
    [SerializeField] private int windowedWidth = WindowedDefaultWidth;
    [SerializeField] private int windowedHeight = WindowedDefaultHeight;
    [SerializeField] private int leftCameraTargetDisplay = 1;
    [SerializeField] private int frontCameraTargetDisplay = 2;
    [SerializeField] private float viewerDistanceFromScreens = DefaultViewerDistanceFromScreens;
    [Header("Default Spotlight")]
    [Tooltip("スポットライトの初期スポット角度（度）。来場者距離に応じて調整してください。")]
    [SerializeField] private float defaultSpotAngle = 18f;
    [Tooltip("スポットライトの初期レンジ（m）")]
    [SerializeField] private float defaultSpotRange = 60f;
    [Tooltip("スポットライトの初期輝度")]
    [SerializeField] private float defaultSpotIntensity = 16f;
    [Tooltip("スポットライトの初期色")]
    [SerializeField] private Color defaultSpotColor = Color.white;
    [Header("Projection Cameras")]
    [Tooltip("フロント投影カメラ。割り当てた場合はそのカメラを使用し、自動生成しません。")]
    [SerializeField] private Camera frontProjectionCameraOverride;
    [Tooltip("レフト投影カメラ。割り当てた場合はそのカメラを使用し、自動生成しません。")]
    [SerializeField] private Camera leftProjectionCameraOverride;
    [Tooltip("trueにすると、カメラが見つからない場合に自動生成します。falseなら手動配置のカメラのみ使用します。")]
    [SerializeField] private bool autoCreateCamerasIfMissing = false;

    private bool hasBuilt;
    private bool editorPreviewQueued;
    private Camera frontProjectionCamera;
    private Camera leftProjectionCamera;
    private ProjectionSurface cachedFrontSurface;
    private ProjectionSurface cachedLeftSurface;
    private Transform cachedViewerOrigin;
    private Transform cachedRotationPivot;
    private StageSequenceController stageSequenceController;
    private int lastAppliedStageIndex = -1;

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

    public void ResetViewerRigPose()
    {
        if (cachedViewerOrigin == null)
        {
            return;
        }

        cachedViewerOrigin.localPosition = GetViewerOriginLocalPosition(viewerDistanceFromScreens);
        cachedViewerOrigin.localRotation = Quaternion.identity;

        if (cachedRotationPivot != null)
        {
            cachedRotationPivot.localPosition = cachedViewerOrigin.localPosition;
            cachedRotationPivot.localRotation = Quaternion.identity;
            AlignRotationPivotToDualScreenCenter(cachedRotationPivot, cachedFrontSurface, cachedLeftSurface);
        }

        if (frontProjectionCamera != null && cachedFrontSurface != null)
        {
            ConfigureCamera(frontProjectionCamera, Mathf.Clamp(frontCameraTargetDisplay, 0, 7), 40f, cachedFrontSurface, cachedViewerOrigin, false, false);
        }

        if (leftProjectionCamera != null && cachedLeftSurface != null)
        {
            ConfigureCamera(leftProjectionCamera, Mathf.Clamp(leftCameraTargetDisplay, 0, 7), 40f, cachedLeftSurface, cachedViewerOrigin, false, false);
        }
    }

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
        if (Application.isPlaying && IsFullscreenTogglePressed())
        {
            ToggleFullscreenMode();
        }

        if (Application.isPlaying)
        {
            ApplyStage3FrontCameraRoutingIfNeeded();
        }

        UpdateSpotlightShaderGlobals();
    }

    private void ApplyStage3FrontCameraRoutingIfNeeded()
    {
        if (stageSequenceController == null)
        {
            stageSequenceController = FindFirstObjectByType<StageSequenceController>();
        }

        if (stageSequenceController == null)
        {
            return;
        }

        int activeStageIndex = stageSequenceController.CurrentStageIndex;
        if (activeStageIndex == lastAppliedStageIndex)
        {
            return;
        }

        lastAppliedStageIndex = activeStageIndex;

        if (activeStageIndex != 2)
        {
            return;
        }

        if (frontProjectionCamera == null || leftProjectionCamera == null || cachedFrontSurface == null || cachedLeftSurface == null || cachedViewerOrigin == null)
        {
            return;
        }

        frontProjectionCamera.enabled = true;
        leftProjectionCamera.enabled = true;

        ConfigureCamera(frontProjectionCamera, Mathf.Clamp(frontCameraTargetDisplay, 0, 7), 40f, cachedFrontSurface, cachedViewerOrigin, false, false);
        ConfigureCamera(leftProjectionCamera, Mathf.Clamp(leftCameraTargetDisplay, 0, 7), 40f, cachedLeftSurface, cachedViewerOrigin, false, false);
    }

    private bool IsFullscreenTogglePressed()
    {
        if (Input.GetKeyDown(KeyCode.F11))
        {
            return true;
        }

        if (Input.GetKeyDown(fullscreenToggleKey))
        {
            return true;
        }

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null)
        {
            return false;
        }

        if (fullscreenToggleKey == KeyCode.F11)
        {
            return Keyboard.current.f11Key.wasPressedThisFrame;
        }

        return false;
#else
        return false;
#endif
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
        Transform viewerOrigin;
        SetupDemoRig(out frontSurface, out leftSurface, out viewerOrigin);
        SetupCameras(frontSurface, leftSurface, viewerOrigin);
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

    private void SetupCameras(ProjectionSurface frontSurface, ProjectionSurface leftSurface, Transform viewerOrigin)
    {
        frontProjectionCamera = ResolveCamera(frontProjectionCameraOverride, "Main Camera", "MainCamera", autoCreateCamerasIfMissing);
        if (frontProjectionCamera != null)
        {
            ConfigureCamera(frontProjectionCamera, Mathf.Clamp(frontCameraTargetDisplay, 0, 7), 40f, frontSurface, viewerOrigin, false, false);
        }

        leftProjectionCamera = ResolveCamera(leftProjectionCameraOverride, "Left Projection Camera", null, autoCreateCamerasIfMissing);
        if (leftProjectionCamera != null)
        {
            ConfigureCamera(leftProjectionCamera, Mathf.Clamp(leftCameraTargetDisplay, 0, 7), 40f, leftSurface, viewerOrigin, false, false);
        }

        // Disable legacy Right Projection Camera if it exists and is not the left camera.
        GameObject rightCameraObject = GameObject.Find("Right Projection Camera");
        if (rightCameraObject != null)
        {
            Camera rightCamera = rightCameraObject.GetComponent<Camera>();
            if (rightCamera != null && rightCamera != leftProjectionCamera)
            {
                rightCamera.enabled = false;
            }
        }
    }

    /// <summary>
    /// インスペクタ参照 → 名前検索 → 任意で新規生成の優先順でカメラを解決します。
    /// </summary>
    private static Camera ResolveCamera(Camera overrideCamera, string searchName, string tagName, bool createIfMissing)
    {
        if (overrideCamera != null)
        {
            return overrideCamera;
        }

        GameObject found = GameObject.Find(searchName);
        Camera sceneCamera = found != null ? found.GetComponent<Camera>() : null;
        if (sceneCamera != null)
        {
            return sceneCamera;
        }

        if (!createIfMissing)
        {
            return null;
        }

        GameObject newObject = new GameObject(searchName);
        Camera newCamera = newObject.AddComponent<Camera>();
        if (!string.IsNullOrEmpty(tagName))
        {
            newObject.tag = tagName;
        }

        return newCamera;
    }

    /// <summary>
    /// 外部（Cinemachine 連携スクリプト等）からカメラを Off-Axis 投影に設定するためのパブリックユーティリティです。
    /// </summary>
    public static void ConfigureOffAxisCamera(Camera cam, int targetDisplay, ProjectionSurface surface, Transform eyePoint, bool flipH = false, bool flipV = false)
    {
        ConfigureCamera(cam, targetDisplay, 40f, surface, eyePoint, flipH, flipV);
    }

    private static void ConfigureCamera(Camera sceneCamera, int targetDisplay, float fieldOfView, ProjectionSurface surface, Transform eyePoint, bool flipHorizontally, bool flipVertically)
    {
        sceneCamera.enabled = true;
        sceneCamera.transform.position = eyePoint != null ? eyePoint.position : Vector3.zero;
        sceneCamera.transform.rotation = eyePoint != null ? eyePoint.rotation : Quaternion.identity;
        sceneCamera.clearFlags = CameraClearFlags.SolidColor;
        sceneCamera.backgroundColor = Color.black;
        sceneCamera.cullingMask = ~0;
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
        cachedFrontSurface = frontSurface;
        cachedLeftSurface = leftSurface;
        cachedViewerOrigin = viewerOrigin;
        lastAppliedStageIndex = -1;
        ViewerOriginTransform = viewerOrigin;

        GameObject rotationPivot = new GameObject(RotationPivotName);
        rotationPivot.transform.SetParent(rigRoot.transform, false);
        rotationPivot.transform.localPosition = viewerOrigin.localPosition;
        rotationPivot.transform.localRotation = Quaternion.identity;
        AlignRotationPivotToDualScreenCenter(rotationPivot.transform, frontSurface, leftSurface);
        cachedRotationPivot = rotationPivot.transform;

        UdpQuaternionReceiver receiver = EnsureComponent<UdpQuaternionReceiver>();
        PoseCalibrationCoordinator calibrationCoordinator = EnsureComponent<PoseCalibrationCoordinator>();
        PoseRotationDriver driver = EnsureComponent<PoseRotationDriver>();
        PoseDebugOverlay overlay = EnsureComponent<PoseDebugOverlay>();
        TestScreenVisualizer visualizer = EnsureComponent<TestScreenVisualizer>();

        SyncDebugPointerVisuals(rotationPivot.transform);

        GameObject tipLightObject = new GameObject("Tip Light");
        tipLightObject.transform.SetParent(rotationPivot.transform, false);
        tipLightObject.transform.localPosition = new Vector3(0f, 0f, 0.0f);
        tipLightObject.transform.localRotation = Quaternion.identity;

        Light tipLight = tipLightObject.AddComponent<Light>();
        tipLight.type = LightType.Spot;
        tipLight.spotAngle = defaultSpotAngle;
        tipLight.range = defaultSpotRange;
        tipLight.intensity = defaultSpotIntensity;
        tipLight.color = defaultSpotColor;
        ActiveSpotLight = tipLight;
        UpdateSpotlightShaderGlobals();

        ApplySpotlightOnlyLighting(tipLight, Application.isPlaying);

        driver.Configure(receiver, rotationPivot.transform, tipLight, 0.0f);
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
        float distanceFromFrontScreen = Mathf.Max(0.05f, DefaultViewerDistanceFromScreens);
        PoseTestBootstrap bootstrap = parent != null ? parent.GetComponentInParent<PoseTestBootstrap>() : null;
        if (bootstrap != null)
        {
            distanceFromFrontScreen = Mathf.Max(0.05f, bootstrap.viewerDistanceFromScreens);
        }

        viewerObject.transform.localPosition = GetViewerOriginLocalPosition(distanceFromFrontScreen);
        viewerObject.transform.localRotation = Quaternion.identity;
        return viewerObject.transform;
    }

    private static Vector3 GetViewerOriginLocalPosition(float distanceFromFrontScreen)
    {
        float clampedDistance = Mathf.Max(0.05f, distanceFromFrontScreen);
        float viewerX = (-FrontScreenWidth * 0.5f) + clampedDistance;
        float viewerZ = ScreenDistance - clampedDistance;
        return new Vector3(viewerX, ViewerHeight, viewerZ);
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

    private static void AlignRotationPivotToDualScreenCenter(Transform rotationPivot, ProjectionSurface frontSurface, ProjectionSurface leftSurface)
    {
        if (rotationPivot == null || frontSurface == null || leftSurface == null)
        {
            return;
        }

        Vector3 targetPoint = (frontSurface.transform.position + leftSurface.transform.position) * 0.5f;
        Vector3 direction = targetPoint - rotationPivot.position;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        rotationPivot.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
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
