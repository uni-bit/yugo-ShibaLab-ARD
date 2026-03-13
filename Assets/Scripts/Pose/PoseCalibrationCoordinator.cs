using UnityEngine;

[AddComponentMenu("Pose/Pose Calibration Coordinator")]
public class PoseCalibrationCoordinator : MonoBehaviour
{
    [SerializeField] private UdpQuaternionReceiver receiver;
    [SerializeField] private PoseRotationDriver driver;
    [SerializeField] private TestScreenVisualizer visualizer;
    [SerializeField] private KeyCode recenterKey = KeyCode.C;

    private int lastHandledRecenterRequestCount;

    private void Reset()
    {
        ResolveReferences();
        lastHandledRecenterRequestCount = receiver != null ? receiver.RecenterRequestCount : 0;
    }

    private void Awake()
    {
        ResolveReferences();
        lastHandledRecenterRequestCount = receiver != null ? receiver.RecenterRequestCount : 0;
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (Input.GetKeyDown(recenterKey))
        {
            ResetAllCalibration();
        }

        if (receiver != null && receiver.ConsumePendingRecenterRequest())
        {
            lastHandledRecenterRequestCount = receiver.RecenterRequestCount;
            ResetAllCalibration();
            return;
        }

        if (receiver != null && receiver.RecenterRequestCount != lastHandledRecenterRequestCount)
        {
            lastHandledRecenterRequestCount = receiver.RecenterRequestCount;
            ResetAllCalibration();
        }
    }

    public void Configure(UdpQuaternionReceiver receiverReference, PoseRotationDriver driverReference, TestScreenVisualizer visualizerReference)
    {
        receiver = receiverReference;
        driver = driverReference;
        visualizer = visualizerReference;
        lastHandledRecenterRequestCount = receiver != null ? receiver.RecenterRequestCount : 0;
    }

    public void ResetAllCalibration()
    {
        if (driver != null)
        {
            driver.ResetCalibration();
        }

        if (visualizer != null)
        {
            visualizer.ResetReference();
        }
    }

    private void ResolveReferences()
    {
        if (receiver == null)
        {
            receiver = GetComponent<UdpQuaternionReceiver>();
        }

        if (driver == null)
        {
            driver = GetComponent<PoseRotationDriver>();
        }

        if (visualizer == null)
        {
            visualizer = GetComponent<TestScreenVisualizer>();
        }
    }
}