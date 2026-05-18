using System;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    public readonly struct CampusNpcPlanningContext
    {
        public readonly CampusCharacterData Data;
        public readonly CampusNpcPersonalProfile Profile;
        public readonly CampusNpcMindState Mind;
        public readonly CampusTimeSegment Segment;
        public readonly float Time;
        public readonly int PersonalSeed;
        public readonly Func<CampusRoomType, float, Vector3> ResolveRoomTarget;

        public CampusNpcPlanningContext(
            CampusCharacterData data,
            CampusNpcPersonalProfile profile,
            CampusNpcMindState mind,
            CampusTimeSegment segment,
            float time,
            int personalSeed,
            Func<CampusRoomType, float, Vector3> resolveRoomTarget)
        {
            Data = data;
            Profile = profile;
            Mind = mind;
            Segment = segment;
            Time = time;
            PersonalSeed = Mathf.Max(1, personalSeed);
            ResolveRoomTarget = resolveRoomTarget;
        }

        public Vector3 RoomTarget(CampusRoomType roomType, float radius)
        {
            return ResolveRoomTarget != null ? ResolveRoomTarget(roomType, radius) : Vector3.zero;
        }
    }
}
