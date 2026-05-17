using UnityEngine;

namespace NtingCampus.Gameplay.Rooms
{
    public static class CampusRoomsBootstrap
    {
        public static void EnsureRoomsForScene()
        {
            CampusRoomRegistry registry = Object.FindFirstObjectByType<CampusRoomRegistry>(FindObjectsInactive.Include);
            if (registry != null)
            {
                registry.RebuildRegistry();
            }
        }
    }
}
