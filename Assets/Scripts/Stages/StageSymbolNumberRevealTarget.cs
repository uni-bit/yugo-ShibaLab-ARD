using UnityEngine;

[AddComponentMenu("Stages/Stage 2 Number Reveal Target")]
public class StageSymbolNumberRevealTarget : MonoBehaviour
{
    [SerializeField] private SpotlightSensor spotlightSensor;
    [SerializeField] private Transform numberRoot;

    public bool IsCurrentlyRevealed => spotlightSensor != null && spotlightSensor.IsLit;
    public bool HasBeenRevealed { get; private set; }

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
    }

    private void Update()
    {
        bool shouldReveal = spotlightSensor != null && spotlightSensor.IsLit;

        if (shouldReveal)
        {
            HasBeenRevealed = true;
        }
    }

    public void Configure(SpotlightSensor sensorReference, Transform numberRootReference)
    {
        spotlightSensor = sensorReference;
        numberRoot = numberRootReference;
        ResetReveal();
    }

    public void ResetReveal()
    {
        HasBeenRevealed = false;
    }

    public void ForceReveal()
    {
        HasBeenRevealed = true;
    }
}