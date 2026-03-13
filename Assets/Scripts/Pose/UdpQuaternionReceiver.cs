using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;

[AddComponentMenu("Networking/UDP Quaternion Receiver")]
public class UdpQuaternionReceiver : MonoBehaviour
{
    private struct OscMessage
    {
        public string Address;
        public string TypeTags;
        public List<float> FloatArguments;
        public List<string> DebugArguments;
    }

    private struct QuaternionPacketParseResult
    {
        public bool Succeeded;
        public bool HasCompleteQuaternion;
        public Quaternion Quaternion;
        public string Message;
    }

    [Header("UDP Settings")]
    [SerializeField] private int listenPort = 8000;

    [Header("Coordinate Conversion")]
    [SerializeField] private QuaternionCoordinatePreset coordinatePreset = QuaternionCoordinatePreset.IPhoneCoreMotion;
    [SerializeField] private bool convertRightHandedToLeftHanded = true;
    [SerializeField] private Vector3 sensorToUnityEulerOffset = Vector3.zero;
    [SerializeField] private bool stabilizeQuaternionHemisphere = true;
    [Tooltip("スマホ画面を下向き（face-down）にして使う場合はtrue。座標軸の左右反転を補正します。")]
    [SerializeField] private bool screenFaceDown = true;

    [Header("Touch Input")]
    [SerializeField] private float touchRecenterCooldownSeconds = 0.8f;

    private readonly object syncRoot = new object();
    private static readonly Regex FloatRegex = new Regex(@"[-+]?\d*\.?\d+(?:[eE][-+]?\d+)?", RegexOptions.Compiled);

    private UdpClient udpClient;
    private Thread receiveThread;
    private volatile bool isRunning;
    private readonly Queue<string> recentPacketLogs = new Queue<string>();

    private Quaternion pendingRotation = Quaternion.identity;
    private bool hasPendingRotation;
    private Quaternion lastNormalizedRawRotation = Quaternion.identity;
    private bool hasLastNormalizedRawRotation;
    private Vector4 partialQuaternion;
    private bool hasX;
    private bool hasY;
    private bool hasZ;
    private bool hasW;
    private DateTime lastTouchRecenterTime = DateTime.MinValue;
    private DateTime lastTouchPacketTime = DateTime.MinValue;
    private bool touchInputActive;
    private int recenterRequestCount;
    private int pendingRecenterRequests;
    private int touchPacketCount;
    private string lastTouchStatus = "No touch received";

    public Quaternion LatestRawRotation { get; private set; } = Quaternion.identity;
    public Quaternion LatestStabilizedRawRotation { get; private set; } = Quaternion.identity;
    public Quaternion LatestConvertedRotation { get; private set; } = Quaternion.identity;
    public QuaternionCoordinatePreset CoordinatePreset => coordinatePreset;
    public bool ScreenFaceDown => screenFaceDown;
    public int ReceivedPacketCount { get; private set; }
    public int RecenterRequestCount => Volatile.Read(ref recenterRequestCount);
    public int TouchPacketCount => Volatile.Read(ref touchPacketCount);
    public string LastTouchStatus { get; private set; } = "No touch received";
    public DateTime LastRecenterTime { get; private set; } = DateTime.MinValue;
    public string LastSender { get; private set; } = "-";
    public string LastStatus { get; private set; } = "Waiting for UDP packets...";
    public DateTime LastReceivedTime { get; private set; } = DateTime.MinValue;
    public string RecentPacketDebug { get; private set; } = "-";

    private void OnEnable()
    {
        StartReceiver();
    }

    private void OnDisable()
    {
        StopReceiver();
    }

    private void OnDestroy()
    {
        StopReceiver();
    }

    public bool ConsumeLatestRotation(out Quaternion rotation)
    {
        lock (syncRoot)
        {
            if (!hasPendingRotation)
            {
                rotation = LatestConvertedRotation;
                return false;
            }

            rotation = pendingRotation;
            hasPendingRotation = false;
            return true;
        }
    }

    public Quaternion GetLatestConvertedRotationSnapshot()
    {
        lock (syncRoot)
        {
            return LatestConvertedRotation;
        }
    }

    public bool ConsumePendingRecenterRequest()
    {
        return Interlocked.Exchange(ref pendingRecenterRequests, 0) > 0;
    }

    public void ClearPendingRotation()
    {
        lock (syncRoot)
        {
            hasPendingRotation = false;
            pendingRotation = LatestConvertedRotation;
        }
    }

