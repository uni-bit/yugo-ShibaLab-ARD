using UnityEngine;

[AddComponentMenu("Stages/Stage Sequence Controller")]
public class StageSequenceController : MonoBehaviour
{
    [SerializeField] private GameObject[] stageRoots = new GameObject[3];
    [SerializeField] private int startingStageIndex;
    [SerializeField] private bool allowKeyboardShortcuts = true;
    [SerializeField] private KeyCode previousStageKey = KeyCode.LeftBracket;
    [SerializeField] private KeyCode nextStageKey = KeyCode.RightBracket;
    [SerializeField] private KeyCode stage1Key = KeyCode.Alpha1;
    [SerializeField] private KeyCode stage2Key = KeyCode.Alpha2;
    [SerializeField] private KeyCode stage3Key = KeyCode.Alpha3;

    public int CurrentStageIndex { get; private set; }

    private void Awake()
    {
        ApplyStageVisibility(Mathf.Clamp(startingStageIndex, 0, Mathf.Max(0, stageRoots.Length - 1)));
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            ApplyStageVisibility(Mathf.Clamp(startingStageIndex, 0, Mathf.Max(0, stageRoots.Length - 1)));
        }
    }

    private void Update()
    {
        if (!allowKeyboardShortcuts)
        {
            return;
        }

        if (Input.GetKeyDown(previousStageKey))
        {
            PreviousStage();
        }
        else if (Input.GetKeyDown(nextStageKey))
        {
            NextStage();
        }
        else if (Input.GetKeyDown(stage1Key))
        {
            SetStage(0);
        }
        else if (Input.GetKeyDown(stage2Key))
        {
            SetStage(1);
        }
        else if (Input.GetKeyDown(stage3Key))
        {
            SetStage(2);
        }
    }

    public void SetStage(int stageIndex)
    {
        ApplyStageVisibility(stageIndex);
    }

    public void NextStage()
    {
        if (stageRoots == null || stageRoots.Length == 0)
        {
            return;
        }

        SetStage((CurrentStageIndex + 1) % stageRoots.Length);
    }

    public void PreviousStage()
    {
        if (stageRoots == null || stageRoots.Length == 0)
        {
            return;
        }

        int previousIndex = CurrentStageIndex - 1;
        if (previousIndex < 0)
        {
            previousIndex = stageRoots.Length - 1;
        }

        SetStage(previousIndex);
    }

    private void ApplyStageVisibility(int activeIndex)
    {
        if (stageRoots == null || stageRoots.Length == 0)
        {
            CurrentStageIndex = 0;
            return;
        }

        int clampedIndex = Mathf.Clamp(activeIndex, 0, stageRoots.Length - 1);
        CurrentStageIndex = clampedIndex;

        for (int index = 0; index < stageRoots.Length; index++)
        {
            GameObject stageRoot = stageRoots[index];
            if (stageRoot == null)
            {
                continue;
            }

            stageRoot.SetActive(index == clampedIndex);
        }
    }
}