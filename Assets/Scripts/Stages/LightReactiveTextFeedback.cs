using UnityEngine;

[ExecuteAlways]
[AddComponentMenu("Stages/Light Reactive Text Feedback")]
public class LightReactiveTextFeedback : MonoBehaviour
{
    [SerializeField] private SpotlightSensor spotlightSensor;
    [SerializeField] private TextMesh targetText;
    [SerializeField] private Color baseColor = new Color(0.45f, 0.8f, 0.9f, 1f);
    [SerializeField] private Color litColor = new Color(1f, 0.95f, 0.55f, 1f);
    [SerializeField] private float idleScale = 1f;
    [SerializeField] private float litScale = 1.18f;
    [SerializeField] private bool hideWhenUnlit = true;
    [SerializeField] private float visibleThreshold = 0.02f;

    private Vector3 baseLocalScale = Vector3.one;
    private MeshRenderer targetRenderer;

    private void Reset()
    {
        spotlightSensor = GetComponentInParent<SpotlightSensor>();
        targetText = GetComponent<TextMesh>();
        CaptureBaseScale();
        RefreshState();
    }

    private void Awake()
    {
        ResolveReferences();
        CaptureBaseScale();
        RefreshState();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CaptureBaseScale();
        RefreshState();
    }

    private void OnValidate()
    {
        ResolveReferences();
        CaptureBaseScale();
        RefreshState();
    }

    private void Update()
    {
        RefreshState();
    }

    public void Configure(SpotlightSensor sensorReference, TextMesh textReference, Color idleColor, Color activeColor)
    {
        spotlightSensor = sensorReference;
        targetText = textReference;
        baseColor = idleColor;
        litColor = activeColor;
        ResolveReferences();
        CaptureBaseScale();
        RefreshState();
    }

    public void RefreshState()
    {
        ResolveReferences();
        if (targetText == null)
        {
            return;
        }

        if (spotlightSensor != null)
        {
            spotlightSensor.RefreshState();
        }

        float glow = spotlightSensor != null ? Mathf.Clamp01(spotlightSensor.Exposure01) : 0f;
        Color appliedColor = Color.Lerp(baseColor, litColor, glow);
        appliedColor.a = hideWhenUnlit ? glow : Mathf.Max(appliedColor.a, glow);
        targetText.color = appliedColor;
        transform.localScale = baseLocalScale * Mathf.Lerp(idleScale, litScale, glow);

        if (targetRenderer != null)
        {
            targetRenderer.enabled = !hideWhenUnlit || glow > visibleThreshold;
        }
    }

    private void ResolveReferences()
    {
        if (spotlightSensor == null)
        {
            spotlightSensor = GetComponentInParent<SpotlightSensor>();
        }

        if (targetText == null)
        {
            targetText = GetComponent<TextMesh>();
        }

        if (targetRenderer == null && targetText != null)
        {
            targetRenderer = targetText.GetComponent<MeshRenderer>();
        }
    }

    private void CaptureBaseScale()
    {
        if (baseLocalScale == Vector3.zero)
        {
            baseLocalScale = Vector3.one;
        }

        if (!Application.isPlaying)
        {
            baseLocalScale = transform.localScale;
        }
        else if (baseLocalScale == Vector3.one && transform.localScale != Vector3.zero)
        {
            baseLocalScale = transform.localScale;
        }
    }
}