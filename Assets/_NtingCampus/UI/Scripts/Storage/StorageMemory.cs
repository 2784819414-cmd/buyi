using System.Collections.Generic;
using UnityEngine;
using NtingCampus.UI.Runtime.Gameplay;

namespace Nting.Storage
{
    [DisallowMultipleComponent]
    public sealed class StorageMemory : MonoBehaviour
    {
        private static StorageMemory instance;

        private readonly Dictionary<string, StorageContainerModel> containers =
            new Dictionary<string, StorageContainerModel>();
        private readonly HashSet<string> sessionFlags = new HashSet<string>();

        public StorageItemRegistry ItemRegistry;

        public IEnumerable<StorageContainerModel> Containers => containers.Values;

        public static StorageMemory Instance => GetOrCreate();

        public static StorageMemory GetOrCreate()
        {
            if (instance != null)
            {
                return instance;
            }

            instance = FindFirstObjectByType<StorageMemory>();
            if (instance != null)
            {
                return instance;
            }

            GameObject memoryObject = new GameObject("StorageMemory");
            DontDestroyOnLoad(memoryObject);
            instance = memoryObject.AddComponent<StorageMemory>();
            return instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void SetRegistry(StorageItemRegistry registry)
        {
            if (registry != null)
            {
                ItemRegistry = registry;
            }
        }

        public bool HasContainer(string containerId)
        {
            return !string.IsNullOrWhiteSpace(containerId) && containers.ContainsKey(containerId);
        }

        public bool TryGetContainer(string containerId, out StorageContainerModel container)
        {
            container = null;
            return !string.IsNullOrWhiteSpace(containerId) &&
                   containers.TryGetValue(containerId.Trim(), out container) &&
                   container != null;
        }

        public StorageContainerModel GetOrCreateContainer(
            string id,
            string displayName,
            int columns,
            int rows,
            float maxWeight)
        {
            return GetOrCreateContainer(id, displayName, default, columns, rows, maxWeight, false);
        }

        public StorageContainerModel GetOrCreateContainer(
            string id,
            string displayName,
            CampusLocalizedText localizedDisplayName,
            int columns,
            int rows,
            float maxWeight,
            bool isSingleItemSlot = false)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogWarning("Storage memory failed: container id is empty.");
                return null;
            }

            string containerId = id.Trim();
            int normalizedColumns = Mathf.Max(1, columns);
            int normalizedRows = Mathf.Max(1, rows);
            if (containers.TryGetValue(containerId, out StorageContainerModel existing) && existing != null)
            {
                ConfigureContainer(existing, displayName, localizedDisplayName, normalizedColumns, normalizedRows, maxWeight, isSingleItemSlot);
                return existing;
            }

            StorageContainerModel container = new StorageContainerModel { Id = containerId };
            ConfigureContainer(container, displayName, localizedDisplayName, normalizedColumns, normalizedRows, maxWeight, isSingleItemSlot);
            containers[containerId] = container;
            return container;
        }

        public void ClearContainers()
        {
            containers.Clear();
        }

        public bool TryPlaceNewItem(string containerId, string definitionId, string instanceId, int x, int y)
        {
            if (!TryGetContainer(containerId, out StorageContainerModel container))
            {
                Debug.LogWarning("Storage memory failed: missing container '" + containerId + "'.");
                return false;
            }

            if (ItemRegistry == null)
            {
                Debug.LogWarning("Storage memory failed: item registry is not assigned.");
                return false;
            }

            StorageItemModel item = ItemRegistry.CreateItem(definitionId, instanceId);
            return item != null && container.PlaceItem(item, x, y);
        }

        public bool IsSessionFlagSet(string flag)
        {
            return !string.IsNullOrWhiteSpace(flag) && sessionFlags.Contains(flag.Trim());
        }

        public void SetSessionFlag(string flag)
        {
            if (!string.IsNullOrWhiteSpace(flag))
            {
                sessionFlags.Add(flag.Trim());
            }
        }

        public StorageMemorySaveData ToSaveData()
        {
            return StorageMemorySerializer.ToSaveData(Containers);
        }

        public void LoadFromSaveData(StorageMemorySaveData data)
        {
            StorageMemorySerializer.LoadInto(this, data);
        }

        public string ToJson(bool prettyPrint = false)
        {
            return JsonUtility.ToJson(ToSaveData(), prettyPrint);
        }

        public void LoadFromJson(string json)
        {
            if (!string.IsNullOrWhiteSpace(json))
            {
                LoadFromSaveData(JsonUtility.FromJson<StorageMemorySaveData>(json));
            }
        }

        public void SaveToPlayerPrefs()
        {
            StorageMemoryPersistence.SaveToPlayerPrefs(this);
        }

        public bool LoadFromPlayerPrefs()
        {
            return StorageMemoryPersistence.LoadFromPlayerPrefs(this);
        }

        private static void ConfigureContainer(
            StorageContainerModel container,
            string displayName,
            CampusLocalizedText localizedDisplayName,
            int columns,
            int rows,
            float maxWeight,
            bool isSingleItemSlot)
        {
            container.DisplayName = displayName;
            container.LocalizedDisplayName = localizedDisplayName;
            if (CanResizeContainer(container, columns, rows, isSingleItemSlot))
            {
                container.ApplyShape(columns, rows, isSingleItemSlot);
            }
            else
            {
                Debug.LogWarning("Storage memory kept previous size for container '" + container.Id + "' because existing items would be out of bounds.");
            }

            container.MaxWeight = maxWeight;
        }

        private static bool CanResizeContainer(
            StorageContainerModel container,
            int columns,
            int rows,
            bool isSingleItemSlot)
        {
            if (isSingleItemSlot)
            {
                return true;
            }

            if (container == null || container.Items == null)
            {
                return true;
            }

            for (int i = 0; i < container.Items.Count; i++)
            {
                StorageItemModel item = container.Items[i];
                if (item != null &&
                    (item.X < 0 || item.Y < 0 ||
                     item.X + item.CurrentWidth > columns ||
                     item.Y + item.CurrentHeight > rows))
                {
                    return false;
                }
            }

            return true;
        }
    }
}

