using UnityEngine;

[ExecuteAlways]
[AddComponentMenu("Stages/Stage Code Lock Button Indicator")]
public class StageCodeLockButtonIndicator : MonoBehaviour
{
    [SerializeField] private SpotlightSensor spotlightSensor;
    [SerializeField] private Renderer buttonRenderer;
    [SerializeField] private Renderer arrowRenderer;
    [SerializeField] private Color idleButtonColor = new Color(0.2f, 0.2f, 0.22f, 1f);
    [SerializeField] private Color activeButtonColor = new Color(0.22f, 0.72f, 0.3f, 1f);
    [SerializeField] private Color idleArrowHiddenColor = new Color(0.45f, 0.8f, 0.9f, 0f);
    [SerializeField] private Color litArrowColor = new Color(1f, 0.95f, 0.55f, 1f);
    [SerializeField] private Color activeArrowColor = new Color(0.45f, 1f, 0.42f, 1f);
    [SerializeField] private float buttonEmissionIntensity = 0f;
    [SerializeField] private float activeButtonEmissionIntensity = 2.3f;
    [SerializeField] private float arrowEmissionIntensity = 0f;
    [SerializeField] private float activeArrowEmissionIntensity = 2.6f;
    [SerializeField] private Color solvedButtonColor = new Color(1f, 0.84f, 0.18f, 1f);
    [SerializeField] private Color solvedArrowColor = new Color(1f, 0.9f, 0.25f, 1f);

    private MaterialPropertyBlock buttonPropertyBlock;
    private MaterialPropertyBlock arrowPropertyBlock;
    private bool isProcessingActive;
    private bool useSolvedAppearance;

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

    public void Configure(SpotlightSensor sensorReference, Renderer buttonRendererReference, Renderer arrowRendererReference)
    {
        spotlightSensor = sensorReference;
        buttonRenderer = buttonRendererReference;
        arrowRenderer = arrowRendererReference;
        ResolveReferences();
        RefreshState();
    }

    public void SetProcessingActive(bool active)
    {
        if (useSolvedAppearance)
        {
            return;
        }

        if (isProcessingActive == active)
        {
            return;
        }

        isProcessingActive = active;
        RefreshState();
    }

    public void SetSolvedAppearance(bool solved)
    {
        useSolvedAppearance = solved;
        isProcessingActive = false;
        RefreshState();
    }

    private void RefreshState()
    {
        ResolveReferences();

        if (spotlightSensor != null)
        {
            spotlightSensor.RefreshState();
        }

        bool showActiveProcessing = !useSolvedAppearance && isProcessingActive && spotlightSensor != null && spotlightSensor.IsLit;
        ApplyButtonState(showActiveProcessing);
        ApplyArrowState(showActiveProcessing);
    }

    private void ApplyButtonState(bool showActiveProcessing)
    {
        if (buttonRenderer == null)
        {
            return;
        }

        EnsureButtonPropertyBlock();
        if (buttonPropertyBlock == null)
        {
            return;
        }

        Color appliedColor = useSolvedAppearance
            ? solvedButtonColor
            : showActiveProcessing ? activeButtonColor : idleButtonColor;
        float emissionIntensity = useSolvedAppearance
            ? activeButtonEmissionIntensity
            : showActiveProcessing ? activeButtonEmissionIntensity : buttonEmissionIntensity;
        Color emissionColor = appliedColor * emissionIntensity;

        buttonRenderer.GetPropertyBlock(buttonPropertyBlock);
        buttonPropertyBlock.SetColor("_Color", appliedColor);
        buttonPropertyBlock.SetColor("_BaseColor", appliedColor);
        buttonPropertyBlock.SetColor("_EmissionColor", emissionColor);
        buttonRenderer.SetPropertyBlock(buttonPropertyBlock);

        Material sharedMaterial = buttonRenderer.sharedMaterial;
        if (sharedMaterial != null && sharedMaterial.HasProperty("_EmissionColor"))
        {
            if (emissionIntensity > 0.0001f)
            {
                sharedMaterial.EnableKeyword("_EMISSION");
            }
            else
            {
                sharedMaterial.DisableKeyword("_EMISSION");
            }
        }
    }

    private void ApplyArrowState(bool showActiveProcessing)
    {
        if (arrowRenderer == null)
        {
            return;
        }

        EnsureArrowPropertyBlock();
        if (arrowPropertyBlock == null)
        {
            return;
        }

        Color litColor = useSolvedAppearance
            ? solvedArrowColor
            : showActiveProcessing ? activeArrowColor : litArrowColor;
        Color hiddenColor = useSolvedAppearance ? solvedArrowColor : idleArrowHiddenColor;
        float emissionIntensity = useSolvedAppearance
            ? activeArrowEmissionIntensity
            : showActiveProcessing ? activeArrowEmissionIntensity : arrowEmissionIntensity;

        arrowRenderer.GetPropertyBlock(arrowPropertyBlock);
        arrowPropertyBlock.SetColor("_HiddenColor", hiddenColor);
        arrowPropertyBlock.SetColor("_LitColor", litColor);
        arrowPropertyBlock.SetColor("_EmissionColor", litColor * emissionIntensity);
        arrowRenderer.SetPropertyBlock(arrowPropertyBlock);
    }

    private void EnsureButtonPropertyBlock()
    {
        if (buttonPropertyBlock == null)
        {
            buttonPropertyBlock = new MaterialPropertyBlock();
        }
    }

    private void EnsureArrowPropertyBlock()
    {
        if (arrowPropertyBlock == null)
        {
            arrowPropertyBlock = new MaterialPropertyBlock();
        }
    }

    private void ResolveReferences()
    {
        if (spotlightSensor == null)
        {
            spotlightSensor = GetComponent<SpotlightSensor>();
        }

        if (buttonRenderer == null)
        {
            buttonRenderer = GetComponent<Renderer>();
        }
    }
}