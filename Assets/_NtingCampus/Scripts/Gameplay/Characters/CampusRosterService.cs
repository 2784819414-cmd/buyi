using System.Collections.Generic;
using NtingCampus.Gameplay.Core;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    [DisallowMultipleComponent]
    public sealed class CampusRosterService : MonoBehaviour
    {
        private const string RuntimeRootName = "NtingCampus_CharacterRuntimeRoot";
        private const string DefaultClassId = "class_1";

        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private Transform runtimeRoot;
        [SerializeField] private CampusCharacterRuntime playerRuntime;
        [SerializeField] private CampusPlayerCharacter playerCharacter;
        [SerializeField] private List<CampusCharacterRuntime> runtimes = new List<CampusCharacterRuntime>();
        [SerializeField] private List<CampusCharacterData> characters = new List<CampusCharacterData>();

        private readonly Dictionary<string, CampusCharacterRuntime> runtimesById =
            new Dictionary<string, CampusCharacterRuntime>(System.StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<CampusCharacterData> Characters => characters;
        public IReadOnlyList<CampusCharacterRuntime> Runtimes => runtimes;
        public CampusCharacterRuntime PlayerRuntime => playerRuntime;
        public int StudentCount => CountByRole(CampusCharacterRole.Student);
        public int TeacherCount => CountByRole(CampusCharacterRole.Teacher);

        public void Initialize(CampusGameBootstrap targetBootstrap)
        {
            bootstrap = targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
            EnsureRuntimeRoot();
            RebuildRoster();
        }

        public CampusCharacterRuntime FindRuntime(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId))
            {
                return null;
            }

            RebuildRuntimeLookup();
            runtimesById.TryGetValue(characterId.Trim(), out CampusCharacterRuntime runtime);
            return runtime;
        }

        public CampusCharacterData FindCharacterData(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId))
            {
                return null;
            }

            string normalizedId = characterId.Trim();
            for (int i = 0; i < characters.Count; i++)
            {
                CampusCharacterData candidate = characters[i];
                if (candidate != null && string.Equals(candidate.Id, normalizedId, System.StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        private void EnsureRuntimeRoot()
        {
            if (runtimeRoot != null)
            {
                return;
            }

            Transform existing = transform.Find(RuntimeRootName);
            if (existing != null)
            {
                runtimeRoot = existing;
                return;
            }

            GameObject root = new GameObject(RuntimeRootName);
            root.transform.SetParent(transform, false);
            runtimeRoot = root.transform;
        }

        private void RebuildRoster()
        {
            ClearGeneratedRoster();

            CampusTestPlayerController playerController = FindFirstObjectByType<CampusTestPlayerController>(FindObjectsInactive.Include);
            if (playerController == null)
            {
                Debug.LogWarning("CampusRosterService could not find CampusTestPlayerController.");
                return;
            }

            List<CampusCharacterData> seeds = BuildInitialRoster();
            Vector3 origin = playerController.transform.position;
            Vector3[] npcOffsets =
            {
                new Vector3(-1.5f, 1.25f, 0f),
                new Vector3(1.5f, 1.25f, 0f),
                new Vector3(-2.25f, -0.25f, 0f),
                new Vector3(2.25f, -0.25f, 0f),
                new Vector3(-1.2f, -1.65f, 0f),
                new Vector3(1.2f, -1.65f, 0f),
                new Vector3(0f, 2.1f, 0f)
            };

            for (int i = 0; i < seeds.Count; i++)
            {
                CampusCharacterData data = seeds[i];
                characters.Add(data);

                if (data.IsPlayerControlled)
                {
                    playerRuntime = playerController.GetComponent<CampusCharacterRuntime>();
                    if (playerRuntime == null)
                    {
                        playerRuntime = playerController.gameObject.AddComponent<CampusCharacterRuntime>();
                    }

                    playerRuntime.Bind(data, false);

                    playerCharacter = playerController.GetComponent<CampusPlayerCharacter>();
                    if (playerCharacter == null)
                    {
                        playerCharacter = playerController.gameObject.AddComponent<CampusPlayerCharacter>();
                    }

                    playerCharacter.Bind(playerRuntime);
                    runtimes.Add(playerRuntime);
                    continue;
                }

                GameObject runtimeObject = new GameObject(data.DisplayName);
                runtimeObject.transform.SetParent(runtimeRoot, false);
                runtimeObject.transform.position = origin + npcOffsets[Mathf.Min(i - 1, npcOffsets.Length - 1)];

                CampusCharacterRuntime runtime = runtimeObject.AddComponent<CampusCharacterRuntime>();
                runtime.Bind(data, true);
                runtimes.Add(runtime);
            }

            RebuildRuntimeLookup();
            WriteInitializationLog();
        }

        private void ClearGeneratedRoster()
        {
            characters.Clear();
            runtimes.Clear();
            runtimesById.Clear();
            playerRuntime = null;
            playerCharacter = null;

            EnsureRuntimeRoot();
            for (int i = runtimeRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = runtimeRoot.GetChild(i);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private List<CampusCharacterData> BuildInitialRoster()
        {
            return new List<CampusCharacterData>
            {
                CreateStudent("student_player", "李小闹", true, 58, 78, CampusCharacterTrait.Troublemaker),
                CreateStudent("student_ordinary_01", "周晓白", false, 38, 24, CampusCharacterTrait.Ordinary),
                CreateStudent("student_ordinary_02", "陈一鸣", false, 42, 27, CampusCharacterTrait.Ordinary),
                CreateStudent("student_sleepy_01", "孙困困", false, 82, 18, CampusCharacterTrait.Sleepyhead),
                CreateStudent("student_good_01", "许安静", false, 34, 12, CampusCharacterTrait.GoodStudent),
                CreateStudent("student_tattle_01", "赵小喇叭", false, 47, 31, CampusCharacterTrait.Tattletale),
                CreateTeacher("teacher_world_homeroom", "林语文", CampusTeacherDuty.WorldLanguageTeacher | CampusTeacherDuty.HomeroomTeacher, 48),
                CreateTeacher("teacher_math_patrol", "高巡导", CampusTeacherDuty.MathTeacher | CampusTeacherDuty.PatrolDirector, 36)
            };
        }

        private static CampusCharacterData CreateStudent(
            string id,
            string displayName,
            bool isPlayerControlled,
            int sleepiness,
            int mischief,
            CampusCharacterTrait primaryTrait)
        {
            CampusCharacterData data = new CampusCharacterData();
            data.Configure(
                id,
                displayName,
                CampusCharacterRole.Student,
                CampusTeacherDuty.None,
                DefaultClassId,
                CampusCharacterState.Normal,
                isPlayerControlled,
                sleepiness,
                mischief,
                new[] { primaryTrait });
            return data;
        }

        private static CampusCharacterData CreateTeacher(
            string id,
            string displayName,
            CampusTeacherDuty duties,
            int sleepiness)
        {
            CampusCharacterData data = new CampusCharacterData();
            data.Configure(
                id,
                displayName,
                CampusCharacterRole.Teacher,
                duties,
                DefaultClassId,
                CampusCharacterState.Normal,
                false,
                sleepiness,
                0,
                new[] { CampusCharacterTrait.Ordinary });
            return data;
        }

        private void RebuildRuntimeLookup()
        {
            runtimesById.Clear();
            for (int i = 0; i < runtimes.Count; i++)
            {
                CampusCharacterRuntime runtime = runtimes[i];
                if (runtime == null || runtime.Data == null || string.IsNullOrWhiteSpace(runtime.CharacterId))
                {
                    continue;
                }

                runtimesById[runtime.CharacterId] = runtime;
            }
        }

        private int CountByRole(CampusCharacterRole role)
        {
            int count = 0;
            for (int i = 0; i < characters.Count; i++)
            {
                CampusCharacterData data = characters[i];
                if (data != null && data.Role == role)
                {
                    count++;
                }
            }

            return count;
        }

        private void WriteInitializationLog()
        {
            if (bootstrap == null || bootstrap.EventLog == null)
            {
                return;
            }

            bootstrap.EventLog.AddLog("[System] Roster ready. Students=" + StudentCount + ", Teachers=" + TeacherCount + ".");
            if (playerRuntime != null && playerRuntime.Data != null)
            {
                bootstrap.EventLog.AddLog("[System] Player student bound to " + playerRuntime.Data.DisplayName + ".");
            }
        }
    }
}
