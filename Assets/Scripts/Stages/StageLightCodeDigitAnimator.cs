using UnityEngine;

[ExecuteAlways]
[AddComponentMenu("Stages/Stage Code Digit Animator")]
public class StageLightCodeDigitAnimator : MonoBehaviour
{
    [SerializeField] private TextMesh primaryText;
    [Header("Timing")]
    [SerializeField] private float animationDuration = 0.34f;
    [SerializeField] private AnimationCurve travelCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Layout")]
    [SerializeField] private float digitSpacing = 0.52f;

    [Header("Opacity")]
    [SerializeField, Range(0f, 1f)] private float previousDigitStartOpacity = 1f;
    [SerializeField, Range(0f, 1f)] private float previousDigitEndOpacity = 0f;
    [SerializeField, Range(0f, 1f)] private float currentDigitStartOpacity = 0f;
    [SerializeField, Range(0f, 1f)] private float currentDigitEndOpacity = 1f;

    private const string SecondaryName = "Next Digit";

    private TextMesh secondaryText;
    private int displayedDigit;
    private int targetDigit;
    private int animationDirection;
    private float animationTime;
    private bool isAnimating;
    private Color baseTextColor = Color.white;

    private void Reset()
    {
        EnsureTexts();
        SetDigitImmediate(0);
    }

    private void Awake()
    {
        EnsureTexts();
        SetDigitImmediate(displayedDigit);
    }

    private void OnEnable()
    {
        EnsureTexts();
        SetDigitImmediate(displayedDigit);
    }

    private void OnValidate()
    {
        EnsureTexts();

        if (isAnimating)
        {
            float normalized = animationDuration <= 0.0001f ? 1f : Mathf.Clamp01(animationTime / animationDuration);
            ApplyAnimationFrame(normalized);
            return;
        }

        SetDigitImmediate(displayedDigit);
    }

    private void Update()
    {
        if (!isAnimating)
        {
            return;
        }

        animationTime += Time.deltaTime;
        float normalized = Mathf.Clamp01(animationDuration <= 0.0001f ? 1f : animationTime / animationDuration);
        ApplyAnimationFrame(normalized);

        if (normalized < 1f)
        {
            return;
        }

        displayedDigit = targetDigit;
        isAnimating = false;
        SetDigitImmediate(displayedDigit);
    }

    public void SetDigitImmediate(int digit)
    {
        displayedDigit = ((digit % 10) + 10) % 10;
        targetDigit = displayedDigit;
        isAnimating = false;
        animationTime = 0f;
        EnsureTexts();
        CacheBaseColor();

        if (primaryText != null)
        {
            primaryText.text = displayedDigit.ToString();
            primaryText.transform.localPosition = Vector3.zero;
            primaryText.color = baseTextColor;
        }

        if (secondaryText != null)
        {
            secondaryText.text = displayedDigit.ToString();
            secondaryText.transform.localPosition = new Vector3(0f, -GetDigitSpacing(), 0f);
            secondaryText.color = new Color(baseTextColor.r, baseTextColor.g, baseTextColor.b, 0f);
            secondaryText.gameObject.SetActive(false);
        }
    }

    public void AnimateToDigit(int digit, int direction)
    {
        int normalizedDigit = ((digit % 10) + 10) % 10;
        if (normalizedDigit == displayedDigit && !isAnimating)
        {
            SetDigitImmediate(normalizedDigit);
            return;
        }

        EnsureTexts();
        CacheBaseColor();
        targetDigit = normalizedDigit;
        animationDirection = direction >= 0 ? 1 : -1;
        animationTime = 0f;
        isAnimating = true;

        if (primaryText != null)
        {
            primaryText.text = displayedDigit.ToString();
        }

        if (secondaryText != null)
        {
            secondaryText.text = targetDigit.ToString();
            secondaryText.gameObject.SetActive(true);
        }

        ApplyAnimationFrame(0f);
    }

    public void RefreshVisualStyle()
    {
        EnsureTexts();
        CopyPrimaryStyle();
    }

    private void EnsureTexts()
    {
        if (primaryText == null)
        {
            primaryText = GetComponent<TextMesh>();
        }

        Transform secondaryTransform = transform.Find(SecondaryName);
        if (secondaryTransform == null)
        {
            secondaryTransform = new GameObject(SecondaryName).transform;
            secondaryTransform.SetParent(transform, false);
        }

        secondaryText = secondaryTransform.GetComponent<TextMesh>();
        if (secondaryText == null)
        {
            secondaryText = secondaryTransform.gameObject.AddComponent<TextMesh>();
        }

        CopyPrimaryStyle();
    }

    private void CopyPrimaryStyle()
    {
        if (primaryText == null || secondaryText == null)
        {
            return;
        }

        secondaryText.font = primaryText.font;
        secondaryText.fontSize = primaryText.fontSize;
        secondaryText.characterSize = primaryText.characterSize;
        secondaryText.anchor = primaryText.anchor;
        secondaryText.alignment = primaryText.alignment;
        secondaryText.richText = primaryText.richText;
        secondaryText.color = primaryText.color;

        MeshRenderer secondaryRenderer = secondaryText.GetComponent<MeshRenderer>();
        MeshRenderer primaryRenderer = primaryText.GetComponent<MeshRenderer>();
        if (secondaryRenderer != null)
        {
            secondaryRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            secondaryRenderer.receiveShadows = false;
            if (primaryRenderer != null && primaryRenderer.sharedMaterial != null)
            {
                secondaryRenderer.sharedMaterial = primaryRenderer.sharedMaterial;
            }
        }

        CacheBaseColor();
    }

    private void ApplyAnimationFrame(float normalized)
    {
        EnsureTexts();
        if (primaryText == null || secondaryText == null)
        {
            return;
        }

        float eased = EvaluateTravel(normalized);
        float spacing = GetDigitSpacing();
        float outgoingY = spacing * eased * animationDirection;
        float incomingY = outgoingY - (spacing * animationDirection);

        primaryText.transform.localPosition = new Vector3(0f, outgoingY, 0f);
        secondaryText.transform.localPosition = new Vector3(0f, incomingY, 0f);

        Color outgoingColor = baseTextColor;
        outgoingColor.a = baseTextColor.a * Mathf.Lerp(previousDigitStartOpacity, previousDigitEndOpacity, eased);
        Color incomingColor = baseTextColor;
        incomingColor.a = baseTextColor.a * Mathf.Lerp(currentDigitStartOpacity, currentDigitEndOpacity, eased);
        primaryText.color = outgoingColor;
        secondaryText.color = incomingColor;

        if (normalized >= 1f)
        {
            secondaryText.gameObject.SetActive(false);
        }
    }

    private void CacheBaseColor()
    {
        if (primaryText != null)
        {
            Color candidate = primaryText.color;
            if (candidate.a > 0.0001f || baseTextColor.a <= 0.0001f)
            {
                baseTextColor = candidate;
            }
        }
    }

    private float EvaluateTravel(float normalized)
    {
        float clamped = Mathf.Clamp01(normalized);
        if (travelCurve == null || travelCurve.length == 0)
        {
            return Mathf.SmoothStep(0f, 1f, clamped);
        }

        return Mathf.Clamp01(travelCurve.Evaluate(clamped));
    }

    private float GetDigitSpacing()
    {
        return Mathf.Max(0.0001f, digitSpacing);
    }
}