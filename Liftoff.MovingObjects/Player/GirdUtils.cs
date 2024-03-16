using System;
using UnityEngine;

namespace Liftoff.MovingObjects.Player;

internal class GirdUtils
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
}