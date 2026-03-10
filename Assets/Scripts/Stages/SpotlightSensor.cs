using UnityEngine;

[AddComponentMenu("Stages/Spotlight Sensor")]
public class SpotlightSensor : MonoBehaviour
{
    [SerializeField] private PoseTestBootstrap bootstrap;
    [SerializeField] private Light sourceLight;
    [SerializeField] private Transform samplePoint;
    [SerializeField] private Renderer sampleRenderer;
    [SerializeField] private Collider sampleCollider;
    [SerializeField] private float activationThreshold = 0.2f;
    [SerializeField] private bool requireLineOfSight;
    [SerializeField] private LayerMask occlusionMask = ~0;

    public bool IsLit { get; private set; }
    public float Exposure01 { get; private set; }

    private void Reset()
    {
        samplePoint = transform;
        sampleRenderer = GetComponentInChildren<Renderer>();
        sampleCollider = GetComponent<Collider>();
    }

    private void Update()
    {
        ResolveLight();
        Exposure01 = EvaluateExposure();
        IsLit = Exposure01 >= activationThreshold;
    }

    public void SetLightSource(Light lightSource)
    {
        sourceLight = lightSource;
    }

    private void ResolveLight()
    {
        if (sourceLight != null)
        {
            return;
        }

        if (bootstrap == null)
        {
            bootstrap = FindFirstObjectByType<PoseTestBootstrap>();
        }

        if (bootstrap != null)
        {
            sourceLight = bootstrap.ActiveSpotLight;
        }
    }

    private float EvaluateExposure()
    {
        if (sourceLight == null || !sourceLight.enabled || sourceLight.type != LightType.Spot)
        {
            return 0f;
        }

        Vector3 sampleWorldPosition = GetSampleWorldPosition();
        Vector3 lightToSample = sampleWorldPosition - sourceLight.transform.position;
        float distance = lightToSample.magnitude;

        if (distance <= 0.0001f || distance > sourceLight.range)
        {
            return 0f;
        }

        Vector3 direction = lightToSample / distance;
        float angleToTarget = Vector3.Angle(sourceLight.transform.forward, direction);
        float halfAngle = sourceLight.spotAngle * 0.5f;
        if (angleToTarget > halfAngle)
        {
            return 0f;
        }

        if (requireLineOfSight && Physics.Raycast(sourceLight.transform.position, direction, out RaycastHit hit, distance, occlusionMask, QueryTriggerInteraction.Ignore))
        {
            Transform hitTransform = hit.transform;
            bool isSelf = hitTransform == transform || hitTransform.IsChildOf(transform);
            if (!isSelf)
            {
                return 0f;
            }
        }

        float angularFalloff = 1f - Mathf.Clamp01(angleToTarget / Mathf.Max(halfAngle, 0.0001f));
        float distanceFalloff = 1f - Mathf.Clamp01(distance / Mathf.Max(sourceLight.range, 0.0001f));
        return angularFalloff * distanceFalloff;
    }

    private Vector3 GetSampleWorldPosition()
    {
        if (samplePoint != null)
        {
            return samplePoint.position;
        }

        if (sampleRenderer != null)
        {
            return sampleRenderer.bounds.center;
        }

        if (sampleCollider != null)
        {
            return sampleCollider.bounds.center;
        }

        return transform.position;
    }
}