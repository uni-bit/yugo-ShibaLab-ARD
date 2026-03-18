using UnityEngine;

[AddComponentMenu("Stages/Stage Root Marker")]
public class StageRootMarker : MonoBehaviour
{
    [SerializeField] private int stageIndex;
    [SerializeField] private string stageName;

    public int StageIndex => stageIndex;
    public string StageName => stageName;

    public void Configure(int index, string displayName)
    {
        stageIndex = index;
        stageName = displayName;
    }
}
