using UnityEngine;

[AddComponentMenu("Stages/Stage 1 Ordered Puzzle")]
public class StageLightOrderedPuzzle : MonoBehaviour
{
    [SerializeField] private StageLightCreatureTarget[] targets = new StageLightCreatureTarget[0];
    [SerializeField] private float requiredFocusSeconds = 0.2f;
    [SerializeField] private bool resetOnWrongTarget = true;
    [SerializeField] private GameObject[] activateOnComplete = new GameObject[0];
    [SerializeField] private GameObject[] deactivateOnComplete = new GameObject[0];

    public int CurrentTargetIndex { get; private set; }
    public bool IsSolved { get; private set; }

    private float focusTimer;

    private void OnEnable()
    {
        ResetPuzzle();
    }

    private void Update()
    {
        if (IsSolved || targets == null || targets.Length == 0)
        {
            return;
        }

        StageLightCreatureTarget currentTarget = targets[CurrentTargetIndex];
        if (currentTarget == null)
        {
            AdvanceProgress();
            return;
        }

        if (IsWrongTargetLit())
        {
            focusTimer = 0f;

            if (resetOnWrongTarget)
            {
                ResetPuzzle();
            }

            return;
        }

        if (!currentTarget.IsLit)
        {
            focusTimer = 0f;
            return;
        }

        focusTimer += Time.deltaTime;
        if (focusTimer < requiredFocusSeconds)
        {
            return;
        }

        currentTarget.TriggerReaction(true);
        AdvanceProgress();
    }

    public void ResetPuzzle()
    {
        IsSolved = false;
        CurrentTargetIndex = 0;
        focusTimer = 0f;

        for (int index = 0; index < targets.Length; index++)
        {
            if (targets[index] != null)
            {
                targets[index].ResetTargetState();
            }
        }

        SetCompletionObjects(false);
    }

    private void AdvanceProgress()
    {
        focusTimer = 0f;
        CurrentTargetIndex++;

        if (CurrentTargetIndex < targets.Length)
        {
            return;
        }

        IsSolved = true;
        SetCompletionObjects(true);
    }

    private bool IsWrongTargetLit()
    {
        for (int index = 0; index < targets.Length; index++)
        {
            if (index == CurrentTargetIndex)
            {
                continue;
            }

            StageLightCreatureTarget target = targets[index];
            if (target != null && target.IsLit)
            {
                return true;
            }
        }

        return false;
    }

    private void SetCompletionObjects(bool solved)
    {
        for (int index = 0; index < activateOnComplete.Length; index++)
        {
            if (activateOnComplete[index] != null)
            {
                activateOnComplete[index].SetActive(solved);
            }
        }

        for (int index = 0; index < deactivateOnComplete.Length; index++)
        {
            if (deactivateOnComplete[index] != null)
            {
                deactivateOnComplete[index].SetActive(!solved);
            }
        }
    }
}