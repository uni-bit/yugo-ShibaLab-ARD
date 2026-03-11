using UnityEngine;

[ExecuteAlways]
[AddComponentMenu("Stages/Stage Code Formula Display")]
public class StageCodeFormulaDisplay : MonoBehaviour
{
    [SerializeField] private TextMesh colorSourceText;
    [SerializeField] private float strokeWidth = 0.045f;
    [SerializeField] private float layoutOffsetX = -0.3f;
    [SerializeField] private Color fallbackColor = Color.white;
    [SerializeField] private Color hiddenColor = new Color(1f, 1f, 1f, 0f);
    [SerializeField] private bool layoutInitialized;

    private const string CircleName = "Circle Symbol";
    private const string TriangleName = "Triangle Symbol";
    private const string SquareName = "Square Symbol";
    private const string EqualsName = "Formula Equals";
    private const string QuestionName = "Formula Question";

    private static Material lineMaterial;

    private LineRenderer circleRenderer;
    private LineRenderer triangleRenderer;
    private LineRenderer squareRenderer;
    private TextMesh equalsText;
    private TextMesh questionText;

    private void Reset()
    {
        EnsureVisuals();
        RefreshState();
    }

    private void Awake()
    {
        EnsureVisuals();
        RefreshState();
    }

    private void OnEnable()
    {
        EnsureVisuals();
        RefreshState();
    }

    private void OnValidate()
    {
        EnsureVisuals();
        RefreshState();
    }

    private void Update()
    {
        RefreshState();
    }

    public void Configure(TextMesh colorReference, SpotlightSensor sensorReference)
    {
        colorSourceText = colorReference;
        fallbackColor = Color.white;
        hiddenColor = new Color(1f, 1f, 1f, 0f);
        EnsureVisuals();
        RefreshState();
    }

    public void RefreshState()
    {
        EnsureVisuals();
        Color displayColor = colorSourceText != null ? colorSourceText.color : fallbackColor;
        StageSpotlightMaterialUtility.ApplySpotlitLine(circleRenderer, hiddenColor, displayColor);
        StageSpotlightMaterialUtility.ApplySpotlitLine(triangleRenderer, hiddenColor, displayColor);
        StageSpotlightMaterialUtility.ApplySpotlitLine(squareRenderer, hiddenColor, displayColor);

        if (equalsText != null)
        {
            equalsText.color = displayColor;
            StageSpotlightMaterialUtility.ApplySpotlitText(equalsText, hiddenColor, displayColor);
        }

        if (questionText != null)
        {
            questionText.color = displayColor;
            StageSpotlightMaterialUtility.ApplySpotlitText(questionText, hiddenColor, displayColor);
        }
    }

    private void EnsureVisuals()
    {
        bool createdCircle;
        bool createdTriangle;
        bool createdSquare;
        bool createdEquals;
        bool createdQuestion;

        circleRenderer = EnsureShapeRenderer(CircleName, out createdCircle);
        triangleRenderer = EnsureShapeRenderer(TriangleName, out createdTriangle);
        squareRenderer = EnsureShapeRenderer(SquareName, out createdSquare);
        equalsText = EnsureFormulaText(EqualsName, out createdEquals);
        questionText = EnsureFormulaText(QuestionName, out createdQuestion);

        ConfigureCircle(circleRenderer, new Vector3(-1.72f, 0f, 0f));
        ConfigureTriangle(triangleRenderer, new Vector3(-0.92f, -0.01f, 0f));
        ConfigureSquare(squareRenderer, new Vector3(-0.12f, 0f, 0f));
        ConfigureFormulaText(equalsText, "=", ApplyLayoutOffset(new Vector3(0.78f, 0f, 0f)), 0.09f, 180);
        ConfigureFormulaText(questionText, "???", ApplyLayoutOffset(new Vector3(1.92f, 0f, 0f)), 0.09f, 180);

        layoutInitialized = true;

    }

