using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Characters;
using UnityEngine;

namespace NtingCampus.Gameplay.Rooms
{
    [DisallowMultipleComponent]
    public sealed class CampusWorldService : MonoBehaviour
    {
        [SerializeField] private CampusGameBootstrap bootstrap;
        [SerializeField] private CampusRoomRegistry roomRegistry;
        [SerializeField] private bool rebuildRegistryOnInitialize = true;

        public CampusRoomRegistry RoomRegistry => roomRegistry;

        public void Initialize(CampusGameBootstrap targetBootstrap)
        {
            bootstrap = targetBootstrap != null ? targetBootstrap : CampusGameBootstrap.Instance;
            ResolveRoomRegistry();
            if (rebuildRegistryOnInitialize && roomRegistry != null)
            {
                roomRegistry.RebuildRegistry();
            }
        }

        public CampusGameplayRoom FindFirstUsableRoom(CampusRoomType roomType)
        {
            if (roomRegistry == null || roomRegistry.Rooms == null)
            {
                return null;
            }

            for (int i = 0; i < roomRegistry.Rooms.Count; i++)
            {
                CampusGameplayRoom room = roomRegistry.Rooms[i];
                if (room != null && room.RoomType == roomType && room.IsUsableForGameplay)
                {
                    return room;
                }
            }

            return null;
        }

        public CampusGameplayRoom FindRoomForPosition(int floorIndex, Vector3 worldPosition)
        {
            if (roomRegistry == null)
            {
                return null;
            }

            Vector3Int cell = new Vector3Int(
                Mathf.RoundToInt(worldPosition.x),
                Mathf.RoundToInt(worldPosition.y),
                0);
            return roomRegistry.FindRoomByCell(floorIndex, cell);
        }

        public CampusGameplayRoom FindRoomForRuntime(CampusCharacterRuntime runtime)
        {
            if (runtime == null)
            {
                return null;
            }

            int floorIndex = 1;
            NtingCampusMapEditor.CampusTestPlayerController playerController =
                runtime.GetComponent<NtingCampusMapEditor.CampusTestPlayerController>();
            if (playerController != null)
            {
                floorIndex = Mathf.Max(1, playerController.FloorIndex);
            }

            return FindRoomForPosition(floorIndex, runtime.transform.position);
        }

        private void ResolveRoomRegistry()
        {
            if (roomRegistry != null)
            {
                return;
            }

            roomRegistry = GetComponent<CampusRoomRegistry>();
            if (roomRegistry == null)
            {
                roomRegistry = FindFirstObjectByType<CampusRoomRegistry>(FindObjectsInactive.Include);
            }

            if (roomRegistry == null)
            {
                roomRegistry = gameObject.AddComponent<CampusRoomRegistry>();
            }
        }
    }
}
