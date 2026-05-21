using System;
using NtingCampus.Gameplay.Rooms;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    [Serializable]
    public sealed class CampusNpcPersonalProfile
    {
        public string CharacterId = string.Empty;
        public CampusCharacterRole Role;
        public string ClassId = string.Empty;

        public string StudentClassroomId = string.Empty;
        public string StudentDeskKey = string.Empty;
        public Vector3 StudentDeskPosition;

        public string TeacherClassroomId = string.Empty;
        public string TeacherPodiumKey = string.Empty;
        public Vector3 TeacherPodiumPosition;

        public string OfficeRoomId = string.Empty;
        public string OfficeDeskKey = string.Empty;
        public Vector3 OfficeDeskPosition;

        public string WorkRoomId = string.Empty;
        public string PrimaryWorkstationKey = string.Empty;
        public Vector3 PrimaryWorkstationPosition;

        public string DormRoomId = string.Empty;
        public Vector3 DormPosition;

        public string CommonRoomId = string.Empty;
        public Vector3 CommonPosition;

        public bool HasStudentDesk => !string.IsNullOrWhiteSpace(StudentDeskKey);
        public bool HasTeacherPodium => !string.IsNullOrWhiteSpace(TeacherPodiumKey);
        public bool HasOfficeDesk => !string.IsNullOrWhiteSpace(OfficeDeskKey);
        public bool HasPrimaryWorkstation => !string.IsNullOrWhiteSpace(PrimaryWorkstationKey);

        public void Reset(CampusCharacterData data)
        {
            CharacterId = data != null ? data.Id : string.Empty;
            Role = data != null ? data.Role : CampusCharacterRole.Student;
            ClassId = data != null ? data.ClassId : string.Empty;
            StudentClassroomId = string.Empty;
            StudentDeskKey = string.Empty;
            StudentDeskPosition = Vector3.zero;
            TeacherClassroomId = string.Empty;
            TeacherPodiumKey = string.Empty;
            TeacherPodiumPosition = Vector3.zero;
            OfficeRoomId = string.Empty;
            OfficeDeskKey = string.Empty;
            OfficeDeskPosition = Vector3.zero;
            WorkRoomId = string.Empty;
            PrimaryWorkstationKey = string.Empty;
            PrimaryWorkstationPosition = Vector3.zero;
            DormRoomId = string.Empty;
            DormPosition = Vector3.zero;
            CommonRoomId = string.Empty;
            CommonPosition = Vector3.zero;
        }

        public void SetStudentDesk(CampusGameplayRoom room, string key, Vector3 position)
        {
            StudentClassroomId = room != null ? room.RoomId : string.Empty;
            StudentDeskKey = key ?? string.Empty;
            StudentDeskPosition = position;
        }

        public void SetTeacherClassroom(CampusGameplayRoom room, string key, Vector3 position)
        {
            TeacherClassroomId = room != null ? room.RoomId : string.Empty;
            TeacherPodiumKey = key ?? string.Empty;
            TeacherPodiumPosition = position;
        }

        public void SetOfficeDesk(CampusGameplayRoom room, string key, Vector3 position)
        {
            OfficeRoomId = room != null ? room.RoomId : string.Empty;
            OfficeDeskKey = key ?? string.Empty;
            OfficeDeskPosition = position;
        }

        public void SetPrimaryWorkstation(CampusGameplayRoom room, string key, Vector3 position)
        {
            WorkRoomId = room != null ? room.RoomId : string.Empty;
            PrimaryWorkstationKey = key ?? string.Empty;
            PrimaryWorkstationPosition = position;
        }

        public void SetDorm(CampusGameplayRoom room, Vector3 position)
        {
            DormRoomId = room != null ? room.RoomId : string.Empty;
            DormPosition = position;
        }

        public void SetCommonRoom(CampusGameplayRoom room, Vector3 position)
        {
            CommonRoomId = room != null ? room.RoomId : string.Empty;
            CommonPosition = position;
        }
    }
}
