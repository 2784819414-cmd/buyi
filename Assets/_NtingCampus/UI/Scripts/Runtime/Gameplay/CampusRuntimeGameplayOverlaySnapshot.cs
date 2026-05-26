using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Rooms;
using UnityEngine;

namespace NtingCampus.UI.Runtime.Gameplay
{
    [Serializable]
    public sealed class CampusRuntimeGameplayOverlaySnapshot
    {
        public string Schema = CampusRuntimeGameplayOverlayStore.Schema;
        public string MapName = string.Empty;
        public List<CampusRuntimeGameplayActorSnapshot> Actors = new List<CampusRuntimeGameplayActorSnapshot>();
        public List<CampusRuntimeGameplayRoomSnapshot> Rooms = new List<CampusRuntimeGameplayRoomSnapshot>();
        public List<CampusRuntimeGameplayFacilitySnapshot> Facilities = new List<CampusRuntimeGameplayFacilitySnapshot>();
        public List<CampusRuntimeGameplayServiceStationSnapshot> ServiceStations =
            new List<CampusRuntimeGameplayServiceStationSnapshot>();
    }

    [Serializable]
    public sealed class CampusRuntimeGameplayRoomSnapshot
    {
        public string Id = string.Empty;
        public string DisplayName = string.Empty;
        public CampusLocalizedText LocalizedDisplayName = default;
        public string RoomTypeId = string.Empty;
        public CampusRoomType RoomType = CampusRoomType.Unknown;
        public int FloorIndex = 1;
        public Vector3Int AnchorCell;
        public Vector2Int Size = Vector2Int.one;
        public bool UsableForGameplay = true;

        public void Normalize()
        {
            Id = string.IsNullOrWhiteSpace(Id) ? string.Empty : Id.Trim();
            DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? string.Empty : DisplayName.Trim();
            RoomType = CampusGameplayOverlayEnumIds.Resolve(RoomTypeId, RoomType);
            RoomTypeId = CampusGameplayOverlayEnumIds.ToId(RoomType);
            FloorIndex = Mathf.Max(1, FloorIndex);
            Size = new Vector2Int(Mathf.Max(1, Size.x), Mathf.Max(1, Size.y));
        }

        public string GetPrimaryDisplayName()
        {
            if (LocalizedDisplayName.HasAnyText)
            {
                return LocalizedDisplayName.ResolvePrimary(DisplayName, CampusRoomTextCatalog.GetLocalizedText(RoomType).ResolvePrimary());
            }

            if (!string.IsNullOrWhiteSpace(DisplayName))
            {
                return DisplayName.Trim();
            }

            return CampusRoomTextCatalog.GetLocalizedText(RoomType).ResolvePrimary();
        }
    }

    [Serializable]
    public sealed class CampusRuntimeGameplayActorSnapshot
    {
        public string Id = string.Empty;
        public string DisplayName = string.Empty;
        public CampusLocalizedText LocalizedDisplayName;
        public string RoleId = string.Empty;
        public CampusCharacterRole Role = CampusCharacterRole.Student;
        public string TeacherDutyId = string.Empty;
        public CampusTeacherDuty TeacherDuty = CampusTeacherDuty.None;
        public string StaffDutyId = string.Empty;
        public CampusStaffDuty StaffDuty = CampusStaffDuty.None;
        public string ClassId = "class_1";
        public string InitialStateId = string.Empty;
        public CampusCharacterState InitialState = CampusCharacterState.Normal;
        public bool IsPlayerControlled;
        public int FloorIndex = 1;
        public Vector3Int Cell;
        public int Sleepiness = 40;
        public int Mischief = 20;
        public int InitialMoney = NtingCampus.Gameplay.Economy.CampusCharacterEconomyDefaults.UseRoleDefaultMoney;
        public string[] TraitIds = Array.Empty<string>();
        public CampusCharacterTrait[] Traits = Array.Empty<CampusCharacterTrait>();
        public CampusCharacterAssignmentData Assignments = new CampusCharacterAssignmentData();

        public void Normalize()
        {
            Id = string.IsNullOrWhiteSpace(Id) ? string.Empty : Id.Trim();
            DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? string.Empty : DisplayName.Trim();
            Role = CampusGameplayOverlayEnumIds.Resolve(RoleId, Role);
            TeacherDuty = CampusGameplayOverlayEnumIds.Resolve(TeacherDutyId, TeacherDuty);
            StaffDuty = CampusGameplayOverlayEnumIds.Resolve(StaffDutyId, StaffDuty);
            InitialState = CampusGameplayOverlayEnumIds.Resolve(InitialStateId, InitialState);
            Traits = CampusGameplayOverlayEnumIds.ResolveArray(TraitIds, Traits);
            RoleId = CampusGameplayOverlayEnumIds.ToId(Role);
            TeacherDutyId = CampusGameplayOverlayEnumIds.ToId(TeacherDuty);
            StaffDutyId = CampusGameplayOverlayEnumIds.ToId(StaffDuty);
            InitialStateId = CampusGameplayOverlayEnumIds.ToId(InitialState);
            TraitIds = CampusGameplayOverlayEnumIds.ToIds(Traits);
            ClassId = string.IsNullOrWhiteSpace(ClassId) ? string.Empty : ClassId.Trim();
            FloorIndex = Mathf.Max(1, FloorIndex);
            InitialMoney = Mathf.Max(NtingCampus.Gameplay.Economy.CampusCharacterEconomyDefaults.UseRoleDefaultMoney, InitialMoney);
            Assignments = Assignments ?? new CampusCharacterAssignmentData();
            Assignments.Normalize();
        }
    }

