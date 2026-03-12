using UnityEngine;

[AddComponentMenu("Stages/Stage 3 Code Dial Column")]
public class StageLightCodeDialColumn : MonoBehaviour
{
    [SerializeField] private TextMesh digitText;
    [SerializeField] private StageLightCodeDigitAnimator digitAnimator;
    [SerializeField] private StageCodeLockRig codeLockRig;
    [SerializeField] private SpotlightSensor incrementSensor;
    [SerializeField] private SpotlightSensor decrementSensor;
    [SerializeField] private StageCodeLockButtonIndicator incrementIndicator;
    [SerializeField] private StageCodeLockButtonIndicator decrementIndicator;
    [SerializeField] private float holdSecondsPerStep = 1f;
    [SerializeField] private int startingDigit;
    [SerializeField] private Color solvedDigitColor = new Color(1f, 0.9f, 0.25f, 1f);

    public int CurrentDigit { get; private set; }
    public SpotlightSensor IncrementSensor => incrementSensor;
    public SpotlightSensor DecrementSensor => decrementSensor;

    private float incrementTimer;
    private float decrementTimer;
    private bool inputLocked;

    private void Awake()
    {
        SetDigit(startingDigit);
        ApplyIndicatorState(false, false);
    }

    private void Update()
    {
        bool incrementActive = ProcessSensor(incrementSensor, ref incrementTimer, 1);
        bool decrementActive = ProcessSensor(decrementSensor, ref decrementTimer, -1);
        ApplyIndicatorState(incrementActive, decrementActive);
    }

    public void Configure(
        TextMesh digitTextReference,
        SpotlightSensor incrementSensorReference,
        SpotlightSensor decrementSensorReference,
        StageCodeLockButtonIndicator incrementIndicatorReference,
        StageCodeLockButtonIndicator decrementIndicatorReference,
        StageCodeLockRig rigReference,
        int initialDigit)
    {
        digitText = digitTextReference;
        digitAnimator = digitText != null ? digitText.GetComponent<StageLightCodeDigitAnimator>() : null;
        incrementSensor = incrementSensorReference;
        decrementSensor = decrementSensorReference;
        incrementIndicator = incrementIndicatorReference;
        decrementIndicator = decrementIndicatorReference;
        codeLockRig = rigReference;
        startingDigit = Mathf.Clamp(initialDigit, 0, 9);
        incrementTimer = 0f;
        decrementTimer = 0f;
        SetDigit(startingDigit);
        ApplyIndicatorState(false, false);
    }

    public void SetDigitImmediate(int digit)
    {
        SetDigit(digit);
    }

    public void SetInputLocked(bool locked)
    {
        inputLocked = locked;
        if (locked)
        {
            incrementTimer = 0f;
            decrementTimer = 0f;
        }
    }

    public void ApplySolvedAppearance()
    {
        SetInputLocked(true);

        if (digitText != null)
        {
            digitText.color = solvedDigitColor;
            StageSpotlightMaterialUtility.ApplySpotlitText(digitText, solvedDigitColor, solvedDigitColor);
        }

        if (digitAnimator != null)
        {
            digitAnimator.RefreshVisualStyle();
        }

        if (incrementIndicator != null)
        {
            incrementIndicator.SetSolvedAppearance(true);
        }

        if (decrementIndicator != null)
        {
            decrementIndicator.SetSolvedAppearance(true);
        }
    }

    private bool ProcessSensor(SpotlightSensor sensor, ref float timer, int direction)
    {
        if (inputLocked)
        {
            timer = 0f;
            return false;
        }

        if (sensor == null || !sensor.IsLit)
        {
            timer = 0f;
            return false;
        }

        if (codeLockRig != null && !codeLockRig.IsDominantSensor(sensor))
        {
            timer = 0f;
            return false;
        }

        timer += Time.deltaTime;
        while (timer >= holdSecondsPerStep)
        {
            timer -= holdSecondsPerStep;
            SetDigit(CurrentDigit + direction, direction);
        }

        return true;
    }

    private void ApplyIndicatorState(bool incrementActive, bool decrementActive)
    {
        if (incrementIndicator != null)
        {
            incrementIndicator.SetProcessingActive(incrementActive);
        }

        if (decrementIndicator != null)
        {
            decrementIndicator.SetProcessingActive(decrementActive);
        }
    }

    private void SetDigit(int nextDigit, int direction = 0)
    {
        CurrentDigit = ((nextDigit % 10) + 10) % 10;
        if (digitAnimator != null)
        {
            if (direction == 0)
            {
                digitAnimator.SetDigitImmediate(CurrentDigit);
            }
            else
            {
                digitAnimator.AnimateToDigit(CurrentDigit, direction);
            }
        }

        if (digitText != null)
        {
            digitText.text = CurrentDigit.ToString();
        }
    }
}