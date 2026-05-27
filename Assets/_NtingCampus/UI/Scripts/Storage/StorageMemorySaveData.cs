using System;
using System.Collections.Generic;
using UnityEngine;
using NtingCampus.UI.Runtime.Gameplay;

namespace Nting.Storage
{
    [Serializable]
    public sealed class StorageMemorySaveData
    {
        public List<StorageContainerSaveData> Containers = new List<StorageContainerSaveData>();
    }

    [Serializable]
    public sealed class StorageContainerSaveData
    {
        public string Id;
        public string DisplayName;
        public CampusLocalizedText LocalizedDisplayName;
        public int Columns;
        public int Rows;
        public float MaxWeight;
        public StorageContainerAccessPolicy AccessPolicy;
        public string OwnerId;
        public string OwnerRole;
        public string RoomId;
        public bool AllowTakingContents = true;
        public bool IsPlayerCarried;
        public bool IsSingleItemSlot;
        public int SuspicionRisk;
        public List<StorageItemSaveData> Items = new List<StorageItemSaveData>();
    }

    [Serializable]
    public sealed class StorageItemSaveData
    {
        public string DefinitionId;
        public string InstanceId;
        public string DisplayName;
        public CampusLocalizedText LocalizedDisplayName;
        public int Width;
        public int Height;
        public string StackGroupId;
        public int MaxStackSize;
        public string StackId;
        public float Weight;
        public int Price;
        public int SmellLevel;
        public int EvidenceWeight;
        public bool CanPrankUse;
        public string Description;
        public CampusLocalizedText LocalizedDescription;
        public int X;
        public int Y;
        public bool Rotated;
        public Color ThemeColor;
        public bool IsUsable;
        public string UseActionId;
        public bool ConsumeOnUse;
        public float StaminaRestore;
        public string UseText;
        public CampusLocalizedText LocalizedUseText;
        public StorageItemLegalState LegalState;
        public string OwnerId;
        public string SourceContainerId;
        public string SourceRoomId;
        public string SourceLocation;
        public bool AllowTaking = true;
        public bool StolenDuringSession;
        public int SuspicionRisk;
    }
}

