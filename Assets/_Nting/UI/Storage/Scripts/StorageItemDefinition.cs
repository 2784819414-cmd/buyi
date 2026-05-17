using UnityEngine;

namespace Nting.Storage
{
    [CreateAssetMenu(menuName = "Nting/Storage/Item Definition", fileName = "StorageItemDefinition")]
    public sealed class StorageItemDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        public int Width = 1;
        public int Height = 1;
        public float Weight;
        [TextArea]
        public string Description;
        public Color ThemeColor = new Color(0.38f, 0.49f, 0.56f, 1f);
        public Sprite Icon;
        public bool IsUsable;
        public string UseActionId;
        public bool ConsumeOnUse = true;
        public string UseText;

        public StorageItemModel CreateItem(string instanceId = null)
        {
            string resolvedDefinitionId = string.IsNullOrWhiteSpace(Id) ? name : Id;
            string resolvedInstanceId = string.IsNullOrWhiteSpace(instanceId)
                ? resolvedDefinitionId + "_" + System.Guid.NewGuid().ToString("N")
                : instanceId;

            return new StorageItemModel
            {
                Id = resolvedInstanceId,
                DefinitionId = resolvedDefinitionId,
                InstanceId = resolvedInstanceId,
                DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? resolvedDefinitionId : DisplayName,
                Width = Mathf.Max(1, Width),
                Height = Mathf.Max(1, Height),
                Weight = Weight,
                Description = Description,
                ThemeColor = ThemeColor,
                Icon = Icon,
                IsUsable = IsUsable,
                UseActionId = UseActionId,
                ConsumeOnUse = ConsumeOnUse,
                UseText = UseText
            };
        }
    }
}
