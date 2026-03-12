using UnityEngine;

[AddComponentMenu("Stages/Stage 2 Puzzle Controller")]
public class Stage2PuzzleController : MonoBehaviour
{
    private enum Stage2State
    {
        Waiting,
        PlayingCompletion,
        Complete
    }

    [SerializeField] private StageSymbolNumberRevealPuzzle revealPuzzle;
    [SerializeField] private StageLightCodeLockPuzzle codeLockPuzzle;
    [SerializeField] private Stage2CompletionSequence completionSequence;
    [SerializeField] private KeyCode debugRevealCompleteKey = KeyCode.Alpha7;
    [SerializeField] private KeyCode debugFillCodeKey = KeyCode.Alpha8;

    private Stage2State currentState;

    private void OnEnable()
    {
        currentState = Stage2State.Waiting;
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (Input.GetKeyDown(debugRevealCompleteKey) && revealPuzzle != null)
        {
            revealPuzzle.ForceComplete();
        }

        if (Input.GetKeyDown(debugFillCodeKey) && codeLockPuzzle != null)
        {
            codeLockPuzzle.ApplyCodeInstantly(codeLockPuzzle.TargetCode);
        }

        switch (currentState)
        {
            case Stage2State.Waiting:
                if (revealPuzzle != null
                    && revealPuzzle.IsSolved
                    && codeLockPuzzle != null
                    && codeLockPuzzle.IsSolved
                    && completionSequence != null)
                {
                    codeLockPuzzle.ApplySolvedVisualState();
                    completionSequence.Play();
                    currentState = Stage2State.PlayingCompletion;
                }
                break;

            case Stage2State.PlayingCompletion:
                if (completionSequence != null && completionSequence.IsComplete)
                {
                    currentState = Stage2State.Complete;
                }
                break;
        }
    }

    public void Configure(
        StageSymbolNumberRevealPuzzle revealPuzzleReference,
        StageLightCodeLockPuzzle codeLockPuzzleReference,
        Stage2CompletionSequence completionSequenceReference)
    {
        revealPuzzle = revealPuzzleReference;
        codeLockPuzzle = codeLockPuzzleReference;
        completionSequence = completionSequenceReference;
        currentState = Stage2State.Waiting;
    }
}