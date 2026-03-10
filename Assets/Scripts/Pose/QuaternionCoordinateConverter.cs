using UnityEngine;

public static class QuaternionCoordinateConverter
{
    public static Quaternion ConvertRightHandedYUpToUnity(Quaternion sensorQuaternion, Vector3 eulerOffset, bool convertHandedness = true)
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

        // Core Motion on iPhone uses device axes based on the handset body:
        // +X = right edge of the screen, +Y = top edge of the screen,
        // +Z = out of the screen toward the user.
        // This remaps that attitude so the projector rig uses:
        // +Z = phone top direction, +Y = screen normal, +X = phone right.
        Vector3 deviceRight = RotateVector(normalizedSensor, Vector3.right);
        Vector3 deviceTop = RotateVector(normalizedSensor, Vector3.up);
        Vector3 deviceScreenOut = RotateVector(normalizedSensor, Vector3.forward);

        if (deviceTop.sqrMagnitude <= 0.000001f || deviceScreenOut.sqrMagnitude <= 0.000001f)
        {
            return Quaternion.identity;
        }

        Quaternion unityRotation = Quaternion.LookRotation(deviceTop.normalized, deviceScreenOut.normalized);

        if (!convertHandedness)
        {
            unityRotation = Quaternion.Inverse(unityRotation);
        }

        Quaternion offsetRotation = Quaternion.Euler(eulerOffset);
        return Quaternion.Normalize(offsetRotation * unityRotation);
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
