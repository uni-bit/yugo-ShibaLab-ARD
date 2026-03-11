using UnityEngine;

[AddComponentMenu("Stages/Stage 3 Code Dial Column")]
public class StageLightCodeDialColumn : MonoBehaviour
{
    [SerializeField] private TextMesh digitText;
    [SerializeField] private StageLightCodeDigitAnimator digitAnimator;
    [SerializeField] private StageCodeLockRig codeLockRig;
    [SerializeField] private SpotlightSensor incrementSensor;
    [SerializeField] private SpotlightSensor decrementSensor;
    [SerializeField] private float holdSecondsPerStep = 1f;
    [SerializeField] private int startingDigit;

    public int CurrentDigit { get; private set; }
    public SpotlightSensor IncrementSensor => incrementSensor;
    public SpotlightSensor DecrementSensor => decrementSensor;

    private float incrementTimer;
    private float decrementTimer;

    private void Awake()
    {
        SetDigit(startingDigit);
    }

    private void Update()
    {
        ProcessSensor(incrementSensor, ref incrementTimer, 1);
        ProcessSensor(decrementSensor, ref decrementTimer, -1);
    }

    public void Configure(TextMesh digitTextReference, SpotlightSensor incrementSensorReference, SpotlightSensor decrementSensorReference, StageCodeLockRig rigReference, int initialDigit)
    {
        digitText = digitTextReference;
        digitAnimator = digitText != null ? digitText.GetComponent<StageLightCodeDigitAnimator>() : null;
        incrementSensor = incrementSensorReference;
        decrementSensor = decrementSensorReference;
        codeLockRig = rigReference;
        startingDigit = Mathf.Clamp(initialDigit, 0, 9);
        incrementTimer = 0f;
        decrementTimer = 0f;
        SetDigit(startingDigit);
    }

    private void ProcessSensor(SpotlightSensor sensor, ref float timer, int direction)
    {
        if (sensor == null || !sensor.IsLit)
        {
            timer = 0f;
            return;
        }

        if (codeLockRig != null && !codeLockRig.IsDominantSensor(sensor))
        {
            timer = 0f;
            return;
        }

        timer += Time.deltaTime;
        while (timer >= holdSecondsPerStep)
        {
            timer -= holdSecondsPerStep;
            SetDigit(CurrentDigit + direction, direction);
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