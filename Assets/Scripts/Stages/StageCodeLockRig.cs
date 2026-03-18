using UnityEngine;

[ExecuteAlways]
[AddComponentMenu("Stages/Stage Code Lock Rig")]
public class StageCodeLockRig : MonoBehaviour
{
    [SerializeField] private Transform panelTransform;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private Transform formulaRoot;
    [SerializeField] private Transform doorTransform;
    [SerializeField] private Transform[] columnRoots = new Transform[0];

    private SpotlightSensor dominantSensor;
    private int dominantSensorFrame = -1;

    public void Configure(
        Transform panelReference,
        Transform contentReference,
        Transform formulaReference,
        Transform doorReference,
        Transform[] columnRoots)
    {
        panelTransform = panelReference;
        contentRoot = contentReference;
        formulaRoot = formulaReference;
        doorTransform = doorReference;
        this.columnRoots = columnRoots ?? new Transform[0];
    }

    public bool IsDominantSensor(SpotlightSensor sensor)
    {
        if (sensor == null)
        {
            return false;
        }

        UpdateDominantSensor();
        return sensor == dominantSensor;
    }

    private void UpdateDominantSensor()
    {
        if (dominantSensorFrame == Time.frameCount)
        {
            return;
        }

        dominantSensorFrame = Time.frameCount;
        dominantSensor = null;
        float bestExposure = 0f;
        float bestDistance = float.MaxValue;

        StageLightCodeDialColumn[] dialColumns = GetComponentsInChildren<StageLightCodeDialColumn>(true);
        for (int index = 0; index < dialColumns.Length; index++)
        {
            StageLightCodeDialColumn column = dialColumns[index];
            if (column == null)
            {
                continue;
            }

            EvaluateSensor(column.IncrementSensor, ref bestExposure, ref bestDistance);
            EvaluateSensor(column.DecrementSensor, ref bestExposure, ref bestDistance);
        }
    }

    private void EvaluateSensor(SpotlightSensor sensor, ref float bestExposure, ref float bestDistance)
    {
        if (sensor == null)
        {
            return;
        }

        sensor.RefreshState();
        if (!sensor.IsLit)
        {
            return;
        }

        float exposure = sensor.Exposure01;
        float distance = sensor.SourceLight != null
            ? Vector3.Distance(sensor.SourceLight.transform.position, sensor.transform.position)
            : float.MaxValue;

        bool isBetter = exposure > bestExposure + 0.0001f;
        bool isTieButCloser = Mathf.Abs(exposure - bestExposure) <= 0.0001f && distance < bestDistance;
        if (!isBetter && !isTieButCloser)
        {
            return;
        }

        bestExposure = exposure;
        bestDistance = distance;
        dominantSensor = sensor;
    }
}