using UnityEngine;

[AddComponentMenu("Stages/Stage 3 Code Lock Puzzle")]
public class StageLightCodeLockPuzzle : MonoBehaviour
{
    [SerializeField] private StageLightCodeDialColumn[] columns = new StageLightCodeDialColumn[0];
    [SerializeField] private Transform doorTransform;
    [SerializeField] private TextMesh formulaText;
    [SerializeField] private string targetCode = "834";
    [SerializeField] private Vector3 openLocalOffset = new Vector3(0f, 4.4f, 0f);
    [SerializeField] private float openSpeed = 2.2f;
    [SerializeField] private Color lockedFormulaColor = Color.white;
    [SerializeField] private Color unlockedFormulaColor = new Color(1f, 0.86f, 0.3f, 1f);

    public bool IsSolved { get; private set; }

    private Vector3 closedDoorLocalPosition;

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
        if (!IsSolved && IsTargetCodeEntered())
        {
            IsSolved = true;
            ApplyFormulaState();
        }

        if (doorTransform != null)
        {
            Vector3 targetPosition = IsSolved ? closedDoorLocalPosition + openLocalOffset : closedDoorLocalPosition;
            float blend = 1f - Mathf.Exp(-openSpeed * Time.deltaTime);
            doorTransform.localPosition = Vector3.Lerp(doorTransform.localPosition, targetPosition, blend);
        }
    }

    public void Configure(StageLightCodeDialColumn[] dialColumns, Transform doorReference, TextMesh formulaTextReference, string code)
    {
        columns = dialColumns;
        doorTransform = doorReference;
        formulaText = formulaTextReference;
        targetCode = code;
        lockedFormulaColor = Color.white;
        unlockedFormulaColor = new Color(1f, 0.86f, 0.3f, 1f);
        IsSolved = false;

        if (doorTransform != null)
        {
            closedDoorLocalPosition = doorTransform.localPosition;
        }

        ApplyFormulaState();
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
            formulaText.color = IsSolved ? unlockedFormulaColor : lockedFormulaColor;
        }
    }
}