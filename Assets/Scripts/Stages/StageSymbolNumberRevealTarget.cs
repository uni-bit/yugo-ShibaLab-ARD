using UnityEngine;

[AddComponentMenu("Stages/Stage 2 Number Reveal Target")]
public class StageSymbolNumberRevealTarget : MonoBehaviour
{
    [SerializeField] private SpotlightSensor spotlightSensor;
    [SerializeField] private Transform numberRoot;
    [SerializeField] private Vector3 hiddenLocalOffset = new Vector3(0f, -0.12f, 0f);
    [SerializeField] private float revealSpeed = 4f;
    [SerializeField] private float hideSpeed = 2f;
    [SerializeField] private float revealHoldSeconds = 0.5f;

    public bool IsCurrentlyRevealed => revealBlend > 0.99f;
    public bool HasBeenRevealed { get; private set; }

    private Vector3 visibleLocalPosition;
    private float revealBlend;
    private float revealHoldTimer;
    private Renderer[] cachedRenderers = new Renderer[0];

    private void Reset()
    {
        spotlightSensor = GetComponent<SpotlightSensor>();
        numberRoot = transform.childCount > 0 ? transform.GetChild(0) : null;
    }

    private void Awake()
    {
        if (numberRoot == null)
        {
            numberRoot = transform;
        }

        visibleLocalPosition = numberRoot.localPosition;
        cachedRenderers = numberRoot.GetComponentsInChildren<Renderer>(true);
        ApplyRevealState(0f);
    }

    private void Update()
    {
        bool shouldReveal = spotlightSensor != null && spotlightSensor.IsLit;

        if (shouldReveal)
        {
            revealHoldTimer = revealHoldSeconds;
        }
        else
        {
            revealHoldTimer = Mathf.Max(0f, revealHoldTimer - Time.deltaTime);
            shouldReveal = revealHoldTimer > 0f;
        }

        float speed = shouldReveal ? revealSpeed : hideSpeed;
        float targetBlend = shouldReveal ? 1f : 0f;
        revealBlend = Mathf.MoveTowards(revealBlend, targetBlend, speed * Time.deltaTime);
        ApplyRevealState(revealBlend);

        if (revealBlend >= 0.99f)
        {
            HasBeenRevealed = true;
        }
    }

    public void ResetReveal()
    {
        HasBeenRevealed = false;
        revealHoldTimer = 0f;
        revealBlend = 0f;
        ApplyRevealState(0f);
    }

    private void ApplyRevealState(float blend)
    {
        if (numberRoot == null)
        {
            return;
        }

        numberRoot.localPosition = visibleLocalPosition + (hiddenLocalOffset * (1f - blend));
        numberRoot.localScale = Vector3.one * Mathf.Lerp(0.75f, 1f, blend);

        bool shouldRender = blend > 0.02f;
        for (int index = 0; index < cachedRenderers.Length; index++)
        {
            if (cachedRenderers[index] != null)
            {
                cachedRenderers[index].enabled = shouldRender;
            }
        }
    }
}