    [Serializable]
    public sealed class CampusRuntimeGameplayFacilitySnapshot
    {
        public string Id = string.Empty;
        public string DisplayName = string.Empty;
        public string FacilityTypeId = string.Empty;
        public CampusFacilityType FacilityType = CampusFacilityType.Unknown;
        public int FloorIndex = 1;
        public Vector3Int Cell;
        public bool CountsAsCoreFacility = true;

        public void Normalize()
        {
            FloorIndex = Mathf.Max(1, FloorIndex);
            DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? string.Empty : DisplayName.Trim();
            FacilityType = CampusGameplayOverlayEnumIds.Resolve(FacilityTypeId, FacilityType);
            FacilityTypeId = CampusGameplayOverlayEnumIds.ToId(FacilityType);
            Id = CampusGameplayFacilityMarker.NormalizeFacilityId(Id);
            if (string.IsNullOrEmpty(Id))
            {
                Id = CampusGameplayFacilityMarker.BuildStableFacilityId(FloorIndex, FacilityType, Cell);
            }
        }
    }

    [Serializable]
    public sealed class CampusRuntimeGameplayServiceStationSnapshot
    {
        public string Id = string.Empty;
        public string StationTypeId = string.Empty;
        public string RoomId = string.Empty;
        public string OwnerFacilityId = string.Empty;
        public List<CampusRuntimeGameplayServiceStationSlotSnapshot> Slots =
            new List<CampusRuntimeGameplayServiceStationSlotSnapshot>();

        public void Normalize()
        {
            Id = NormalizeId(Id);
            StationTypeId = NormalizeId(StationTypeId);
            RoomId = NormalizeId(RoomId);
            OwnerFacilityId = NormalizeId(OwnerFacilityId);
            Slots = Slots ?? new List<CampusRuntimeGameplayServiceStationSlotSnapshot>();
            for (int i = 0; i < Slots.Count; i++)
            {
                if (Slots[i] != null)
                {
                    Slots[i].Normalize();
                }
            }
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    [Serializable]
    public sealed class CampusRuntimeGameplayServiceStationSlotSnapshot
    {
        public string RoleId = string.Empty;
        public List<string> FacilityIds = new List<string>();

        public void Normalize()
        {
            RoleId = string.IsNullOrWhiteSpace(RoleId) ? string.Empty : RoleId.Trim();
            FacilityIds = FacilityIds ?? new List<string>();
            for (int i = FacilityIds.Count - 1; i >= 0; i--)
            {
                string normalized = string.IsNullOrWhiteSpace(FacilityIds[i]) ? string.Empty : FacilityIds[i].Trim();
                if (string.IsNullOrEmpty(normalized))
                {
                    FacilityIds.RemoveAt(i);
                    continue;
                }

                FacilityIds[i] = normalized;
            }
        }
    }

    internal static class CampusGameplayOverlayEnumIds
    {
        public static TEnum Resolve<TEnum>(string id, TEnum fallback) where TEnum : struct
        {
            return !string.IsNullOrWhiteSpace(id) &&
                   Enum.TryParse(id.Trim(), true, out TEnum parsed)
                ? parsed
                : fallback;
        }

        public static TEnum[] ResolveArray<TEnum>(string[] ids, TEnum[] fallback) where TEnum : struct
        {
            if (ids == null || ids.Length == 0)
            {
                return fallback ?? Array.Empty<TEnum>();
            }

            List<TEnum> parsed = new List<TEnum>();
            for (int i = 0; i < ids.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(ids[i]) &&
                    Enum.TryParse(ids[i].Trim(), true, out TEnum value))
                {
                    parsed.Add(value);
                }
            }

            return parsed.Count > 0 ? parsed.ToArray() : fallback ?? Array.Empty<TEnum>();
        }

        public static string ToId<TEnum>(TEnum value) where TEnum : struct
        {
            return value.ToString();
        }

        public static string[] ToIds<TEnum>(TEnum[] values) where TEnum : struct
        {
            if (values == null || values.Length == 0)
            {
                return Array.Empty<string>();
            }

            string[] ids = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                ids[i] = values[i].ToString();
            }

            return ids;
        }
    }
}

