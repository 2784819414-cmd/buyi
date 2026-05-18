using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    public static class CampusNpcIntentPlanner
    {
        private const float DeliveryOrderHoldSeconds = 4.5f;

        public static CampusNpcPlanDecision Choose(CampusNpcPlanningContext context)
        {
            CampusCharacterData data = context.Data;
            if (data == null)
            {
                return new CampusNpcPlanDecision(CampusNpcIntent.Idle("NoData"));
            }

            if (data.State == CampusCharacterState.Punished)
            {
                return new CampusNpcPlanDecision(MoveToOffice(context, "Punished"));
            }

            if (context.Mind != null &&
                context.Mind.CurrentIntent != null &&
                context.Mind.CurrentIntent.Kind == CampusNpcIntentKind.UsePhoneForDelivery &&
                context.Time < context.Mind.IntentHoldUntil)
            {
                return new CampusNpcPlanDecision(context.Mind.CurrentIntent);
            }

            if (context.Mind != null && context.Mind.HasActiveFocus && ShouldRespondToFocus(context))
            {
                return new CampusNpcPlanDecision(BuildFocusIntent(context));
            }

            switch (data.Role)
            {
                case CampusCharacterRole.Teacher:
                    return new CampusNpcPlanDecision(ChooseTeacherIntent(context));
                case CampusCharacterRole.Staff:
                    return new CampusNpcPlanDecision(ChooseStaffIntent(context));
                default:
                    return ChooseStudentIntent(context);
            }
        }

        private static CampusNpcPlanDecision ChooseStudentIntent(CampusNpcPlanningContext context)
        {
            if (CampusNpcScheduleFacts.IsClassSession(context.Segment))
            {
                return new CampusNpcPlanDecision(MoveToStudentDesk(context, "Class"));
            }

            if (context.Mind != null && context.Mind.DeliveryState == CampusNpcDeliveryState.ReadyForPickup)
            {
                return new CampusNpcPlanDecision(MoveToDeliveryPoint(
                    context,
                    CampusNpcIntentKind.PickupDelivery,
                    "PickupDelivery"));
            }

            if (ShouldStartDeliveryOrder(context))
            {
                return new CampusNpcPlanDecision(
                    CampusNpcIntent.Hold(
                        CampusNpcIntentKind.UsePhoneForDelivery,
                        "UsePhoneDelivery",
                        DeliveryOrderHoldSeconds),
                    true);
            }

            if (CampusNpcScheduleFacts.IsDormWindow(context.Segment))
            {
                return new CampusNpcPlanDecision(MoveToDorm(context, "Dorm"));
            }

            if (CampusNpcScheduleFacts.IsMealPeak(context.Segment))
            {
                return new CampusNpcPlanDecision(MoveToRoomType(
                    context,
                    CampusRoomType.Canteen,
                    CampusNpcIntentKind.Roam,
                    "MealBreak",
                    0.25f));
            }

            return new CampusNpcPlanDecision(MoveToCommon(context, "FreeRoam"));
        }

        private static CampusNpcIntent ChooseTeacherIntent(CampusNpcPlanningContext context)
        {
            if (CampusNpcScheduleFacts.IsClassSession(context.Segment))
            {
                return MoveToTeacherPodium(context, "Teach");
            }

            if (CampusNpcScheduleFacts.IsTeacherOfficeWindow(context.Segment))
            {
                return MoveToOffice(context, "Office");
            }

            return MoveToCommon(context, "TeacherFree");
        }

        private static CampusNpcIntent ChooseStaffIntent(CampusNpcPlanningContext context)
        {
            CampusStaffDuty duty = context.Data != null ? context.Data.StaffDuty : CampusStaffDuty.None;
            if ((duty & CampusStaffDuty.StoreOwner) != 0 || (duty & CampusStaffDuty.BookstoreOwner) != 0)
            {
                if (CampusNpcScheduleFacts.IsStoreOpen(context.Segment))
                {
                    return MoveToPrimaryWorkstation(context, CampusNpcIntentKind.WorkStoreCheckout, "StoreCheckout");
                }

                if (CampusNpcScheduleFacts.IsStoreStocktakeWindow(context.Segment))
                {
                    return MoveToShelfAuditPoint(context);
                }

                return MoveToCommon(context, "StoreOffDuty");
            }

            if ((duty & CampusStaffDuty.DeliveryWatcher) != 0)
            {
                if (!CampusNpcScheduleFacts.IsStaffOffDuty(context.Segment))
                {
                    return MoveToDeliveryPoint(context, CampusNpcIntentKind.WatchDeliveryPoint, "WatchDelivery");
                }

                return MoveToCommon(context, "DeliveryOffDuty");
            }

            if (CampusNpcScheduleFacts.IsMealPeak(context.Segment))
            {
                return MoveToPrimaryWorkstation(context, CampusNpcIntentKind.WorkCanteenCounter, "CanteenCounter");
            }

            if (CampusNpcScheduleFacts.IsCanteenShiftActive(context.Segment))
            {
                return MoveToSecondaryWorkstation(context, CampusNpcIntentKind.CoverCanteenWindows, "CoverCanteen");
            }

            return MoveToCommon(context, "StaffFree");
        }

        private static CampusNpcIntent BuildFocusIntent(CampusNpcPlanningContext context)
        {
            CampusNpcIntentKind kind = context.Data.Role == CampusCharacterRole.Student
                ? CampusNpcIntentKind.WatchEvent
                : CampusNpcIntentKind.InvestigateEvent;
            return CampusNpcIntent.Move(kind, kind.ToString(), context.Mind.FocusRoomId, context.Mind.FocusPosition, 0.24f);
        }

        private static bool ShouldRespondToFocus(CampusNpcPlanningContext context)
        {
            CampusCharacterData data = context.Data;
            if (data == null)
            {
                return false;
            }

            if (data.Role == CampusCharacterRole.Teacher || data.Role == CampusCharacterRole.Staff)
            {
                return !CampusNpcScheduleFacts.IsClassSession(context.Segment) || data.Role == CampusCharacterRole.Teacher;
            }

            return CampusNpcScheduleFacts.IsStudentFreeMovementWindow(context.Segment);
        }

        private static bool ShouldStartDeliveryOrder(CampusNpcPlanningContext context)
        {
            CampusCharacterData data = context.Data;
            CampusNpcMindState mind = context.Mind;
            if (data == null ||
                mind == null ||
                mind.DeliveryState == CampusNpcDeliveryState.Ordering ||
                mind.DeliveryState == CampusNpcDeliveryState.Waiting ||
                mind.DeliveryState == CampusNpcDeliveryState.ReadyForPickup ||
                context.Time < mind.NextDeliveryOrderAllowedAt ||
                !CampusNpcScheduleFacts.IsStudentDeliveryOrderWindow(context.Segment))
            {
                return false;
            }

            int threshold = data.HasTrait(CampusCharacterTrait.SecretDeliveryBuyer) ? 82 : 18;
            threshold += Mathf.Clamp(data.Mischief / 8, 0, 12);
            int roll = PositiveModulo(StableHash(data.Id + ":" + context.Segment), 100);
            return roll < threshold;
        }

        private static CampusNpcIntent MoveToStudentDesk(CampusNpcPlanningContext context, string label)
        {
            CampusNpcPersonalProfile profile = context.Profile;
            Vector3 target = profile != null && profile.HasStudentDesk
                ? profile.StudentDeskPosition
                : context.RoomTarget(CampusRoomType.Classroom, 0.25f);
            return CampusNpcIntent.Move(
                CampusNpcIntentKind.AttendAssignedDesk,
                label,
                profile != null ? profile.StudentClassroomId : string.Empty,
                target,
                0.16f);
        }

        private static CampusNpcIntent MoveToTeacherPodium(CampusNpcPlanningContext context, string label)
        {
            CampusNpcPersonalProfile profile = context.Profile;
            Vector3 target = profile != null && profile.HasTeacherPodium
                ? profile.TeacherPodiumPosition
                : context.RoomTarget(CampusRoomType.Classroom, 0.2f);
            return CampusNpcIntent.Move(
                CampusNpcIntentKind.TeachAssignedClass,
                label,
                profile != null ? profile.TeacherClassroomId : string.Empty,
                target,
                0.18f);
        }

        private static CampusNpcIntent MoveToOffice(CampusNpcPlanningContext context, string label)
        {
            CampusNpcPersonalProfile profile = context.Profile;
            Vector3 target = profile != null && profile.HasOfficeDesk
                ? profile.OfficeDeskPosition
                : context.RoomTarget(CampusRoomType.Office, 0.25f);
            return CampusNpcIntent.Move(
                CampusNpcIntentKind.ReturnToOfficeDesk,
                label,
                profile != null ? profile.OfficeRoomId : string.Empty,
                target,
                0.18f);
        }

        private static CampusNpcIntent MoveToDorm(CampusNpcPlanningContext context, string label)
        {
            CampusNpcPersonalProfile profile = context.Profile;
            Vector3 target = profile != null ? profile.DormPosition : Vector3.zero;
            if (target == Vector3.zero)
            {
                target = context.RoomTarget(CampusRoomType.Dormitory, 0.35f);
            }

            return CampusNpcIntent.Move(
                CampusNpcIntentKind.RestInDorm,
                label,
                profile != null ? profile.DormRoomId : string.Empty,
                target,
                0.22f);
        }

        private static CampusNpcIntent MoveToCommon(CampusNpcPlanningContext context, string label)
        {
            CampusNpcPersonalProfile profile = context.Profile;
            Vector3 target = profile != null ? profile.CommonPosition : Vector3.zero;
            if (target == Vector3.zero)
            {
                target = context.RoomTarget(CampusRoomType.Corridor, 0.45f);
            }

            return CampusNpcIntent.Move(
                CampusNpcIntentKind.Roam,
                label,
                profile != null ? profile.CommonRoomId : string.Empty,
                target,
                0.22f);
        }

        private static CampusNpcIntent MoveToDeliveryPoint(
            CampusNpcPlanningContext context,
            CampusNpcIntentKind kind,
            string label)
        {
            CampusNpcPersonalProfile profile = context.Profile;
            Vector3 target = profile != null && profile.HasDeliveryPoint
                ? profile.DeliveryPointPosition
                : context.RoomTarget(CampusRoomType.Outdoor, 0.25f);
            return CampusNpcIntent.Move(
                kind,
                label,
                profile != null ? profile.DeliveryRoomId : string.Empty,
                target,
                0.18f);
        }

        private static CampusNpcIntent MoveToPrimaryWorkstation(
            CampusNpcPlanningContext context,
            CampusNpcIntentKind kind,
            string label)
        {
            CampusNpcPersonalProfile profile = context.Profile;
            Vector3 target = profile != null && profile.HasPrimaryWorkstation
                ? profile.PrimaryWorkstationPosition
                : context.RoomTarget(CampusRoomType.Canteen, 0.25f);
            return CampusNpcIntent.Move(
                kind,
                label,
                profile != null ? profile.WorkRoomId : string.Empty,
                target,
                0.16f);
        }

        private static CampusNpcIntent MoveToSecondaryWorkstation(
            CampusNpcPlanningContext context,
            CampusNpcIntentKind kind,
            string label)
        {
            CampusNpcPersonalProfile profile = context.Profile;
            if (profile != null && profile.SecondaryWorkstationPositions != null && profile.SecondaryWorkstationPositions.Count > 0)
            {
                int index = PositiveModulo(Mathf.FloorToInt(context.Time / 6f) + context.PersonalSeed, profile.SecondaryWorkstationPositions.Count);
                return CampusNpcIntent.Move(kind, label, profile.WorkRoomId, profile.SecondaryWorkstationPositions[index], 0.18f);
            }

            return MoveToPrimaryWorkstation(context, kind, label);
        }

        private static CampusNpcIntent MoveToShelfAuditPoint(CampusNpcPlanningContext context)
        {
            CampusNpcPersonalProfile profile = context.Profile;
            if (profile != null && profile.ShelfPositions != null && profile.ShelfPositions.Count > 0)
            {
                int index = PositiveModulo(Mathf.FloorToInt(context.Time / 8f) + context.PersonalSeed, profile.ShelfPositions.Count);
                return CampusNpcIntent.Move(
                    CampusNpcIntentKind.AuditStoreShelves,
                    "AuditShelf",
                    profile.WorkRoomId,
                    profile.ShelfPositions[index],
                    0.18f);
            }

            return MoveToPrimaryWorkstation(context, CampusNpcIntentKind.AuditStoreShelves, "AuditShelf");
        }

        private static CampusNpcIntent MoveToRoomType(
            CampusNpcPlanningContext context,
            CampusRoomType roomType,
            CampusNpcIntentKind kind,
            string label,
            float radius)
        {
            return CampusNpcIntent.Move(kind, label, string.Empty, context.RoomTarget(roomType, radius), 0.24f);
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 23;
                string normalized = value ?? string.Empty;
                for (int i = 0; i < normalized.Length; i++)
                {
                    hash = hash * 31 + char.ToUpperInvariant(normalized[i]);
                }

                return hash;
            }
        }

        private static int PositiveModulo(int value, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            int result = value % count;
            return result < 0 ? result + count : result;
        }
    }
}
