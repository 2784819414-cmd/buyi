using NtingCampus.Gameplay.Rooms;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    internal static class CampusNpcIntentActions
    {
        public static CampusNpcIntent StudentDesk(CampusNpcAiRuntime npc, string label)
        {
            CampusNpcPersonalProfile profile = npc != null ? npc.Profile : null;
            Vector3 target = profile != null && profile.HasStudentDesk
                ? profile.StudentDeskPosition
                : npc != null ? npc.RoomTarget(CampusRoomType.Classroom, 0.25f) : Vector3.zero;
            return CampusNpcIntent.Move(
                CampusNpcIntentKind.AttendAssignedDesk,
                label,
                profile != null ? profile.StudentClassroomId : string.Empty,
                target,
                0.16f);
        }

        public static CampusNpcIntent TeacherPodium(CampusNpcAiRuntime npc, string label)
        {
            CampusNpcPersonalProfile profile = npc != null ? npc.Profile : null;
            Vector3 target = profile != null && profile.HasTeacherPodium
                ? profile.TeacherPodiumPosition
                : npc != null ? npc.RoomTarget(CampusRoomType.Classroom, 0.2f) : Vector3.zero;
            return CampusNpcIntent.Move(
                CampusNpcIntentKind.TeachAssignedClass,
                label,
                profile != null ? profile.TeacherClassroomId : string.Empty,
                target,
                0.18f);
        }

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

        public static CampusNpcIntent Dorm(CampusNpcAiRuntime npc, string label)
        {
            CampusNpcPersonalProfile profile = npc != null ? npc.Profile : null;
            Vector3 target = profile != null ? profile.DormPosition : Vector3.zero;
            if (target == Vector3.zero)
            {
                target = npc != null ? npc.RoomTarget(CampusRoomType.Dormitory, 0.35f) : Vector3.zero;
            }

            return CampusNpcIntent.Move(
                CampusNpcIntentKind.RestInDorm,
                label,
                profile != null ? profile.DormRoomId : string.Empty,
                target,
                0.22f);
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

        public static CampusNpcIntent DeliveryPoint(
            CampusNpcAiRuntime npc,
            CampusNpcIntentKind kind,
            string label)
        {
            CampusNpcPersonalProfile profile = npc != null ? npc.Profile : null;
            Vector3 target = profile != null && profile.HasDeliveryPoint
                ? profile.DeliveryPointPosition
                : npc != null ? npc.RoomTarget(CampusRoomType.Outdoor, 0.25f) : Vector3.zero;
            return CampusNpcIntent.Move(
                kind,
                label,
                profile != null ? profile.DeliveryRoomId : string.Empty,
                target,
                0.18f);
        }

        public static CampusNpcIntent PrimaryWorkstation(
            CampusNpcAiRuntime npc,
            CampusNpcIntentKind kind,
            string label)
        {
            CampusNpcPersonalProfile profile = npc != null ? npc.Profile : null;
            Vector3 target = profile != null && profile.HasPrimaryWorkstation
                ? profile.PrimaryWorkstationPosition
                : npc != null ? npc.RoomTarget(CampusRoomType.Canteen, 0.25f) : Vector3.zero;
            return CampusNpcIntent.Move(
                kind,
                label,
                profile != null ? profile.WorkRoomId : string.Empty,
                target,
                0.16f);
        }

        public static CampusNpcIntent SecondaryWorkstation(
            CampusNpcAiRuntime npc,
            CampusNpcIntentKind kind,
            string label)
        {
            CampusNpcPersonalProfile profile = npc != null ? npc.Profile : null;
            if (profile != null && profile.SecondaryWorkstationPositions != null && profile.SecondaryWorkstationPositions.Count > 0)
            {
                int index = CampusNpcStableIds.PositiveModulo(
                    Mathf.FloorToInt((npc != null ? npc.Time : Time.time) / 6f) + (npc != null ? npc.PersonalSeed : 1),
                    profile.SecondaryWorkstationPositions.Count);
                return CampusNpcIntent.Move(kind, label, profile.WorkRoomId, profile.SecondaryWorkstationPositions[index], 0.18f);
            }

            return PrimaryWorkstation(npc, kind, label);
        }

        public static CampusNpcIntent ShelfAuditPoint(CampusNpcAiRuntime npc)
        {
            CampusNpcPersonalProfile profile = npc != null ? npc.Profile : null;
            if (profile != null && profile.ShelfPositions != null && profile.ShelfPositions.Count > 0)
            {
                int index = CampusNpcStableIds.PositiveModulo(
                    Mathf.FloorToInt((npc != null ? npc.Time : Time.time) / 8f) + (npc != null ? npc.PersonalSeed : 1),
                    profile.ShelfPositions.Count);
                return CampusNpcIntent.Move(
                    CampusNpcIntentKind.AuditStoreShelves,
                    "AuditShelf",
                    profile.WorkRoomId,
                    profile.ShelfPositions[index],
                    0.18f);
            }

            return PrimaryWorkstation(npc, CampusNpcIntentKind.AuditStoreShelves, "AuditShelf");
        }

        public static CampusNpcIntent RoomType(
            CampusNpcAiRuntime npc,
            CampusRoomType roomType,
            CampusNpcIntentKind kind,
            string label,
            float radius)
        {
            return CampusNpcIntent.Move(
                kind,
                label,
                string.Empty,
                npc != null ? npc.RoomTarget(roomType, radius) : Vector3.zero,
                0.24f);
        }

        public static CampusNpcIntent Opportunity(
            CampusNpcActionOpportunity opportunity,
            CampusNpcIntentKind kind,
            string label)
        {
            return opportunity != null
                ? opportunity.ToIntent(kind, label)
                : CampusNpcIntent.Idle(label);
        }
    }
}
