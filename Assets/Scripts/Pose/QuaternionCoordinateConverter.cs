using UnityEngine;

public enum QuaternionCoordinatePreset
{
    IPhoneCoreMotion = 0,
    AndroidRotationVector = 1
}

public enum QuaternionComponentSource
{
    X = 0,
    Y = 1,
    Z = 2,
    W = 3
}

public static class QuaternionCoordinateConverter
{
    public static Quaternion RemapRawQuaternion(
        Quaternion sensorQuaternion,
        QuaternionComponentSource xSource,
        QuaternionComponentSource ySource,
        QuaternionComponentSource zSource,
        QuaternionComponentSource wSource,
        Vector4 signMultiplier)
    {
        return new Quaternion(
            ReadComponent(sensorQuaternion, xSource) * signMultiplier.x,
            ReadComponent(sensorQuaternion, ySource) * signMultiplier.y,
            ReadComponent(sensorQuaternion, zSource) * signMultiplier.z,
            ReadComponent(sensorQuaternion, wSource) * signMultiplier.w);
    }

    public static Quaternion ConvertToUnity(
        Quaternion sensorQuaternion,
        QuaternionCoordinatePreset coordinatePreset,
        Vector3 eulerOffset,
        bool convertHandedness = true)
    {
        Quaternion normalizedSensor = NormalizeQuaternion(sensorQuaternion);

        if (normalizedSensor.w < 0f)
        {
            normalizedSensor = new Quaternion(
                -normalizedSensor.x,
                -normalizedSensor.y,
                -normalizedSensor.z,
                -normalizedSensor.w);
        }

        Quaternion unityRotation = coordinatePreset == QuaternionCoordinatePreset.IPhoneCoreMotion
            ? ConvertIPhoneCoreMotion(normalizedSensor, convertHandedness)
            : (convertHandedness ? ConvertHandedness(normalizedSensor, coordinatePreset) : normalizedSensor);

        if (unityRotation.x == 0f
            && unityRotation.y == 0f
            && unityRotation.z == 0f
            && unityRotation.w == 0f)
        {
            return Quaternion.identity;
        }

        Quaternion offsetRotation = Quaternion.Euler(eulerOffset);
        return Quaternion.Normalize(offsetRotation * unityRotation);
    }

    public static Quaternion ApplyRelativeAxisPreset(Quaternion relativeRotation, QuaternionCoordinatePreset coordinatePreset, Vector3 iPhoneAxisSigns, Vector3 androidAxisSigns)
    {
        Vector3 euler = ToSignedEuler(relativeRotation);
        Vector3 axisSigns = coordinatePreset == QuaternionCoordinatePreset.AndroidRotationVector
            ? androidAxisSigns
            : iPhoneAxisSigns;

        euler = new Vector3(
            euler.x * Mathf.Sign(Mathf.Approximately(axisSigns.x, 0f) ? 1f : axisSigns.x),
            euler.y * Mathf.Sign(Mathf.Approximately(axisSigns.y, 0f) ? 1f : axisSigns.y),
            euler.z * Mathf.Sign(Mathf.Approximately(axisSigns.z, 0f) ? 1f : axisSigns.z));

        return Quaternion.Euler(euler);
    }

    public static Quaternion ConvertRightHandedYUpToUnity(Quaternion sensorQuaternion, Vector3 eulerOffset, bool convertHandedness = true)
    {
        return ConvertToUnity(
            sensorQuaternion,
            QuaternionCoordinatePreset.IPhoneCoreMotion,
            eulerOffset,
            convertHandedness);
    }

    private static Quaternion NormalizeQuaternion(Quaternion quaternion)
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
        return new Quaternion(
            quaternion.x * inverseMagnitude,
            quaternion.y * inverseMagnitude,
            quaternion.z * inverseMagnitude,
            quaternion.w * inverseMagnitude);
    }

    private static Quaternion ConvertHandedness(Quaternion sensorQuaternion, QuaternionCoordinatePreset coordinatePreset)
    {
        switch (coordinatePreset)
        {
            case QuaternionCoordinatePreset.AndroidRotationVector:
                return new Quaternion(sensorQuaternion.x, sensorQuaternion.y, -sensorQuaternion.z, -sensorQuaternion.w);

            case QuaternionCoordinatePreset.IPhoneCoreMotion:
            default:
                return new Quaternion(sensorQuaternion.x, sensorQuaternion.y, -sensorQuaternion.z, -sensorQuaternion.w);
        }
    }

    private static Quaternion ConvertIPhoneCoreMotion(Quaternion sensorQuaternion, bool convertHandedness)
    {
        if (!convertHandedness)
        {
            return sensorQuaternion;
        }

        Vector3 deviceRight = RotateVector(sensorQuaternion, Vector3.right);
        Vector3 deviceTop = RotateVector(sensorQuaternion, Vector3.up);
        Vector3 deviceScreenOut = RotateVector(sensorQuaternion, Vector3.forward);

        if (deviceTop.sqrMagnitude <= 0.000001f || deviceScreenOut.sqrMagnitude <= 0.000001f)
        {
            return Quaternion.identity;
        }

        // Core Motion device axes:
        // +X = screen right, +Y = screen top, +Z = out of the screen.
        // The rig expects +Z to point along the handset top direction so roll
        // becomes a twist around the ray instead of lateral spotlight motion.
        return Quaternion.LookRotation(deviceTop.normalized, -deviceScreenOut.normalized);
    }

    private static Vector3 ToSignedEuler(Quaternion rotation)
    {
        Vector3 euler = rotation.eulerAngles;
        return new Vector3(
            Mathf.DeltaAngle(0f, euler.x),
            Mathf.DeltaAngle(0f, euler.y),
            Mathf.DeltaAngle(0f, euler.z));
    }

    private static float ReadComponent(Quaternion quaternion, QuaternionComponentSource componentSource)
    {
        switch (componentSource)
        {
            case QuaternionComponentSource.X:
                return quaternion.x;
            case QuaternionComponentSource.Y:
                return quaternion.y;
            case QuaternionComponentSource.Z:
                return quaternion.z;
            case QuaternionComponentSource.W:
                return quaternion.w;
            default:
                return 0f;
        }
    }

    private static Vector3 RotateVector(Quaternion quaternion, Vector3 vector)
    {
        float xx = quaternion.x + quaternion.x;
        float yy = quaternion.y + quaternion.y;
        float zz = quaternion.z + quaternion.z;
        float wx = quaternion.w * xx;
        float wy = quaternion.w * yy;
        float wz = quaternion.w * zz;
        float xx2 = quaternion.x * xx;
        float xy2 = quaternion.x * yy;
        float xz2 = quaternion.x * zz;
        float yy2 = quaternion.y * yy;
        float yz2 = quaternion.y * zz;
        float zz2 = quaternion.z * zz;

        return new Vector3(
            (1f - (yy2 + zz2)) * vector.x + (xy2 - wz) * vector.y + (xz2 + wy) * vector.z,
            (xy2 + wz) * vector.x + (1f - (xx2 + zz2)) * vector.y + (yz2 - wx) * vector.z,
            (xz2 - wy) * vector.x + (yz2 + wx) * vector.y + (1f - (xx2 + yy2)) * vector.z);
    }
}
