using UnityEngine;

[ExecuteAlways]
[AddComponentMenu("Stages/Light Reactive Renderer Feedback")]
public class LightReactiveRendererFeedback : MonoBehaviour
{
    [SerializeField] private SpotlightSensor spotlightSensor;
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Color baseColor = new Color(0.2f, 0.2f, 0.22f, 1f);
    [SerializeField] private Color litColor = new Color(0.45f, 0.95f, 1f, 1f);
    [SerializeField] private float emissionIntensity = 1.8f;

    private MaterialPropertyBlock propertyBlock;

    private void Reset()
    {
        spotlightSensor = GetComponent<SpotlightSensor>();
        targetRenderer = GetComponent<Renderer>();
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

    public void Configure(SpotlightSensor sensorReference, Renderer rendererReference, Color idleColor, Color activeColor)
    {
        spotlightSensor = sensorReference;
        targetRenderer = rendererReference;
        baseColor = idleColor;
        litColor = activeColor;
        ResolveReferences();
        RefreshState();
    }

    public void RefreshState()
    {
        ResolveReferences();
        if (targetRenderer == null)
        {
            return;
        }

        EnsurePropertyBlock();
        if (propertyBlock == null)
        {
            return;
        }

        if (spotlightSensor != null)
        {
            spotlightSensor.RefreshState();
        }

        float glow = spotlightSensor != null ? Mathf.Clamp01(spotlightSensor.Exposure01) : 0f;
        Color appliedColor = Color.Lerp(baseColor, litColor, glow);
        Color emissionColor = litColor * (glow * emissionIntensity);

        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor("_Color", appliedColor);
        propertyBlock.SetColor("_BaseColor", appliedColor);
        propertyBlock.SetColor("_EmissionColor", emissionColor);
        targetRenderer.SetPropertyBlock(propertyBlock);

        Material sharedMaterial = targetRenderer.sharedMaterial;
        if (sharedMaterial != null && sharedMaterial.HasProperty("_EmissionColor"))
        {
            sharedMaterial.EnableKeyword("_EMISSION");
        }
    }

    private void EnsurePropertyBlock()
    {
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }
    }

    private void ResolveReferences()
    {
        if (spotlightSensor == null)
        {
            spotlightSensor = GetComponent<SpotlightSensor>();
        }

        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<Renderer>();
        }
    }
}