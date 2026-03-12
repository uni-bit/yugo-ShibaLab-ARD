using UnityEngine;

[AddComponentMenu("Stages/Stage Spotlight Settings")]
public class StageSpotlightSettings : MonoBehaviour
{
    [SerializeField] private bool lightEnabled = true;
    [SerializeField] private float spotAngle = 18f;
    [SerializeField] private float range = 60f;
    [SerializeField] private float intensity = 16f;
    [SerializeField] private Color color = Color.white;

    public bool LightEnabled => lightEnabled;
    public float SpotAngle => spotAngle;
    public float Range => range;
    public float Intensity => intensity;
    public Color Color => color;

    public void Configure(bool enabled, float angle, float lightRange, float lightIntensity, Color lightColor)
    {
        lightEnabled = enabled;
        spotAngle = angle;
        range = lightRange;
        intensity = lightIntensity;
        color = lightColor;
    }

    public void ApplyTo(Light targetLight)
    {
        if (targetLight == null)
        {
            return;
        }

        targetLight.enabled = lightEnabled;
        targetLight.spotAngle = spotAngle;
        targetLight.range = range;
        targetLight.intensity = intensity;
        targetLight.color = color;
    }
}