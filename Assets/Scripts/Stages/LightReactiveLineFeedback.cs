using UnityEngine;

[ExecuteAlways]
[AddComponentMenu("Stages/Light Reactive Line Feedback")]
public class LightReactiveLineFeedback : MonoBehaviour
{
    [SerializeField] private SpotlightSensor spotlightSensor;
    [SerializeField] private LineRenderer targetLine;
    [SerializeField] private Color unlitColor = new Color(0.55f, 0.9f, 1f, 0f);
    [SerializeField] private Color litColor = new Color(0.55f, 0.9f, 1f, 1f);
    [SerializeField] private float visibleThreshold = 0.02f;

    private void Reset()
    {
        spotlightSensor = GetComponent<SpotlightSensor>();
        targetLine = GetComponent<LineRenderer>();
        RefreshState();
    }

    private void Awake()
    {
        ResolveReferences();
        RefreshState();
    }

    private void OnEnable()
    {
        ResolveReferences();
        RefreshState();
    }

    private void OnValidate()
    {
        ResolveReferences();
        RefreshState();
    }

    private void Update()
    {
        RefreshState();
    }

    public void Configure(SpotlightSensor sensorReference, LineRenderer lineReference, Color hiddenColor, Color activeColor)
    {
        spotlightSensor = sensorReference;
        targetLine = lineReference;
        unlitColor = hiddenColor;
        litColor = activeColor;
        ResolveReferences();
        RefreshState();
    }

    public void RefreshState()
    {
        ResolveReferences();
        if (targetLine == null)
        {
            return;
        }

        if (spotlightSensor != null)
        {
            spotlightSensor.RefreshState();
        }

        float glow = spotlightSensor != null ? Mathf.Clamp01(spotlightSensor.Exposure01) : 0f;
        Color appliedColor = Color.Lerp(unlitColor, litColor, glow);
        targetLine.enabled = glow > visibleThreshold;
        targetLine.startColor = appliedColor;
        targetLine.endColor = appliedColor;
    }

    private void ResolveReferences()
    {
        if (spotlightSensor == null)
        {
            spotlightSensor = GetComponent<SpotlightSensor>();
        }

        if (targetLine == null)
        {
            targetLine = GetComponent<LineRenderer>();
        }
    }
}