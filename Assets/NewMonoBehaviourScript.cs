using UnityEngine;

[AddComponentMenu("Legacy/New Mono Behaviour Script")]
public class NewMonoBehaviourScript : MonoBehaviour
{
    private void Awake()
    {
        Debug.Log("Use the separated scripts under Assets/Scripts/Pose instead of NewMonoBehaviourScript.", this);
    }
}
