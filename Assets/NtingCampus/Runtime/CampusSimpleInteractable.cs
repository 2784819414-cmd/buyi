using Nting.Storage;
using UnityEngine;

namespace NtingCampusMapEditor
{
    [DisallowMultipleComponent]
    public sealed class CampusSimpleInteractable : CampusInteractionPromptSource, ICampusInteractable, ICampusInteractionActionHandler
    {
        public bool LogInteraction = true;
        public string InteractionLogMessage;

        [Header("Unified Action")]
        public string DefaultActionId;

        public void Interact(GameObject actor)
        {
            string actionId = ResolveDefaultActionId(null);
            if (!TryHandleInteractionAction(null, actionId, null, actor))
            {
                LogDefaultInteraction(actor);
            }
        }

        public bool TryHandleInteractionAction(CampusInteractionAnchor anchor, string actionId, string payload, GameObject actor)
        {
            string resolvedActionId = CampusInteractionActionIds.Normalize(actionId);
            if (string.IsNullOrEmpty(resolvedActionId))
            {
                resolvedActionId = ResolveDefaultActionId(anchor);
            }

            if (CampusInteractionActionIds.Equals(resolvedActionId, CampusInteractionActionIds.OpenStorage))
            {
                return TryOpenStorageWindow(payload);
            }

            if (CampusInteractionActionIds.Equals(resolvedActionId, CampusInteractionActionIds.ToggleDoor))
            {
                return TryToggleDoor(anchor);
            }

            if (CampusInteractionActionIds.Equals(resolvedActionId, CampusInteractionActionIds.InteractTarget))
            {
                return TryInteractTarget(anchor, actor);
            }

            if (CampusInteractionActionIds.Equals(resolvedActionId, CampusInteractionActionIds.Log))
            {
                LogDefaultInteraction(actor);
                return true;
            }

            return false;
        }

        private string ResolveDefaultActionId(CampusInteractionAnchor anchor)
        {
            if (!string.IsNullOrWhiteSpace(DefaultActionId))
            {
                return DefaultActionId;
            }

            if (anchor != null && anchor.InteractionTarget is ICampusInteractable target && !ReferenceEquals(target, this))
            {
                return CampusInteractionActionIds.InteractTarget;
            }

            return CampusInteractionActionIds.Log;
        }

        private bool TryOpenStorageWindow(string payload)
        {
            StorageMemory memory = StorageMemory.GetOrCreate();
            StorageItemRegistry registry = Resources.Load<StorageItemRegistry>("StorageItemRegistry");
            if (registry == null)
            {
                registry = StorageItemRegistry.CreateDemoRegistry();
            }

            memory.SetRegistry(registry);

            StorageContainerModel[] pockets =
            {
                memory.GetOrCreateContainer("pocket_left_chest", "\u5de6\u80f8\u888b", 2, 3, 1.5f),
                memory.GetOrCreateContainer("pocket_right_chest", "\u53f3\u80f8\u888b", 2, 3, 1.5f),
                memory.GetOrCreateContainer("pocket_left_pants", "\u5de6\u88e4\u888b", 2, 3, 2f),
                memory.GetOrCreateContainer("pocket_right_pants", "\u53f3\u88e4\u888b", 2, 3, 2f)
            };
            StorageContainerModel backpack = memory.GetOrCreateContainer("school_backpack", "\u5b66\u751f\u4e66\u5305", 5, 6, 20f);
            string containerId = ResolveStorageContainerId(payload);
            Vector2Int storageSize = ResolveObjectStorageSize();
            StorageContainerModel container = memory.GetOrCreateContainer(
                containerId,
                ResolveObjectDisplayName(),
                storageSize.x,
                storageSize.y,
                ResolveObjectStorageMaxWeight());

            StorageWindowUI window = FindFirstObjectByType<StorageWindowUI>();
            if (window == null)
            {
                GameObject windowObject = new GameObject("Canvas_Storage", typeof(RectTransform), typeof(StorageWindowUI));
                window = windowObject.GetComponent<StorageWindowUI>();
            }

            window.Open(pockets, backpack, true, container);
            return true;
        }

        private Vector2Int ResolveObjectStorageSize()
        {
            CampusPlacedObject placedObject = GetComponent<CampusPlacedObject>();
            if (placedObject == null)
            {
                return CampusPlacedObject.DefaultStorageSize;
            }

            placedObject.NormalizeStorageSettings();
            return placedObject.NormalizedStorageSize;
        }

