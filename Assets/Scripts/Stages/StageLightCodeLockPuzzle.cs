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

    public bool IsSolved { get; private set; }
    public string TargetCode => targetCode;

    private Vector3 closedDoorLocalPosition;
    private bool solvedVisualsApplied;

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
    }
}