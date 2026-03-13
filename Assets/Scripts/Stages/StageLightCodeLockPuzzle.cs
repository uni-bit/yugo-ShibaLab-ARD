using UnityEngine;

[AddComponentMenu("Stages/Stage 3 Code Lock Puzzle")]
public class StageLightCodeLockPuzzle : MonoBehaviour
{
    [SerializeField] private StageLightCodeDialColumn[] columns = new StageLightCodeDialColumn[0];
    [SerializeField] private Transform doorTransform;
    [SerializeField] private TextMesh formulaText;
    [SerializeField] private StageCodeFormulaDisplay formulaDisplay;
    [SerializeField] private string targetCode = "834";
    [SerializeField] private bool animateDoorOnSolved;
    [SerializeField] private Vector3 openLocalOffset = new Vector3(0f, 4.4f, 0f);
    [SerializeField] private float openSpeed = 2.2f;
    [SerializeField] private Color lockedFormulaColor = Color.white;
    [SerializeField] private Color solvedTextColor = new Color(1f, 0.9f, 0.25f, 1f);
    [SerializeField] private Color solvedDoorGlowColor = new Color(1f, 0.9f, 0.32f, 1f);
    [SerializeField] private float solvedDoorGlowEmissionIntensity = 2.4f;
    [SerializeField] private float solvedDoorGlowLightIntensity = 1.25f;
    [SerializeField] private float solvedDoorGlowLightRange = 3.2f;
    [SerializeField] private float solvedDoorGlowFrameThickness = 0.045f;

    public bool IsSolved { get; private set; }
    public string TargetCode => targetCode;

    private Vector3 closedDoorLocalPosition;
    private bool solvedVisualsApplied;
    private readonly LineRenderer[] doorGlowLines = new LineRenderer[4];
    private Light doorGlowLight;

    private void Awake()
    {
        if (doorTransform != null)
        {
            closedDoorLocalPosition = doorTransform.localPosition;
        }

        ApplyFormulaState();
    }

    private void Update()
    {
        bool isTargetCodeEntered = IsTargetCodeEntered();
        if (!solvedVisualsApplied && IsSolved != isTargetCodeEntered)
        {
            IsSolved = isTargetCodeEntered;
            ApplyFormulaState();
        }

        if (doorTransform != null)
        {
            Vector3 targetPosition = IsSolved && animateDoorOnSolved
                ? closedDoorLocalPosition + openLocalOffset
                : closedDoorLocalPosition;
            float blend = 1f - Mathf.Exp(-openSpeed * Time.deltaTime);
            doorTransform.localPosition = Vector3.Lerp(doorTransform.localPosition, targetPosition, blend);
        }
    }

    public void Configure(
        StageLightCodeDialColumn[] dialColumns,
        Transform doorReference,
        TextMesh formulaTextReference,
        StageCodeFormulaDisplay formulaDisplayReference,
        string code)
    {
        columns = dialColumns;
        doorTransform = doorReference;
        formulaText = formulaTextReference;
        formulaDisplay = formulaDisplayReference;
        targetCode = code;
        lockedFormulaColor = Color.white;
        IsSolved = false;
        solvedVisualsApplied = false;

        if (doorTransform != null)
        {
            closedDoorLocalPosition = doorTransform.localPosition;
        }

        ApplyFormulaState();
    }

    public void ApplySolvedVisualState()
    {
        if (solvedVisualsApplied)
        {
            return;
        }

        solvedVisualsApplied = true;

        for (int index = 0; index < columns.Length; index++)
        {
            if (columns[index] != null)
            {
                columns[index].ApplySolvedAppearance();
            }
        }

        if (formulaText != null)
        {
            formulaText.color = solvedTextColor;
        }

        if (formulaDisplay != null)
        {
            formulaDisplay.SetDisplayColorOverride(solvedTextColor, solvedTextColor);
        }

        SetDoorGlowActive(true);
    }

    public void DisableSolvedGlowForCollapse()
    {
        SetDoorGlowActive(false);
    }

    public void ApplyCodeInstantly(string code)
    {
        if (columns == null || string.IsNullOrEmpty(code))
        {
            return;
        }

        int digitCount = Mathf.Min(columns.Length, code.Length);
        for (int index = 0; index < digitCount; index++)
        {
            StageLightCodeDialColumn column = columns[index];
            if (column == null)
            {
                continue;
            }

            int digit = code[index] - '0';
            if (digit < 0 || digit > 9)
            {
                continue;
            }

            column.SetDigitImmediate(digit);
        }
    }

