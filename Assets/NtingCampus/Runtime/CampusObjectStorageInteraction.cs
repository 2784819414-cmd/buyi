using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
using NtingCampus.Gameplay.UI;
using UnityEngine;

namespace NtingCampusMapEditor
{
    public static class CampusObjectStorageInteraction
    {
        public static bool TryOpenStorageView(Component source, GameObject actor, string payload)
        {
            if (source == null)
            {
                return false;
            }

            StorageMemory memory = StorageMemory.GetOrCreate();
            CampusCharacterRuntime actorRuntime = ResolveActorRuntime(actor);
            CampusPlacedObject placedObject = source.GetComponent<CampusPlacedObject>();
            string containerId = ResolveStorageContainerId(source, placedObject, payload);
            Vector2Int storageSize = ResolveObjectStorageSize(placedObject);
            StorageContainerModel container = memory.GetOrCreateContainer(
                containerId,
                ResolveObjectDisplayName(source, placedObject),
                ResolveObjectLocalizedDisplayName(source, placedObject),
                storageSize.x,
                storageSize.y,
                ResolveObjectStorageMaxWeight(placedObject));

            ConfigureObjectStorageContainer(source, placedObject, container);

            return CampusCharacterActionExecutor.TryExecute(
                actorRuntime,
                CampusCharacterAction.OpenInventoryView(container, source.gameObject, false),
                out _);
        }

        private static CampusCharacterRuntime ResolveActorRuntime(GameObject actor)
        {
            if (actor != null)
            {
                CampusCharacterRuntime actorRuntime = actor.GetComponentInParent<CampusCharacterRuntime>();
                if (actorRuntime != null)
                {
                    return actorRuntime;
                }
            }

            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            return bootstrap != null && bootstrap.RosterService != null
                ? bootstrap.RosterService.PlayerRuntime
                : null;
        }

        private static void ConfigureObjectStorageContainer(
            Component source,
            CampusPlacedObject placedObject,
            StorageContainerModel container)
        {
            if (container == null || source == null)
            {
                return;
            }

            CampusGameplayRoom room = ResolveGameplayRoom(source, placedObject);
            container.AccessPolicy = StorageContainerAccessPolicy.ProtectedPublic;
            container.OwnerId = placedObject != null && !string.IsNullOrWhiteSpace(placedObject.ObjectId)
                ? placedObject.ObjectId
                : source.gameObject.name;
            container.OwnerRole = "Campus";
            container.RoomId = room != null ? room.RoomId : string.Empty;
            container.AllowTakingContents = false;
            container.IsPlayerCarried = false;
            container.SuspicionRisk = ResolveStorageSuspicionRisk(room);
        }

        private static CampusGameplayRoom ResolveGameplayRoom(Component source, CampusPlacedObject placedObject)
        {
            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            CampusWorldService worldService = bootstrap != null ? bootstrap.WorldService : null;
            if (worldService == null || source == null)
            {
                return null;
            }

            int floorIndex = placedObject != null ? placedObject.FloorIndex : 1;
            return worldService.FindRoomForPosition(floorIndex, source.transform.position);
        }

        private static int ResolveStorageSuspicionRisk(CampusGameplayRoom room)
        {
            if (room == null)
            {
                return 2;
            }

            switch (room.RoomType)
            {
                case CampusRoomType.Office:
                    return 12;
                case CampusRoomType.Classroom:
                    return 5;
                case CampusRoomType.Dormitory:
                    return 4;
                default:
                    return 3;
            }
        }

        private static Vector2Int ResolveObjectStorageSize(CampusPlacedObject placedObject)
        {
            if (placedObject == null)
            {
                return CampusPlacedObject.DefaultStorageSize;
            }

            placedObject.NormalizeStorageSettings();
            return placedObject.NormalizedStorageSize;
        }

        private static float ResolveObjectStorageMaxWeight(CampusPlacedObject placedObject)
        {
            if (placedObject == null)
            {
                return CampusPlacedObject.DefaultStorageMaxWeight;
            }

            placedObject.NormalizeStorageSettings();
            return placedObject.NormalizedStorageMaxWeight;
        }

        private static string ResolveStorageContainerId(Component source, CampusPlacedObject placedObject, string payload)
        {
            if (!string.IsNullOrWhiteSpace(payload))
            {
                return "object_storage_" + SanitizeStorageId(payload);
            }

            Vector3 position = source.transform.position;
            int px = Mathf.RoundToInt(position.x * 100f);
            int py = Mathf.RoundToInt(position.y * 100f);
            int pz = Mathf.RoundToInt(position.z * 100f);

            if (placedObject != null)
            {
                Vector3Int cell = placedObject.Cell;
                string objectId = string.IsNullOrWhiteSpace(placedObject.ObjectId)
                    ? source.gameObject.name
                    : placedObject.ObjectId;
                return "object_storage_" + SanitizeStorageId(objectId) +
                       "_f" + placedObject.FloorIndex +
                       "_c" + cell.x + "_" + cell.y + "_" + cell.z +
                       "_p" + px + "_" + py + "_" + pz;
            }

            return "object_storage_" + SanitizeStorageId(source.gameObject.name) + "_p" + px + "_" + py + "_" + pz + "_" + source.gameObject.GetInstanceID();
        }

        private static string ResolveObjectDisplayName(Component source, CampusPlacedObject placedObject)
        {
            if (placedObject != null)
            {
                return placedObject.DisplayName;
            }

            return source != null
                ? CampusObjectNames.GetDisplayName(source.gameObject.name)
                : string.Empty;
        }

        private static CampusLocalizedText ResolveObjectLocalizedDisplayName(Component source, CampusPlacedObject placedObject)
        {
            if (placedObject != null && placedObject.LocalizedDisplayNameOverride.HasAnyText)
            {
                return placedObject.LocalizedDisplayNameOverride;
            }

            string displayName = ResolveObjectDisplayName(source, placedObject);
            return new CampusLocalizedText(displayName, displayName);
        }

        private static string SanitizeStorageId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unnamed";
            }

            char[] chars = value.Trim().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_' && chars[i] != '-')
                {
                    chars[i] = '_';
                }
            }

            string result = new string(chars).Trim('_');
            return string.IsNullOrEmpty(result) ? "unnamed" : result;
        }
    }
}