    private void StartReceiver()
    {
        if (isRunning)
        {
            return;
        }

        try
        {
            udpClient = new UdpClient(listenPort);
            udpClient.Client.ReceiveTimeout = 1000;

            isRunning = true;
            receiveThread = new Thread(ReceiveLoop)
            {
                IsBackground = true,
                Name = "UDP Quaternion Receiver"
            };
            receiveThread.Start();

            lock (syncRoot)
            {
                LastStatus = string.Format(CultureInfo.InvariantCulture, "Listening on UDP {0}", listenPort);
            }
        }
        catch (Exception ex)
        {
            isRunning = false;
            lock (syncRoot)
            {
                LastStatus = "Receiver start failed: " + ex.Message;
            }
            Debug.LogError(LastStatus, this);
        }
    }

    private void StopReceiver()
    {
        isRunning = false;

        if (udpClient != null)
        {
            try
            {
                udpClient.Close();
            }
            catch
            {
            }

            udpClient = null;
        }

        if (receiveThread != null && receiveThread.IsAlive)
        {
            if (!receiveThread.Join(500))
            {
                receiveThread.Interrupt();
            }
        }

        receiveThread = null;
    }

    private void ReceiveLoop()
    {
        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

        while (isRunning)
        {
            try
            {
                byte[] packet = udpClient.Receive(ref remoteEndPoint);
                RecordPacketDebug(packet, remoteEndPoint);

                bool touchTriggered = false;
                List<OscMessage> oscMessages;
                string oscError;
                bool hasOscMessages = TryParseOscPacket(packet, out oscMessages, out oscError);
                if (hasOscMessages)
                {
                    touchTriggered = TryRequestRecenterFromTouchMessages(oscMessages);
                }

                if (!touchTriggered)
                {
                    touchTriggered = TryRequestRecenterFromRawPayload(packet);
                }

                QuaternionPacketParseResult parseResult = TryParseQuaternionPacket(packet);
                if (!parseResult.Succeeded)
                {
                    lock (syncRoot)
                    {
                        LastSender = remoteEndPoint.ToString();
                        LastReceivedTime = DateTime.Now;
                        LastStatus = touchTriggered
                            ? "Touch packet received. Recenter requested."
                            : "Packet received but parse failed.";
                    }
                    continue;
                }

                if (!parseResult.HasCompleteQuaternion)
                {
                    lock (syncRoot)
                    {
                        LastSender = remoteEndPoint.ToString();
                        LastReceivedTime = DateTime.Now;
                        LastStatus = touchTriggered
                            ? parseResult.Message + " Touch packet received."
                            : parseResult.Message;
                    }
                    continue;
                }

                Quaternion packetQuaternion = parseResult.Quaternion;
                Quaternion rawQuaternion = StabilizeRawQuaternion(packetQuaternion);

                Quaternion convertedQuaternion = QuaternionCoordinateConverter.ConvertToUnity(
                    rawQuaternion,
                    coordinatePreset,
                    sensorToUnityEulerOffset,
                    convertRightHandedToLeftHanded,
                    screenFaceDown);

                lock (syncRoot)
                {
                    LatestRawRotation = packetQuaternion;
                    LatestStabilizedRawRotation = rawQuaternion;
                    LatestConvertedRotation = convertedQuaternion;
                    pendingRotation = convertedQuaternion;
                    hasPendingRotation = true;
                    ReceivedPacketCount++;
                    LastSender = remoteEndPoint.ToString();
                    LastReceivedTime = DateTime.Now;
                    LastStatus = touchTriggered
                        ? parseResult.Message + " Touch packet received."
                        : parseResult.Message;
                }
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode == SocketError.TimedOut)
                {
                    continue;
                }

                if (!isRunning)
                {
                    break;
                }

                lock (syncRoot)
                {
                    LastStatus = "Socket error: " + ex.Message;
                }
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (ThreadInterruptedException)
            {
                break;
            }
            catch (Exception ex)
            {
                lock (syncRoot)
                {
                    LastStatus = "Receive error: " + ex.Message;
                }
            }
        }
    }

    private QuaternionPacketParseResult TryParseQuaternionPacket(byte[] data)
    {
        Quaternion quaternion;
        List<OscMessage> oscMessages;
        string oscError;
        if (TryParseOscPacket(data, out oscMessages, out oscError))
        {
            QuaternionPacketParseResult oscResult = TryBuildQuaternionFromOscMessages(oscMessages);
            if (oscResult.Succeeded)
            {
                return oscResult;
            }
        }

        if (TryParseTextQuaternion(data, out quaternion))
        {
            return CreateCompleteResult(quaternion, "Text quaternion packet received.");
        }

        if (TryParseBinaryQuaternion(data, out quaternion))
        {
            return CreateCompleteResult(quaternion, "Binary quaternion packet received.");
        }

        return default;
    }

