using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.UI;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    [DisallowMultipleComponent]
    public sealed class CampusRosterService : MonoBehaviour
    {
        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusWorldService worldService;
        [SerializeField] private CampusCharacterRuntime playerRuntime;
        [SerializeField] private CampusPlayerCharacter playerCharacter;
        [SerializeField] private List<CampusCharacterRuntime> runtimes = new List<CampusCharacterRuntime>();
        [SerializeField] private List<CampusCharacterData> characters = new List<CampusCharacterData>();

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
            RebuildRosterFromScene();
        }

        public void RebuildRosterFromScene()
        {
            CollectSceneRuntimes();
            RebuildRuntimeLookup();
            SyncCurrentRoomsFromWorld();
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

        public bool TrySwitchPlayerCharacter(string characterId)
        {
            CampusCharacterRuntime targetRuntime = FindRuntime(characterId);
            return TrySwitchPlayerCharacter(targetRuntime);
        }

        public bool TrySwitchPlayerCharacter(CampusCharacterRuntime targetRuntime)
        {
            if (targetRuntime == null || targetRuntime.Data == null)
            {
                return false;
            }

            if (playerRuntime == targetRuntime)
            {
                return true;
            }

            CampusCharacterRuntime previousPlayer = playerRuntime;
            if (previousPlayer != null && previousPlayer.Data != null)
            {
                previousPlayer.Data.SetPlayerControlled(false);
                EnsureNpcAgent(previousPlayer);
                SetGameplayInput(previousPlayer, false);
            }

            playerRuntime = targetRuntime;
            playerRuntime.Data.SetPlayerControlled(true);
            EnsurePlayerActorStack(playerRuntime);

            CampusNpcAgent promotedAgent = playerRuntime.GetComponent<CampusNpcAgent>();
            if (promotedAgent != null)
            {
                Destroy(promotedAgent);
            }

            playerCharacter = playerRuntime.GetComponent<CampusPlayerCharacter>();
            if (playerCharacter == null)
            {
                playerCharacter = playerRuntime.gameObject.AddComponent<CampusPlayerCharacter>();
            }

            playerCharacter.Bind(playerRuntime);
            SetGameplayInput(playerRuntime, true);

            if (bootstrap != null && bootstrap.EventLog != null)
            {
                bootstrap.EventLog.AddLog(CampusCharacterTextCatalog.FormatPlayerControlSwitched(
                    CampusLanguageState.CurrentLanguage,
                    playerRuntime.Data.GetDisplayName(CampusLanguageState.CurrentLanguage)));
            }

            return true;
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

        private void CollectSceneRuntimes()
        {
            characters.Clear();
            runtimes.Clear();
            runtimesById.Clear();
            playerRuntime = null;
            playerCharacter = null;

            CampusRuntimeGameplayOverlayLoader overlayLoader = CampusRuntimeGameplayOverlayLoader.Instance;
            bool restrictToOverlayActors = overlayLoader != null && overlayLoader.UseRuntimeOverlayOnly;
            HashSet<CampusCharacterRuntime> processedRuntimes = new HashSet<CampusCharacterRuntime>();
            if (!restrictToOverlayActors)
            {
                CampusSceneCharacterDefinition[] definitions = FindObjectsByType<CampusSceneCharacterDefinition>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

                for (int i = 0; i < definitions.Length; i++)
                {
                    CampusSceneCharacterDefinition definition = definitions[i];
                    if (definition == null)
                    {
                        continue;
                    }

                    CampusCharacterRuntime runtime = definition.GetComponent<CampusCharacterRuntime>();
                    if (runtime == null)
                    {
                        runtime = definition.gameObject.AddComponent<CampusCharacterRuntime>();
                    }

                    runtime.Bind(definition.BuildData(), renameGameObject: false);
                    RegisterRuntime(runtime, processedRuntimes);
                }
            }

            CampusCharacterRuntime[] discoveredRuntimes =
                FindObjectsByType<CampusCharacterRuntime>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < discoveredRuntimes.Length; i++)
            {
                CampusCharacterRuntime runtime = discoveredRuntimes[i];
                if (overlayLoader != null && !overlayLoader.ShouldIncludeActorRuntime(runtime))
                {
                    continue;
                }

                RegisterRuntime(runtime, processedRuntimes);
            }

            if (playerRuntime == null)
            {
                Debug.LogWarning("CampusRosterService did not find any player-controlled scene character.");
            }
        }

        private void RegisterRuntime(CampusCharacterRuntime runtime, HashSet<CampusCharacterRuntime> processedRuntimes)
        {
            if (runtime == null || runtime.Data == null || !processedRuntimes.Add(runtime))
            {
                return;
            }

            runtimes.Add(runtime);
            characters.Add(runtime.Data);
            EnsureBodyController(runtime);

            if (runtime.Data.IsPlayerControlled)
            {
                BindPlayerRuntime(runtime);
                return;
            }

            EnsureNpcAgent(runtime);
        }

        private void BindPlayerRuntime(CampusCharacterRuntime runtime)
        {
            if (playerRuntime != null && playerRuntime != runtime)
            {
                Debug.LogWarning(
                    "CampusRosterService found multiple player-controlled runtimes. Keeping " +
                    playerRuntime.name +
                    " and ignoring " +
                    runtime.name +
                    ".");
                runtime.Data.SetPlayerControlled(false);
                EnsureNpcAgent(runtime);
                return;
            }

            playerRuntime = runtime;
            EnsurePlayerActorStack(runtime);
            playerCharacter = runtime.GetComponent<CampusPlayerCharacter>();
            if (playerCharacter == null)
            {
                playerCharacter = runtime.gameObject.AddComponent<CampusPlayerCharacter>();
            }

            playerCharacter.Bind(runtime);
        }

        private void EnsurePlayerActorStack(CampusCharacterRuntime runtime)
        {
            if (runtime == null)
            {
                return;
            }

            EnsureBodyController(runtime);
            CampusHeldItemVisual heldItemVisual = runtime.GetComponent<CampusHeldItemVisual>();
            if (heldItemVisual == null)
            {
                heldItemVisual = runtime.gameObject.AddComponent<CampusHeldItemVisual>();
            }

            heldItemVisual.RefreshImmediate();

            CampusTestPlayerController controller = runtime.GetComponent<CampusTestPlayerController>();
            if (controller == null)
            {
                controller = runtime.gameObject.AddComponent<CampusTestPlayerController>();
            }

            controller.SetGameplayInputEnabled(true);
        }

        private static void EnsureBodyController(CampusCharacterRuntime runtime)
        {
            if (runtime == null)
            {
                return;
            }

            CampusCharacterBodyController body = runtime.GetComponent<CampusCharacterBodyController>();
            if (body == null)
            {
                body = runtime.gameObject.AddComponent<CampusCharacterBodyController>();
            }

            body.EnsureSetup();
        }

        private void SyncCurrentRoomsFromWorld()
        {
            if (worldService == null)
            {
                return;
            }

            for (int i = 0; i < runtimes.Count; i++)
            {
                CampusCharacterRuntime runtime = runtimes[i];
                if (runtime == null || runtime.Data == null)
                {
                    continue;
                }

                CampusGameplayRoom room = worldService.FindRoomForRuntime(runtime);
                if (room != null)
                {
                    runtime.Data.SetCurrentRoom(room.RoomId);
                }
            }
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

            bootstrap.EventLog.AddLog(CampusCharacterTextCatalog.FormatSceneRosterReady(
                CampusLanguageState.CurrentLanguage,
                StudentCount,
                TeacherCount));
            if (playerRuntime != null && playerRuntime.Data != null)
            {
                bootstrap.EventLog.AddLog(CampusCharacterTextCatalog.FormatPlayerCharacterBound(
                    CampusLanguageState.CurrentLanguage,
                    playerRuntime.Data.GetDisplayName(CampusLanguageState.CurrentLanguage)));
            }
        }

        private void EnsureNpcAgent(CampusCharacterRuntime runtime)
        {
            if (runtime == null || runtime.Data == null || runtime.Data.IsPlayerControlled)
            {
                return;
            }

            SetGameplayInput(runtime, false);
            CampusNpcAgent agent = runtime.GetComponent<CampusNpcAgent>();
            if (agent == null)
            {
                agent = runtime.gameObject.AddComponent<CampusNpcAgent>();
            }

            agent.Initialize(runtime, bootstrap, worldService);
        }

        private static void SetGameplayInput(CampusCharacterRuntime runtime, bool enabled)
        {
            if (runtime == null)
            {
                return;
            }

            CampusTestPlayerController controller = runtime.GetComponent<CampusTestPlayerController>();
            if (controller != null)
            {
                controller.SetGameplayInputEnabled(enabled);
                controller.enabled = enabled;
            }
        }
    }
}
