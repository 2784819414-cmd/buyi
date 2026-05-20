using System;
using System.Collections.Generic;
using Nting.Storage;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
using UnityEngine;

namespace NtingCampus.Gameplay.Inventory
{
    internal enum CampusInspectionNpcOpportunityKind
    {
        None = 0,
        Search = 1,
        Question = 2,
        Report = 3
    }

    internal readonly struct CampusInspectionNpcOpportunity
    {
        private CampusInspectionNpcOpportunity(
            CampusInspectionNpcOpportunityKind kind,
            CampusCharacterRuntime target,
            CampusGameplayRoom room,
            float score)
        {
            Kind = kind;
            Target = target;
            TargetPosition = target != null ? target.transform.position : Vector3.zero;
            RoomId = room != null ? room.RoomId : string.Empty;
            Score = score;
        }

        public CampusInspectionNpcOpportunityKind Kind { get; }
        public CampusCharacterRuntime Target { get; }
        public Vector3 TargetPosition { get; }
        public string RoomId { get; }
        public float Score { get; }
        public bool IsValid => Kind != CampusInspectionNpcOpportunityKind.None && Target != null;
        public string ActionId => "inspection_" + Kind.ToString().ToLowerInvariant();

        public static CampusInspectionNpcOpportunity Search(
            CampusCharacterRuntime target,
            CampusGameplayRoom room,
            float score)
        {
            return new CampusInspectionNpcOpportunity(CampusInspectionNpcOpportunityKind.Search, target, room, score);
        }

        public static CampusInspectionNpcOpportunity Question(
            CampusCharacterRuntime target,
            CampusGameplayRoom room,
            float score)
        {
            return new CampusInspectionNpcOpportunity(CampusInspectionNpcOpportunityKind.Question, target, room, score);
        }

        public static CampusInspectionNpcOpportunity Report(
            CampusCharacterRuntime target,
            CampusGameplayRoom room,
            float score)
        {
            return new CampusInspectionNpcOpportunity(CampusInspectionNpcOpportunityKind.Report, target, room, score);
        }
    }

    internal sealed class CampusInspectionNpcOpportunityProvider : ICampusNpcActionOpportunityProvider
    {
        public static CampusInspectionNpcOpportunityProvider Instance { get; } =
            new CampusInspectionNpcOpportunityProvider();

        public string ProviderId => "inspection";

        private CampusInspectionNpcOpportunityProvider()
        {
        }

        public bool CanCollect(CampusNpcOpportunityContext npc, CampusNpcOpportunityQuery query)
        {
            return npc.IsValid &&
                   !npc.Data.IsPlayerControlled &&
                   (query.Purpose == CampusNpcOpportunityPurpose.FreeMovement ||
                    query.Purpose == CampusNpcOpportunityPurpose.Duty);
        }

        public void CollectOpportunities(
            CampusNpcOpportunityContext npc,
            CampusNpcOpportunityQuery query,
            List<CampusNpcActionOpportunity> results)
        {
            if (results == null || !CanCollect(npc, query))
            {
                return;
            }

            CampusInspectionService inspectionService = ResolveInspectionService(npc);
            CampusCharacterRuntime target = ResolveDefaultTarget(npc);
            if (inspectionService == null ||
                !inspectionService.TryBuildNpcProactiveOpportunity(
                    npc.Runtime,
                    target,
                    out CampusInspectionNpcOpportunity opportunity) ||
                !opportunity.IsValid)
            {
                return;
            }

            results.Add(new CampusNpcActionOpportunity(
                opportunity.ActionId,
                CampusCharacterAction.RunCommand(new CampusInspectionNpcActionCommand(inspectionService, target)),
                opportunity.TargetPosition,
                opportunity.RoomId,
                1.15f,
                opportunity.Score,
                CampusNpcIntentKind.Roam,
                opportunity.ActionId,
                actor => actor != null &&
                         inspectionService.TryBuildNpcProactiveOpportunity(actor, target, out _)));
        }

        private static CampusInspectionService ResolveInspectionService(CampusNpcOpportunityContext npc)
        {
            if (npc.Bootstrap != null && npc.Bootstrap.InspectionService != null)
            {
                return npc.Bootstrap.InspectionService;
            }

            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            if (bootstrap != null && bootstrap.InspectionService != null)
            {
                return bootstrap.InspectionService;
            }

            return UnityEngine.Object.FindFirstObjectByType<CampusInspectionService>(FindObjectsInactive.Include);
        }

        private static CampusCharacterRuntime ResolveDefaultTarget(CampusNpcOpportunityContext npc)
        {
            if (npc.RosterService != null)
            {
                return npc.RosterService.PlayerRuntime;
            }

            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            return bootstrap != null && bootstrap.RosterService != null
                ? bootstrap.RosterService.PlayerRuntime
                : null;
        }
    }

    internal sealed class CampusInspectionNpcActionCommand : ICampusCharacterActionCommand
    {
        private readonly CampusInspectionService inspectionService;
        private readonly CampusCharacterRuntime target;

        public CampusInspectionNpcActionCommand(
            CampusInspectionService inspectionService,
            CampusCharacterRuntime target)
        {
            this.inspectionService = inspectionService;
            this.target = target;
        }

        public bool TryExecute(CampusCharacterRuntime actor, out StorageTransferResult result)
        {
            result = StorageTransferResult.Fail(string.Empty);
            if (inspectionService == null || actor == null)
            {
                return false;
            }

            bool succeeded = inspectionService.TryNpcProactiveInspection(actor, target, out string line);
            result = succeeded
                ? new StorageTransferResult(true, false, false, line, string.Empty)
                : StorageTransferResult.Fail(line);
            return succeeded;
        }
    }
}
