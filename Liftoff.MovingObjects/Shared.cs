using System;
using UnityEngine;

namespace Liftoff.MovingObjects;

internal static class Shared
{
    internal static class PlacementUtils
    {
        public static float GridRound { get; set; } = 0.5f;
        public static float DragGridRound { get; set; } = 0.0f;
        public static bool EnchantedEditor { get; set; }
    }

    internal static class Editor
    {
        public static event Action<ItemInfo> OnItemSelected;
        public static event Action OnItemCleared;
        public static event Action OnRefreshGuiRequest;

        public static void ItemSelected(ItemInfo info)
        {
            OnItemSelected?.Invoke(info);
        }

        public static void ItemCleared()
        {
            OnItemCleared?.Invoke();
        }
        public static void RequestRefreshGui()
        {
            OnRefreshGuiRequest?.Invoke();
        }

        public class ItemInfo
        {
            public TrackBlueprint blueprint;
            public GameObject gameObject;
        }
    }
}