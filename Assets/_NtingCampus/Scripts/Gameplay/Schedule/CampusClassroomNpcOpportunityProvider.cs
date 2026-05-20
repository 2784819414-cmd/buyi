using System.Collections.Generic;
using NtingCampus.Gameplay.Characters;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Rooms;
using UnityEngine;

namespace NtingCampus.Gameplay.Schedule
{
    internal sealed class CampusClassroomNpcOpportunityProvider : ICampusNpcActionOpportunityProvider
    {
        public static CampusClassroomNpcOpportunityProvider Instance { get; } =
            new CampusClassroomNpcOpportunityProvider();

        public string ProviderId => "classroom";

        private CampusClassroomNpcOpportunityProvider()
        {
        }

        public bool CanCollect(CampusNpcOpportunityContext npc, CampusNpcOpportunityQuery query)
        {
            return npc.IsValid &&
                   query.Purpose == CampusNpcOpportunityPurpose.Required &&
                   npc.Data.Role == CampusCharacterRole.Student &&
                   !npc.Data.IsPlayerControlled;
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

            CampusClassroomLoopService classroom = ResolveClassroomLoopService(npc);
            if (classroom == null ||
                !ShouldDozeThisDecision(npc, classroom) ||
                !classroom.CanStudentDozeOff(npc.Runtime, false))
            {
                return;
            }

            CampusGameplayRoom room = classroom.Facts.ResolveRuntimeRoom(npc.Runtime);
            results.Add(new CampusNpcActionOpportunity(
                "classroom_doze_off",
                CampusCharacterAction.RunCommand(new CampusClassroomDozeActionCommand(classroom)),
                npc.Position,
                room != null ? room.RoomId : string.Empty,
                0.12f,
                ResolveScore(npc),
                CampusNpcIntentKind.DozeInClass,
                "ClassDoze",
                false,
                0.8f,
                actor => actor != null && classroom.CanStudentDozeOff(actor, false)));
        }

        private static CampusClassroomLoopService ResolveClassroomLoopService(CampusNpcOpportunityContext npc)
        {
            if (npc.Bootstrap != null && npc.Bootstrap.ClassroomLoopService != null)
            {
                return npc.Bootstrap.ClassroomLoopService;
            }

            CampusGameBootstrap bootstrap = CampusGameBootstrap.Instance;
            if (bootstrap != null && bootstrap.ClassroomLoopService != null)
            {
                return bootstrap.ClassroomLoopService;
            }

            return Object.FindFirstObjectByType<CampusClassroomLoopService>(FindObjectsInactive.Include);
        }

        private static bool ShouldDozeThisDecision(
            CampusNpcOpportunityContext npc,
            CampusClassroomLoopService classroom)
        {
            if (!npc.IsValid ||
                classroom == null ||
                !classroom.CanStudentDozeOff(npc.Runtime, false))
            {
                return false;
            }

            int day = npc.Bootstrap != null && npc.Bootstrap.GameState != null
                ? npc.Bootstrap.GameState.Day
                : 0;
            int chance = Mathf.Clamp((npc.Data.Sleepiness - 50) * 2, 8, 70);
            if (npc.Data.HasTrait(CampusCharacterTrait.Sleepyhead))
            {
                chance += 18;
            }

            if (npc.Data.HasTrait(CampusCharacterTrait.GoodStudent))
            {
                chance -= 14;
            }

            int roll = CampusNpcStableIds.PositiveModulo(
                CampusNpcStableIds.Hash(npc.Data.Id + ":classroom_doze:" + day + ":" + npc.Segment),
                100);
            return roll < Mathf.Clamp(chance, 0, 90);
        }

        private static float ResolveScore(CampusNpcOpportunityContext npc)
        {
            float score = 86f + Mathf.Clamp((npc.Data.Sleepiness - 50) * 0.2f, 0f, 12f);
            if (npc.Data.HasTrait(CampusCharacterTrait.Sleepyhead))
            {
                score += 6f;
            }

            return score;
        }
    }
}
