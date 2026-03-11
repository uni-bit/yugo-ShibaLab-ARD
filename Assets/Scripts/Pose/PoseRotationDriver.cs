using UnityEngine;

[AddComponentMenu("Pose/Pose Rotation Driver")]
public class PoseRotationDriver : MonoBehaviour
{
    [SerializeField] private UdpQuaternionReceiver receiver;
    [SerializeField] private Transform rotationTarget;
    [SerializeField] private Light tipLight;
    [SerializeField] private float tipLightForwardOffset = 0.2f;
    [SerializeField] private bool alignTipLightToForward = true;
    [SerializeField] private bool autoCalibrateOnFirstPacket = true;
    [SerializeField] private KeyCode recenterKey = KeyCode.C;
    [SerializeField] private float rotationSmoothing = 18f;
    [SerializeField] private float recenterInputIgnoreSeconds = 0.12f;
    [SerializeField] private Vector3 modelEulerOffset = Vector3.zero;
    [SerializeField] private bool usePresetRelativeAxisCorrection = true;
    [SerializeField] private Vector3 iPhoneRelativeAxisSigns = new Vector3(-1f, -1f, 1f);
    [SerializeField] private Vector3 androidRelativeAxisSigns = Vector3.one;

    public Quaternion LatestAppliedRotation { get; private set; } = Quaternion.identity;

    private Quaternion referenceSensorRotation = Quaternion.identity;
    private Quaternion initialLocalRotation = Quaternion.identity;
    private Quaternion targetLocalRotation = Quaternion.identity;
    private bool hasCalibration;
    private bool pendingRecenterSample;
    private int lastHandledRecenterRequestCount;
    private float ignoreIncomingUntilTime;

    private void Reset()
    {
        receiver = GetComponent<UdpQuaternionReceiver>();
        rotationTarget = transform;
        tipLight = GetComponentInChildren<Light>();
    }

    private void Awake()
    {
        if (rotationTarget == null)
        {
            rotationTarget = transform;
        }

        if (receiver == null)
        {
            receiver = GetComponent<UdpQuaternionReceiver>();
        }

        initialLocalRotation = rotationTarget != null ? rotationTarget.localRotation : Quaternion.identity;
        targetLocalRotation = initialLocalRotation;
        LatestAppliedRotation = initialLocalRotation;
        lastHandledRecenterRequestCount = receiver != null ? receiver.RecenterRequestCount : 0;
    }

    private void OnValidate()
    {
        if (tipLightForwardOffset < 0f)
        {
            tipLightForwardOffset = 0f;
        }

        if (rotationTarget == null)
        {
            rotationTarget = transform;
        }
    }

    public void Configure(UdpQuaternionReceiver receiverReference, Transform rotationTargetReference, Light tipLightReference, float tipLightOffset)
    {
        receiver = receiverReference;
        rotationTarget = rotationTargetReference;
        tipLight = tipLightReference;
        tipLightForwardOffset = Mathf.Max(0f, tipLightOffset);
        initialLocalRotation = rotationTarget != null ? rotationTarget.localRotation : Quaternion.identity;
        targetLocalRotation = initialLocalRotation;
        LatestAppliedRotation = initialLocalRotation;
        hasCalibration = false;
        pendingRecenterSample = false;
        lastHandledRecenterRequestCount = receiver != null ? receiver.RecenterRequestCount : 0;
    }

    public void SetTipLightAlignment(bool shouldAlign)
    {
        alignTipLightToForward = shouldAlign;
    }

    public void ResetCalibration()
    {
        if (receiver != null && receiver.ReceivedPacketCount > 0)
        {
            hasCalibration = false;
            pendingRecenterSample = true;
            receiver.ClearPendingRotation();
            ignoreIncomingUntilTime = Time.unscaledTime + Mathf.Max(0f, recenterInputIgnoreSeconds);
        }
        else
        {
            hasCalibration = false;
            pendingRecenterSample = false;
            ignoreIncomingUntilTime = 0f;
        }

        targetLocalRotation = initialLocalRotation;

        if (rotationTarget != null)
        {
            rotationTarget.localRotation = initialLocalRotation;
            LatestAppliedRotation = rotationTarget.localRotation;
        }
        else
        {
            LatestAppliedRotation = initialLocalRotation;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(recenterKey))
        {
            ResetCalibration();
        }

        if (receiver != null && receiver.RecenterRequestCount != lastHandledRecenterRequestCount)
        {
            lastHandledRecenterRequestCount = receiver.RecenterRequestCount;
            ResetCalibration();
        }

        if (receiver != null)
        {
            if (Time.unscaledTime < ignoreIncomingUntilTime)
            {
                receiver.ClearPendingRotation();
            }

            Quaternion nextRotation;
            if (Time.unscaledTime >= ignoreIncomingUntilTime
                && receiver.ConsumeLatestRotation(out nextRotation)
                && rotationTarget != null)
            {
                if (pendingRecenterSample)
                {
                    referenceSensorRotation = nextRotation;
                    hasCalibration = true;
                    pendingRecenterSample = false;
                    targetLocalRotation = initialLocalRotation;
                    return;
                }

                if (autoCalibrateOnFirstPacket && !hasCalibration)
                {
                    referenceSensorRotation = nextRotation;
                    hasCalibration = true;
                }

                Quaternion relativeRotation = hasCalibration
                    ? QuaternionCalibrationUtility.CalculateRelativeRotation(referenceSensorRotation, nextRotation)
                    : nextRotation;

                if (usePresetRelativeAxisCorrection
                    && receiver != null
                    && receiver.CoordinatePreset == QuaternionCoordinatePreset.AndroidRotationVector)
                {
                    relativeRotation = QuaternionCoordinateConverter.ApplyRelativeAxisPreset(
                        relativeRotation,
                        receiver.CoordinatePreset,
                        iPhoneRelativeAxisSigns,
                        androidRelativeAxisSigns);
                }

                Quaternion modelOffsetRotation = Quaternion.Euler(modelEulerOffset);
                targetLocalRotation = initialLocalRotation * relativeRotation * modelOffsetRotation;
            }
        }

        if (rotationTarget != null)
        {
            float blend = rotationSmoothing <= 0f ? 1f : 1f - Mathf.Exp(-rotationSmoothing * Time.deltaTime);
            rotationTarget.localRotation = Quaternion.Slerp(rotationTarget.localRotation, targetLocalRotation, blend);
            LatestAppliedRotation = rotationTarget.localRotation;
        }

        UpdateTipLightTransform();
    }

    private void UpdateTipLightTransform()
    {
        if (tipLight == null || rotationTarget == null)
        {
            return;
        }

        Transform lightTransform = tipLight.transform;

        if (lightTransform.parent == rotationTarget)
        {
            lightTransform.localPosition = Vector3.forward * tipLightForwardOffset;

            if (alignTipLightToForward)
            {
                lightTransform.localRotation = Quaternion.identity;
            }

            return;
        }

        lightTransform.position = rotationTarget.position + (rotationTarget.forward * tipLightForwardOffset);

        if (alignTipLightToForward)
        {
            lightTransform.rotation = rotationTarget.rotation;
        }
    }
}
