using System.Collections.Generic;
using UnityEngine;

namespace Nting.Storage
{
    [DisallowMultipleComponent]
    public sealed class StorageMemory : MonoBehaviour
    {
        private const string PlayerPrefsKey = "Nting.Storage.Memory";

        private static StorageMemory instance;

        private readonly Dictionary<string, StorageContainerModel> containers = new Dictionary<string, StorageContainerModel>();
        private readonly HashSet<string> sessionFlags = new HashSet<string>();

        public StorageItemRegistry ItemRegistry;

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

        public StorageContainerModel GetOrCreateContainer(string id, string displayName, int columns, int rows, float maxWeight)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogWarning("Storage memory failed: container id is empty.");
                return null;
            }

            int normalizedColumns = Mathf.Max(1, columns);
            int normalizedRows = Mathf.Max(1, rows);
            if (containers.TryGetValue(id, out StorageContainerModel existing) && existing != null)
            {
                existing.DisplayName = displayName;
                if (CanResizeContainer(existing, normalizedColumns, normalizedRows))
                {
                    existing.Columns = normalizedColumns;
                    existing.Rows = normalizedRows;
                }
                else
                {
                    Debug.LogWarning("Storage memory kept previous size for container '" + id + "' because existing items would be out of bounds.");
                }

                existing.MaxWeight = maxWeight;
                return existing;
            }

            StorageContainerModel container = new StorageContainerModel
            {
                Id = id,
                DisplayName = displayName,
                Columns = normalizedColumns,
                Rows = normalizedRows,
                MaxWeight = maxWeight
            };

            containers[id] = container;
            return container;
        }

        private static bool CanResizeContainer(StorageContainerModel container, int columns, int rows)
        {
            if (container == null || container.Items == null)
            {
                return true;
            }

            for (int i = 0; i < container.Items.Count; i++)
            {
                StorageItemModel item = container.Items[i];
                if (item == null)
                {
                    continue;
                }

                if (item.X < 0 || item.Y < 0 ||
                    item.X + item.CurrentWidth > columns ||
                    item.Y + item.CurrentHeight > rows)
                {
                    return false;
                }
            }

            return true;
        }

        public bool TryPlaceNewItem(string containerId, string definitionId, string instanceId, int x, int y)
        {
            if (!containers.TryGetValue(containerId, out StorageContainerModel container) || container == null)
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
            if (item == null)
            {
                return false;
            }

            return container.PlaceItem(item, x, y);
        }

        public bool IsSessionFlagSet(string flag)
        {
            return !string.IsNullOrWhiteSpace(flag) && sessionFlags.Contains(flag);
        }

        public void SetSessionFlag(string flag)
        {
            if (!string.IsNullOrWhiteSpace(flag))
            {
                sessionFlags.Add(flag);
            }
        }

        public StorageMemorySaveData ToSaveData()
        {
            StorageMemorySaveData data = new StorageMemorySaveData();
            foreach (KeyValuePair<string, StorageContainerModel> pair in containers)
            {
                StorageContainerModel container = pair.Value;
                if (container == null)
                {
                    continue;
                }

                StorageContainerSaveData containerData = new StorageContainerSaveData
                {
                    Id = container.Id,
                    DisplayName = container.DisplayName,
                    Columns = container.Columns,
                    Rows = container.Rows,
                    MaxWeight = container.MaxWeight
                };

                for (int i = 0; i < container.Items.Count; i++)
                {
                    StorageItemModel item = container.Items[i];
                    if (item == null)
                    {
                        continue;
                    }

                    containerData.Items.Add(new StorageItemSaveData
                    {
                        DefinitionId = item.DefinitionId,
                        InstanceId = string.IsNullOrWhiteSpace(item.InstanceId) ? item.Id : item.InstanceId,
                        DisplayName = item.DisplayName,
                        Width = item.CurrentWidth,
                        Height = item.CurrentHeight,
                        Weight = item.Weight,
                        Description = item.Description,
                        X = item.X,
                        Y = item.Y,
                        Rotated = item.Rotated,
                        ThemeColor = item.ThemeColor
                    });
                }

                data.Containers.Add(containerData);
            }

            return data;
        }

        public void LoadFromSaveData(StorageMemorySaveData data)
        {
            containers.Clear();
            if (data == null || data.Containers == null)
            {
                return;
            }

            for (int i = 0; i < data.Containers.Count; i++)
            {
                StorageContainerSaveData containerData = data.Containers[i];
                if (containerData == null || string.IsNullOrWhiteSpace(containerData.Id))
                {
                    continue;
                }

                StorageContainerModel container = GetOrCreateContainer(
                    containerData.Id,
                    containerData.DisplayName,
                    containerData.Columns,
                    containerData.Rows,
                    containerData.MaxWeight);

                container.Items.Clear();
                if (containerData.Items == null)
                {
                    continue;
                }

                for (int itemIndex = 0; itemIndex < containerData.Items.Count; itemIndex++)
                {
                    StorageItemSaveData itemData = containerData.Items[itemIndex];
                    StorageItemModel item = CreateItemFromSaveData(itemData);
                    if (item == null)
                    {
                        continue;
                    }

                    if (!container.PlaceItem(item, itemData.X, itemData.Y))
                    {
                        Debug.LogWarning("Storage memory load skipped item '" + item.DisplayName + "' because its saved position is invalid.");
                    }
                }
            }
        }

        public string ToJson(bool prettyPrint = false)
        {
            return JsonUtility.ToJson(ToSaveData(), prettyPrint);
        }

        public void LoadFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            LoadFromSaveData(JsonUtility.FromJson<StorageMemorySaveData>(json));
        }

        public void SaveToPlayerPrefs()
        {
            PlayerPrefs.SetString(PlayerPrefsKey, ToJson(false));
            PlayerPrefs.Save();
        }

        public bool LoadFromPlayerPrefs()
        {
            if (!PlayerPrefs.HasKey(PlayerPrefsKey))
            {
                return false;
            }

            LoadFromJson(PlayerPrefs.GetString(PlayerPrefsKey));
            return true;
        }

        private StorageItemModel CreateItemFromSaveData(StorageItemSaveData itemData)
        {
            if (itemData == null)
            {
                return null;
            }

            StorageItemModel item = null;
            if (ItemRegistry != null && !string.IsNullOrWhiteSpace(itemData.DefinitionId))
            {
                item = ItemRegistry.CreateItem(itemData.DefinitionId, itemData.InstanceId);
            }

            if (item == null)
            {
                item = new StorageItemModel
                {
                    Id = itemData.InstanceId,
                    InstanceId = itemData.InstanceId,
                    DefinitionId = itemData.DefinitionId
                };
            }

            item.DisplayName = itemData.DisplayName;
            item.Width = Mathf.Max(1, itemData.Width);
            item.Height = Mathf.Max(1, itemData.Height);
            item.Weight = itemData.Weight;
            item.Description = itemData.Description;
            item.Rotated = itemData.Rotated;
            item.ThemeColor = itemData.ThemeColor;
            return item;
        }
    }
}
