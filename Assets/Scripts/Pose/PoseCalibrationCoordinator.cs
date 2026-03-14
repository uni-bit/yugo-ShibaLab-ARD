using UnityEngine;

[AddComponentMenu("Pose/Pose Calibration Coordinator")]
public class PoseCalibrationCoordinator : MonoBehaviour
{
    [SerializeField] private UdpQuaternionReceiver receiver;
    [SerializeField] private PoseRotationDriver driver;
    [SerializeField] private TestScreenVisualizer visualizer;
    [SerializeField] private PoseTestBootstrap bootstrap;
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

            Vector2 touchPos;
            if (receiver.ConsumePendingTouchPosition(out touchPos))
            {
                ResetAllCalibrationWithTouchPoint(touchPos);
            }
            else
            {
                ResetAllCalibration();
            }
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
        ResolveBootstrap();
        lastHandledRecenterRequestCount = receiver != null ? receiver.RecenterRequestCount : 0;
    }

    public void ResetAllCalibration()
    {
        ResolveBootstrap();

        if (driver != null)
        {
            driver.ResetCalibration();
        }

        if (visualizer != null)
        {
            visualizer.ResetReference();
        }

        if (bootstrap != null)
        {
            bootstrap.ResetViewerRigPose();
        }
    }

    public void ResetAllCalibrationWithTouchPoint(Vector2 normalizedTouchPosition)
    {
        // キャリブレーションリセット前に現在のスポットライトターゲットを保存する。
        // driver/visualizer のリセット後でも previousTarget は変化しないため、
        // 差分オフセットの計算に使用できる。
        Vector3 previousTarget = visualizer != null ? visualizer.CurrentTargetPoint : Vector3.zero;

        if (driver != null)
        {
            driver.ResetCalibration();
        }

        if (visualizer != null)
        {
            visualizer.ResetReference();
            // タッチ位置とリセット前ターゲットから正しいオフセットを計算して適用する
            visualizer.SetCalibrationFromTouch(normalizedTouchPosition, previousTarget);
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

        ResolveBootstrap();
    }

    private void ResolveBootstrap()
    {
        if (bootstrap == null)
        {
            bootstrap = GetComponent<PoseTestBootstrap>();
        }
    }
}