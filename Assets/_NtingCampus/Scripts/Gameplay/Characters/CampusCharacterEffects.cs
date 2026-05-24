using NtingCampus.Gameplay.Rooms;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    public enum CampusCharacterEffectSource
    {
        Unknown = 0,
        Classroom = 10,
        Inspection = 20,
        Sanction = 30
    }

    public static class CampusCharacterEffects
    {
        public static bool TrySetState(
            CampusCharacterRuntime actor,
            CampusCharacterState state,
            CampusCharacterEffectSource source = CampusCharacterEffectSource.Unknown)
        {
            if (actor == null || actor.Data == null)
            {
                return false;
            }

            actor.Data.SetState(state);
            return true;
        }

        public static bool TryMoveToRoom(
            CampusCharacterRuntime actor,
            CampusGameplayRoom room,
            Vector3 offset,
            CampusCharacterEffectSource source = CampusCharacterEffectSource.Unknown)
        {
            if (actor == null || actor.Data == null || room == null)
            {
                return false;
            }

            actor.transform.position = room.WorldCenter + offset;
            CampusCharacterCurrentRoomTracker.SyncRuntime(actor);
            return true;
        }
    }
}