    private QuaternionPacketParseResult TryBuildQuaternionFromOscMessages(List<OscMessage> messages)
    {
        bool hasQuaternionComponent = false;

        for (int index = 0; index < messages.Count; index++)
        {
            OscMessage message = messages[index];

            if (message.FloatArguments != null && message.FloatArguments.Count >= 4)
            {
                Quaternion quaternion = new Quaternion(
                    message.FloatArguments[0],
                    message.FloatArguments[1],
                    message.FloatArguments[2],
                    message.FloatArguments[3]);

                if (IsFiniteQuaternion(quaternion))
                {
                    return CreateCompleteResult(quaternion, "OSC quaternion packet received.");
                }
            }

            if (message.FloatArguments == null || message.FloatArguments.Count != 1)
            {
                continue;
            }

            string componentName = ExtractQuaternionComponentFromAddress(message.Address);
            if (string.IsNullOrEmpty(componentName))
            {
                continue;
            }

            hasQuaternionComponent = true;
            QuaternionPacketParseResult partialResult = BuildQuaternionFromComponent(componentName, message.FloatArguments[0]);
            if (partialResult.HasCompleteQuaternion)
            {
                return partialResult;
            }
        }

        if (hasQuaternionComponent)
        {
            return new QuaternionPacketParseResult
            {
                Succeeded = true,
                HasCompleteQuaternion = false,
                Message = BuildPartialComponentStatus()
            };
        }

        return default;
    }

    private QuaternionPacketParseResult BuildQuaternionFromComponent(string componentName, float componentValue)
    {
        string normalized = NormalizeComponentName(componentName);
        if (string.IsNullOrEmpty(normalized))
        {
            return default;
        }

        switch (normalized)
        {
            case "x":
                partialQuaternion.x = componentValue;
                hasX = true;
                break;
            case "y":
                partialQuaternion.y = componentValue;
                hasY = true;
                break;
            case "z":
                partialQuaternion.z = componentValue;
                hasZ = true;
                break;
            case "w":
                partialQuaternion.w = componentValue;
                hasW = true;
                break;
            default:
                return default;
        }

        if (!(hasX && hasY && hasZ && hasW))
        {
            return new QuaternionPacketParseResult
            {
                Succeeded = true,
                HasCompleteQuaternion = false,
                Message = BuildPartialComponentStatus()
            };
        }

        Quaternion quaternion = new Quaternion(partialQuaternion.x, partialQuaternion.y, partialQuaternion.z, partialQuaternion.w);
        hasX = false;
        hasY = false;
        hasZ = false;
        hasW = false;

        if (!IsFiniteQuaternion(quaternion))
        {
            return default;
        }

        return CreateCompleteResult(quaternion, "OSC quaternion components assembled.");
    }

