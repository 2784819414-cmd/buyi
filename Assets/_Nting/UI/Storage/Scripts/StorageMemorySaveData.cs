using System;
using System.Collections.Generic;
using UnityEngine;

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
        public int Columns;
        public int Rows;
        public float MaxWeight;
        public StorageContainerAccessPolicy AccessPolicy;
        public string OwnerId;
        public string OwnerRole;
        public string RoomId;
        public bool AllowTakingContents = true;
        public bool IsPlayerCarried;
        public int SuspicionRisk;
        public List<StorageItemSaveData> Items = new List<StorageItemSaveData>();
    }

    [Serializable]
    public sealed class StorageItemSaveData
    {
        public string DefinitionId;
        public string InstanceId;
        public string DisplayName;
        public int Width;
        public int Height;
        public float Weight;
        public string Description;
        public int X;
        public int Y;
        public bool Rotated;
        public Color ThemeColor;
        public bool IsUsable;
        public string UseActionId;
        public bool ConsumeOnUse;
        public string UseText;
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
