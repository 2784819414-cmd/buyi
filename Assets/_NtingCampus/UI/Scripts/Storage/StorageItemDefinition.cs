using UnityEngine;
using NtingCampus.UI.Runtime.Gameplay;

namespace Nting.Storage
{
    [CreateAssetMenu(menuName = "Nting/Storage/Item Definition", fileName = "StorageItemDefinition")]
    public sealed class StorageItemDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        public CampusLocalizedText LocalizedDisplayName;
        public int Width = 1;
        public int Height = 1;
        public string StackGroupId;
        [Range(1, StorageItemStackingService.MaxSupportedStackSize)]
        public int MaxStackSize = 1;
        public float Weight;
        [Min(0)] public int Price;
        [Min(0)] public int SmellLevel;
        [Min(0)] public int EvidenceWeight;
        public bool CanPrankUse;
        [TextArea]
        public string Description;
        public CampusLocalizedText LocalizedDescription;
        public Color ThemeColor = new Color(0.38f, 0.49f, 0.56f, 1f);
        public Sprite Icon;
        public bool IsUsable;
        public string UseActionId;
        public bool ConsumeOnUse = true;
        [Min(0f)] public float StaminaRestore;
        public string UseText;
        public CampusLocalizedText LocalizedUseText;

        public string ResolveId()
        {
            return string.IsNullOrWhiteSpace(Id) ? name : Id.Trim();
        }

        public StorageItemModel CreateItem(string instanceId = null)
        {
            StorageItemModel item = new StorageItemModel();
            item.ApplyDefinition(this, instanceId);
            return item;
        }

        public string ResolveDisplayName(CampusDisplayLanguage language)
        {
            return LocalizedDisplayName.Get(language, DisplayName, ResolveId());
        }

        public string ResolveDescription(CampusDisplayLanguage language)
        {
            return LocalizedDescription.Get(language, Description);
        }

        public string ResolveUseText(CampusDisplayLanguage language)
        {
            return LocalizedUseText.Get(language, UseText);
        }
    }
}

