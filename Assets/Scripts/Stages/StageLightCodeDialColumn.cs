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

    private void Reset()
    {
        ResolveDigitAnimator();
    }

    private void Awake()
    {
        ResolveDigitAnimator();
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
        ResolveDigitAnimator();
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

        Color emissionColor = new Color(
            solvedDigitColor.r * 2.5f,
            solvedDigitColor.g * 2.5f,
            solvedDigitColor.b * 2.5f,
            solvedDigitColor.a
        );

        if (digitText != null)
        {
            // 強制的にHDRカラー（2.5倍のEmission色）を本体の頂点カラーにも設定し、子テキスト全てに伝搬させる
            digitText.color = emissionColor;
            StageSpotlightMaterialUtility.ApplySpotlitText(digitText, emissionColor, emissionColor);
        }

        if (digitAnimator != null)
        {
            digitAnimator.RefreshVisualStyle();
        }

        if (digitAnimator != null && digitText != null)
        {
            MeshRenderer r = digitText.GetComponent<MeshRenderer>();
            if (r != null) r.enabled = false;
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

    public void ResetState()
    {
        inputLocked = false;
        incrementTimer = 0f;
        decrementTimer = 0f;

        if (digitText != null)
        {
            digitText.color = Color.white;
            StageSpotlightMaterialUtility.ApplySpotlitText(digitText, new Color(1f, 1f, 1f, 0f), Color.white);
        }

        SetDigit(startingDigit);

        if (digitAnimator != null)
        {
            digitAnimator.SetDigitImmediate(CurrentDigit);
            digitAnimator.RefreshVisualStyle();
        }

        if (digitAnimator != null && digitText != null)
        {
            MeshRenderer r = digitText.GetComponent<MeshRenderer>();
            if (r != null) r.enabled = false;
        }

        if (incrementIndicator != null)
        {
            incrementIndicator.SetSolvedAppearance(false);
            incrementIndicator.SetProcessingActive(false);
        }

        if (decrementIndicator != null)
        {
            decrementIndicator.SetSolvedAppearance(false);
            decrementIndicator.SetProcessingActive(false);
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
        ResolveDigitAnimator();
        CurrentDigit = ((nextDigit % 10) + 10) % 10;
        bool hasAnimator = digitAnimator != null;
        if (hasAnimator)
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
            if (hasAnimator)
            {
                // アニメーターがある場合、元の巨大なテキストは常に空にして絶対に表示させない
                digitText.text = "";
            }
            else
            {
                digitText.text = CurrentDigit.ToString();
            }
        }
    }

    private void ResolveDigitAnimator()
    {
        if (digitText == null)
        {
            digitAnimator = null;
            return;
        }

        digitAnimator = digitText.GetComponent<StageLightCodeDigitAnimator>();
        if (digitAnimator == null)
        {
            digitAnimator = digitText.gameObject.AddComponent<StageLightCodeDigitAnimator>();
        }
    }
}