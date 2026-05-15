using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    [DisallowMultipleComponent]
    public sealed class CampusRosterService : MonoBehaviour
    {
        private const string RuntimeRootName = "NtingCampus_CharacterRuntimeRoot";

        [Serializable]
        private sealed class CampusCharacterSeed
        {
            [SerializeField] private string id = string.Empty;
            [SerializeField] private string displayName = string.Empty;
            [SerializeField] private CampusCharacterRole role = CampusCharacterRole.Student;
            [SerializeField] private CampusTeacherDuty teacherDuty = CampusTeacherDuty.None;
            [SerializeField] private string classId = "class_1";
            [SerializeField] private bool isPlayerControlled;
            [SerializeField, Range(0, 100)] private int sleepiness = 40;
            [SerializeField, Range(0, 100)] private int mischief = 20;
            [SerializeField] private CampusCharacterTrait[] traits = Array.Empty<CampusCharacterTrait>();
            [SerializeField] private CampusRoomType anchorRoomType = CampusRoomType.Classroom;
            [SerializeField] private Vector3 localOffset = Vector3.zero;

            public bool IsPlayerControlled => isPlayerControlled;
            public CampusRoomType AnchorRoomType => anchorRoomType;
            public Vector3 LocalOffset => localOffset;
            public string DisplayName => displayName;

            public void Initialize(
                string targetId,
                string targetDisplayName,
                CampusCharacterRole targetRole,
                CampusTeacherDuty targetTeacherDuty,
                bool playerControlled,
                int initialSleepiness,
                int initialMischief,
                CampusRoomType targetAnchorRoomType,
                Vector3 targetLocalOffset,
                CampusCharacterTrait[] targetTraits)
            {
                id = targetId ?? string.Empty;
                displayName = targetDisplayName ?? string.Empty;
                role = targetRole;
                teacherDuty = targetTeacherDuty;
                classId = "class_1";
                isPlayerControlled = playerControlled;
                sleepiness = initialSleepiness;
                mischief = initialMischief;
                anchorRoomType = targetAnchorRoomType;
                localOffset = targetLocalOffset;
                traits = targetTraits ?? Array.Empty<CampusCharacterTrait>();
            }

            public CampusCharacterData BuildData()
            {
                CampusCharacterData data = new CampusCharacterData();
                data.Configure(
                    id,
                    displayName,
                    role,
                    teacherDuty,
                    classId,
                    CampusCharacterState.Normal,
                    isPlayerControlled,
                    sleepiness,
                    mischief,
                    traits);
                return data;
            }
        }

        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusWorldService worldService;
        [SerializeField] private Transform runtimeRoot;
        [SerializeField] private CampusCharacterRuntime playerRuntime;
        [SerializeField] private CampusPlayerCharacter playerCharacter;
        [SerializeField] private List<CampusCharacterRuntime> runtimes = new List<CampusCharacterRuntime>();
        [SerializeField] private List<CampusCharacterData> characters = new List<CampusCharacterData>();
        [SerializeField] private List<CampusCharacterSeed> initialSeeds = new List<CampusCharacterSeed>();

        private readonly Dictionary<string, CampusCharacterRuntime> runtimesById =
            new Dictionary<string, CampusCharacterRuntime>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<CampusCharacterData> Characters => characters;
        public IReadOnlyList<CampusCharacterRuntime> Runtimes => runtimes;
        public CampusCharacterRuntime PlayerRuntime => playerRuntime;
        public int StudentCount => CountByRole(CampusCharacterRole.Student);
        public int TeacherCount => CountByRole(CampusCharacterRole.Teacher);

        public void Initialize(CampusGameBootstrap targetBootstrap)
        {
            bootstrap = targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
            worldService = bootstrap != null ? bootstrap.WorldService : null;
            EnsureRuntimeRoot();
            EnsureDefaultSeeds();
            BuildFormalRoster();
        }

        public void RebuildRosterFromScene()
        {
            CollectSceneRuntimes();
            WriteInitializationLog();
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
            CampusCharacterRuntime runtime = FindRuntime(characterId);
            return runtime != null ? runtime.Data : null;
        }

        public IEnumerable<CampusCharacterRuntime> EnumerateByRole(CampusCharacterRole role)
        {
            for (int i = 0; i < runtimes.Count; i++)
            {
                CampusCharacterRuntime runtime = runtimes[i];
                if (runtime != null && runtime.Data != null && runtime.Data.Role == role)
                {
                    yield return runtime;
                }
            }
        }

        private void BuildFormalRoster()
        {
            ClearGeneratedNpcObjects();
            EnsurePlayerRuntimeFromScene();
            SpawnNonPlayerSeeds();
            CollectSceneRuntimes();
            WriteInitializationLog();
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

        private void EnsureDefaultSeeds()
        {
            if (initialSeeds != null && initialSeeds.Count > 0)
            {
                return;
            }

            initialSeeds = new List<CampusCharacterSeed>
            {
                CreateSeed("student_player", "LiXiaonao", CampusCharacterRole.Student, CampusTeacherDuty.None, true, 58, 78, CampusRoomType.Classroom, new Vector3(-0.5f, -0.4f, 0f), CampusCharacterTrait.Troublemaker),
                CreateSeed("student_good_01", "XuAnjing", CampusCharacterRole.Student, CampusTeacherDuty.None, false, 34, 12, CampusRoomType.Classroom, new Vector3(0.8f, -0.2f, 0f), CampusCharacterTrait.GoodStudent),
                CreateSeed("student_sleepy_01", "QiaoMianmian", CampusCharacterRole.Student, CampusTeacherDuty.None, false, 82, 10, CampusRoomType.Classroom, new Vector3(1.25f, -0.55f, 0f), CampusCharacterTrait.Sleepyhead),
                CreateSeed("student_tattletale_01", "SunXiaobao", CampusCharacterRole.Student, CampusTeacherDuty.None, false, 28, 18, CampusRoomType.Classroom, new Vector3(0.15f, -0.75f, 0f), CampusCharacterTrait.Tattletale),
                CreateSeed("student_ordinary_01", "ChenJiale", CampusCharacterRole.Student, CampusTeacherDuty.None, false, 46, 20, CampusRoomType.CommonActivityZone, new Vector3(-0.7f, 0.15f, 0f), CampusCharacterTrait.Ordinary),
                CreateSeed("teacher_world_homeroom", "LinYuwen", CampusCharacterRole.Teacher, CampusTeacherDuty.WorldLanguageTeacher | CampusTeacherDuty.HomeroomTeacher, false, 42, 0, CampusRoomType.Classroom, new Vector3(0f, 0.7f, 0f), CampusCharacterTrait.Ordinary),
                CreateSeed("teacher_math_patrol", "ZhaoShude", CampusCharacterRole.Teacher, CampusTeacherDuty.MathTeacher | CampusTeacherDuty.PatrolDirector, false, 35, 0, CampusRoomType.Office, new Vector3(0.25f, 0.3f, 0f), CampusCharacterTrait.Ordinary)
            };
        }

        private void EnsurePlayerRuntimeFromScene()
        {
            CampusCharacterSeed playerSeed = initialSeeds.Find(seed => seed != null && seed.IsPlayerControlled);
            NtingCampusMapEditor.CampusTestPlayerController playerController =
                FindFirstObjectByType<NtingCampusMapEditor.CampusTestPlayerController>(FindObjectsInactive.Include);

            if (playerController == null)
            {
                Debug.LogWarning("CampusRosterService could not find CampusTestPlayerController.");
                return;
            }

            GameObject playerObject = playerController.gameObject;
            playerRuntime = playerObject.GetComponent<CampusCharacterRuntime>();
            if (playerRuntime == null)
            {
                playerRuntime = playerObject.AddComponent<CampusCharacterRuntime>();
            }

            CampusCharacterData data = playerSeed != null ? playerSeed.BuildData() : BuildFallbackPlayerData();
            playerRuntime.Bind(data, false);

            playerCharacter = playerObject.GetComponent<CampusPlayerCharacter>();
            if (playerCharacter == null)
            {
                playerCharacter = playerObject.AddComponent<CampusPlayerCharacter>();
            }

            playerCharacter.Bind(playerRuntime);
        }

        private void SpawnNonPlayerSeeds()
        {
            if (initialSeeds == null || initialSeeds.Count == 0)
            {
                return;
            }

            Dictionary<string, int> roomSpawnCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < initialSeeds.Count; i++)
            {
                CampusCharacterSeed seed = initialSeeds[i];
                if (seed == null || seed.IsPlayerControlled)
                {
                    continue;
                }

                CampusGameplayRoom anchorRoom = worldService != null
                    ? worldService.FindFirstUsableRoom(seed.AnchorRoomType)
                    : null;
                string spawnGroupKey = anchorRoom != null ? anchorRoom.RoomId : seed.AnchorRoomType.ToString();
                roomSpawnCounts.TryGetValue(spawnGroupKey, out int spawnIndex);
                roomSpawnCounts[spawnGroupKey] = spawnIndex + 1;

                Vector3 spawnPosition = ResolveSpawnPosition(anchorRoom, seed.LocalOffset, spawnIndex);

                GameObject runtimeObject = new GameObject(seed.DisplayName);
                runtimeObject.transform.SetParent(runtimeRoot, false);
                runtimeObject.transform.position = spawnPosition;

                CampusCharacterRuntime runtime = runtimeObject.AddComponent<CampusCharacterRuntime>();
                CampusCharacterData data = seed.BuildData();
                if (anchorRoom != null)
                {
                    data.SetCurrentRoom(anchorRoom.RoomId);
                }

                runtime.Bind(data, true);
                EnsureNpcAgent(runtime);
            }
        }

        private void ClearGeneratedNpcObjects()
        {
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

        private void CollectSceneRuntimes()
        {
            characters.Clear();
            runtimes.Clear();
            runtimesById.Clear();
            playerRuntime = null;
            playerCharacter = null;

            CampusCharacterRuntime[] discoveredRuntimes =
                FindObjectsByType<CampusCharacterRuntime>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < discoveredRuntimes.Length; i++)
            {
                CampusCharacterRuntime runtime = discoveredRuntimes[i];
                if (runtime == null || runtime.Data == null)
                {
                    continue;
                }

                runtimes.Add(runtime);
                characters.Add(runtime.Data);

                if (!runtime.Data.IsPlayerControlled)
                {
                    EnsureNpcAgent(runtime);
                }

                if (!runtime.Data.IsPlayerControlled)
                {
                    continue;
                }

                playerRuntime = runtime;
                playerCharacter = runtime.GetComponent<CampusPlayerCharacter>();
                if (playerCharacter == null)
                {
                    playerCharacter = runtime.gameObject.AddComponent<CampusPlayerCharacter>();
                }

                playerCharacter.Bind(runtime);
            }

            RebuildRuntimeLookup();
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

            bootstrap.EventLog.AddLog("[System] Formal roster ready. Students=" + StudentCount + ", Teachers=" + TeacherCount + ".");
            if (playerRuntime != null && playerRuntime.Data != null)
            {
                bootstrap.EventLog.AddLog("[System] Player student bound to " + playerRuntime.Data.DisplayName + ".");
            }
        }

        private static CampusCharacterSeed CreateSeed(
            string id,
            string displayName,
            CampusCharacterRole role,
            CampusTeacherDuty duty,
            bool isPlayerControlled,
            int sleepiness,
            int mischief,
            CampusRoomType anchorRoomType,
            Vector3 localOffset,
            params CampusCharacterTrait[] traits)
        {
            CampusCharacterSeed seed = new CampusCharacterSeed();
            seed.Initialize(
                id,
                displayName,
                role,
                duty,
                isPlayerControlled,
                sleepiness,
                mischief,
                anchorRoomType,
                localOffset,
                traits);
            return seed;
        }

        private static CampusCharacterData BuildFallbackPlayerData()
        {
            CampusCharacterData data = new CampusCharacterData();
            data.Configure(
                "student_player",
                "LiXiaonao",
                CampusCharacterRole.Student,
                CampusTeacherDuty.None,
                "class_1",
                CampusCharacterState.Normal,
                true,
                58,
                78,
                new[] { CampusCharacterTrait.Troublemaker });
            return data;
        }

        private void EnsureNpcAgent(CampusCharacterRuntime runtime)
        {
            if (runtime == null || runtime.Data == null || runtime.Data.IsPlayerControlled)
            {
                return;
            }

            CampusNpcAgent agent = runtime.GetComponent<CampusNpcAgent>();
            if (agent == null)
            {
                agent = runtime.gameObject.AddComponent<CampusNpcAgent>();
            }

            agent.Initialize(runtime, bootstrap, worldService);
        }

        private static Vector3 ResolveSpawnPosition(CampusGameplayRoom anchorRoom, Vector3 localOffset, int spawnIndex)
        {
            if (anchorRoom == null)
            {
                return localOffset + ResolveSpreadOffset(spawnIndex);
            }

            return anchorRoom.WorldCenter + localOffset + ResolveSpreadOffset(spawnIndex);
        }

        private static Vector3 ResolveSpreadOffset(int spawnIndex)
        {
            int column = spawnIndex % 3;
            int row = spawnIndex / 3;
            float x = (column - 1) * 0.95f;
            float y = row == 0 ? 0f : -row * 0.72f;
            return new Vector3(x, y, 0f);
        }
    }
}
