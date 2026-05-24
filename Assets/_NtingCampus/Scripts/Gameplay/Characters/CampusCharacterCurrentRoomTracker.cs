using System;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Inventory;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.UI.Runtime.Gameplay;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    [DisallowMultipleComponent]
    public sealed class CampusCharacterCurrentRoomTracker : MonoBehaviour
    {
        [SerializeField] private CampusCharacterRuntime runtime;
        [SerializeField] private CampusWorldService worldService;

        public void Configure(CampusCharacterRuntime targetRuntime, CampusWorldService targetWorldService)
        {
            runtime = targetRuntime != null ? targetRuntime : runtime;
            worldService = targetWorldService != null ? targetWorldService : ResolveWorldService();
        }

        public void SyncNow()
        {
            SyncRuntimeState();
        }

        private void Awake()
        {
            if (runtime == null)
            {
                runtime = GetComponent<CampusCharacterRuntime>();
            }

            if (worldService == null)
            {
                worldService = ResolveWorldService();
            }

            SyncRuntimeState();
        }

        private void LateUpdate()
        {
            SyncRuntimeState();
        }

        public static CampusCharacterCurrentRoomTracker EnsureFor(
            CampusCharacterRuntime runtime,
            CampusWorldService worldService = null)
        {
            if (runtime == null)
            {
                return null;
            }

            CampusCharacterCurrentRoomTracker tracker =
                runtime.GetComponent<CampusCharacterCurrentRoomTracker>();
            if (tracker == null)
            {
                tracker = runtime.gameObject.AddComponent<CampusCharacterCurrentRoomTracker>();
            }

            tracker.Configure(runtime, worldService);
            return tracker;
        }

        public static void SyncRuntime(
            CampusCharacterRuntime runtime,
            CampusWorldService worldService = null)
        {
            CampusCharacterCurrentRoomTracker tracker = runtime != null
                ? runtime.GetComponent<CampusCharacterCurrentRoomTracker>()
                : null;
            if (tracker != null)
            {
                tracker.Configure(runtime, worldService);
                tracker.SyncRuntimeState();
                return;
            }

            SyncRuntimeDirect(runtime, worldService);
        }

        private void SyncRuntimeState()
        {
            SyncRuntimeDirect(runtime, worldService);
        }

        private static void SyncRuntimeDirect(
            CampusCharacterRuntime runtime,
            CampusWorldService worldService)
        {
            if (runtime == null || runtime.Data == null)
            {
                return;
            }

            CampusWorldService resolvedWorldService = worldService != null
                ? worldService
                : ResolveWorldService();
            CampusGameplayRoom room = resolvedWorldService != null
                ? resolvedWorldService.FindRoomForPosition(
                    ResolveFloorIndex(runtime, resolvedWorldService),
                    runtime.transform.position)
                : null;

            string previousRoomId = NormalizeId(runtime.Data.CurrentRoomId);
            string nextRoomId = room != null ? NormalizeId(room.RoomId) : string.Empty;
            runtime.Data.SetCurrentRoom(nextRoomId);

            if (!string.Equals(previousRoomId, nextRoomId, StringComparison.OrdinalIgnoreCase))
            {
                CampusProtectedTransferState.PromotePendingTransfersForRoomTransition(
                    runtime,
                    previousRoomId,
                    nextRoomId);
            }
        }

        private static CampusWorldService ResolveWorldService()
        {
            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            if (bootstrap != null && bootstrap.WorldService != null)
            {
                return bootstrap.WorldService;
            }

            return UnityEngine.Object.FindFirstObjectByType<CampusWorldService>(FindObjectsInactive.Include);
        }

        private static int ResolveFloorIndex(
            CampusCharacterRuntime runtime,
            CampusWorldService worldService)
        {
            if (runtime != null &&
                CampusRuntimeGameplayOverlayLoader.TryGetManagedEntity(
                    runtime,
                    out CampusRuntimeGameplayOverlayEntity entity))
            {
                return Mathf.Max(1, entity.FloorIndex);
            }

            CampusSceneCharacterDefinition sceneCharacter =
                runtime != null ? runtime.GetComponent<CampusSceneCharacterDefinition>() : null;
            if (sceneCharacter != null)
            {
                return Mathf.Max(1, sceneCharacter.FloorIndex);
            }

            if (runtime != null &&
                runtime.Data != null &&
                worldService != null &&
                !string.IsNullOrWhiteSpace(runtime.Data.CurrentRoomId))
            {
                CampusGameplayRoom room = worldService.FindRoomById(runtime.Data.CurrentRoomId);
                if (room != null)
                {
                    return Mathf.Max(1, room.FloorIndex);
                }
            }

            return 1;
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}

