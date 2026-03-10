using UnityEngine;

[AddComponentMenu("Stages/Stage 3 Code Dial Column")]
public class StageLightCodeDialColumn : MonoBehaviour
{
    [SerializeField] private TextMesh digitText;
    [SerializeField] private SpotlightSensor incrementSensor;
    [SerializeField] private SpotlightSensor decrementSensor;
    [SerializeField] private float holdSecondsPerStep = 1f;
    [SerializeField] private int startingDigit;

    public int CurrentDigit { get; private set; }

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

    public void Configure(TextMesh digitTextReference, SpotlightSensor incrementSensorReference, SpotlightSensor decrementSensorReference, int initialDigit)
    {
        digitText = digitTextReference;
        incrementSensor = incrementSensorReference;
        decrementSensor = decrementSensorReference;
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

        timer += Time.deltaTime;
        while (timer >= holdSecondsPerStep)
        {
            timer -= holdSecondsPerStep;
            SetDigit(CurrentDigit + direction);
        }
    }

    private void SetDigit(int nextDigit)
    {
        CurrentDigit = ((nextDigit % 10) + 10) % 10;
        if (digitText != null)
        {
            digitText.text = CurrentDigit.ToString();
        }
    }
}