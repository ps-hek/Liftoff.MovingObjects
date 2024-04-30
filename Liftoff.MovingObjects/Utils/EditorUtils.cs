using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Liftoff.MovingObjects.Utils;

internal class EditorUtils
{

    private static readonly Type[] TrackItemTypes = new[]
    {
        typeof(TrackItemFlag),
        typeof(TrackItemKillDroneTrigger),
        typeof(TrackItemShowTextTrigger),
        typeof(TrackItemPlaySoundTrigger),
        typeof(TrackItemRepairPropellersTrigger),
        typeof(TrackItemChargeBatteryTrigger),
        typeof(TrackItemFlexibleCheckpointTrigger),
    };

    public static List<Component> FindAllFlags()
    {
        var flags = new List<Component>();
        foreach (var type in TrackItemTypes)
            flags.AddRange(Object.FindObjectsOfType(type).OfType<Component>());
        return flags;
    }

    public static Component FindFlagInParent(GameObject parent)
    {
        return TrackItemTypes.Select(parent.GetComponentInParent).FirstOrDefault(component => component != null);
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