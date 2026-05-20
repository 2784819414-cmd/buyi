using System;
using System.Collections.Generic;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    internal sealed class CampusNpcEventFactAwareness
    {
        private const float ReadIntervalSeconds = 0.48f;
        private const int ReadFrameSlots = 17;

        private readonly List<CampusNpcEventFact> scratchFacts =
            new List<CampusNpcEventFact>(8);

        private int readFrameSlot = -1;
        private int lastSeenSerial;
        private bool initializedCursor;
        private float nextReadTime;

        public void Observe(CampusNpcAiRuntime npc)
        {
            CampusNpcEcologyService facts = npc != null && npc.Bootstrap != null
                ? npc.Bootstrap.NpcEcologyService
                : null;
            if (!CanRead(npc, facts))
            {
                return;
            }

            scratchFacts.Clear();
            lastSeenSerial = facts.CopyFactsAfter(lastSeenSerial, scratchFacts);
            for (int i = 0; i < scratchFacts.Count; i++)
            {
                CampusNpcEventFact fact = scratchFacts[i];
                if (CanNotice(npc, fact))
                {
                    ApplyPersonalReaction(npc, fact);
                    return;
                }
            }
        }

        private bool CanRead(CampusNpcAiRuntime npc, CampusNpcEcologyService facts)
        {
            if (npc == null || facts == null || npc.Data == null || npc.Runtime == null)
            {
                return false;
            }

            if (!initializedCursor)
            {
                lastSeenSerial = facts.LatestFactSerial;
                initializedCursor = true;
            }

            if (readFrameSlot < 0)
            {
                readFrameSlot = CampusNpcStableIds.PositiveModulo(npc.PersonalSeed * 11 + 3, ReadFrameSlots);
            }

            float now = Time.time;
            if (now < nextReadTime || Time.frameCount % ReadFrameSlots != readFrameSlot)
            {
                return false;
            }

            nextReadTime = now + ReadIntervalSeconds + ResolveReadOffset(npc.PersonalSeed + Mathf.FloorToInt(now * 23f));
            return true;
        }

        private static bool CanNotice(CampusNpcAiRuntime npc, CampusNpcEventFact fact)
        {
            if (npc == null || npc.Data == null || string.IsNullOrWhiteSpace(fact.FactId))
            {
                return false;
            }

            if (fact.FactId.StartsWith("time.", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (IsSameActor(npc.Runtime.CharacterId, fact.ActorId))
            {
                return false;
            }

            if (IsSameActor(npc.Runtime.CharacterId, fact.TargetId))
            {
                return true;
            }

            return string.IsNullOrWhiteSpace(fact.RoomId) ||
                   string.Equals(npc.Data.CurrentRoomId, fact.RoomId, StringComparison.OrdinalIgnoreCase);
        }

        private static void ApplyPersonalReaction(CampusNpcAiRuntime npc, CampusNpcEventFact fact)
        {
            CampusCharacterData data = npc.Data;
            if (data == null)
            {
                return;
            }

            string factId = fact.FactId ?? string.Empty;
            if (factId.StartsWith("prank.attempted.", StringComparison.OrdinalIgnoreCase))
            {
                ReactToPrankAttempt(data, fact.ActorId);
                return;
            }

            if (factId.StartsWith("prank.resolved.", StringComparison.OrdinalIgnoreCase))
            {
                ReactToPrankResolved(data, fact);
                return;
            }

            if (factId.StartsWith("sanction.issued.", StringComparison.OrdinalIgnoreCase))
            {
                ReactToSanction(data, fact.ActorId);
                return;
            }

            if (factId.StartsWith("classroom.student_dozed_off", StringComparison.OrdinalIgnoreCase))
            {
                ReactToDozing(data);
                return;
            }

            if (factId.StartsWith("classroom.teacher_distracted", StringComparison.OrdinalIgnoreCase))
            {
                ReactToTeacherDistraction(data);
                return;
            }

            if (factId.StartsWith("classroom.skip_class.", StringComparison.OrdinalIgnoreCase))
            {
                ReactToClassSkipping(data, fact);
                return;
            }

            if (factId.StartsWith("item.transfer.illegal.", StringComparison.OrdinalIgnoreCase))
            {
                ReactToIllegalItemMove(data, fact.ActorId);
                return;
            }

            if (factId.StartsWith("inventory.questioned.", StringComparison.OrdinalIgnoreCase) ||
                factId.StartsWith("inventory.contraband.found", StringComparison.OrdinalIgnoreCase))
            {
                ReactToInspectionFact(data, fact);
            }
        }

        private static void ReactToPrankAttempt(CampusCharacterData data, string actorId)
        {
            if (data.Role == CampusCharacterRole.Teacher || data.Role == CampusCharacterRole.Staff)
            {
                AddSuspicion(data, actorId, 4);
                AddMemoryIfMissing(data, CampusCharacterMemoryId.WitnessedActorPrank);
                return;
            }

            if (data.HasTrait(CampusCharacterTrait.Tattletale))
            {
                AddSuspicion(data, actorId, 5);
                AddMemoryIfMissing(data, CampusCharacterMemoryId.WarnedAboutActor);
                return;
            }

            if (data.HasTrait(CampusCharacterTrait.Troublemaker))
            {
                data.AddMood(1);
                AddTrust(data, actorId, 1);
            }
        }

        private static void ReactToPrankResolved(CampusCharacterData data, CampusNpcEventFact fact)
        {
            bool succeeded = fact.FactId.IndexOf(".success", StringComparison.OrdinalIgnoreCase) >= 0;
            bool detected = fact.FactId.IndexOf(".detected", StringComparison.OrdinalIgnoreCase) >= 0;
            if (IsSameActor(data.Id, fact.TargetId))
            {
                data.AddMood(succeeded ? -4 : -2);
                AddTrust(data, fact.ActorId, succeeded ? -6 : -2);
                AddSuspicion(data, fact.ActorId, detected ? 8 : 5);
                AddMemoryIfMissing(data, CampusCharacterMemoryId.DistrustsActor);
                return;
            }

            if (data.Role == CampusCharacterRole.Teacher || data.Role == CampusCharacterRole.Staff)
            {
                AddSuspicion(data, fact.ActorId, detected ? 8 : 4);
                data.AddMood(detected ? -1 : 0);
                return;
            }

            if (data.HasTrait(CampusCharacterTrait.Troublemaker) && succeeded)
            {
                AddTrust(data, fact.ActorId, detected ? 1 : 3);
                data.AddMood(detected ? 1 : 2);
                AddMemoryIfMissing(data, CampusCharacterMemoryId.ImpressedByActor);
                return;
            }

            if (data.HasTrait(CampusCharacterTrait.GoodStudent))
            {
                AddTrust(data, fact.ActorId, -2);
                AddSuspicion(data, fact.ActorId, 2);
                data.AddMood(-1);
                return;
            }

            if (data.HasTrait(CampusCharacterTrait.Tattletale))
            {
                AddSuspicion(data, fact.ActorId, detected ? 5 : 7);
                AddMemoryIfMissing(data, CampusCharacterMemoryId.WarnedAboutActor);
            }
        }

        private static void ReactToSanction(CampusCharacterData data, string actorId)
        {
            if (data.Role == CampusCharacterRole.Teacher)
            {
                data.AddMood(1);
            }
            else
            {
                data.AddMood(data.HasTrait(CampusCharacterTrait.Troublemaker) ? 1 : -2);
                data.AddSocialEnergy(-2);
            }

            AddSuspicion(data, actorId, 2);
            AddTrust(data, actorId, -1);
        }

        private static void ReactToDozing(CampusCharacterData data)
        {
            if (data.Role == CampusCharacterRole.Teacher)
            {
                data.AddMood(-1);
            }
            else if (data.HasTrait(CampusCharacterTrait.Troublemaker))
            {
                data.AddMood(1);
            }
            else if (data.HasTrait(CampusCharacterTrait.GoodStudent))
            {
                data.AddMood(-1);
            }
        }

        private static void ReactToTeacherDistraction(CampusCharacterData data)
        {
            if (data.Role != CampusCharacterRole.Student)
            {
                return;
            }

            if (data.HasTrait(CampusCharacterTrait.Troublemaker))
            {
                data.AddMood(1);
                data.AddSocialEnergy(1);
            }
            else if (data.HasTrait(CampusCharacterTrait.GoodStudent))
            {
                data.AddMood(-1);
            }
        }

        private static void ReactToClassSkipping(CampusCharacterData data, CampusNpcEventFact fact)
        {
            bool detected = fact.FactId.IndexOf(".detected", StringComparison.OrdinalIgnoreCase) >= 0;
            if (data.Role == CampusCharacterRole.Teacher)
            {
                AddSuspicion(data, fact.ActorId, detected ? 7 : 4);
                data.AddMood(detected ? 1 : -1);
                return;
            }

            if (detected)
            {
                AddTrust(data, fact.ActorId, -2);
                AddSuspicion(data, fact.ActorId, 3);
                data.AddMood(-1);
                return;
            }

            if (data.HasTrait(CampusCharacterTrait.Troublemaker))
            {
                AddTrust(data, fact.ActorId, 3);
                data.AddMood(2);
                AddMemoryIfMissing(data, CampusCharacterMemoryId.ImpressedByActor);
                return;
            }

            if (data.HasTrait(CampusCharacterTrait.Tattletale))
            {
                AddSuspicion(data, fact.ActorId, 5);
                AddMemoryIfMissing(data, CampusCharacterMemoryId.WarnedAboutActor);
            }
        }

        private static void ReactToIllegalItemMove(CampusCharacterData data, string actorId)
        {
            if (data.Role == CampusCharacterRole.Teacher ||
                data.Role == CampusCharacterRole.Staff ||
                data.HasTrait(CampusCharacterTrait.Tattletale))
            {
                AddSuspicion(data, actorId, 3);
            }
        }

        private static void ReactToInspectionFact(CampusCharacterData data, CampusNpcEventFact fact)
        {
            bool contraband = fact.FactId.IndexOf(".hit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              fact.FactId.EndsWith(".found", StringComparison.OrdinalIgnoreCase);
            if (IsSameActor(data.Id, fact.TargetId))
            {
                AddSuspicion(data, fact.ActorId, contraband ? 8 : 2);
                data.AddMood(contraband ? -1 : 1);
                return;
            }

            if (data.Role == CampusCharacterRole.Teacher || data.Role == CampusCharacterRole.Staff)
            {
                AddSuspicion(data, fact.ActorId, contraband ? 8 : 2);
                data.AddMood(contraband ? -1 : 0);
                return;
            }

            if (data.HasTrait(CampusCharacterTrait.Tattletale))
            {
                AddSuspicion(data, fact.ActorId, contraband ? 8 : 3);
                AddMemoryIfMissing(data, CampusCharacterMemoryId.WarnedAboutActor);
                return;
            }

            if (data.HasTrait(CampusCharacterTrait.GoodStudent))
            {
                AddSuspicion(data, fact.ActorId, contraband ? 4 : 1);
                AddTrust(data, fact.ActorId, contraband ? -3 : -1);
            }
        }

        private static void AddTrust(CampusCharacterData data, string actorId, int amount)
        {
            if (data != null && !string.IsNullOrWhiteSpace(actorId))
            {
                data.AddRelationshipTrust(actorId, amount);
            }
        }

        private static void AddSuspicion(CampusCharacterData data, string actorId, int amount)
        {
            if (data != null && !string.IsNullOrWhiteSpace(actorId))
            {
                data.AddRelationshipSuspicion(actorId, amount);
            }
        }

        private static void AddMemoryIfMissing(CampusCharacterData data, CampusCharacterMemoryId memory)
        {
            if (data == null || memory == CampusCharacterMemoryId.None || data.HasMemory(memory))
            {
                return;
            }

            data.AddMemory(memory);
        }

        private static bool IsSameActor(string left, string right)
        {
            return !string.IsNullOrWhiteSpace(left) &&
                   !string.IsNullOrWhiteSpace(right) &&
                   string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static float ResolveReadOffset(int seed)
        {
            return Mathf.Lerp(0.02f, 0.24f, CampusNpcStableIds.PositiveModulo(seed * 43, 100) / 99f);
        }
    }
}
