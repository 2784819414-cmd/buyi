using System;
using UnityEngine;
using NtingCampusMapEditor;

namespace NtingCampus.Gameplay.Skeleton
{
    public enum CampusSkeletonActorRole
    {
        None = 0,
        ShopClerk = 1,
        CanteenClerk = 2,
        LibrarianZhuzi = 3,
        LibraryTeacher = 4,
        DeliveryOwnerStudent = 5,
        SkewerBoss = 6
    }

    public static class CampusSkeletonActorStates
    {
        public const string Idle = "Idle";
        public const string Busy = "Busy";
        public const string SortingBooks = "SortingBooks";
    }

    [DisallowMultipleComponent]
    public sealed class CampusSkeletonActor : MonoBehaviour
    {
        public string ActorName;
        public CampusSkeletonActorRole Role;
        public string State = CampusSkeletonActorStates.Idle;
        [Min(0)] public int Alertness;

        private void Awake()
        {
            EnsureShadowCasterProfile();
        }

        private void Reset()
        {
            EnsureShadowCasterProfile();
        }

        public void Configure(string actorName, CampusSkeletonActorRole role, string state, int alertness)
        {
            ActorName = string.IsNullOrWhiteSpace(actorName) ? gameObject.name : actorName.Trim();
            Role = role;
            State = string.IsNullOrWhiteSpace(state) ? CampusSkeletonActorStates.Idle : state.Trim();
            Alertness = Mathf.Max(0, alertness);
            gameObject.name = ActorName;
        }

        public bool HasState(string expectedState)
        {
            return string.Equals(State, expectedState, StringComparison.OrdinalIgnoreCase);
        }

        private void OnValidate()
        {
            Alertness = Mathf.Max(0, Alertness);
            if (string.IsNullOrWhiteSpace(State))
            {
                State = CampusSkeletonActorStates.Idle;
            }
        }

        private void EnsureShadowCasterProfile()
        {
            NtingShadowCasterProfile profile = NtingShadowCasterProfile.EnsureForObject(gameObject);
            if (profile == null)
            {
                return;
            }

            profile.ApplyCharacterDefaults();
            profile.castCustomShadows = true;
            profile.castPointLightShadows = true;
            profile.castSunShadow = true;
        }
    }
}
