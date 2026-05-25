using System.Collections.Generic;
using NtingCampus.Gameplay.Core;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    internal static partial class CampusNpcEcologyPresetCatalog
    {
        public static bool TryResolveScheduledIntent(CampusNpcAiRuntime npc, out CampusNpcIntent intent)
        {
            intent = null;
            if (npc == null || npc.Runtime == null || npc.Data == null)
            {
                return false;
            }

            SelectedEntry best = default;
            bool hasBest = false;
            IReadOnlyList<NpcDecisionProfileRecord> profiles = Data.NpcDecisionProfiles;
            for (int profileIndex = 0; profileIndex < profiles.Count; profileIndex++)
            {
                NpcDecisionProfileRecord profile = profiles[profileIndex];
                if (!MatchesProfile(profile, npc))
                {
                    continue;
                }

                for (int entryIndex = 0; entryIndex < profile.Entries.Count; entryIndex++)
                {
                    ScheduleEntryRecord entry = profile.Entries[entryIndex];
                    if (!MatchesEntry(entry, npc) ||
                        !TryResolveActionChain(entry, out ActionChainRecord actionChain) ||
                        !TryResolveCurrentAction(npc, entry, actionChain, out int stepIndex, out ActionRecord action) ||
                        IsActionRepeatBlocked(npc, entry, actionChain, stepIndex, action) ||
                        !TryBuildChainActionOpportunity(npc, entry, actionChain, stepIndex, action, out CampusNpcActionOpportunity opportunity))
                    {
                        continue;
                    }

                    float finalScore = CalculateOpportunityScore(
                        npc,
                        profile,
                        entry,
                        action,
                        opportunity);
                    if (!hasBest || finalScore > best.Score)
                    {
                        best = new SelectedEntry(profile, entry, actionChain, action, stepIndex, opportunity, finalScore);
                        hasBest = true;
                    }
                }
            }

            if (!hasBest)
            {
                return false;
            }

            npc.BeginActionChainStep(best.Entry.Id, best.ActionChain.Id, best.StepIndex);
            intent = best.Opportunity.ToIntent();
            LogSelection(npc, best);
            return true;
        }

        private static bool MatchesProfile(NpcDecisionProfileRecord profile, CampusNpcAiRuntime npc)
        {
            CampusCharacterData data = npc.Data;
            if (profile == null || data == null || data.Role != profile.Role)
            {
                return false;
            }

            if (profile.CharacterIds.Length > 0 && !ContainsId(profile.CharacterIds, data.Id))
            {
                return false;
            }

            if (profile.TeacherDutyMask != CampusTeacherDuty.None &&
                (data.TeacherDuty & profile.TeacherDutyMask) == 0)
            {
                return false;
            }

            if (profile.StaffDutyMask != CampusStaffDuty.None &&
                (data.StaffDuty & profile.StaffDutyMask) == 0)
            {
                return false;
            }

            for (int i = 0; i < profile.Traits.Length; i++)
            {
                if (!data.HasTrait(profile.Traits[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool MatchesEntry(ScheduleEntryRecord entry, CampusNpcAiRuntime npc)
        {
            if (entry == null || npc == null)
            {
                return false;
            }

            if (entry.Segments.Length > 0 && !ContainsSegment(entry.Segments, npc.Segment))
            {
                return false;
            }

            if (entry.ScheduleWindows.Length > 0 && !MatchesAnyScheduleWindow(entry.ScheduleWindows, npc.Segment))
            {
                return false;
            }

            if (entry.HasClockRange &&
                !IsWithinClockRange(npc.CurrentClockMinute, entry.StartMinute, entry.EndMinute))
            {
                return false;
            }

            return true;
        }

        private static bool MatchesAnyScheduleWindow(
            CampusNpcEcologyScheduleWindow[] windows,
            CampusTimeSegment segment)
        {
            for (int i = 0; i < windows.Length; i++)
            {
                if (MatchesScheduleWindow(windows[i], segment))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesScheduleWindow(CampusNpcEcologyScheduleWindow window, CampusTimeSegment segment)
        {
            switch (window)
            {
                case CampusNpcEcologyScheduleWindow.Always:
                    return true;
                case CampusNpcEcologyScheduleWindow.ClassSession:
                    return CampusNpcScheduleFacts.IsClassSession(segment);
                case CampusNpcEcologyScheduleWindow.Break:
                    return CampusNpcScheduleFacts.IsBreak(segment);
                case CampusNpcEcologyScheduleWindow.MealPeak:
                    return CampusNpcScheduleFacts.IsMealPeak(segment);
                case CampusNpcEcologyScheduleWindow.StudentFreeMovement:
                    return CampusNpcScheduleFacts.IsStudentFreeMovementWindow(segment);
                case CampusNpcEcologyScheduleWindow.DormWindow:
                    return CampusNpcScheduleFacts.IsDormWindow(segment);
                case CampusNpcEcologyScheduleWindow.TeacherOfficeWindow:
                    return CampusNpcScheduleFacts.IsTeacherOfficeWindow(segment);
                case CampusNpcEcologyScheduleWindow.StaffOffDuty:
                    return CampusNpcScheduleFacts.IsStaffOffDuty(segment);
                default:
                    return false;
            }
        }

        private static bool ContainsSegment(CampusTimeSegment[] segments, CampusTimeSegment segment)
        {
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] == segment)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsWithinClockRange(int currentMinute, int startMinute, int endMinute)
        {
            if (startMinute == endMinute)
            {
                return true;
            }

            return startMinute < endMinute
                ? currentMinute >= startMinute && currentMinute < endMinute
                : currentMinute >= startMinute || currentMinute < endMinute;
        }

        private static bool TryResolveActionChain(
            ScheduleEntryRecord entry,
            out ActionChainRecord actionChain)
        {
            actionChain = null;
            if (entry == null || string.IsNullOrEmpty(entry.ActionChainId))
            {
                return false;
            }

            return Data.ActionChains.TryGetValue(entry.ActionChainId, out actionChain);
        }

        private static bool TryResolveCurrentAction(
            CampusNpcAiRuntime npc,
            ScheduleEntryRecord entry,
            ActionChainRecord actionChain,
            out int stepIndex,
            out ActionRecord action)
        {
            stepIndex = 0;
            action = null;
            if (entry == null || actionChain == null || actionChain.ActionIds.Length == 0)
            {
                return false;
            }

            if (npc != null &&
                npc.TryGetActiveActionChainStep(entry.Id, actionChain.Id, out int activeStepIndex) &&
                activeStepIndex >= 0 &&
                activeStepIndex < actionChain.ActionIds.Length)
            {
                stepIndex = activeStepIndex;
            }

            return Data.ActionCatalog.TryGetValue(actionChain.ActionIds[stepIndex], out action);
        }

        private static bool TryBuildChainActionOpportunity(
            CampusNpcAiRuntime npc,
            ScheduleEntryRecord entry,
            ActionChainRecord actionChain,
            int stepIndex,
            ActionRecord action,
            out CampusNpcActionOpportunity opportunity)
        {
            if (TryBuildOpportunity(npc, entry, actionChain, action, out opportunity))
            {
                bool advancesChain = opportunity.Action != null &&
                                     (opportunity.Action.Kind != CampusCharacterActionKind.NoOp ||
                                      action.ActionMode == CampusNpcEcologyActionMode.NoOp);
                opportunity.AssignActionChain(
                    entry.Id,
                    actionChain.Id,
                    stepIndex,
                    actionChain.ActionIds.Length,
                    advancesChain,
                    BuildActionCompletionKey(entry, actionChain, stepIndex, action));
                return true;
            }

            if (npc != null && npc.IsActiveActionChain(entry.Id, actionChain.Id))
            {
                npc.ClearActionChainProgress();
            }

            return false;
        }

        private static bool IsActionRepeatBlocked(
            CampusNpcAiRuntime npc,
            ScheduleEntryRecord entry,
            ActionChainRecord actionChain,
            int stepIndex,
            ActionRecord action)
        {
            if (npc == null ||
                entry == null ||
                actionChain == null ||
                action == null ||
                action.RepeatPolicy == CampusNpcEcologyActionRepeatPolicy.Always ||
                npc.IsActiveActionChain(entry.Id, actionChain.Id))
            {
                return false;
            }

            return npc.HasCompletedActionStep(BuildActionCompletionKey(entry, actionChain, stepIndex, action));
        }

        private static string BuildActionCompletionKey(
            ScheduleEntryRecord entry,
            ActionChainRecord actionChain,
            int stepIndex,
            ActionRecord action)
        {
            string entryId = NormalizeId(entry != null ? entry.Id : string.Empty);
            string chainId = NormalizeId(actionChain != null ? actionChain.Id : string.Empty);
            string actionId = NormalizeId(action != null ? action.ActionId : string.Empty);
            return entryId + ":" + chainId + ":" + stepIndex + ":" + actionId;
        }

        private static void LogSelection(CampusNpcAiRuntime npc, SelectedEntry selected)
        {
            if (!EnableSelectionDebug ||
                npc == null ||
                selected.Profile == null ||
                selected.Entry == null ||
                selected.Action == null)
            {
                return;
            }

            Debug.Log(
                "[NpcSchedule] npc=" + (npc.Data != null ? npc.Data.Id : string.Empty) +
                " profile=" + selected.Profile.Id +
                " entry=" + selected.Entry.Id +
                " chain=" + selected.ActionChain.Id +
                " action=" + selected.Action.ActionId +
                " score=" + selected.Score.ToString("0.###"));
        }
    }
}
