using System.Collections.Generic;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Events;
using NtingCampus.Gameplay.Rooms;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    public enum CampusNpcOpportunityPurpose
    {
        Required = 0,
        FreeMovement = 1,
        Duty = 2
    }

    public readonly struct CampusNpcOpportunityQuery
    {
        public CampusNpcOpportunityQuery(CampusNpcOpportunityPurpose purpose)
        {
            Purpose = purpose;
        }

        public CampusNpcOpportunityPurpose Purpose { get; }

        public static CampusNpcOpportunityQuery Required()
        {
            return new CampusNpcOpportunityQuery(CampusNpcOpportunityPurpose.Required);
        }

        public static CampusNpcOpportunityQuery FreeMovement()
        {
            return new CampusNpcOpportunityQuery(CampusNpcOpportunityPurpose.FreeMovement);
        }

        public static CampusNpcOpportunityQuery Duty()
        {
            return new CampusNpcOpportunityQuery(CampusNpcOpportunityPurpose.Duty);
        }
    }

    public readonly struct CampusNpcOpportunityContext
    {
        private readonly CampusNpcAiRuntime runtime;

        internal CampusNpcOpportunityContext(CampusNpcAiRuntime runtime)
        {
            this.runtime = runtime;
        }

        public bool IsValid => runtime != null && runtime.Runtime != null && runtime.Data != null;
        public CampusCharacterRuntime Runtime => runtime != null ? runtime.Runtime : null;
        public CampusCharacterData Data => runtime != null ? runtime.Data : null;
        public CampusNpcPersonalProfile Profile => runtime != null ? runtime.Profile : null;
        public CampusNpcVisionProfile VisionProfile => runtime != null ? runtime.VisionProfile : null;
        public CampusGameBootstrap Bootstrap => runtime != null ? runtime.Bootstrap : null;
        public CampusWorldService WorldService => runtime != null ? runtime.WorldService : null;
        public CampusRosterService RosterService => runtime != null ? runtime.RosterService : null;
        public CampusGameplayEventHub EventHub => runtime != null ? runtime.EventHub : null;
        public CampusTimeSegment Segment => runtime != null ? runtime.Segment : CampusTimeSegment.MorningClass1;
        public float Time => runtime != null ? runtime.Time : UnityEngine.Time.time;
        public int PersonalSeed => runtime != null ? runtime.PersonalSeed : 1;
        public Vector3 Position => Runtime != null ? Runtime.transform.position : Vector3.zero;
        public bool HasMindState => runtime != null && runtime.Mind != null;
        public CampusNpcDeliveryState DeliveryState => HasMindState ? runtime.Mind.DeliveryState : CampusNpcDeliveryState.None;
        public float IntentHoldUntil => HasMindState ? runtime.Mind.IntentHoldUntil : -1f;
        public float DeliveryReadyAt => HasMindState ? runtime.Mind.DeliveryReadyAt : -1f;
        public float NextDeliveryOrderAllowedAt => HasMindState ? runtime.Mind.NextDeliveryOrderAllowedAt : -1f;
        public float NextStoreVisitAllowedAt => HasMindState ? runtime.Mind.NextStoreVisitAllowedAt : -1f;

        internal CampusNpcAiRuntime RuntimeState => runtime;
    }

    public interface ICampusNpcActionOpportunityProvider
    {
        string ProviderId { get; }
        bool CanCollect(CampusNpcOpportunityContext npc, CampusNpcOpportunityQuery query);
        void CollectOpportunities(
            CampusNpcOpportunityContext npc,
            CampusNpcOpportunityQuery query,
            List<CampusNpcActionOpportunity> results);
    }
}