    private LineRenderer EnsureShapeRenderer(string childName, out bool wasCreated)
    {
        Transform child = transform.Find(childName);
        wasCreated = child == null;
        if (child == null)
        {
            child = new GameObject(childName).transform;
            child.SetParent(transform, false);
        }

        LineRenderer renderer = child.GetComponent<LineRenderer>();
        if (renderer == null)
        {
            renderer = child.gameObject.AddComponent<LineRenderer>();
        }

        renderer.useWorldSpace = false;
        renderer.loop = false;
        renderer.alignment = LineAlignment.View;
        renderer.textureMode = LineTextureMode.Stretch;
        renderer.numCapVertices = 0;
        renderer.numCornerVertices = 4;
        renderer.startWidth = strokeWidth;
        renderer.endWidth = strokeWidth;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sharedMaterial = GetLineMaterial();
        return renderer;
    }

    private TextMesh EnsureFormulaText(string childName, out bool wasCreated)
    {
        Transform child = transform.Find(childName);
        wasCreated = child == null;
        if (child == null)
        {
            child = new GameObject(childName).transform;
            child.SetParent(transform, false);
        }

        child.localRotation = Quaternion.identity;
        child.localScale = Vector3.one;

        TextMesh textMesh = child.GetComponent<TextMesh>();
        if (textMesh == null)
        {
            textMesh = child.gameObject.AddComponent<TextMesh>();
        }

        Font builtInFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (builtInFont != null)
        {
            textMesh.font = builtInFont;
            MeshRenderer renderer = textMesh.GetComponent<MeshRenderer>();
            if (renderer != null && builtInFont.material != null)
            {
                renderer.sharedMaterial = builtInFont.material;
            }
        }

        MeshRenderer textRenderer = textMesh.GetComponent<MeshRenderer>();
        if (textRenderer != null)
        {
            textRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            textRenderer.receiveShadows = false;
        }

        return textMesh;
    }

    private void ConfigureCircle(LineRenderer renderer, Vector3 localPosition)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.transform.localPosition = ApplyLayoutOffset(localPosition);
        renderer.loop = false;
        renderer.positionCount = 41;
        float radius = 0.27f;
        for (int index = 0; index < renderer.positionCount - 1; index++)
        {
            float angle = ((float)index / (renderer.positionCount - 1)) * Mathf.PI * 2f;
            renderer.SetPosition(index, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }

        renderer.SetPosition(renderer.positionCount - 1, renderer.GetPosition(0));
    }

    private void ConfigureTriangle(LineRenderer renderer, Vector3 localPosition)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.transform.localPosition = ApplyLayoutOffset(localPosition);
        renderer.loop = false;
        renderer.positionCount = 4;
        renderer.SetPosition(0, new Vector3(0f, 0.3f, 0f));
        renderer.SetPosition(1, new Vector3(-0.28f, -0.22f, 0f));
        renderer.SetPosition(2, new Vector3(0.28f, -0.22f, 0f));
        renderer.SetPosition(3, new Vector3(0f, 0.3f, 0f));
    }

    private void ConfigureSquare(LineRenderer renderer, Vector3 localPosition)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.transform.localPosition = ApplyLayoutOffset(localPosition);
        renderer.loop = false;
        renderer.positionCount = 5;
        renderer.SetPosition(0, new Vector3(-0.24f, 0.24f, 0f));
        renderer.SetPosition(1, new Vector3(-0.24f, -0.24f, 0f));
        renderer.SetPosition(2, new Vector3(0.24f, -0.24f, 0f));
        renderer.SetPosition(3, new Vector3(0.24f, 0.24f, 0f));
        renderer.SetPosition(4, new Vector3(-0.24f, 0.24f, 0f));
    }

    private static void ConfigureFormulaText(TextMesh textMesh, string text, Vector3 localPosition, float characterSize, int fontSize)
    {
        if (textMesh == null)
        {
            return;
        }

        textMesh.text = text;
        textMesh.characterSize = characterSize;
        textMesh.fontSize = fontSize;
        textMesh.alignment = TextAlignment.Center;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.transform.localPosition = localPosition;
        textMesh.transform.localRotation = Quaternion.identity;
        textMesh.transform.localScale = Vector3.one;
    }

    private Vector3 ApplyLayoutOffset(Vector3 localPosition)
    {
        localPosition.x += layoutOffsetX;
        return localPosition;
    }

    private static Material GetLineMaterial()
    {
        if (lineMaterial != null)
        {
            return lineMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        if (shader == null)
        {
            return null;
        }

        lineMaterial = new Material(shader);
        lineMaterial.name = "Stage Code Formula Line Material";
        return lineMaterial;
    }
}