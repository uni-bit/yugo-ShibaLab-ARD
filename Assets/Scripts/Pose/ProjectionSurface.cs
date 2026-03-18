using UnityEngine;

[ExecuteAlways]
[AddComponentMenu("Projection/Projection Surface")]
public class ProjectionSurface : MonoBehaviour
{
    [SerializeField] private float width = 5.3333335f;
    [SerializeField] private float height = 3f;
    [SerializeField] private Color gizmoColor = new Color(0f, 1f, 1f, 0.35f);

    public float Width => width;
    public float Height => height;
    public Vector3 BottomLeft => transform.position - (transform.right * (width * 0.5f)) - (transform.up * (height * 0.5f));
    public Vector3 BottomRight => transform.position + (transform.right * (width * 0.5f)) - (transform.up * (height * 0.5f));
    public Vector3 TopLeft => transform.position - (transform.right * (width * 0.5f)) + (transform.up * (height * 0.5f));
    public Vector3 TopRight => transform.position + (transform.right * (width * 0.5f)) + (transform.up * (height * 0.5f));

    public void Configure(float surfaceWidth, float surfaceHeight)
    {
        width = Mathf.Max(0.0001f, surfaceWidth);
        height = Mathf.Max(0.0001f, surfaceHeight);
    }

    public Vector3 GetPoint(float normalizedX, float normalizedY)
    {
        float clampedX = Mathf.Clamp01(normalizedX);
        float clampedY = Mathf.Clamp01(normalizedY);

        return transform.position
            + transform.right * ((clampedX - 0.5f) * width)
            + transform.up * ((clampedY - 0.5f) * height);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawLine(BottomLeft, BottomRight);
        Gizmos.DrawLine(BottomRight, TopRight);
        Gizmos.DrawLine(TopRight, TopLeft);
        Gizmos.DrawLine(TopLeft, BottomLeft);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.4f);
    }
}
