using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.UI.Runtime.Gameplay;
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
        private readonly CampusRosterIndex index = new CampusRosterIndex();

        private bool runtimeLookupReady;

        public IReadOnlyList<CampusCharacterData> Characters => characters;
        public IReadOnlyList<CampusCharacterRuntime> Runtimes => runtimes;
        public CampusRosterIndex Index => index;
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
            index.Rebuild(runtimes);
            runtimeLookupReady = true;
            SyncCurrentRoomsFromWorld();
            WriteInitializationLog();
        }

        public CampusCharacterRuntime FindRuntime(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId))
            {
                return null;
            }

            if (!runtimeLookupReady)
            {
                RebuildRuntimeLookup();
            }

            runtimesById.TryGetValue(NormalizeRuntimeId(characterId), out CampusCharacterRuntime runtime);
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

            bool alreadyCurrentPlayer = playerRuntime == targetRuntime;
            DemoteCurrentPlayerExcept(targetRuntime);
            PromotePlayerRuntime(targetRuntime);

            if (alreadyCurrentPlayer)
            {
                return true;
            }

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
            runtimeLookupReady = false;
            playerRuntime = null;
            playerCharacter = null;
            index.Rebuild(runtimes);

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
                CampusRosterLogTextCatalog.Warning(CampusRosterLogTextId.MissingPlayerControlledRuntime);
            }

            index.Rebuild(runtimes);
        }

        private void RegisterRuntime(CampusCharacterRuntime runtime, HashSet<CampusCharacterRuntime> processedRuntimes)
        {
            if (runtime == null || runtime.Data == null || !processedRuntimes.Add(runtime))
            {
                return;
            }

            runtimes.Add(runtime);
            characters.Add(runtime.Data);
            IndexRuntime(runtime);

            if (runtime.Data.IsPlayerControlled)
            {
                BindPlayerRuntime(runtime);
                return;
            }

            EnsureNpcActor(runtime);
        }

        private void BindPlayerRuntime(CampusCharacterRuntime runtime)
        {
            if (playerRuntime != null && playerRuntime != runtime)
            {
                CampusRosterLogTextCatalog.Warning(
                    CampusRosterLogTextId.MultiplePlayerControlledRuntimes,
                    playerRuntime.name,
                    runtime.name);
                DemotePlayerRuntime(runtime);
                return;
            }

            PromotePlayerRuntime(runtime);
        }

        private void PromotePlayerRuntime(CampusCharacterRuntime runtime)
        {
            if (runtime == null || runtime.Data == null)
            {
                return;
            }

            playerRuntime = runtime;
            runtime.Data.SetPlayerControlled(true);
            EnsurePlayerActorStack(runtime);
            RemoveNpcActor(runtime);
            playerCharacter = EnsurePlayerCharacter(runtime);
            SetGameplayInput(runtime, true);
        }

        private void DemoteCurrentPlayerExcept(CampusCharacterRuntime promotedRuntime)
        {
            if (playerRuntime != null && playerRuntime != promotedRuntime)
            {
                DemotePlayerRuntime(playerRuntime);
            }

            for (int i = 0; i < runtimes.Count; i++)
            {
                CampusCharacterRuntime runtime = runtimes[i];
                if (runtime == null || runtime == promotedRuntime)
                {
                    continue;
                }

                bool isMarkedPlayer =
                    (runtime.Data != null && runtime.Data.IsPlayerControlled) ||
                    runtime.GetComponent<CampusPlayerCharacter>() != null;
                if (isMarkedPlayer)
                {
                    DemotePlayerRuntime(runtime);
                }
            }
        }

        private void DemotePlayerRuntime(CampusCharacterRuntime runtime)
        {
            if (runtime == null || runtime.Data == null)
            {
                return;
            }

            runtime.Data.SetPlayerControlled(false);
            RemovePlayerCharacter(runtime);
            SetGameplayInput(runtime, false);
            EnsureNpcActor(runtime);
            if (playerRuntime == runtime)
            {
                playerRuntime = null;
            }
        }

        private CampusPlayerCharacter EnsurePlayerCharacter(CampusCharacterRuntime runtime)
        {
            CampusPlayerCharacter marker = runtime.GetComponent<CampusPlayerCharacter>();
            if (marker == null)
            {
                marker = runtime.gameObject.AddComponent<CampusPlayerCharacter>();
            }

            marker.Bind(runtime);
            return marker;
        }

        private void EnsurePlayerActorStack(CampusCharacterRuntime runtime)
        {
            if (runtime == null)
            {
                return;
            }

            EnsureBodyController(runtime);
            EnsureStaminaController(runtime);
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

        private static void EnsureStaminaController(CampusCharacterRuntime runtime)
        {
            if (runtime == null)
            {
                return;
            }

            CampusCharacterStaminaController stamina = runtime.GetComponent<CampusCharacterStaminaController>();
            if (stamina == null)
            {
                stamina = runtime.gameObject.AddComponent<CampusCharacterStaminaController>();
            }

            stamina.EnsureSetup();
        }

        private void SyncCurrentRoomsFromWorld()
        {
            for (int i = 0; i < runtimes.Count; i++)
            {
                CampusCharacterRuntime runtime = runtimes[i];
                if (runtime == null || runtime.Data == null)
                {
                    continue;
                }

                CampusCharacterCurrentRoomTracker.SyncRuntime(runtime, worldService);
            }
        }

        private void RebuildRuntimeLookup()
        {
            runtimesById.Clear();
            for (int i = 0; i < runtimes.Count; i++)
            {
                IndexRuntime(runtimes[i]);
            }

            runtimeLookupReady = true;
        }

        private void IndexRuntime(CampusCharacterRuntime runtime)
        {
            if (runtime == null || runtime.Data == null || string.IsNullOrWhiteSpace(runtime.CharacterId))
            {
                return;
            }

            runtimesById[NormalizeRuntimeId(runtime.CharacterId)] = runtime;
        }

        private static string NormalizeRuntimeId(string characterId)
        {
            return string.IsNullOrWhiteSpace(characterId) ? string.Empty : characterId.Trim();
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

        private void EnsureNpcActor(CampusCharacterRuntime runtime)
        {
            if (runtime == null || runtime.Data == null || runtime.Data.IsPlayerControlled)
            {
                return;
            }

            RemovePlayerCharacter(runtime);
            SetGameplayInput(runtime, false);
            CampusNpcActor actor = runtime.GetComponent<CampusNpcActor>();
            if (actor == null)
            {
                actor = runtime.gameObject.AddComponent<CampusNpcActor>();
            }

            actor.Initialize(runtime, bootstrap, worldService);
        }

        private void RemovePlayerCharacter(CampusCharacterRuntime runtime)
        {
            if (runtime == null)
            {
                return;
            }

            CampusPlayerCharacter marker = runtime.GetComponent<CampusPlayerCharacter>();
            if (marker == null)
            {
                return;
            }

            if (playerCharacter == marker)
            {
                playerCharacter = null;
            }

            marker.Clear();
            DestroyRuntimeComponent(marker);
        }

        private static void RemoveNpcActor(CampusCharacterRuntime runtime)
        {
            if (runtime == null)
            {
                return;
            }

            CampusNpcActor actor = runtime.GetComponent<CampusNpcActor>();
            if (actor != null)
            {
                DestroyRuntimeComponent(actor);
            }
        }

        private static void DestroyRuntimeComponent(Component component)
        {
            if (component == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(component);
                return;
            }

            UnityEngine.Object.DestroyImmediate(component);
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

