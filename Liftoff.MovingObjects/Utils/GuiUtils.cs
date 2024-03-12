using System;
using System.Globalization;
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

    public static void ToggleVisible(VisualElement element)
    {
        element.style.display = element.style.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public static void ConvertToIntField(TextField field, Action<int> valueCallback, int defaultValue = 0)
    {
        field.SetValueWithoutNotify(defaultValue.ToString());
        field.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (!char.IsDigit(evt.character))
                evt.PreventDefault();
        });

        field.RegisterValueChangedCallback(evt =>
        {
            if (!int.TryParse(evt.newValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                field.SetValueWithoutNotify(evt.previousValue);
                evt.PreventDefault();
                return;
            }

            var strInt = value.ToString();
            if (strInt != evt.newValue)
                field.SetValueWithoutNotify(strInt);
            valueCallback(value);
        });
    }


    public static void ConvertToFloatField(TextField field, Action<float> valueCallback, float defaultValue = 0f)
    {
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