using UnityEngine;

[ExecuteAlways]
[AddComponentMenu("Stages/Stage Code Digit Animator")]
public class StageLightCodeDigitAnimator : MonoBehaviour
{
    [SerializeField] private TextMesh primaryText;
    [SerializeField] private float animationDuration = 0.2f;
    [SerializeField] private float travelDistance = 0.32f;

    private const string SecondaryName = "Next Digit";

    private TextMesh secondaryText;
    private int displayedDigit;
    private int targetDigit;
    private int animationDirection;
    private float animationTime;
    private bool isAnimating;

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
        ApplyAnimationFrame(1f);
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
        EnsureTexts();

        if (primaryText != null)
        {
            primaryText.text = displayedDigit.ToString();
            primaryText.transform.localPosition = Vector3.zero;
        }

        if (secondaryText != null)
        {
            secondaryText.text = displayedDigit.ToString();
            secondaryText.transform.localPosition = new Vector3(0f, -travelDistance, 0f);
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
    }

    private void ApplyAnimationFrame(float normalized)
    {
        EnsureTexts();
        if (primaryText == null || secondaryText == null)
        {
            return;
        }

        float eased = Mathf.SmoothStep(0f, 1f, normalized);
        float outgoingY = travelDistance * eased * animationDirection;
        float incomingY = travelDistance * (eased - 1f) * animationDirection;

        primaryText.transform.localPosition = new Vector3(0f, outgoingY, 0f);
        secondaryText.transform.localPosition = new Vector3(0f, incomingY, 0f);
        if (normalized >= 1f)
        {
            secondaryText.gameObject.SetActive(false);
        }
    }
}