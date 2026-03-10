using System.Globalization;
using UnityEngine;

[AddComponentMenu("Pose/Pose Debug Overlay")]
public class PoseDebugOverlay : MonoBehaviour
{
    [SerializeField] private UdpQuaternionReceiver receiver;
    [SerializeField] private PoseRotationDriver driver;
    [SerializeField] private TestScreenVisualizer visualizer;
    [SerializeField] private bool showOverlay = true;
    [SerializeField] private bool showPacketDebug = true;

    private void Reset()
    {
        receiver = GetComponent<UdpQuaternionReceiver>();
        driver = GetComponent<PoseRotationDriver>();
        visualizer = GetComponent<TestScreenVisualizer>();
    }

    private void Awake()
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

    private void OnGUI()
    {
        HandleRecenterShortcut();

        if (!showOverlay)
        {
            return;
        }

        string lastStatus = receiver != null ? receiver.LastStatus : "Receiver missing";
        string lastSender = receiver != null ? receiver.LastSender : "-";
        int packets = receiver != null ? receiver.ReceivedPacketCount : 0;
        Quaternion raw = receiver != null ? receiver.LatestRawRotation : Quaternion.identity;
        Quaternion converted = receiver != null ? receiver.LatestConvertedRotation : Quaternion.identity;
        Quaternion applied = driver != null ? driver.LatestAppliedRotation : Quaternion.identity;
        string sinceLastPacket = "-";

        if (receiver != null && receiver.LastReceivedTime != System.DateTime.MinValue)
        {
            sinceLastPacket = string.Format(
                CultureInfo.InvariantCulture,
                "{0:F2}s ago",
                (System.DateTime.Now - receiver.LastReceivedTime).TotalSeconds);
        }

        GUILayout.BeginArea(new Rect(10f, 10f, 760f, showPacketDebug ? 420f : 210f), GUI.skin.box);
        GUILayout.Label("UDP Pose Receiver Debug");
        GUILayout.Label("Status: " + lastStatus);
        GUILayout.Label("Sender: " + lastSender);
        GUILayout.Label("Packets: " + packets.ToString(CultureInfo.InvariantCulture));
        GUILayout.Label("Raw Quaternion: " + raw.ToString("F4"));
        GUILayout.Label("Converted Quaternion: " + converted.ToString("F4"));
        GUILayout.Label("Applied Quaternion: " + applied.ToString("F4"));
        GUILayout.Label("Applied Euler: " + applied.eulerAngles.ToString("F1"));
        GUILayout.Label("Projection Surface: " + (visualizer != null ? visualizer.CurrentSurfaceName : "-"));
        GUILayout.Label("Recenter Key: C");

        if (driver != null && GUILayout.Button("Reset Calibration", GUILayout.Height(28f)))
        {
            ResetAllCalibration();
        }

        GUILayout.Label("Last Packet: " + sinceLastPacket);

        if (showPacketDebug)
        {
            GUILayout.Space(8f);
            GUILayout.Label("Packet Debug:");
            string packetDebug = receiver != null ? receiver.RecentPacketDebug : "Receiver missing";
            bool previousGuiEnabled = GUI.enabled;
            GUI.enabled = false;
            GUILayout.TextArea(packetDebug, GUILayout.Height(180f));
            GUI.enabled = previousGuiEnabled;
        }

        GUILayout.EndArea();
    }

    private void HandleRecenterShortcut()
    {
        Event currentEvent = Event.current;
        if (currentEvent == null)
        {
            return;
        }

        if (currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.C)
        {
            ResetAllCalibration();
            currentEvent.Use();
        }
    }

    private void ResetAllCalibration()
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
}