    private bool IsTargetCodeEntered()
    {
        if (columns == null || columns.Length == 0 || string.IsNullOrEmpty(targetCode) || targetCode.Length != columns.Length)
        {
            return false;
        }

        for (int index = 0; index < columns.Length; index++)
        {
            if (columns[index] == null)
            {
                return false;
            }

            int expectedDigit = targetCode[index] - '0';
            if (columns[index].CurrentDigit != expectedDigit)
            {
                return false;
            }
        }

        return true;
    }

    private void ApplyFormulaState()
    {
        if (formulaText != null)
        {
            formulaText.color = solvedVisualsApplied ? solvedTextColor : lockedFormulaColor;
        }

        if (formulaDisplay != null && !solvedVisualsApplied)
        {
            formulaDisplay.RefreshState();
        }

        if (!solvedVisualsApplied)
        {
            SetDoorGlowActive(false);
        }
    }

    private void SetDoorGlowActive(bool isActive)
    {
        if (doorTransform == null)
        {
            return;
        }

        EnsureDoorGlowFrame();
        Color frameColor = solvedDoorGlowColor * solvedDoorGlowEmissionIntensity;

        for (int index = 0; index < doorGlowLines.Length; index++)
        {
            LineRenderer line = doorGlowLines[index];
            if (line == null)
            {
                continue;
            }

            line.enabled = isActive;
            line.startWidth = solvedDoorGlowFrameThickness;
            line.endWidth = solvedDoorGlowFrameThickness;
            line.startColor = solvedDoorGlowColor;
            line.endColor = solvedDoorGlowColor;

            Material material = line.sharedMaterial;
            if (material != null && material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", isActive ? frameColor : Color.black);
                if (isActive)
                {
                    material.EnableKeyword("_EMISSION");
                }
                else
                {
                    material.DisableKeyword("_EMISSION");
                }
            }
        }

        if (doorGlowLight != null)
        {
            doorGlowLight.color = solvedDoorGlowColor;
            doorGlowLight.range = solvedDoorGlowLightRange;
            doorGlowLight.intensity = isActive ? solvedDoorGlowLightIntensity : 0f;
            doorGlowLight.enabled = isActive;
        }
    }

    private void EnsureDoorGlowFrame()
    {
        if (doorTransform == null)
        {
            return;
        }

        Renderer doorRenderer = doorTransform.GetComponent<Renderer>();
        if (doorRenderer == null)
        {
            return;
        }

        Bounds bounds = doorRenderer.bounds;
        Vector3[] corners =
        {
            new Vector3(bounds.min.x, bounds.min.y, bounds.center.z),
            new Vector3(bounds.max.x, bounds.min.y, bounds.center.z),
            new Vector3(bounds.max.x, bounds.max.y, bounds.center.z),
            new Vector3(bounds.min.x, bounds.max.y, bounds.center.z)
        };

        for (int edgeIndex = 0; edgeIndex < 4; edgeIndex++)
        {
            if (doorGlowLines[edgeIndex] == null)
            {
                GameObject edgeObject = new GameObject("Door Glow Edge " + edgeIndex);
                edgeObject.transform.SetParent(doorTransform, true);
                LineRenderer line = edgeObject.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.alignment = LineAlignment.View;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.numCapVertices = 2;
                line.numCornerVertices = 2;
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Unlit/Color")
                    ?? Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    Material material = new Material(shader);
                    material.name = "Door Glow Edge Material " + edgeIndex;
                    line.sharedMaterial = material;
                }

                doorGlowLines[edgeIndex] = line;
            }

            int nextIndex = (edgeIndex + 1) % 4;
            LineRenderer edgeLine = doorGlowLines[edgeIndex];
            edgeLine.SetPosition(0, corners[edgeIndex]);
            edgeLine.SetPosition(1, corners[nextIndex]);
        }

        if (doorGlowLight == null)
        {
            Transform glowLightTransform = doorTransform.Find("Door Glow Light");
            if (glowLightTransform == null)
            {
                glowLightTransform = new GameObject("Door Glow Light").transform;
                glowLightTransform.SetParent(doorTransform, false);
            }

            glowLightTransform.position = bounds.center - (doorTransform.forward * 0.25f);
            glowLightTransform.rotation = Quaternion.identity;

            doorGlowLight = glowLightTransform.GetComponent<Light>();
            if (doorGlowLight == null)
            {
                doorGlowLight = glowLightTransform.gameObject.AddComponent<Light>();
            }

            doorGlowLight.type = LightType.Point;
            doorGlowLight.shadows = LightShadows.None;
            doorGlowLight.enabled = false;
        }
    }
}