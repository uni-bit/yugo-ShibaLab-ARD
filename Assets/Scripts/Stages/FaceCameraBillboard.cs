using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[AddComponentMenu("Stages/Face Camera Billboard")]
public class FaceCameraBillboard : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool invertForward = true;

    private void LateUpdate()
    {
        Camera activeCamera = ResolveCamera();
        if (activeCamera == null)
        {
            return;
        }

        Vector3 toCamera = activeCamera.transform.position - transform.position;
        if (toCamera.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        Vector3 facingDirection = invertForward ? -toCamera.normalized : toCamera.normalized;
        transform.rotation = Quaternion.LookRotation(facingDirection, Vector3.up);
    }

    private Camera ResolveCamera()
    {
        if (targetCamera != null)
        {
            return targetCamera;
        }

        if (Camera.main != null)
        {
            return Camera.main;
        }

        Camera anyCamera = FindFirstObjectByType<Camera>();
        if (anyCamera != null)
        {
            return anyCamera;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying && SceneView.lastActiveSceneView != null)
        {
            return SceneView.lastActiveSceneView.camera;
        }
#endif

        return null;
    }
}