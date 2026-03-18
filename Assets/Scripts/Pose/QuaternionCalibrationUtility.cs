using UnityEngine;

public static class QuaternionCalibrationUtility
{
    public static Quaternion CalculateRelativeRotation(Quaternion referenceRotation, Quaternion currentRotation)
    {
        return Quaternion.Inverse(referenceRotation) * currentRotation;
    }
}
