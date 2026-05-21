using NtingCampus.Gameplay.Rooms;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    internal static class CampusNpcIntentActions
    {
        public static CampusNpcIntent OfficeDesk(CampusNpcAiRuntime npc, string label)
        {
            CampusNpcPersonalProfile profile = npc != null ? npc.Profile : null;
            Vector3 target = profile != null && profile.HasOfficeDesk
                ? profile.OfficeDeskPosition
                : npc != null ? npc.RoomTarget(CampusRoomType.Office, 0.25f) : Vector3.zero;
            return CampusNpcIntent.Move(
                CampusNpcIntentKind.ReturnToOfficeDesk,
                label,
                profile != null ? profile.OfficeRoomId : string.Empty,
                target,
                0.18f);
        }

        public static CampusNpcIntent Common(CampusNpcAiRuntime npc, string label)
        {
            CampusNpcPersonalProfile profile = npc != null ? npc.Profile : null;
            Vector3 target = profile != null ? profile.CommonPosition : Vector3.zero;
            if (target == Vector3.zero)
            {
                target = npc != null ? npc.RoomTarget(CampusRoomType.Corridor, 0.45f) : Vector3.zero;
            }

            return CampusNpcIntent.Move(
                CampusNpcIntentKind.Roam,
                label,
                profile != null ? profile.CommonRoomId : string.Empty,
                target,
                0.22f);
        }
    }
}
