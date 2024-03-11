using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Liftoff.MovingObjects.Utils;

internal static class GuiUtils
{
    public static string FloatToString(float value)
    {
        return value.ToString("0.000", CultureInfo.InvariantCulture);
    }

    public static void SetVisible(VisualElement element, bool visible)
    {
        element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public static void ConvertToFloatField(TextField field, float defaultValue, Action<float> valueCallback)
    {
        Debug.LogWarning($"{field} {defaultValue}");
        ;
        field.SetValueWithoutNotify(FloatToString(defaultValue));
        field.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (!char.IsDigit(evt.character))
                evt.PreventDefault();
        });

        field.RegisterValueChangedCallback(evt =>
        {
            if (!float.TryParse(evt.newValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                field.SetValueWithoutNotify(evt.previousValue);
                evt.PreventDefault();
                return;
            }

            var strFloat = FloatToString(value);
            if (strFloat != evt.newValue)
                field.SetValueWithoutNotify(strFloat);
            valueCallback(value);
        });
    }

    public static string VectorToString(SerializableVector3 vec)
    {
        return $"{FloatToString(vec.x)}, {FloatToString(vec.y)}, {FloatToString(vec.z)}";
    }
}