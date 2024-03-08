using System;
using System.Globalization;
using BepInEx.Logging;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace Liftoff.MovingObjects.Utils;

internal static class GuiUtils
{
    private static readonly ManualLogSource Log =
        Logger.CreateLogSource($"{PluginInfo.PLUGIN_NAME}.{nameof(GuiUtils)}");

    private static bool _guiActive;
    private static Rect _guiRect;

    public static void Unlock()
    {
        _guiActive = false;
    }

    public static void Lock(Rect rect)
    {
        _guiActive = true;
        _guiRect = rect;
    }

    public static bool IsGuiLocked()
    {
        if (!_guiActive)
            return false;
        return GUIUtility.hotControl != 0 || _guiRect.Contains(Event.current.mousePosition);
    }

    public static void TextBoxFloat(string label, ref float value, bool enabled = true)
    {
        //GUILayout.BeginHorizontal();
        GUILayout.Label(label);

        GUI.enabled = enabled;
        try
        {
            value = float.Parse(GUILayout.TextField(value.ToString("F4")), CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Invalid {label} float value {value}, enabled: {enabled}: {ex}");
        }

        GUI.enabled = true;
        //GUILayout.EndHorizontal();
    }
}