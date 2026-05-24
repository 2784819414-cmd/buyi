using UnityEngine;

namespace Nting.Storage
{
    public static class StorageMemoryPersistence
    {
        private const string PlayerPrefsKey = "Nting.Storage.Memory";

        public static void SaveToPlayerPrefs(StorageMemory memory)
        {
            if (memory == null)
            {
                return;
            }

            PlayerPrefs.SetString(PlayerPrefsKey, memory.ToJson(false));
            PlayerPrefs.Save();
        }

        public static bool LoadFromPlayerPrefs(StorageMemory memory)
        {
            if (memory == null || !PlayerPrefs.HasKey(PlayerPrefsKey))
            {
                return false;
            }

            memory.LoadFromJson(PlayerPrefs.GetString(PlayerPrefsKey));
            return true;
        }
    }
}
