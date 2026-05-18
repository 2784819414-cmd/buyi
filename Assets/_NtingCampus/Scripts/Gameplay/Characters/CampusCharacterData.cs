using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.UI;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    [Serializable]
    public sealed class CampusCharacterData
    {
        [SerializeField] private string id = string.Empty;
        [SerializeField] private string legacyDisplayName = string.Empty;
        [SerializeField] private CampusLocalizedText localizedDisplayName;
        [SerializeField] private CampusCharacterRole role = CampusCharacterRole.Student;
        [SerializeField] private CampusTeacherDuty teacherDuty = CampusTeacherDuty.None;
        [SerializeField] private CampusStaffDuty staffDuty = CampusStaffDuty.None;
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
        [SerializeField] private bool ecologyInitialized;
        [SerializeField, Range(0, 100)] private int mood = 50;
        [SerializeField, Range(0, 100)] private int socialEnergy = 50;
        [SerializeField] private List<CampusCharacterRelationship> relationships = new List<CampusCharacterRelationship>();
        [SerializeField] private List<CampusCharacterPossession> possessions = new List<CampusCharacterPossession>();
        [SerializeField] private List<CampusCharacterTrait> traits = new List<CampusCharacterTrait>();
        [SerializeField] private List<CampusCharacterMemoryId> memories = new List<CampusCharacterMemoryId>();
        [SerializeField] private CampusCharacterAssignmentData assignments = new CampusCharacterAssignmentData();

        public string Id => id;
        public string DisplayName => GetDisplayName(CampusLanguageState.CurrentLanguage);
        public CampusLocalizedText LocalizedDisplayName => localizedDisplayName;
        public CampusCharacterRole Role => role;
        public CampusTeacherDuty TeacherDuty => teacherDuty;
        public CampusStaffDuty StaffDuty => staffDuty;
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
        public int Mood => mood;
        public int SocialEnergy => socialEnergy;
        public IReadOnlyList<CampusCharacterRelationship> Relationships => relationships;
        public IReadOnlyList<CampusCharacterPossession> Possessions => possessions;
        public IReadOnlyList<CampusCharacterTrait> Traits => traits;
        public IReadOnlyList<CampusCharacterMemoryId> Memories => memories;
        public CampusCharacterAssignmentData Assignments => assignments ?? (assignments = new CampusCharacterAssignmentData());

        public void Configure(
            string characterId,
            string legacyCharacterName,
            CampusLocalizedText characterName,
            CampusCharacterRole characterRole,
            CampusTeacherDuty duties,
            string characterClassId,
            CampusCharacterState characterState,
            bool playerControlled,
            int initialSleepiness,
            int initialMischief,
            IEnumerable<CampusCharacterTrait> characterTraits,
            CampusStaffDuty staffDuties = CampusStaffDuty.None)
        {
            id = string.IsNullOrWhiteSpace(characterId) ? Guid.NewGuid().ToString("N") : characterId.Trim();
            legacyDisplayName = string.IsNullOrWhiteSpace(legacyCharacterName) ? string.Empty : legacyCharacterName.Trim();
            localizedDisplayName = characterName;
            role = characterRole;
            teacherDuty = duties;
            staffDuty = staffDuties;
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
            possessions = new List<CampusCharacterPossession>(4);
            memories = new List<CampusCharacterMemoryId>(5);
            assignments = new CampusCharacterAssignmentData();
            ResetEcologyFromTraits();
        }

        public void SetAssignments(CampusCharacterAssignmentData source)
        {
            assignments = source != null
                ? source.Clone()
                : new CampusCharacterAssignmentData();
            assignments.Normalize();
        }

        public void SyncAssignmentsFromProfile(CampusNpcPersonalProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            CampusCharacterAssignmentData target = Assignments;
            target.StudentClassroomId = UseResolvedOrPreserve(profile.StudentClassroomId, target.StudentClassroomId);
            target.StudentDeskId = UseResolvedOrPreserve(profile.StudentDeskKey, target.StudentDeskId);
            target.TeacherClassroomId = UseResolvedOrPreserve(profile.TeacherClassroomId, target.TeacherClassroomId);
            target.TeacherPodiumId = UseResolvedOrPreserve(profile.TeacherPodiumKey, target.TeacherPodiumId);
            target.OfficeRoomId = UseResolvedOrPreserve(profile.OfficeRoomId, target.OfficeRoomId);
            target.OfficeDeskId = UseResolvedOrPreserve(profile.OfficeDeskKey, target.OfficeDeskId);
            target.WorkRoomId = UseResolvedOrPreserve(profile.WorkRoomId, target.WorkRoomId);
            target.PrimaryWorkstationId = UseResolvedOrPreserve(profile.PrimaryWorkstationKey, target.PrimaryWorkstationId);
            target.DeliveryRoomId = UseResolvedOrPreserve(profile.DeliveryRoomId, target.DeliveryRoomId);
            target.DeliveryPointId = UseResolvedOrPreserve(profile.DeliveryPointKey, target.DeliveryPointId);
            target.Normalize();
        }

        private static string UseResolvedOrPreserve(string resolved, string current)
        {
            return !string.IsNullOrWhiteSpace(resolved)
                ? resolved.Trim()
                : string.IsNullOrWhiteSpace(current)
                    ? string.Empty
                    : current.Trim();
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

        public void EnsureEcologyInitialized()
        {
            if (!ecologyInitialized)
            {
                ResetEcologyFromTraits();
            }
        }

        public void ResetEcologyFromTraits()
        {
            ecologyInitialized = true;
            mood = 50;
            socialEnergy = 50;
            relationships = new List<CampusCharacterRelationship>();

            if (HasTrait(CampusCharacterTrait.Sleepyhead))
            {
                socialEnergy -= 15;
                mood -= 4;
            }

            if (HasTrait(CampusCharacterTrait.Troublemaker))
            {
                socialEnergy += 16;
            }

            if (HasTrait(CampusCharacterTrait.GoodStudent))
            {
                socialEnergy -= 4;
            }

            if (HasTrait(CampusCharacterTrait.Tattletale))
            {
                socialEnergy += 6;
            }

            if (HasTrait(CampusCharacterTrait.SecretDeliveryBuyer))
            {
                socialEnergy += 5;
            }

            mood = ClampEcologyStat(mood);
            socialEnergy = ClampEcologyStat(socialEnergy);
        }

        public void SetMood(int value)
        {
            EnsureEcologyInitialized();
            mood = ClampEcologyStat(value);
        }

        public void AddMood(int delta)
        {
            SetMood(mood + delta);
        }

        public int GetRelationshipTrust(string targetId)
        {
            CampusCharacterRelationship relationship = FindRelationship(targetId);
            return relationship != null ? relationship.Trust : ResolveTraitTrustBaseline();
        }

        public int GetRelationshipSuspicion(string targetId)
        {
            CampusCharacterRelationship relationship = FindRelationship(targetId);
            return relationship != null ? relationship.Suspicion : ResolveTraitSuspicionBaseline();
        }

        public void SetRelationshipTrust(string targetId, int value)
        {
            EnsureEcologyInitialized();
            CampusCharacterRelationship relationship = GetOrCreateRelationship(targetId);
            if (relationship != null)
            {
                relationship.SetTrust(value);
            }
        }

        public void AddRelationshipTrust(string targetId, int delta)
        {
            SetRelationshipTrust(targetId, GetRelationshipTrust(targetId) + delta);
        }

        public void SetRelationshipSuspicion(string targetId, int value)
        {
            EnsureEcologyInitialized();
            CampusCharacterRelationship relationship = GetOrCreateRelationship(targetId);
            if (relationship != null)
            {
                relationship.SetSuspicion(value);
            }
        }

        public void AddRelationshipSuspicion(string targetId, int delta)
        {
            SetRelationshipSuspicion(targetId, GetRelationshipSuspicion(targetId) + delta);
        }

        public void SetSocialEnergy(int value)
        {
            EnsureEcologyInitialized();
            socialEnergy = ClampEcologyStat(value);
        }

        public void AddSocialEnergy(int delta)
        {
            SetSocialEnergy(socialEnergy + delta);
        }

        public void ApplyDailyEcologyRecovery()
        {
            EnsureEcologyInitialized();
            mood = MoveToward(mood, 50, 10);
            socialEnergy = MoveToward(socialEnergy, ResolveTraitSocialEnergyBaseline(), 18);
            int trustBaseline = ResolveTraitTrustBaseline();
            int suspicionBaseline = ResolveTraitSuspicionBaseline();
            if (relationships == null)
            {
                return;
            }

            for (int i = relationships.Count - 1; i >= 0; i--)
            {
                CampusCharacterRelationship relationship = relationships[i];
                if (relationship == null || string.IsNullOrWhiteSpace(relationship.TargetId))
                {
                    relationships.RemoveAt(i);
                    continue;
                }

                relationship.SetTrust(MoveToward(relationship.Trust, trustBaseline, 3));
                relationship.SetSuspicion(MoveToward(relationship.Suspicion, suspicionBaseline, 14));
            }
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

        public string GetDisplayName(CampusDisplayLanguage language)
        {
            return localizedDisplayName.Get(language, legacyDisplayName, id);
        }

        public string GetPreferredObjectName()
        {
            return localizedDisplayName.ResolvePrimary(legacyDisplayName, id);
        }

        public void AddMemory(CampusCharacterMemoryId memory)
        {
            if (memory == CampusCharacterMemoryId.None)
            {
                return;
            }

            if (memories == null)
            {
                memories = new List<CampusCharacterMemoryId>(5);
            }

            memories.Add(memory);
            const int maxMemoryCount = 5;
            if (memories.Count > maxMemoryCount)
            {
                memories.RemoveRange(0, memories.Count - maxMemoryCount);
            }
        }

        public bool HasMemory(CampusCharacterMemoryId memory)
        {
            if (memory == CampusCharacterMemoryId.None || memories == null)
            {
                return false;
            }

            for (int i = 0; i < memories.Count; i++)
            {
                if (memories[i] == memory)
                {
                    return true;
                }
            }

            return false;
        }

        public void AddPossession(string itemId, string displayName, string source, int acquiredDay)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return;
            }

            possessions = possessions ?? new List<CampusCharacterPossession>(4);
            CampusCharacterPossession possession = new CampusCharacterPossession();
            possession.Configure(itemId, displayName, source, acquiredDay);
            possessions.Add(possession);
            const int maxPossessionCount = 8;
            if (possessions.Count > maxPossessionCount)
            {
                possessions.RemoveRange(0, possessions.Count - maxPossessionCount);
            }
        }

        public bool HasPossession(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId) || possessions == null)
            {
                return false;
            }

            string normalizedItemId = itemId.Trim();
            for (int i = 0; i < possessions.Count; i++)
            {
                CampusCharacterPossession possession = possessions[i];
                if (possession != null &&
                    string.Equals(possession.ItemId, normalizedItemId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private CampusCharacterRelationship GetOrCreateRelationship(string targetId)
        {
            string normalizedTargetId = NormalizeRelationshipTargetId(targetId);
            if (string.IsNullOrEmpty(normalizedTargetId))
            {
                return null;
            }

            relationships = relationships ?? new List<CampusCharacterRelationship>();
            CampusCharacterRelationship relationship = FindRelationship(normalizedTargetId);
            if (relationship != null)
            {
                return relationship;
            }

            relationship = new CampusCharacterRelationship();
            relationship.Configure(normalizedTargetId, ResolveTraitTrustBaseline(), ResolveTraitSuspicionBaseline());
            relationships.Add(relationship);
            return relationship;
        }

        private CampusCharacterRelationship FindRelationship(string targetId)
        {
            string normalizedTargetId = NormalizeRelationshipTargetId(targetId);
            if (string.IsNullOrEmpty(normalizedTargetId) || relationships == null)
            {
                return null;
            }

            for (int i = 0; i < relationships.Count; i++)
            {
                CampusCharacterRelationship relationship = relationships[i];
                if (relationship != null &&
                    string.Equals(relationship.TargetId, normalizedTargetId, StringComparison.OrdinalIgnoreCase))
                {
                    return relationship;
                }
            }

            return null;
        }

        private int ResolveTraitTrustBaseline()
        {
            int baseline = 50;
            if (HasTrait(CampusCharacterTrait.Troublemaker))
            {
                baseline += 8;
            }

            if (HasTrait(CampusCharacterTrait.GoodStudent))
            {
                baseline -= 2;
            }

            if (HasTrait(CampusCharacterTrait.SecretDeliveryBuyer))
            {
                baseline += 5;
            }

            return ClampEcologyStat(baseline);
        }

        private int ResolveTraitSuspicionBaseline()
        {
            int baseline = role == CampusCharacterRole.Teacher ? 18 : role == CampusCharacterRole.Staff ? 14 : 0;
            if (HasTrait(CampusCharacterTrait.Tattletale))
            {
                baseline += 12;
            }

            return ClampEcologyStat(baseline);
        }

        private int ResolveTraitSocialEnergyBaseline()
        {
            int baseline = 50;
            if (HasTrait(CampusCharacterTrait.Sleepyhead))
            {
                baseline -= 15;
            }

            if (HasTrait(CampusCharacterTrait.Troublemaker))
            {
                baseline += 16;
            }

            if (HasTrait(CampusCharacterTrait.GoodStudent))
            {
                baseline -= 4;
            }

            if (HasTrait(CampusCharacterTrait.Tattletale))
            {
                baseline += 6;
            }

            if (HasTrait(CampusCharacterTrait.SecretDeliveryBuyer))
            {
                baseline += 5;
            }

            return ClampEcologyStat(baseline);
        }

        private static int ClampEcologyStat(int value)
        {
            return Mathf.Clamp(value, 0, 100);
        }

        private static int MoveToward(int current, int target, int maxDelta)
        {
            if (current < target)
            {
                return Mathf.Min(target, current + Mathf.Max(0, maxDelta));
            }

            if (current > target)
            {
                return Mathf.Max(target, current - Mathf.Max(0, maxDelta));
            }

            return current;
        }

        private static string NormalizeRelationshipTargetId(string targetId)
        {
            return string.IsNullOrWhiteSpace(targetId) ? string.Empty : targetId.Trim();
        }
    }

    [Serializable]
    public sealed class CampusCharacterAssignmentData
    {
        public string StudentClassroomId = string.Empty;
        public string StudentDeskId = string.Empty;
        public string TeacherClassroomId = string.Empty;
        public string TeacherPodiumId = string.Empty;
        public string OfficeRoomId = string.Empty;
        public string OfficeDeskId = string.Empty;
        public string WorkRoomId = string.Empty;
        public string PrimaryWorkstationId = string.Empty;
        public string DeliveryRoomId = string.Empty;
        public string DeliveryPointId = string.Empty;

        public bool HasAny()
        {
            Normalize();
            return !string.IsNullOrEmpty(StudentClassroomId) ||
                   !string.IsNullOrEmpty(StudentDeskId) ||
                   !string.IsNullOrEmpty(TeacherClassroomId) ||
                   !string.IsNullOrEmpty(TeacherPodiumId) ||
                   !string.IsNullOrEmpty(OfficeRoomId) ||
                   !string.IsNullOrEmpty(OfficeDeskId) ||
                   !string.IsNullOrEmpty(WorkRoomId) ||
                   !string.IsNullOrEmpty(PrimaryWorkstationId) ||
                   !string.IsNullOrEmpty(DeliveryRoomId) ||
                   !string.IsNullOrEmpty(DeliveryPointId);
        }

        public CampusCharacterAssignmentData Clone()
        {
            CampusCharacterAssignmentData clone = new CampusCharacterAssignmentData
            {
                StudentClassroomId = StudentClassroomId,
                StudentDeskId = StudentDeskId,
                TeacherClassroomId = TeacherClassroomId,
                TeacherPodiumId = TeacherPodiumId,
                OfficeRoomId = OfficeRoomId,
                OfficeDeskId = OfficeDeskId,
                WorkRoomId = WorkRoomId,
                PrimaryWorkstationId = PrimaryWorkstationId,
                DeliveryRoomId = DeliveryRoomId,
                DeliveryPointId = DeliveryPointId
            };
            clone.Normalize();
            return clone;
        }

        public void Normalize()
        {
            StudentClassroomId = NormalizeId(StudentClassroomId);
            StudentDeskId = NormalizeId(StudentDeskId);
            TeacherClassroomId = NormalizeId(TeacherClassroomId);
            TeacherPodiumId = NormalizeId(TeacherPodiumId);
            OfficeRoomId = NormalizeId(OfficeRoomId);
            OfficeDeskId = NormalizeId(OfficeDeskId);
            WorkRoomId = NormalizeId(WorkRoomId);
            PrimaryWorkstationId = NormalizeId(PrimaryWorkstationId);
            DeliveryRoomId = NormalizeId(DeliveryRoomId);
            DeliveryPointId = NormalizeId(DeliveryPointId);
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    [Serializable]
    public sealed class CampusCharacterRelationship
    {
        [SerializeField] private string targetId = string.Empty;
        [SerializeField, Range(0, 100)] private int trust = 50;
        [SerializeField, Range(0, 100)] private int suspicion;

        public string TargetId => targetId;
        public int Trust => trust;
        public int Suspicion => suspicion;

        public void Configure(string relationshipTargetId, int initialTrust, int initialSuspicion)
        {
            targetId = string.IsNullOrWhiteSpace(relationshipTargetId) ? string.Empty : relationshipTargetId.Trim();
            trust = Clamp(initialTrust);
            suspicion = Clamp(initialSuspicion);
        }

        public void SetTrust(int value)
        {
            trust = Clamp(value);
        }

        public void SetSuspicion(int value)
        {
            suspicion = Clamp(value);
        }

        private static int Clamp(int value)
        {
            return Mathf.Clamp(value, 0, 100);
        }
    }

    [Serializable]
    public sealed class CampusCharacterPossession
    {
        [SerializeField] private string itemId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private string source = string.Empty;
        [SerializeField, Min(0)] private int acquiredDay;

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public string Source => source;
        public int AcquiredDay => acquiredDay;

        public void Configure(string possessionItemId, string possessionDisplayName, string possessionSource, int day)
        {
            itemId = string.IsNullOrWhiteSpace(possessionItemId) ? string.Empty : possessionItemId.Trim();
            displayName = string.IsNullOrWhiteSpace(possessionDisplayName) ? itemId : possessionDisplayName.Trim();
            source = string.IsNullOrWhiteSpace(possessionSource) ? string.Empty : possessionSource.Trim();
            acquiredDay = Mathf.Max(0, day);
        }
    }
}
