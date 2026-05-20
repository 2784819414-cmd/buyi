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
                                  LegalState == StorageItemLegalState.Suspicious ||
                                  StolenDuringSession;

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
            OwnerId = string.IsNullOrWhiteSpace(OwnerId) ? ownerId : OwnerId;
            SourceContainerId = source != null ? source.Id : SourceContainerId;
            SourceRoomId = string.IsNullOrWhiteSpace(roomId) ? SourceRoomId : roomId;
            SourceLocation = !string.IsNullOrWhiteSpace(sourceLocation)
                ? sourceLocation
                : source != null && !string.IsNullOrWhiteSpace(source.DisplayName)
                    ? source.DisplayName
                    : SourceLocation;
            SuspicionRisk = Mathf.Max(SuspicionRisk, suspicionRisk);
        }

        public void MarkAsSuspicious()
        {
            AllowTaking = false;
            if (LegalState == StorageItemLegalState.Unknown || LegalState == StorageItemLegalState.Personal)
            {
                LegalState = StorageItemLegalState.Suspicious;
            }
        }

        public void MarkEvidenceDestroyed()
        {
            LegalState = StorageItemLegalState.EvidenceDestroyed;
        }
    }
}
