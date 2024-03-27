using System.Collections.Generic;
using UnityEngine;

namespace Liftoff.MovingObjects.Utils;

internal class EditorUtils
{

    public static List<Component> FindAllFlags()
    {
        var flags = new List<Component>();
        flags.AddRange(Object.FindObjectsOfType<TrackItemFlag>());
        flags.AddRange(Object.FindObjectsOfType<TrackItemKillDroneTrigger>());
        flags.AddRange(Object.FindObjectsOfType<TrackItemShowTextTrigger>());
        flags.AddRange(Object.FindObjectsOfType<TrackItemPlaySoundTrigger>());
        flags.AddRange(Object.FindObjectsOfType<TrackItemRepairPropellersTrigger>());
        flags.AddRange(Object.FindObjectsOfType<TrackItemChargeBatteryTrigger>());
        flags.AddRange(Object.FindObjectsOfType<TrackItemFlexibleCheckpointTrigger>());
        return flags;
    }

    public static List<Component> FindFlagsByGroupId(string groupId)
    {
        var flags = new List<Component>();
        foreach (var flag in FindAllFlags())
        {
            var blueprint = ReflectionUtils.GetPrivateFieldValueByType<TrackBlueprint>(flag);
            if (string.Equals(blueprint?.mo_groupId, groupId))
                flags.Add(flag);
        }
        return flags;
    }
}