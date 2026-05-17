using System;
using System.Collections.Generic;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    [Serializable]
    public sealed class CampusCharacterData
    {
        [SerializeField] private string id = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private CampusCharacterRole role = CampusCharacterRole.Student;
        [SerializeField] private CampusTeacherDuty teacherDuty = CampusTeacherDuty.None;
        [SerializeField] private string classId = string.Empty;
        [SerializeField] private string currentRoomId = string.Empty;
        [SerializeField] private CampusCharacterState state = CampusCharacterState.Normal;
        [SerializeField] private bool isPlayerControlled;
        [SerializeField, Min(0)] private int sleepiness;
        [SerializeField, Min(0)] private int mischief;
        [SerializeField, Min(0)] private int studyTodayWorldLanguage;
        [SerializeField, Min(0)] private int studyTodayMath;
        [SerializeField, Min(0)] private int masteryWorldLanguage;
        [SerializeField, Min(0)] private int masteryMath;
        [SerializeField, Min(0)] private int warningCountToday;
        [SerializeField] private List<CampusCharacterTrait> traits = new List<CampusCharacterTrait>();
        [SerializeField] private List<string> memories = new List<string>();

        public string Id => id;
        public string DisplayName => displayName;
        public CampusCharacterRole Role => role;
        public CampusTeacherDuty TeacherDuty => teacherDuty;
        public string ClassId => classId;
        public string CurrentRoomId => currentRoomId;
        public CampusCharacterState State => state;
        public bool IsPlayerControlled => isPlayerControlled;
        public int Sleepiness => sleepiness;
        public int Mischief => mischief;
        public int StudyTodayWorldLanguage => studyTodayWorldLanguage;
        public int StudyTodayMath => studyTodayMath;
        public int MasteryWorldLanguage => masteryWorldLanguage;
        public int MasteryMath => masteryMath;
        public int WarningCountToday => warningCountToday;
        public IReadOnlyList<CampusCharacterTrait> Traits => traits;
        public IReadOnlyList<string> Memories => memories;

        public void Configure(
            string characterId,
            string characterName,
            CampusCharacterRole characterRole,
            CampusTeacherDuty duties,
            string characterClassId,
            CampusCharacterState characterState,
            bool playerControlled,
            int initialSleepiness,
            int initialMischief,
            IEnumerable<CampusCharacterTrait> characterTraits)
        {
            id = string.IsNullOrWhiteSpace(characterId) ? Guid.NewGuid().ToString("N") : characterId.Trim();
            displayName = string.IsNullOrWhiteSpace(characterName) ? id : characterName.Trim();
            role = characterRole;
            teacherDuty = duties;
            classId = string.IsNullOrWhiteSpace(characterClassId) ? string.Empty : characterClassId.Trim();
            state = characterState;
            isPlayerControlled = playerControlled;
            sleepiness = Mathf.Clamp(initialSleepiness, 0, 100);
            mischief = Mathf.Clamp(initialMischief, 0, 100);
            studyTodayWorldLanguage = 0;
            studyTodayMath = 0;
            masteryWorldLanguage = 0;
            masteryMath = 0;
            warningCountToday = 0;
            currentRoomId = string.Empty;
            traits = characterTraits != null ? new List<CampusCharacterTrait>(characterTraits) : new List<CampusCharacterTrait>();
            memories = new List<string>(5);
        }

        public void SetCurrentRoom(string roomId)
        {
            currentRoomId = string.IsNullOrWhiteSpace(roomId) ? string.Empty : roomId.Trim();
        }

        public void SetPlayerControlled(bool value)
        {
            isPlayerControlled = value;
        }

        public void SetState(CampusCharacterState nextState)
        {
            state = nextState;
        }

        public void SetSleepiness(int value)
        {
            sleepiness = Mathf.Clamp(value, 0, 100);
        }

        public void SetMischief(int value)
        {
            mischief = Mathf.Clamp(value, 0, 100);
        }

        public void AddStudyProgress(CampusSubjectType subjectType, int amount)
        {
            int clampedAmount = Mathf.Max(0, amount);
            switch (subjectType)
            {
                case CampusSubjectType.Math:
                    studyTodayMath += clampedAmount;
                    masteryMath = Mathf.Clamp(masteryMath + clampedAmount, 0, 999);
                    break;
                default:
                    studyTodayWorldLanguage += clampedAmount;
                    masteryWorldLanguage = Mathf.Clamp(masteryWorldLanguage + clampedAmount, 0, 999);
                    break;
            }
        }

        public void ClearDailyStudyProgress()
        {
            studyTodayWorldLanguage = 0;
            studyTodayMath = 0;
        }

        public void SetWarningCountToday(int value)
        {
            warningCountToday = Mathf.Max(0, value);
        }

        public void AddWarningCountToday(int delta)
        {
            warningCountToday = Mathf.Max(0, warningCountToday + delta);
        }

        public bool HasTrait(CampusCharacterTrait trait)
        {
            if (traits == null)
            {
                return false;
            }

            for (int i = 0; i < traits.Count; i++)
            {
                if (traits[i] == trait)
                {
                    return true;
                }
            }

            return false;
        }

        public void AddMemory(string memory)
        {
            if (string.IsNullOrWhiteSpace(memory))
            {
                return;
            }

            if (memories == null)
            {
                memories = new List<string>(5);
            }

            memories.Add(memory.Trim());
            const int maxMemoryCount = 5;
            if (memories.Count > maxMemoryCount)
            {
                memories.RemoveRange(0, memories.Count - maxMemoryCount);
            }
        }
    }
}
