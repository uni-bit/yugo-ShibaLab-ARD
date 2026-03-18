using UnityEngine;

// Execute well after CinemachineBrain (typically order ~100) so that
// off-axis projection matrix overrides are applied last every LateUpdate.
[DefaultExecutionOrder(1000)]
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
[AddComponentMenu("Projection/Off Axis Projection Camera")]
public class OffAxisProjectionCamera : MonoBehaviour
{
    [SerializeField] private ProjectionSurface projectionSurface;
    [SerializeField] private Transform eyePoint;
    [SerializeField] private float nearClipPlane = 0.05f;
    [SerializeField] private float farClipPlane = 100f;
    [SerializeField] private bool flipHorizontally;
    [SerializeField] private bool flipVertically;

    private Camera targetCamera;

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        ApplyProjection();
    }

    private void LateUpdate()
    {
        ApplyProjection();
    }

    private void OnDisable()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        if (targetCamera != null)
        {
            targetCamera.ResetProjectionMatrix();
            targetCamera.ResetWorldToCameraMatrix();
        }
    }

    public void Configure(ProjectionSurface surface, Transform eye, bool horizontalFlip = false, bool verticalFlip = false)
    {
        projectionSurface = surface;
        eyePoint = eye;
        flipHorizontally = horizontalFlip;
        flipVertically = verticalFlip;
        ApplyProjection();
    }

    private void ApplyProjection()
    {
        if (projectionSurface == null || eyePoint == null)
        {
            return;
        }

        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        Vector3 pa = projectionSurface.BottomLeft;
        Vector3 pb = projectionSurface.BottomRight;
        Vector3 pc = projectionSurface.TopLeft;
        Vector3 pe = eyePoint.position;

        Vector3 vr = (pb - pa).normalized;
        Vector3 vu = (pc - pa).normalized;
        Vector3 vn = Vector3.Cross(vr, vu).normalized;

        if (Vector3.Dot(pe - pa, vn) < 0f)
        {
            vn = -vn;
        }

        Vector3 va = pa - pe;
        Vector3 vb = pb - pe;
        Vector3 vc = pc - pe;

        float distance = Vector3.Dot(-va, vn);
        if (distance <= 0.0001f)
        {
            return;
        }

        float near = Mathf.Max(0.001f, nearClipPlane);
        float far = Mathf.Max(near + 0.01f, farClipPlane);

        float left = Vector3.Dot(vr, va) * near / distance;
        float right = Vector3.Dot(vr, vb) * near / distance;
        float bottom = Vector3.Dot(vu, va) * near / distance;
        float top = Vector3.Dot(vu, vc) * near / distance;

        if (flipHorizontally)
        {
            float originalLeft = left;
            left = -right;
            right = -originalLeft;
        }

        if (flipVertically)
        {
            float originalBottom = bottom;
            bottom = -top;
            top = -originalBottom;
        }

        Matrix4x4 projection = PerspectiveOffCenter(left, right, bottom, top, near, far);
        Matrix4x4 rotation = Matrix4x4.identity;
        rotation.SetRow(0, new Vector4(vr.x, vr.y, vr.z, 0f));
        rotation.SetRow(1, new Vector4(vu.x, vu.y, vu.z, 0f));
        rotation.SetRow(2, new Vector4(vn.x, vn.y, vn.z, 0f));
        rotation.SetRow(3, new Vector4(0f, 0f, 0f, 1f));

        Matrix4x4 translation = Matrix4x4.Translate(-pe);

        targetCamera.nearClipPlane = near;
        targetCamera.farClipPlane = far;
        targetCamera.worldToCameraMatrix = rotation * translation;
        targetCamera.projectionMatrix = projection;

        transform.position = pe;
        transform.rotation = Quaternion.LookRotation(-vn, vu);
    }

    private static Matrix4x4 PerspectiveOffCenter(float left, float right, float bottom, float top, float near, float far)
    {
        float x = (2.0f * near) / (right - left);
        float y = (2.0f * near) / (top - bottom);
        float a = (right + left) / (right - left);
        float b = (top + bottom) / (top - bottom);
        float c = -(far + near) / (far - near);
        float d = -(2.0f * far * near) / (far - near);
        float e = -1.0f;

        Matrix4x4 matrix = new Matrix4x4();
        matrix[0, 0] = x;
        matrix[0, 2] = a;
        matrix[1, 1] = y;
        matrix[1, 2] = b;
        matrix[2, 2] = c;
        matrix[2, 3] = d;
        matrix[3, 2] = e;
        return matrix;
    }
}
