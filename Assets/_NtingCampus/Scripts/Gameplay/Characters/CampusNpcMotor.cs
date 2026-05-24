using NtingCampus.Gameplay.Rooms;
using NtingCampus.UI.Runtime.Gameplay;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    [DisallowMultipleComponent]
    public sealed class CampusNpcMotor : MonoBehaviour
    {
        [SerializeField] private CampusCharacterRuntime runtime;
        [SerializeField] private CampusWorldService worldService;
        [SerializeField] private CampusCharacterBodyController bodyController;
        [SerializeField] private CampusGridNavigationAgent navigationAgent;
        [SerializeField] private int personalSeed;
        [SerializeField] private int cachedRuntimeFloorIndex = 1;
        [SerializeField] private float nextRuntimeFloorRefreshTime;

        public CampusCharacterBodyController BodyController => bodyController;
        public CampusGridNavigationAgent NavigationAgent => navigationAgent;
        public float MoveSpeed => CampusCharacterMovementTuning.ResolveNpcMoveSpeed(ResolvePersonalSeed());
        public int FloorIndex => ResolveRuntimeFloorIndex();
        public int PersonalSeed => ResolvePersonalSeed();

        public void Configure(CampusCharacterRuntime targetRuntime, CampusWorldService targetWorldService)
        {
            runtime = targetRuntime != null ? targetRuntime : runtime;
            worldService = targetWorldService != null ? targetWorldService : worldService;
        }

        public void Ensure()
        {
            EnsureBodyController();
            EnsureNavigationAgent();
        }

        public void ClearNavigation()
        {
            navigationAgent?.ClearDestination();
            bodyController?.StopMovement();
        }

        internal void BindNavigator(CampusNpcNavigatorHandle navigator)
        {
            if (navigator == null)
            {
                return;
            }

            Ensure();
            navigator.Bind(
                navigationAgent,
                bodyController,
                transform,
                () => MoveSpeed,
                () => FloorIndex,
                () => PersonalSeed);
        }

        private void EnsureBodyController()
        {
            if (bodyController == null)
            {
                bodyController = GetComponent<CampusCharacterBodyController>();
            }

            if (bodyController == null)
            {
                bodyController = gameObject.AddComponent<CampusCharacterBodyController>();
            }

            bodyController.MoveSpeed = MoveSpeed;
            bodyController.FloorIndex = FloorIndex;
            bodyController.SetMovementEnabled(true);
            bodyController.EnsureSetup();
        }

        private void EnsureNavigationAgent()
        {
            if (navigationAgent == null)
            {
                navigationAgent = GetComponent<CampusGridNavigationAgent>();
            }

            if (navigationAgent == null)
            {
                navigationAgent = gameObject.AddComponent<CampusGridNavigationAgent>();
            }

            navigationAgent.Configure(
                MoveSpeed,
                FloorIndex,
                PersonalSeed,
                0.8f,
                0.16f,
                0.9f,
                0.035f);
        }

        private int ResolveRuntimeFloorIndex()
        {
            if (Application.isPlaying &&
                cachedRuntimeFloorIndex > 0 &&
                Time.time < nextRuntimeFloorRefreshTime)
            {
                return cachedRuntimeFloorIndex;
            }

            int resolvedFloor = 1;
            if (runtime != null && CampusRuntimeGameplayOverlayLoader.TryGetManagedEntity(runtime, out CampusRuntimeGameplayOverlayEntity entity))
            {
                resolvedFloor = entity.FloorIndex;
                CacheRuntimeFloorIndex(resolvedFloor);
                return resolvedFloor;
            }

            CampusSceneCharacterDefinition sceneCharacter = GetComponent<CampusSceneCharacterDefinition>();
            if (sceneCharacter != null)
            {
                resolvedFloor = sceneCharacter.FloorIndex;
                CacheRuntimeFloorIndex(resolvedFloor);
                return resolvedFloor;
            }

            if (runtime != null && runtime.Data != null && worldService != null)
            {
                CampusGameplayRoom room = worldService.FindRoomById(runtime.Data.CurrentRoomId);
                if (room != null)
                {
                    resolvedFloor = room.FloorIndex;
                    CacheRuntimeFloorIndex(resolvedFloor);
                    return resolvedFloor;
                }
            }

            CacheRuntimeFloorIndex(resolvedFloor);
            return resolvedFloor;
        }

        private void CacheRuntimeFloorIndex(int floorIndex)
        {
            cachedRuntimeFloorIndex = Mathf.Max(1, floorIndex);
            nextRuntimeFloorRefreshTime = Application.isPlaying
                ? Time.time + 0.45f + Mathf.Lerp(0.02f, 0.16f, CampusNpcStableIds.PositiveModulo(ResolvePersonalSeed() * 23, 100) / 99f)
                : 0f;
        }

        private int ResolvePersonalSeed()
        {
            if (personalSeed == 0)
            {
                string id = runtime != null && runtime.Data != null ? runtime.Data.Id : name;
                personalSeed = Mathf.Max(1, Mathf.Abs(CampusNpcStableIds.Hash(id)));
            }

            return personalSeed;
        }
    }
}

