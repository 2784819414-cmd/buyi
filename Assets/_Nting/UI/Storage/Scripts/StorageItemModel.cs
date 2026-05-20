using System;
using UnityEngine;
using NtingCampus.Gameplay.UI;

namespace Nting.Storage
{
    [Serializable]
    public sealed class StorageItemModel
    {
        [Header("Identity")]
        public string Id;
        public string DefinitionId;
        public string InstanceId;

        [Header("Definition Snapshot")]
        public string DisplayName;
        public CampusLocalizedText LocalizedDisplayName;
        public int Width = 1;
        public int Height = 1;
        public float Weight;
        [TextArea]
        public string Description;
        public CampusLocalizedText LocalizedDescription;
        public Color ThemeColor = new Color(0.38f, 0.49f, 0.56f, 1f);
        public Sprite Icon;

        [Header("Use")]
        public bool IsUsable;
        public string UseActionId;
        public bool ConsumeOnUse = true;
        public string UseText;
        public CampusLocalizedText LocalizedUseText;

        [Header("Placement")]
        public int X;
        public int Y;
        public bool Rotated;

        [Header("Campus Evidence")]
        public StorageItemEvidenceState Evidence = new StorageItemEvidenceState();

        [NonSerialized]
        public StorageContainerModel CurrentContainer;

        public string CurrentContainerId => CurrentContainer != null ? CurrentContainer.Id : string.Empty;

        public int CurrentWidth => Mathf.Max(1, Width);

        public int CurrentHeight => Mathf.Max(1, Height);

        public bool IsStolenEvidence => Evidence != null && Evidence.IsEvidence;

        public StorageItemLegalState LegalState
        {
            get => Evidence != null ? Evidence.LegalState : StorageItemLegalState.Personal;
            set => EnsureEvidence().LegalState = value;
        }

        public string OwnerId
        {
            get => Evidence != null ? Evidence.OwnerId : string.Empty;
            set => EnsureEvidence().OwnerId = value;
        }

        public string SourceContainerId
        {
            get => Evidence != null ? Evidence.SourceContainerId : string.Empty;
            set => EnsureEvidence().SourceContainerId = value;
        }

        public string SourceRoomId
        {
            get => Evidence != null ? Evidence.SourceRoomId : string.Empty;
            set => EnsureEvidence().SourceRoomId = value;
        }

        public string SourceLocation
        {
            get => Evidence != null ? Evidence.SourceLocation : string.Empty;
            set => EnsureEvidence().SourceLocation = value;
        }

        public bool AllowTaking
        {
            get => Evidence == null || Evidence.AllowTaking;
            set => EnsureEvidence().AllowTaking = value;
        }

        public bool StolenDuringSession
        {
            get => Evidence != null && Evidence.StolenDuringSession;
            set => EnsureEvidence().StolenDuringSession = value;
        }

        public int SuspicionRisk
        {
            get => Evidence != null ? Evidence.SuspicionRisk : 0;
            set => EnsureEvidence().SuspicionRisk = Mathf.Max(0, value);
        }

        public void ApplyDefinition(StorageItemDefinition definition, string instanceId)
        {
            if (definition == null)
            {
                return;
            }

            string definitionId = definition.ResolveId();
            string resolvedInstanceId = string.IsNullOrWhiteSpace(instanceId)
                ? definitionId + "_" + Guid.NewGuid().ToString("N")
                : instanceId.Trim();

            Id = resolvedInstanceId;
            DefinitionId = definitionId;
            InstanceId = resolvedInstanceId;
            DisplayName = string.IsNullOrWhiteSpace(definition.DisplayName) ? definitionId : definition.DisplayName;
            LocalizedDisplayName = definition.LocalizedDisplayName;
            Width = Mathf.Max(1, definition.Width);
            Height = Mathf.Max(1, definition.Height);
            Weight = Mathf.Max(0f, definition.Weight);
            Description = definition.Description;
            LocalizedDescription = definition.LocalizedDescription;
            ThemeColor = definition.ThemeColor;
            Icon = StorageItemIconUtility.Resolve(definitionId, definition.Icon);
            IsUsable = definition.IsUsable;
            UseActionId = definition.UseActionId;
            ConsumeOnUse = definition.ConsumeOnUse;
            UseText = definition.UseText;
            LocalizedUseText = definition.LocalizedUseText;
            EnsureEvidence().ResetAsPersonal();
        }

        public string GetDisplayName(CampusDisplayLanguage language)
        {
            return LocalizedDisplayName.Get(language, DisplayName, DefinitionId, Id);
        }

        public string GetDisplayName()
        {
            return GetDisplayName(CampusLanguageState.CurrentLanguage);
        }

        public string GetDescription(CampusDisplayLanguage language)
        {
            return LocalizedDescription.Get(language, Description);
        }

        public string GetDescription()
        {
            return GetDescription(CampusLanguageState.CurrentLanguage);
        }

        public string GetUseText(CampusDisplayLanguage language)
        {
            return LocalizedUseText.Get(language, UseText);
        }

        public string GetUseText()
        {
            return GetUseText(CampusLanguageState.CurrentLanguage);
        }

        public void Rotate()
        {
            int previousWidth = Width;
            Width = Height;
            Height = previousWidth;
            Rotated = !Rotated;
        }

        public StorageItemModel CloneForPreview()
        {
            return (StorageItemModel)MemberwiseClone();
        }

        public StorageItemEvidenceState EnsureEvidence()
        {
            if (Evidence == null)
            {
                Evidence = new StorageItemEvidenceState();
            }

            return Evidence;
        }
    }
}
