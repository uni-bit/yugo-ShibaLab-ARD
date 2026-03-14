using UnityEngine;

[AddComponentMenu("Stages/Stage 1 Creature Target")]
public class StageLightCreatureTarget : MonoBehaviour
{
    public enum ReactionMode
    {
        Hide,
        Hop
    }

    [SerializeField] private SpotlightSensor spotlightSensor;
    [SerializeField] private Transform animatedRoot;
    [SerializeField] private ReactionMode reactionMode = ReactionMode.Hop;
    [SerializeField] private float reactionDuration = 0.45f;
    [SerializeField] private float hopHeight = 0.35f;
    [SerializeField] private Vector3 hideOffset = new Vector3(0f, -0.45f, -0.2f);
    [SerializeField] private Vector3 startledRotation = new Vector3(-18f, 0f, 0f);

    public bool IsLit => spotlightSensor != null && spotlightSensor.IsLit;
    public bool IsSolved { get; private set; }
    public ReactionMode CurrentReactionMode => reactionMode;

    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;
    private float reactionTimer = -1f;

    private void Reset()
    {
        spotlightSensor = GetComponent<SpotlightSensor>();
        animatedRoot = transform;
    }

    private void Awake()
    {
        if (animatedRoot == null)
        {
            animatedRoot = transform;
        }

        initialLocalPosition = animatedRoot.localPosition;
        initialLocalRotation = animatedRoot.localRotation;
    }

    private void Update()
    {
        if (reactionTimer < 0f || animatedRoot == null)
        {
            return;
        }

        reactionTimer += Time.deltaTime;
        float normalizedTime = Mathf.Clamp01(reactionTimer / Mathf.Max(0.01f, reactionDuration));
        ApplyReactionPose(normalizedTime);

        if (normalizedTime >= 1f)
        {
            reactionTimer = -1f;
        }
    }

    public void TriggerReaction(bool solved)
    {
        IsSolved = solved;
        reactionTimer = 0f;
    }

    public void Configure(SpotlightSensor sensorReference, Transform animatedRootReference, ReactionMode reaction)
    {
        spotlightSensor = sensorReference;
        animatedRoot = animatedRootReference;
        reactionMode = reaction;

        if (animatedRoot == null)
        {
            animatedRoot = transform;
        }

        initialLocalPosition = animatedRoot.localPosition;
        initialLocalRotation = animatedRoot.localRotation;
        ResetTargetState();
    }

    public void ResetTargetState()
    {
        IsSolved = false;
        reactionTimer = -1f;

        if (animatedRoot != null)
        {
            animatedRoot.localPosition = initialLocalPosition;
            animatedRoot.localRotation = initialLocalRotation;
        }
    }

    private void ApplyReactionPose(float normalizedTime)
    {
        if (reactionMode == ReactionMode.Hide)
        {
            float ease = 1f - Mathf.Pow(1f - normalizedTime, 3f);
            animatedRoot.localPosition = initialLocalPosition + (hideOffset * ease);
            animatedRoot.localRotation = initialLocalRotation * Quaternion.Euler(startledRotation * (1f - ease));
            return;
        }

        float hop = Mathf.Sin(normalizedTime * Mathf.PI) * hopHeight;
        animatedRoot.localPosition = initialLocalPosition + Vector3.up * hop;
        animatedRoot.localRotation = initialLocalRotation * Quaternion.Euler(startledRotation * (1f - normalizedTime));
    }
}