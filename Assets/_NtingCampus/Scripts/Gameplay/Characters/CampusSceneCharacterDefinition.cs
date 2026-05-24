using System;
using NtingCampus.UI.Runtime.Gameplay;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CampusCharacterRuntime))]
    public sealed class CampusSceneCharacterDefinition : MonoBehaviour
    {
        [SerializeField] private string characterId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private CampusLocalizedText localizedDisplayName = default;
        [SerializeField] private CampusCharacterRole role = CampusCharacterRole.Student;
        [SerializeField] private CampusTeacherDuty teacherDuty = CampusTeacherDuty.None;
        [SerializeField] private CampusStaffDuty staffDuty = CampusStaffDuty.None;
        [SerializeField] private string classId = "class_1";
        [SerializeField] private CampusCharacterState initialState = CampusCharacterState.Normal;
        [SerializeField] private bool isPlayerControlled = false;
        [SerializeField, Min(1)] private int floorIndex = 1;
        [SerializeField, Range(0, 100)] private int sleepiness = 40;
        [SerializeField, Range(0, 100)] private int mischief = 20;
        [SerializeField] private int initialMoney = NtingCampus.Gameplay.Economy.CampusCharacterEconomyDefaults.UseRoleDefaultMoney;
        [SerializeField] private CampusCharacterTrait[] traits = Array.Empty<CampusCharacterTrait>();
        [SerializeField] private CampusCharacterAssignmentData assignments = new CampusCharacterAssignmentData();

        public int FloorIndex => Mathf.Max(1, floorIndex);

        public CampusCharacterData BuildData()
        {
            CampusCharacterData data = new CampusCharacterData();
            data.Configure(
                characterId,
                displayName,
                localizedDisplayName,
                role,
                teacherDuty,
                classId,
                initialState,
                isPlayerControlled,
                sleepiness,
                mischief,
                traits,
                staffDuty,
                initialMoney);
            data.SetAssignments(assignments);
            return data;
        }

        private void OnValidate()
        {
            floorIndex = Mathf.Max(1, floorIndex);
            sleepiness = Mathf.Clamp(sleepiness, 0, 100);
            mischief = Mathf.Clamp(mischief, 0, 100);
            initialMoney = Mathf.Max(NtingCampus.Gameplay.Economy.CampusCharacterEconomyDefaults.UseRoleDefaultMoney, initialMoney);
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = gameObject.name;
            }
        }
    }
}

