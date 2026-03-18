using UnityEngine;

[ExecuteAlways]
[AddComponentMenu("Stages/Stage 2 Mapping Display")]
public class StageSymbolMappingDisplay : MonoBehaviour
{
    [SerializeField] private string mappingText = "□=4";
    [SerializeField] private SpotlightSensor spotlightSensor;
    [SerializeField] private Vector3 localPosition = new Vector3(0f, 0.02f, -0.19f);
    [SerializeField] private float characterSize = 0.11f;
    [SerializeField] private int fontSize = 48;
    [SerializeField] private float visibleExposureThreshold = 0.02f;
    [SerializeField] private Color hiddenTextColor = new Color(0f, 0f, 0f, 0f);
    [SerializeField] private Color litTextColor = new Color(0.45f, 0.95f, 1f, 1f);

    private const string GeneratedRootName = "Mapping Display";

    private Font builtInFont;
    private Transform generatedRoot;
    private TextMesh cachedTextMesh;
    private MeshRenderer cachedRenderer;

    private void Reset()
    {
        spotlightSensor = GetComponent<SpotlightSensor>();
        Rebuild();
    }

    private void Awake()
    {
        ResolveSensor();
        Rebuild();
    }

    private void OnEnable()
    {
        ResolveSensor();
        Rebuild();
    }

    private void OnValidate()
    {
        ResolveSensor();
        Rebuild();
    }

    private void Update()
    {
        RefreshState();
    }

    public void Configure(string text)
    {
        mappingText = text;
        characterSize = 0.11f;
        fontSize = 48;
        RefreshState();
    }

    public void RefreshState()
    {
        ResolveSensor();
        EnsureDisplay();
        ApplyTextSettings();

        if (spotlightSensor != null)
        {
            spotlightSensor.RefreshState();
        }

        UpdateVisibility();
    }

    private void Rebuild()
    {
        RefreshState();
    }

    private void EnsureDisplay()
    {
        RemoveLegacyChildren();

        generatedRoot = transform.Find(GeneratedRootName);
        if (generatedRoot == null)
        {
            generatedRoot = new GameObject(GeneratedRootName).transform;
            generatedRoot.SetParent(transform, false);
        }

        generatedRoot.localPosition = localPosition;
        generatedRoot.localRotation = Quaternion.identity;
        generatedRoot.localScale = Vector3.one;

        FaceCameraBillboard billboard = generatedRoot.GetComponent<FaceCameraBillboard>();
        if (billboard == null)
        {
            billboard = generatedRoot.gameObject.AddComponent<FaceCameraBillboard>();
        }

        cachedTextMesh = generatedRoot.GetComponent<TextMesh>();
        if (cachedTextMesh == null)
        {
            cachedTextMesh = generatedRoot.gameObject.AddComponent<TextMesh>();
        }

        cachedRenderer = generatedRoot.GetComponent<MeshRenderer>();
        if (cachedRenderer != null)
        {
            cachedRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            cachedRenderer.receiveShadows = false;
        }
    }

    private void ApplyTextSettings()
    {
        if (cachedTextMesh == null)
        {
            return;
        }

        if (builtInFont == null)
        {
            builtInFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        if (builtInFont != null)
        {
            cachedTextMesh.font = builtInFont;
        }

        cachedTextMesh.text = mappingText;
        cachedTextMesh.fontSize = fontSize;
        cachedTextMesh.color = litTextColor;
        cachedTextMesh.characterSize = characterSize;
        cachedTextMesh.anchor = TextAnchor.MiddleCenter;
        cachedTextMesh.alignment = TextAlignment.Center;
        cachedTextMesh.richText = false;

        if (cachedRenderer != null && cachedTextMesh.font != null && cachedTextMesh.font.material != null)
        {
            cachedRenderer.sharedMaterial = cachedTextMesh.font.material;
        }

        StageSpotlightMaterialUtility.ApplySpotlitText(cachedTextMesh, hiddenTextColor, litTextColor);
    }

    private void ResolveSensor()
    {
        if (spotlightSensor == null)
        {
            spotlightSensor = GetComponent<SpotlightSensor>();
        }
    }

    private void UpdateVisibility()
    {
        if (cachedRenderer == null || cachedTextMesh == null)
        {
            return;
        }

        cachedRenderer.enabled = true;
        cachedTextMesh.color = litTextColor;
    }

    private void RemoveLegacyChildren()
    {
        for (int index = transform.childCount - 1; index >= 0; index--)
        {
            Transform child = transform.GetChild(index);
            if (child == null || child.name == GeneratedRootName)
            {
                continue;
            }

            if (child.name == "Lit Text Root" || child.name.StartsWith("Char ") || child.name == "Segment")
            {
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
    }
}