using UnityEngine;

/// <summary>
/// Stage 2 後半のコードロックパズルコンポーネント。
/// <para>
/// 3 桁のダイヤル (<see cref="StageLightCodeDialColumn"/>) をライトで操作し、<br/>
/// <see cref="TargetCode"/>（デフォルト <c>"834"</c>）と一致したとき <see cref="IsSolved"/> が <c>true</c> になる。
/// </para>
/// <para>
/// 解除フロー: 一致検出 → <see cref="ApplySolvedVisualState"/> で式ラベル色変更・ドア枠発光 →<br/>
/// ドアが <see cref="openLocalOffset"/> 方向へアニメーション移動する。
/// </para>
/// <para>
/// このコンポーネントは Stage 2 専用です。Stage 3 / Stage 4 には使用されていません。
/// </para>
/// </summary>
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

    public bool IsSolved { get; private set; }
    public string TargetCode => targetCode;

    private Vector3 closedDoorLocalPosition;
    private bool solvedVisualsApplied;
    private Light doorGlowLight;

    private void Awake()
    {
        if (doorTransform != null)
        {
            closedDoorLocalPosition = doorTransform.localPosition;
        }

        ResetPuzzleState();
    }

    private void OnEnable()
    {
        ResetPuzzleState();
    }

    public void ResetRuntimeState()
    {
        ResetPuzzleState();
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

        ResetPuzzleState();
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
        if (doorGlowLight != null && solvedVisualsApplied)
        {
            doorGlowLight.color = solvedDoorGlowColor;
            doorGlowLight.range = solvedDoorGlowLightRange;
            doorGlowLight.intensity = solvedDoorGlowLightIntensity;
            doorGlowLight.enabled = true;
        }
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

    private void ResetPuzzleState()
    {
        IsSolved = false;
        solvedVisualsApplied = false;

        if (doorTransform != null)
        {
            doorTransform.localPosition = closedDoorLocalPosition;
        }

        if (formulaText != null)
        {
            formulaText.color = lockedFormulaColor;
        }

        if (formulaDisplay != null)
        {
            formulaDisplay.ClearDisplayColorOverride();
            formulaDisplay.RefreshState();
        }

        for (int index = 0; index < columns.Length; index++)
        {
            if (columns[index] != null)
            {
                columns[index].ResetState();
            }
        }

        SetDoorGlowActive(false);
    }

    private void SetDoorGlowActive(bool isActive)
    {
        if (doorTransform == null)
        {
            return;
        }

        EnsureDoorGlowLight();

        if (doorGlowLight != null)
        {
            doorGlowLight.color = solvedDoorGlowColor;
            doorGlowLight.range = solvedDoorGlowLightRange;
            doorGlowLight.intensity = isActive ? solvedDoorGlowLightIntensity : 0f;
            doorGlowLight.enabled = isActive;
        }
    }

    private void EnsureDoorGlowLight()
    {
        if (doorGlowLight != null || doorTransform == null)
        {
            return;
        }

        Transform glowLightTransform = doorTransform.Find("Door Glow Light");
        if (glowLightTransform == null)
        {
            glowLightTransform = new GameObject("Door Glow Light").transform;
            glowLightTransform.SetParent(doorTransform, false);
        }

        glowLightTransform.localPosition = new Vector3(0f, 1f, -0.25f);
        glowLightTransform.localRotation = Quaternion.identity;

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