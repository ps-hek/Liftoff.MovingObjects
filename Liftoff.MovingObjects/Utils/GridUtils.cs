using System;
using UnityEngine;

namespace Liftoff.MovingObjects.Utils;

internal class GridUtils
{
    private static float RoundToStep(float value, float step, int decimals = 3)
    {
        if (step == 0)
            return value;

        var d = MathF.Pow(10, decimals);
        var rawVal = MathF.Round(value / step, MidpointRounding.AwayFromZero) * step;

        return MathF.Floor(rawVal * d) / d;
    }

    public static Vector3 RoundVectorToStep(Vector3 value, float step)
    {
        return new Vector3(RoundToStep(value.x, step), RoundToStep(value.y, step), RoundToStep(value.z, step));
    }

    public static float SmartRound(float value, float tolerance)
    {
        var r = Mathf.Round(value);
        if (Mathf.Abs(r - value) > tolerance)
            return value;
        return r;
    }

    public static Vector3 SmartRound(Vector3 value, float tolerance = 0.05f)
    {
        return new Vector3(SmartRound(value.x, tolerance), SmartRound(value.y, tolerance),
            SmartRound(value.z, tolerance));
    }
}