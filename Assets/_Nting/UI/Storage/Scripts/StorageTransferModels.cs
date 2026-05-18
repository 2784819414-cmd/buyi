using System;
using UnityEngine;

namespace Nting.Storage
{
    public enum StorageItemLegalState
    {
        Unknown = 0,
        Personal = 1,
        Public = 2,
        Suspicious = 3,
        Stolen = 4,
        EvidenceDestroyed = 5
    }

    public enum StorageContainerAccessPolicy
    {
        Open = 0,
        PlayerCarried = 1,
        OwnedPrivate = 2,
        ProtectedPublic = 3,
        StaffOnly = 4,
        Commerce = 5,
        Ground = 6
    }

    public enum StorageTransferReason
    {
        Move = 0,
        QuickTransfer = 1,
        DropToGround = 2,
        UseItem = 3,
        Pickup = 4,
        PrankTheft = 5,
        SystemSeed = 6,
        InspectionConfiscation = 7
    }

    public sealed class StorageTransferContext
    {
        public GameObject Actor;
        public string ActorId;
        public string RoomId;
        public StorageTransferReason Reason = StorageTransferReason.Move;
        public bool ForceIllegal;
        public bool SuppressNpcDetection;
        public bool SuppressSuspicion;
        public bool AllowProtectedTake;
        public int SuspicionRiskOverride = -1;
        public string SourceLocation;
        public string OwnerId;

        public static StorageTransferContext ForActor(GameObject actor, StorageTransferReason reason)
        {
            return new StorageTransferContext
            {
                Actor = actor,
                Reason = reason
            };
        }
    }

    public readonly struct StorageTransferResult
    {
        public StorageTransferResult(
            bool succeeded,
            bool illegal,
            bool observed,
            string message,
            string witnessId)
        {
            Succeeded = succeeded;
            Illegal = illegal;
            Observed = observed;
            Message = message ?? string.Empty;
            WitnessId = witnessId ?? string.Empty;
        }

        public bool Succeeded { get; }
        public bool Illegal { get; }
        public bool Observed { get; }
        public string Message { get; }
        public string WitnessId { get; }

        public static StorageTransferResult Fail(string message)
        {
            return new StorageTransferResult(false, false, false, message, string.Empty);
        }
    }
}
