using System;
using UnityEngine;

namespace Nting.Storage
{
    [Serializable]
    public sealed class StorageItemEvidenceState
    {
        public StorageItemLegalState LegalState = StorageItemLegalState.Personal;
        public string OwnerId;
        public string SourceContainerId;
        public string SourceRoomId;
        public string SourceLocation;
        public bool AllowTaking = true;
        public bool StolenDuringSession;
        public int SuspicionRisk;

        public bool IsEvidence => LegalState == StorageItemLegalState.Stolen ||
                                  StolenDuringSession;

        public bool IsPendingProtectedTransfer =>
            LegalState == StorageItemLegalState.PendingProtectedTransfer &&
            !StolenDuringSession;

        // Legacy alias kept for older mod code.
        public bool IsUnclearedProtectedTransfer => IsPendingProtectedTransfer;

        public StorageItemEvidenceState Clone()
        {
            return new StorageItemEvidenceState
            {
                LegalState = LegalState,
                OwnerId = OwnerId,
                SourceContainerId = SourceContainerId,
                SourceRoomId = SourceRoomId,
                SourceLocation = SourceLocation,
                AllowTaking = AllowTaking,
                StolenDuringSession = StolenDuringSession,
                SuspicionRisk = SuspicionRisk
            };
        }

        public void CopyFrom(StorageItemEvidenceState state)
        {
            if (state == null)
            {
                ResetAsPersonal();
                return;
            }

            LegalState = state.LegalState;
            OwnerId = state.OwnerId;
            SourceContainerId = state.SourceContainerId;
            SourceRoomId = state.SourceRoomId;
            SourceLocation = state.SourceLocation;
            AllowTaking = state.AllowTaking;
            StolenDuringSession = state.StolenDuringSession;
            SuspicionRisk = state.SuspicionRisk;
        }

        public void ResetAsPersonal()
        {
            LegalState = StorageItemLegalState.Personal;
            AllowTaking = true;
            StolenDuringSession = false;
            OwnerId = string.Empty;
            SourceContainerId = string.Empty;
            SourceRoomId = string.Empty;
            SourceLocation = string.Empty;
            SuspicionRisk = 0;
        }

        public void MarkAsStolen(
            StorageContainerModel source,
            string roomId,
            string ownerId,
            string sourceLocation,
            int suspicionRisk)
        {
            LegalState = StorageItemLegalState.Stolen;
            AllowTaking = false;
            StolenDuringSession = true;
            OwnerId = string.IsNullOrWhiteSpace(OwnerId) ? NormalizeId(ownerId) : OwnerId;
            SourceContainerId = source != null ? NormalizeId(source.Id) : NormalizeId(SourceContainerId);
            SourceRoomId = string.IsNullOrWhiteSpace(roomId) ? NormalizeId(SourceRoomId) : NormalizeId(roomId);
            SourceLocation = !string.IsNullOrWhiteSpace(sourceLocation)
                ? sourceLocation.Trim()
                : source != null && !string.IsNullOrWhiteSpace(source.DisplayName)
                    ? source.DisplayName.Trim()
                    : NormalizeDisplayText(SourceLocation);
            SuspicionRisk = Mathf.Max(SuspicionRisk, suspicionRisk);
        }

        public void MarkAsPendingProtectedTransfer()
        {
            AllowTaking = true;
            if (LegalState == StorageItemLegalState.Unknown || LegalState == StorageItemLegalState.Personal)
            {
                LegalState = StorageItemLegalState.PendingProtectedTransfer;
            }
        }

        public void BeginProtectedTransferPending(
            string sourceContainerId,
            string sourceRoomId,
            string ownerId,
            string sourceLocation,
            int suspicionRisk)
        {
            LegalState = StorageItemLegalState.PendingProtectedTransfer;
            AllowTaking = true;
            StolenDuringSession = false;
            OwnerId = string.IsNullOrWhiteSpace(ownerId) ? OwnerId : ownerId.Trim();
            SourceContainerId = string.IsNullOrWhiteSpace(sourceContainerId) ? SourceContainerId : sourceContainerId.Trim();
            SourceRoomId = string.IsNullOrWhiteSpace(sourceRoomId) ? SourceRoomId : sourceRoomId.Trim();
            SourceLocation = string.IsNullOrWhiteSpace(sourceLocation) ? SourceLocation : sourceLocation.Trim();
            SuspicionRisk = Mathf.Max(SuspicionRisk, suspicionRisk);
        }

        public void ClearProtectedTransferPending(string ownerId)
        {
            LegalState = StorageItemLegalState.Personal;
            AllowTaking = true;
            StolenDuringSession = false;
            OwnerId = string.IsNullOrWhiteSpace(ownerId) ? string.Empty : ownerId.Trim();
            SourceContainerId = string.Empty;
            SourceRoomId = string.Empty;
            SourceLocation = string.Empty;
            SuspicionRisk = 0;
        }

        public void PromoteProtectedTransferToStolenEvidence(
            string fallbackOwnerId,
            int suspicionRisk)
        {
            LegalState = StorageItemLegalState.Stolen;
            AllowTaking = false;
            StolenDuringSession = true;
            OwnerId = string.IsNullOrWhiteSpace(OwnerId) ? NormalizeId(fallbackOwnerId) : NormalizeId(OwnerId);
            SourceContainerId = NormalizeId(SourceContainerId);
            SourceRoomId = NormalizeId(SourceRoomId);
            SourceLocation = NormalizeDisplayText(SourceLocation);
            SuspicionRisk = Mathf.Max(SuspicionRisk, suspicionRisk);
        }

        // Legacy compatibility wrappers for older mod code.
        public void MarkAsSuspicious()
        {
            MarkAsPendingProtectedTransfer();
        }

        public void MarkAsProtectedTransferPending(
            string sourceContainerId,
            string sourceRoomId,
            string ownerId,
            string sourceLocation,
            int suspicionRisk)
        {
            BeginProtectedTransferPending(
                sourceContainerId,
                sourceRoomId,
                ownerId,
                sourceLocation,
                suspicionRisk);
        }

        public void ClearProtectedTransfer(string ownerId)
        {
            ClearProtectedTransferPending(ownerId);
        }

        public void MarkProtectedTransferAsStolen(
            string fallbackOwnerId,
            int suspicionRisk)
        {
            PromoteProtectedTransferToStolenEvidence(fallbackOwnerId, suspicionRisk);
        }

        public void MarkEvidenceDestroyed()
        {
            LegalState = StorageItemLegalState.EvidenceDestroyed;
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string NormalizeDisplayText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
