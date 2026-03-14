using UnityEngine;

[ExecuteAlways]
[AddComponentMenu("Stages/Stage Code Digit Animator")]
public class StageLightCodeDigitAnimator : MonoBehaviour
{
    private const float IncomingExtraOffset = 0.55f;

    [SerializeField] private TextMesh primaryText;
    [SerializeField] private float animationDuration = 0.34f;
    [SerializeField] private float exitDistance = 0.9f;
    [SerializeField] private float entryDistance = 1.6f;
    [SerializeField] private float adjacentDigitOpacity = 0.36f;

    private const string SecondaryName = "Next Digit";
    private const string AdjacentName = "Adjacent Digit";

    private TextMesh secondaryText;
    private TextMesh adjacentText;
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
            secondaryText.transform.localPosition = new Vector3(0f, -(exitDistance + IncomingExtraOffset), 0f);
            secondaryText.gameObject.SetActive(false);
        }

        if (adjacentText != null)
        {
            adjacentText.text = displayedDigit.ToString();
            adjacentText.transform.localPosition = new Vector3(0f, -exitDistance, 0f);
            adjacentText.gameObject.SetActive(false);
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

        if (adjacentText != null)
        {
            adjacentText.gameObject.SetActive(false);
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

        Transform adjacentTransform = transform.Find(AdjacentName);
        if (adjacentTransform == null)
        {
            adjacentTransform = new GameObject(AdjacentName).transform;
            adjacentTransform.SetParent(transform, false);
        }

        adjacentText = adjacentTransform.GetComponent<TextMesh>();
        if (adjacentText == null)
        {
            adjacentText = adjacentTransform.gameObject.AddComponent<TextMesh>();
        }

        CopyPrimaryStyle();
    }

    private void CopyPrimaryStyle()
    {
        if (primaryText == null || secondaryText == null || adjacentText == null)
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
        adjacentText.font = primaryText.font;
        adjacentText.fontSize = primaryText.fontSize;
        adjacentText.characterSize = primaryText.characterSize;
        adjacentText.anchor = primaryText.anchor;
        adjacentText.alignment = primaryText.alignment;
        adjacentText.richText = primaryText.richText;
        adjacentText.color = primaryText.color;

        MeshRenderer secondaryRenderer = secondaryText.GetComponent<MeshRenderer>();
        MeshRenderer adjacentRenderer = adjacentText.GetComponent<MeshRenderer>();
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

        if (adjacentRenderer != null)
        {
            adjacentRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            adjacentRenderer.receiveShadows = false;
            if (primaryRenderer != null && primaryRenderer.sharedMaterial != null)
            {
                adjacentRenderer.sharedMaterial = primaryRenderer.sharedMaterial;
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
        float travelDistance = Mathf.Max(0.0001f, exitDistance);
        float incomingStartDistance = travelDistance + IncomingExtraOffset;
        float outgoingY = travelDistance * eased * animationDirection;
        float incomingY = (incomingStartDistance * (1f - eased) * -animationDirection);

        primaryText.transform.localPosition = new Vector3(0f, outgoingY, 0f);
        secondaryText.transform.localPosition = new Vector3(0f, incomingY, 0f);
        adjacentText.transform.localPosition = new Vector3(0f, incomingY, 0f);

        Color baseColor = primaryText.color;
        Color outgoingColor = baseColor;
        outgoingColor.a = Mathf.Lerp(1f, 0f, eased);
        Color incomingColor = baseColor;
        incomingColor.a = Mathf.Lerp(0f, 1f, eased);
        primaryText.color = outgoingColor;
        secondaryText.color = incomingColor;
        adjacentText.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);

        if (normalized >= 1f)
        {
            secondaryText.gameObject.SetActive(false);
            adjacentText.gameObject.SetActive(false);
        }
    }
}