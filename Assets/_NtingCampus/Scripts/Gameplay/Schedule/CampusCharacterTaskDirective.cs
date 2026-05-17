using System;
using NtingCampus.Gameplay.Rooms;

namespace NtingCampus.Gameplay.Schedule
{
    [Serializable]
    public sealed class CampusCharacterTaskDirective
    {
        public CampusCharacterTaskType TaskType = CampusCharacterTaskType.Idle;
        public CampusRoomType TargetRoomType = CampusRoomType.Unknown;
        public CampusFacilityType PreferredFacilityType = CampusFacilityType.Unknown;
        public float HoldRadius = 0.24f;
        public bool RequiresSeat;
        public bool RequiresFacingFront;
        public string DebugLabel = string.Empty;
    }
}