    private string BuildPartialComponentStatus()
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "Waiting quaternion components x:{0} y:{1} z:{2} w:{3}",
            hasX ? partialQuaternion.x.ToString("F4", CultureInfo.InvariantCulture) : "-",
            hasY ? partialQuaternion.y.ToString("F4", CultureInfo.InvariantCulture) : "-",
            hasZ ? partialQuaternion.z.ToString("F4", CultureInfo.InvariantCulture) : "-",
            hasW ? partialQuaternion.w.ToString("F4", CultureInfo.InvariantCulture) : "-");
    }

    private static QuaternionPacketParseResult CreateCompleteResult(Quaternion quaternion, string message)
    {
        return new QuaternionPacketParseResult
        {
            Succeeded = true,
            HasCompleteQuaternion = true,
            Quaternion = quaternion,
            Message = message
        };
    }

    private Quaternion StabilizeRawQuaternion(Quaternion quaternion)
    {
        float magnitude = Mathf.Sqrt(
            quaternion.x * quaternion.x
            + quaternion.y * quaternion.y
            + quaternion.z * quaternion.z
            + quaternion.w * quaternion.w);

        if (magnitude <= 0.000001f)
        {
            return Quaternion.identity;
        }

        float inverseMagnitude = 1f / magnitude;
        Quaternion normalized = new Quaternion(
            quaternion.x * inverseMagnitude,
            quaternion.y * inverseMagnitude,
            quaternion.z * inverseMagnitude,
            quaternion.w * inverseMagnitude);

        if (stabilizeQuaternionHemisphere && hasLastNormalizedRawRotation)
        {
            float dot = (normalized.x * lastNormalizedRawRotation.x)
                + (normalized.y * lastNormalizedRawRotation.y)
                + (normalized.z * lastNormalizedRawRotation.z)
                + (normalized.w * lastNormalizedRawRotation.w);

            if (dot < 0f)
            {
                normalized = new Quaternion(-normalized.x, -normalized.y, -normalized.z, -normalized.w);
            }
        }

        lastNormalizedRawRotation = normalized;
        hasLastNormalizedRawRotation = true;
        return normalized;
    }

    private static bool TryParseTextQuaternion(byte[] data, out Quaternion quaternion)
    {
        quaternion = Quaternion.identity;

        string text = Encoding.UTF8.GetString(data).Trim('\0', ' ', '\r', '\n', '\t');
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        MatchCollection matches = FloatRegex.Matches(text);
        if (matches.Count < 4)
        {
            return false;
        }

        float x;
        float y;
        float z;
        float w;

        if (!float.TryParse(matches[0].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out x)
            || !float.TryParse(matches[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out y)
            || !float.TryParse(matches[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out z)
            || !float.TryParse(matches[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out w))
        {
            return false;
        }

        quaternion = new Quaternion(x, y, z, w);
        return IsFiniteQuaternion(quaternion);
    }

    private static bool TryParseBinaryQuaternion(byte[] data, out Quaternion quaternion)
    {
        quaternion = Quaternion.identity;

        if (data == null || data.Length != 16)
        {
            return false;
        }

        Quaternion littleEndian = new Quaternion(
            ReadFloatLittleEndian(data, 0),
            ReadFloatLittleEndian(data, 4),
            ReadFloatLittleEndian(data, 8),
            ReadFloatLittleEndian(data, 12));

        Quaternion bigEndian = new Quaternion(
            ReadFloatBigEndian(data, 0),
            ReadFloatBigEndian(data, 4),
            ReadFloatBigEndian(data, 8),
            ReadFloatBigEndian(data, 12));

        bool littleValid = IsFiniteQuaternion(littleEndian);
        bool bigValid = IsFiniteQuaternion(bigEndian);

        if (!littleValid && !bigValid)
        {
            return false;
        }

        if (littleValid && !bigValid)
        {
            quaternion = littleEndian;
            return true;
        }

        if (!littleValid && bigValid)
        {
            quaternion = bigEndian;
            return true;
        }

        float littleScore = Mathf.Abs(1f - QuaternionMagnitudeSquared(littleEndian));
        float bigScore = Mathf.Abs(1f - QuaternionMagnitudeSquared(bigEndian));

        quaternion = littleScore <= bigScore ? littleEndian : bigEndian;
        return true;
    }

    private static bool TryParseOscPacket(byte[] data, out List<OscMessage> messages, out string error)
    {
        messages = new List<OscMessage>();
        error = null;

        if (data == null || data.Length == 0)
        {
            return false;
        }

        if (IsOscBundle(data, 0, data.Length))
        {
            return TryParseOscBundle(data, 0, data.Length, messages, out error);
        }

        if (data[0] == (byte)'/')
        {
            return TryParseOscMessage(data, 0, data.Length, messages, out error);
        }

        return false;
    }

    private static bool TryParseOscBundle(byte[] data, int startIndex, int length, List<OscMessage> messages, out string error)
    {
        error = null;
        int endIndex = startIndex + length;
        if (length < 16 || !IsOscBundle(data, startIndex, length))
        {
            return false;
        }

        int offset = startIndex + 16;
        while (offset < endIndex)
        {
            if (offset + 4 > endIndex)
            {
                error = "OSC bundle element size is truncated.";
                return false;
            }

            int elementSize = ReadIntBigEndian(data, offset);
            offset += 4;

            if (elementSize <= 0 || offset + elementSize > endIndex)
            {
                error = "OSC bundle element size is invalid.";
                return false;
            }

            if (IsOscBundle(data, offset, elementSize))
            {
                if (!TryParseOscBundle(data, offset, elementSize, messages, out error))
                {
                    return false;
                }
            }
            else if (data[offset] == (byte)'/')
            {
                if (!TryParseOscMessage(data, offset, elementSize, messages, out error))
                {
                    return false;
                }
            }

            offset += elementSize;
        }

        return messages.Count > 0;
    }

    private static bool TryParseOscMessage(byte[] data, int startIndex, int length, List<OscMessage> messages, out string error)
    {
        error = null;
        int endIndex = startIndex + length;

        if (length <= 0 || startIndex < 0 || endIndex > data.Length || data[startIndex] != (byte)'/')
        {
            return false;
        }

        int addressEnd = FindOscStringEnd(data, startIndex, endIndex);
        if (addressEnd < 0)
        {
            error = "OSC address was not terminated.";
            return false;
        }

        string address = Encoding.ASCII.GetString(data, startIndex, addressEnd - startIndex);
        int typeTagOffset = AlignOscIndex(addressEnd + 1);
        if (typeTagOffset >= endIndex)
        {
            error = "OSC typetag offset is invalid.";
            return false;
        }

        int typeTagEnd = FindOscStringEnd(data, typeTagOffset, endIndex);
        if (typeTagEnd < 0)
        {
            error = "OSC typetag was not terminated.";
            return false;
        }

        string typeTags = Encoding.ASCII.GetString(data, typeTagOffset, typeTagEnd - typeTagOffset);
        if (string.IsNullOrEmpty(typeTags) || typeTags[0] != ',')
        {
            error = "OSC typetag is invalid.";
            return false;
        }

        int payloadOffset = AlignOscIndex(typeTagEnd + 1);
        List<float> floatArguments = new List<float>();
        List<string> debugArguments = new List<string>();

        for (int i = 1; i < typeTags.Length; i++)
        {
            char tag = typeTags[i];

            switch (tag)
            {
                case 'f':
                    if (payloadOffset + 4 > endIndex)
                    {
                        error = "OSC float payload is truncated.";
                        return false;
                    }

                    float floatValue = ReadFloatBigEndian(data, payloadOffset);
                    payloadOffset += 4;
                    floatArguments.Add(floatValue);
                    debugArguments.Add(floatValue.ToString("F6", CultureInfo.InvariantCulture));
                    break;

                case 'i':
                    if (payloadOffset + 4 > endIndex)
                    {
                        error = "OSC int payload is truncated.";
                        return false;
                    }

                    int intValue = ReadIntBigEndian(data, payloadOffset);
                    payloadOffset += 4;
                    debugArguments.Add(intValue.ToString(CultureInfo.InvariantCulture));
                    break;

                case 'd':
                    if (payloadOffset + 8 > endIndex)
                    {
                        error = "OSC double payload is truncated.";
                        return false;
                    }

                    double doubleValue = ReadDoubleBigEndian(data, payloadOffset);
                    payloadOffset += 8;
                    floatArguments.Add((float)doubleValue);
                    debugArguments.Add(doubleValue.ToString("F6", CultureInfo.InvariantCulture));
                    break;

                case 'h':
                    if (payloadOffset + 8 > endIndex)
                    {
                        error = "OSC int64 payload is truncated.";
                        return false;
                    }

                    long longValue = ReadLongBigEndian(data, payloadOffset);
                    payloadOffset += 8;
                    debugArguments.Add(longValue.ToString(CultureInfo.InvariantCulture));
                    break;

                case 't':
                    if (payloadOffset + 8 > endIndex)
                    {
                        error = "OSC timetag payload is truncated.";
                        return false;
                    }

                    ulong timetag = (ulong)ReadLongBigEndian(data, payloadOffset);
                    payloadOffset += 8;
                    debugArguments.Add("timetag:" + timetag.ToString(CultureInfo.InvariantCulture));
                    break;

                case 'r':
                case 'm':
                case 'c':
                    if (payloadOffset + 4 > endIndex)
                    {
                        error = "OSC 4-byte payload is truncated.";
                        return false;
                    }

                    payloadOffset += 4;
                    debugArguments.Add("0x" + ReadIntBigEndian(data, payloadOffset - 4).ToString("X8", CultureInfo.InvariantCulture));
                    break;

                case 'N':
                case 'I':
                    debugArguments.Add(tag == 'N' ? "nil" : "infinitum");
                    break;

                case 's':
                    int stringEnd = FindOscStringEnd(data, payloadOffset, endIndex);
                    if (stringEnd < 0)
                    {
                        error = "OSC string payload is truncated.";
                        return false;
                    }

                    string stringValue = Encoding.ASCII.GetString(data, payloadOffset, stringEnd - payloadOffset);
                    payloadOffset = AlignOscIndex(stringEnd + 1);
                    debugArguments.Add("\"" + stringValue + "\"");
                    break;

                case 'T':
                    debugArguments.Add("true");
                    break;

                case 'F':
                    debugArguments.Add("false");
                    break;

                default:
                    error = "Unsupported OSC typetag: " + tag;
                    return false;
            }
        }

        messages.Add(new OscMessage
        {
            Address = address,
            TypeTags = typeTags,
            FloatArguments = floatArguments,
            DebugArguments = debugArguments
        });

        return true;
    }

    private static bool IsOscBundle(byte[] data, int startIndex, int length)
    {
        return length >= 8
            && startIndex >= 0
            && startIndex + length <= data.Length
            && data[startIndex] == (byte)'#'
            && Encoding.ASCII.GetString(data, startIndex, 8) == "#bundle\0";
    }

    private static string ExtractQuaternionComponentFromAddress(string address)
    {
        if (string.IsNullOrEmpty(address))
        {
            return null;
        }

        string normalized = address.Trim().ToLowerInvariant();
        normalized = normalized.Replace("\\", "/");
        normalized = normalized.Replace(":", "/");
        normalized = normalized.Trim('/');

        string[] parts = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i] == "quaternion")
            {
                return NormalizeComponentName(parts[i + 1]);
            }
        }

        if (parts.Length > 0)
        {
            return NormalizeComponentName(parts[parts.Length - 1]);
        }

        return null;
    }

    private bool TryRequestRecenterFromTouchMessages(List<OscMessage> messages)
    {
        if (messages == null || messages.Count == 0)
        {
            return TryRequestRecenterFromTouchState(false);
        }

        bool hasActiveTouch = false;
        for (int index = 0; index < messages.Count; index++)
        {
            if (IsActiveTouchMessage(messages[index]))
            {
                hasActiveTouch = true;
                break;
            }
        }

        return TryRequestRecenterFromTouchState(hasActiveTouch);
    }

    private bool TryRequestRecenterFromRawPayload(byte[] payload)
    {
        if (payload == null || payload.Length == 0)
        {
            return false;
        }

        string textPayload = Encoding.UTF8.GetString(payload).Trim('\0', ' ', '\r', '\n', '\t').ToLowerInvariant();
        if (string.IsNullOrEmpty(textPayload)
            || (!textPayload.Contains("touch")
                && !textPayload.Contains("pointer")
                && !textPayload.Contains("tap")
                && !textPayload.Contains("press")))
        {
            return false;
        }

        bool hasActiveTouch = true;
        bool indicatesTouchRelease = textPayload.Contains("false")
            || textPayload.Contains("ended")
            || textPayload.Contains("cancel")
            || textPayload.Contains("up");
        bool indicatesTouchPress = textPayload.Contains("true")
            || textPayload.Contains("down")
            || textPayload.Contains("begin")
            || textPayload.Contains("start")
            || textPayload.Contains("tap")
            || textPayload.Contains("press");

        if (indicatesTouchRelease && !indicatesTouchPress)
        {
            hasActiveTouch = false;
        }

        return TryRequestRecenterFromTouchState(hasActiveTouch);
    }

    private bool TryRequestRecenterFromTouchState(bool hasActiveTouch)
    {
        lock (syncRoot)
        {
            if (!hasActiveTouch)
            {
                touchInputActive = false;
                return false;
            }

            Interlocked.Increment(ref touchPacketCount);

            DateTime now = DateTime.UtcNow;
            double secondsSinceTouchPacket = lastTouchPacketTime == DateTime.MinValue
                ? double.MaxValue
                : (now - lastTouchPacketTime).TotalSeconds;
            lastTouchPacketTime = now;

            // Merge x/y pair packets and tiny OSC bursts into one recenter request.
            if (secondsSinceTouchPacket < 0.12d)
            {
                LastTouchStatus = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "Touch detected (debounce {0:F3}s < 0.12s)",
                    secondsSinceTouchPacket);
                return false;
            }

            double secondsSinceLastTrigger = lastTouchRecenterTime == DateTime.MinValue
                ? double.MaxValue
                : (now - lastTouchRecenterTime).TotalSeconds;

            if (secondsSinceLastTrigger < touchRecenterCooldownSeconds)
            {
                LastTouchStatus = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "Touch detected (cooldown {0:F2}s / {1:F2}s)",
                    secondsSinceLastTrigger,
                    touchRecenterCooldownSeconds);
                return false;
            }

            lastTouchRecenterTime = now;
            LastRecenterTime = DateTime.Now;
            LastTouchStatus = "Recenter triggered by touch!";
            Interlocked.Increment(ref recenterRequestCount);
            Interlocked.Increment(ref pendingRecenterRequests);
            return true;
        }
    }

    private static bool IsActiveTouchMessage(OscMessage message)
    {
        if (!AddressLooksLikeTouch(message.Address))
        {
            return false;
        }

        string normalizedAddress = string.IsNullOrEmpty(message.Address)
            ? string.Empty
            : message.Address.Trim().ToLowerInvariant().Replace("\\", "/").Replace(":", "/");

        // ZIG SIM touch format: /(deviceUUID)/touch(touchId)1  (X position, float)
        //                       /(deviceUUID)/touch(touchId)2  (Y position, float)
        //                       /(deviceUUID)/touchradius(touchId)
        //                       /(deviceUUID)/touchforce(touchId)
        // When a finger is touching, these messages are sent every frame.
        // When no finger is touching, these messages are NOT sent at all.
        // Therefore: receiving any touch/touchradius/touchforce message = active touch.
        if (IsZigSimTouchAddress(normalizedAddress))
        {
            // touchforce with value 0 means no force (but finger IS touching)
            // touchradius presence always means touch is active
            // touch position (1 float) always means touch is active
            if (normalizedAddress.Contains("touchforce"))
            {
                return message.FloatArguments != null
                    && message.FloatArguments.Count > 0
                    && message.FloatArguments[0] > 0.0001f;
            }
            return true;
        }

        // TUIO-style touch stream (/tuio/2Dcur):
        //   set  -> active touch sample
        //   alive with ids -> active while finger exists
        //   alive only -> no active touches
        //   fseq -> frame sequence marker only
        if (normalizedAddress.Contains("2dcur") || normalizedAddress.Contains("/tuio/"))
        {
            if (message.DebugArguments != null && message.DebugArguments.Count > 0)
            {
                string first = message.DebugArguments[0];
                if (string.Equals(first, "\"set\"", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (string.Equals(first, "\"alive\"", StringComparison.OrdinalIgnoreCase))
                {
                    return message.DebugArguments.Count > 1;
                }

                if (string.Equals(first, "\"fseq\"", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }

        if (!string.IsNullOrEmpty(message.TypeTags))
        {
            if (message.TypeTags.IndexOf('T') >= 0)
            {
                return true;
            }

            if (message.TypeTags.IndexOf('F') >= 0)
            {
                return false;
            }
        }

        if (message.FloatArguments != null && message.FloatArguments.Count >= 2)
        {
            return true;
        }

        if (message.FloatArguments != null && message.FloatArguments.Count == 1)
        {
            string address = message.Address != null ? message.Address.ToLowerInvariant() : string.Empty;
            bool isForceOrRadius = address.Contains("force") || address.Contains("radius");
            if (isForceOrRadius)
            {
                return message.FloatArguments[0] > 0.0001f;
            }

            return true;
        }

        if (!string.IsNullOrEmpty(message.TypeTags)
            && (message.TypeTags.IndexOf('i') >= 0
                || message.TypeTags.IndexOf('s') >= 0
                || message.TypeTags.IndexOf('b') >= 0))
        {
            return message.DebugArguments != null && message.DebugArguments.Count > 0;
        }

        if (message.DebugArguments != null && message.DebugArguments.Count > 0)
        {
            return true;
        }

        return true;
    }

    // ZIG SIM touch address pattern: /(uuid)/touch(id)1 or /touch(id)2
    // Also matches: /(uuid)/touchradius(id), /(uuid)/touchforce(id)
    // The last path segment starts with "touch" and contains a digit suffix.
    private static bool IsZigSimTouchAddress(string normalizedAddress)
    {
        if (string.IsNullOrEmpty(normalizedAddress))
        {
            return false;
        }

        int lastSlash = normalizedAddress.LastIndexOf('/');
        string lastSegment = lastSlash >= 0
            ? normalizedAddress.Substring(lastSlash + 1)
            : normalizedAddress;

        if (!lastSegment.StartsWith("touch", StringComparison.Ordinal))
        {
            return false;
        }

        // Confirm it looks like ZIG SIM: touch(id)1, touch(id)2, touchradius(id), touchforce(id)
        // i.e. "touch" followed by at least one more character (digit or keyword)
        return lastSegment.Length > 5;
    }

    private static bool AddressLooksLikeTouch(string address)
    {
        if (string.IsNullOrEmpty(address))
        {
            return false;
        }

        string normalized = address.Trim().ToLowerInvariant();
        normalized = normalized.Replace("\\", "/");
        normalized = normalized.Replace(":", "/");
        normalized = normalized.Trim('/');

        // ZIG SIM touch address: last segment starts with "touch" and has suffix
        if (IsZigSimTouchAddress(normalized))
        {
            return true;
        }

        string[] parts = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        for (int index = 0; index < parts.Length; index++)
        {
            string part = parts[index];
            if (part.Contains("touch")
                || part.Contains("2d")
                || part.Contains("tuio")
                || part.Contains("mti")
                || part.Contains("touches")
                || part.Contains("pointer")
                || part.Contains("tap"))
            {
                return true;
            }
        }

        return normalized.Contains("touch")
            || normalized.Contains("2d")
            || normalized.Contains("tuio")
            || normalized.Contains("mti")
            || normalized.Contains("touches")
            || normalized.Contains("pointer")
            || normalized.Contains("tap");
    }

    private static string NormalizeComponentName(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        string normalized = value.Trim().ToLowerInvariant();
        if (normalized.EndsWith("x", StringComparison.Ordinal))
        {
            return "x";
        }

        if (normalized.EndsWith("y", StringComparison.Ordinal))
        {
            return "y";
        }

        if (normalized.EndsWith("z", StringComparison.Ordinal))
        {
            return "z";
        }

        if (normalized.EndsWith("w", StringComparison.Ordinal))
        {
            return "w";
        }

        return null;
    }

    private static int FindOscStringEnd(byte[] data, int startIndex, int endIndex)
    {
        for (int index = startIndex; index < endIndex; index++)
        {
            if (data[index] == 0)
            {
                return index;
            }
        }

        return -1;
    }

    private static int AlignOscIndex(int index)
    {
        return (index + 3) & ~3;
    }

    private static int ReadIntBigEndian(byte[] data, int offset)
    {
        if (!BitConverter.IsLittleEndian)
        {
            return BitConverter.ToInt32(data, offset);
        }

        byte[] reversed = new byte[4];
        Array.Copy(data, offset, reversed, 0, 4);
        Array.Reverse(reversed);
        return BitConverter.ToInt32(reversed, 0);
    }

    private static long ReadLongBigEndian(byte[] data, int offset)
    {
        if (!BitConverter.IsLittleEndian)
        {
            return BitConverter.ToInt64(data, offset);
        }

        byte[] reversed = new byte[8];
        Array.Copy(data, offset, reversed, 0, 8);
        Array.Reverse(reversed);
        return BitConverter.ToInt64(reversed, 0);
    }

    private static double ReadDoubleBigEndian(byte[] data, int offset)
    {
        if (!BitConverter.IsLittleEndian)
        {
            return BitConverter.ToDouble(data, offset);
        }

        byte[] reversed = new byte[8];
        Array.Copy(data, offset, reversed, 0, 8);
        Array.Reverse(reversed);
        return BitConverter.ToDouble(reversed, 0);
    }

    private static float ReadFloatLittleEndian(byte[] data, int offset)
    {
        if (BitConverter.IsLittleEndian)
        {
            return BitConverter.ToSingle(data, offset);
        }

        byte[] reversed = new byte[4];
        Array.Copy(data, offset, reversed, 0, 4);
        Array.Reverse(reversed);
        return BitConverter.ToSingle(reversed, 0);
    }

    private static float ReadFloatBigEndian(byte[] data, int offset)
    {
        if (!BitConverter.IsLittleEndian)
        {
            return BitConverter.ToSingle(data, offset);
        }

        byte[] reversed = new byte[4];
        Array.Copy(data, offset, reversed, 0, 4);
        Array.Reverse(reversed);
        return BitConverter.ToSingle(reversed, 0);
    }

    private static bool IsFiniteQuaternion(Quaternion quaternion)
    {
        return IsFinite(quaternion.x)
            && IsFinite(quaternion.y)
            && IsFinite(quaternion.z)
            && IsFinite(quaternion.w);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static float QuaternionMagnitudeSquared(Quaternion quaternion)
    {
        return quaternion.x * quaternion.x
            + quaternion.y * quaternion.y
            + quaternion.z * quaternion.z
            + quaternion.w * quaternion.w;
    }

    private void RecordPacketDebug(byte[] packet, IPEndPoint remoteEndPoint)
    {
        string summary = BuildPacketDebugSummary(packet, remoteEndPoint);

        lock (syncRoot)
        {
            recentPacketLogs.Enqueue(summary);
            while (recentPacketLogs.Count > 6)
            {
                recentPacketLogs.Dequeue();
            }

            RecentPacketDebug = string.Join("\n\n", recentPacketLogs.ToArray());
        }
    }

    private static string BuildPacketDebugSummary(byte[] packet, IPEndPoint remoteEndPoint)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendFormat(CultureInfo.InvariantCulture, "[{0:HH:mm:ss.fff}] {1} bytes={2}", DateTime.Now, remoteEndPoint, packet != null ? packet.Length : 0);

        if (packet == null || packet.Length == 0)
        {
            return builder.ToString();
        }

        List<OscMessage> oscMessages;
        string oscError;
        if (TryParseOscPacket(packet, out oscMessages, out oscError))
        {
            for (int i = 0; i < oscMessages.Count; i++)
            {
                OscMessage message = oscMessages[i];
                builder.Append("\nOSC ");
                builder.Append(message.Address);
                builder.Append(' ');
                builder.Append(message.TypeTags);

                if (message.DebugArguments != null && message.DebugArguments.Count > 0)
                {
                    builder.Append(" -> ");
                    builder.Append(string.Join(", ", message.DebugArguments.ToArray()));
                }
            }

            return builder.ToString();
        }

        if (!string.IsNullOrEmpty(oscError))
        {
            builder.Append("\nOSC parse error: ");
            builder.Append(oscError);
        }

        string text = Encoding.UTF8.GetString(packet).Trim('\0', '\r', '\n', ' ');
        if (!string.IsNullOrEmpty(text))
        {
            builder.Append("\nText: ");
            builder.Append(text.Length > 120 ? text.Substring(0, 120) : text);
        }

        builder.Append("\nHex: ");
        int hexLength = Mathf.Min(packet.Length, 32);
        for (int i = 0; i < hexLength; i++)
        {
            builder.Append(packet[i].ToString("X2", CultureInfo.InvariantCulture));
            if (i + 1 < hexLength)
            {
                builder.Append(' ');
            }
        }

        if (packet.Length > hexLength)
        {
            builder.Append(" ...");
        }

        return builder.ToString();
    }
}