        private float ResolveObjectStorageMaxWeight()
        {
            CampusPlacedObject placedObject = GetComponent<CampusPlacedObject>();
            if (placedObject == null)
            {
                return CampusPlacedObject.DefaultStorageMaxWeight;
            }

            placedObject.NormalizeStorageSettings();
            return placedObject.NormalizedStorageMaxWeight;
        }

        private bool TryToggleDoor(CampusInteractionAnchor anchor)
        {
            CampusDoor3D door3D = ResolveDoor3D(anchor);
            if (door3D != null)
            {
                door3D.ToggleOpen();
                return true;
            }

            RestroomStallDoor stallDoor = ResolveRestroomStallDoor(anchor);
            if (stallDoor != null)
            {
                stallDoor.ToggleOpen();
                return true;
            }

            return false;
        }

        private bool TryInteractTarget(CampusInteractionAnchor anchor, GameObject actor)
        {
            if (anchor == null || !(anchor.InteractionTarget is ICampusInteractable target) || ReferenceEquals(target, this))
            {
                return false;
            }

            target.Interact(actor);
            return true;
        }

        private CampusDoor3D ResolveDoor3D(CampusInteractionAnchor anchor)
        {
            Component target = anchor != null ? anchor.InteractionTarget as Component : null;
            if (target is CampusDoor3D directDoor)
            {
                return directDoor;
            }

            CampusDoor3D targetDoor = target != null ? target.GetComponentInParent<CampusDoor3D>() : null;
            if (targetDoor != null)
            {
                return targetDoor;
            }

            targetDoor = GetComponentInChildren<CampusDoor3D>(true);
            return targetDoor != null ? targetDoor : GetComponentInParent<CampusDoor3D>();
        }

        private RestroomStallDoor ResolveRestroomStallDoor(CampusInteractionAnchor anchor)
        {
            Component target = anchor != null ? anchor.InteractionTarget as Component : null;
            if (target is RestroomStallDoor directDoor)
            {
                return directDoor;
            }

            RestroomStallDoor targetDoor = target != null ? target.GetComponentInParent<RestroomStallDoor>() : null;
            if (targetDoor != null)
            {
                return targetDoor;
            }

            targetDoor = GetComponentInChildren<RestroomStallDoor>(true);
            return targetDoor != null ? targetDoor : GetComponentInParent<RestroomStallDoor>();
        }

        private void LogDefaultInteraction(GameObject actor)
        {
            if (!LogInteraction)
            {
                return;
            }

            string actorName = actor != null ? actor.name : "UnknownActor";
            string message = string.IsNullOrWhiteSpace(InteractionLogMessage)
                ? actorName + " interacted with " + ResolveObjectDisplayName()
                : InteractionLogMessage;

            Debug.Log(message, this);
        }

        private string ResolveStorageContainerId(string payload)
        {
            if (!string.IsNullOrWhiteSpace(payload))
            {
                return "object_storage_" + SanitizeStorageId(payload);
            }

            CampusPlacedObject placedObject = GetComponent<CampusPlacedObject>();
            Vector3 position = transform.position;
            int px = Mathf.RoundToInt(position.x * 100f);
            int py = Mathf.RoundToInt(position.y * 100f);
            int pz = Mathf.RoundToInt(position.z * 100f);

            if (placedObject != null)
            {
                Vector3Int cell = placedObject.Cell;
                string objectId = string.IsNullOrWhiteSpace(placedObject.ObjectId) ? gameObject.name : placedObject.ObjectId;
                return "object_storage_" + SanitizeStorageId(objectId) +
                       "_f" + placedObject.FloorIndex +
                       "_c" + cell.x + "_" + cell.y + "_" + cell.z +
                       "_p" + px + "_" + py + "_" + pz;
            }

            return "object_storage_" + SanitizeStorageId(gameObject.name) + "_p" + px + "_" + py + "_" + pz + "_" + gameObject.GetInstanceID();
        }

        private string ResolveObjectDisplayName()
        {
            CampusPlacedObject placedObject = GetComponent<CampusPlacedObject>();
            if (placedObject != null)
            {
                return placedObject.DisplayName;
            }

            return CampusObjectNames.GetDisplayName(gameObject.name);
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
