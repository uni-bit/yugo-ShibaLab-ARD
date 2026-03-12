using UnityEngine;

[AddComponentMenu("Stages/Stage 2 Number Reveal Puzzle")]
public class StageSymbolNumberRevealPuzzle : MonoBehaviour
{
    [SerializeField] private StageSymbolNumberRevealTarget[] revealTargets = new StageSymbolNumberRevealTarget[0];
    [SerializeField] private bool completeWhenAllNumbersFound = true;
    [SerializeField] private GameObject[] activateOnComplete = new GameObject[0];
    [SerializeField] private GameObject[] deactivateOnComplete = new GameObject[0];

    public bool IsSolved { get; private set; }

    private void OnEnable()
    {
        IsSolved = false;

        for (int index = 0; index < revealTargets.Length; index++)
        {
            if (revealTargets[index] != null)
            {
                revealTargets[index].ResetReveal();
            }
        }

        SetCompletionObjects(false);
    }

    private void Update()
    {
        if (IsSolved || !completeWhenAllNumbersFound || revealTargets.Length == 0)
        {
            return;
        }

        for (int index = 0; index < revealTargets.Length; index++)
        {
            StageSymbolNumberRevealTarget revealTarget = revealTargets[index];
            if (revealTarget == null || !revealTarget.HasBeenRevealed)
            {
                return;
            }
        }

        IsSolved = true;
        SetCompletionObjects(true);
    }

    public void Configure(StageSymbolNumberRevealTarget[] targets, GameObject[] activateOnSolved, GameObject[] deactivateOnSolved)
    {
        revealTargets = targets;
        activateOnComplete = activateOnSolved;
        deactivateOnComplete = deactivateOnSolved;
        IsSolved = false;
        SetCompletionObjects(false);
    }

    public void ForceComplete()
    {
        for (int index = 0; index < revealTargets.Length; index++)
        {
            if (revealTargets[index] != null)
            {
                revealTargets[index].ForceReveal();
            }
        }

        IsSolved = true;
        SetCompletionObjects(true);
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