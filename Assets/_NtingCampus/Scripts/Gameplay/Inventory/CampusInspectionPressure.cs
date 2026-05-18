using System;
using NtingCampus.Gameplay.Rooms;
using UnityEngine;

namespace NtingCampus.Gameplay.Inventory
{
    [Serializable]
    public struct CampusInspectionPressure
    {
        [SerializeField, Range(0, 100)] private int value;

        public CampusInspectionPressure(int pressureValue)
        {
            value = Mathf.Clamp(pressureValue, 0, 100);
        }

        public int Value => Mathf.Clamp(value, 0, 100);
        public float Chance01 => Value / 100f;

        public static CampusInspectionPressure Of(int pressureValue)
        {
            return new CampusInspectionPressure(pressureValue);
        }
    }

    [Serializable]
    public sealed class CampusAreaInspectionPressureRule
    {
        public string RoomId;
        public CampusRoomType RoomType = CampusRoomType.Unknown;
        public CampusInspectionPressure SearchPressure = new CampusInspectionPressure(10);
        public CampusInspectionPressure QuestioningPressure = new CampusInspectionPressure(10);

        public bool Matches(CampusGameplayRoom room)
        {
            if (room == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(RoomId) &&
                string.Equals(RoomId.Trim(), room.RoomId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return RoomType != CampusRoomType.Unknown && RoomType == room.RoomType;
        }
    }

    [Serializable]
    public sealed class CampusNpcInspectionPressureRule
    {
        public string CharacterId;
        public CampusInspectionPressure VigilancePressure = new CampusInspectionPressure(35);

        public bool Matches(string characterId)
        {
            return !string.IsNullOrWhiteSpace(CharacterId) &&
                   !string.IsNullOrWhiteSpace(characterId) &&
                   string.Equals(CharacterId.Trim(), characterId.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
