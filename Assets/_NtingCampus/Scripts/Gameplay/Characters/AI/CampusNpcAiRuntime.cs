using System;
using System.Collections.Generic;
using NtingCampus.Gameplay.Core;
using NtingCampus.Gameplay.Events;
using NtingCampus.Gameplay.Rooms;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    internal sealed class CampusNpcAiRuntime
    {
        private const float SenseIntervalSeconds = 0.32f;
        private const int SenseFrameSlots = 11;

        private readonly Dictionary<CampusRoomType, CachedRoomTarget> roomTargets =
            new Dictionary<CampusRoomType, CachedRoomTarget>();
        private readonly CampusNpcEventFactAwareness eventFactAwareness = new CampusNpcEventFactAwareness();
        private readonly CampusNpcInventoryAwareness inventoryAwareness = new CampusNpcInventoryAwareness();

        private CampusCharacterRuntime runtime;
        private CampusGameBootstrap bootstrap;
        private CampusWorldService worldService;
        private CampusRosterService rosterService;
        private CampusTimeController timeController;
        private CampusGameplayEventHub eventHub;
        private Transform ownerTransform;
        private CampusCharacterFacingState facingState;
        private Func<int> resolvePersonalSeed;
        private Action<string, float, bool> speak;
        private float nextDecisionTime = -1f;
        private float nextSenseTime;
        private int senseFrameSlot;
        private string activeActionChainEntryId = string.Empty;
        private string activeActionChainId = string.Empty;
        private int activeActionChainStepIndex;

        public readonly CampusNpcNavigatorHandle Navigator = new CampusNpcNavigatorHandle();

        public CampusCharacterRuntime Runtime => runtime;
        public CampusWorldService WorldService => worldService;
        public CampusRosterService RosterService => rosterService;
        public CampusGameplayEventHub EventHub => eventHub;
        public CampusGameBootstrap Bootstrap => bootstrap;
        public CampusCharacterData Data => runtime != null ? runtime.Data : null;
        public CampusNpcPersonalProfile Profile { get; private set; }
        public CampusNpcVisionProfile VisionProfile { get; private set; } = new CampusNpcVisionProfile();
        public CampusCharacterFacingState FacingState => facingState;
        public CampusNpcMindState Mind { get; private set; }
        public CampusTimeSegment Segment { get; private set; } = CampusTimeSegment.MorningClass1;
        public int CurrentClockMinute => timeController != null
            ? Mathf.FloorToInt(Mathf.Repeat(timeController.CurrentGameHour * 60f, 24f * 60f))
            : CampusTimeSchedule.GetStartMinute(Segment);
        public float Time { get; private set; }
        public int PersonalSeed => resolvePersonalSeed != null ? resolvePersonalSeed() : 1;
        public bool IsDecisionDue => UnityEngine.Time.time >= nextDecisionTime;

        public void Bind(
            CampusCharacterRuntime targetRuntime,
            CampusGameBootstrap targetBootstrap,
            CampusWorldService targetWorldService,
            CampusRosterService targetRosterService,
            CampusTimeController targetTimeController,
            CampusGameplayEventHub targetEventHub,
            CampusNpcMindState targetMind,
            CampusNpcPersonalProfile targetProfile,
            Transform targetTransform,
            Func<int> personalSeedProvider,
            Action<string, float, bool> speakCallback)
        {
            runtime = targetRuntime;
            bootstrap = targetBootstrap;
            worldService = targetWorldService;
            rosterService = targetRosterService;
            timeController = targetTimeController;
            eventHub = targetEventHub;
            Mind = targetMind;
            Profile = targetProfile;
            ownerTransform = targetTransform;
            resolvePersonalSeed = personalSeedProvider;
            speak = speakCallback;
            Segment = timeController != null ? timeController.CurrentSegment : Segment;
            EnsureFacingState();
            VisionProfile = CampusNpcVisionProfileResolver.Resolve(Data);

            int seed = Mathf.Max(1, PersonalSeed);
            senseFrameSlot = CampusNpcStableIds.PositiveModulo(seed, SenseFrameSlots);
            if (nextSenseTime <= 0f)
            {
                nextSenseTime = UnityEngine.Time.time + ResolveSenseOffset(seed);
            }
        }

        public void SetProfile(CampusNpcPersonalProfile profile)
        {
            Profile = profile;
            VisionProfile = CampusNpcVisionProfileResolver.Resolve(Data);
        }

        public void RefreshSensesIfDue()
        {
            RefreshSenses(false);
        }

        public void ObserveInventoryFacts()
        {
            inventoryAwareness.Observe(this);
        }

        public void ObserveEventFacts()
        {
            eventFactAwareness.Observe(this);
        }

        public void HandleSegmentChanged(CampusTimeSegment currentSegment)
        {
            Segment = currentSegment;
            roomTargets.Clear();
            ClearActionChainProgress();
            nextSenseTime = UnityEngine.Time.time + ResolveSenseOffset(PersonalSeed);
            RequestDecisionSoon();
        }

        public bool IsActiveActionChain(string entryId, string actionChainId)
        {
            return string.Equals(activeActionChainEntryId, NormalizeId(entryId), StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(activeActionChainId, NormalizeId(actionChainId), StringComparison.OrdinalIgnoreCase);
        }

        public bool TryGetActiveActionChainStep(string entryId, string actionChainId, out int stepIndex)
        {
            stepIndex = activeActionChainStepIndex;
            return IsActiveActionChain(entryId, actionChainId);
        }

        public void BeginActionChainStep(string entryId, string actionChainId, int stepIndex)
        {
            activeActionChainEntryId = NormalizeId(entryId);
            activeActionChainId = NormalizeId(actionChainId);
            activeActionChainStepIndex = Mathf.Max(0, stepIndex);
        }

        public void CompleteActionChainStep(CampusNpcActionOpportunity opportunity, bool succeeded)
        {
            if (opportunity == null || string.IsNullOrEmpty(opportunity.ActionChainId))
            {
                return;
            }

            if (!IsActiveActionChain(opportunity.ScheduleEntryId, opportunity.ActionChainId))
            {
                return;
            }

            if (!opportunity.AdvancesActionChain)
            {
                return;
            }

            if (!succeeded)
            {
                ClearActionChainProgress();
                return;
            }

            int nextStepIndex = opportunity.ChainStepIndex + 1;
            if (nextStepIndex >= opportunity.ChainStepCount)
            {
                ClearActionChainProgress();
                return;
            }

            activeActionChainStepIndex = nextStepIndex;
        }

        public void ClearActionChainProgress()
        {
            activeActionChainEntryId = string.Empty;
            activeActionChainId = string.Empty;
            activeActionChainStepIndex = 0;
        }

        public void Speak(string line, float durationSeconds, bool writeToLog)
        {
            speak?.Invoke(line, durationSeconds, writeToLog);
        }

        public void RequestDecisionSoon()
        {
            float requestedTime = UnityEngine.Time.time + ResolveDecisionStartOffset(PersonalSeed);
            if (nextDecisionTime < 0f || requestedTime < nextDecisionTime)
            {
                nextDecisionTime = requestedTime;
            }
        }

        public void ScheduleNextDecision(float baseIntervalSeconds)
        {
            float interval = Mathf.Max(0.05f, baseIntervalSeconds);
            nextDecisionTime = UnityEngine.Time.time + interval + ResolveDecisionIntervalOffset(PersonalSeed);
        }

        public void ApplyIntent(
            CampusNpcIntent nextIntent,
            Func<CampusNpcIntentKind, string> resolveIntentLine)
        {
            if (Mind == null)
            {
                return;
            }

            if (nextIntent == null)
            {
                nextIntent = CampusNpcIntent.Idle("Idle");
            }

            CampusNpcIntent previousIntent = Mind.CurrentIntent;
            bool changed = previousIntent == null || !previousIntent.SameTargetAs(nextIntent);
            Mind.CurrentIntent = nextIntent;

            if (!nextIntent.UsesNavigation &&
                nextIntent.HoldSeconds > 0f &&
                Mind.IntentHoldUntil < UnityEngine.Time.time)
            {
                Mind.IntentHoldUntil = UnityEngine.Time.time + nextIntent.HoldSeconds;
            }

            if (!nextIntent.UsesNavigation)
            {
                if (Navigator.HasDestination)
                {
                    Navigator.Clear();
                }

                SpeakChangedIntentLine(changed, nextIntent.Kind, resolveIntentLine);
                return;
            }

            if (changed || !Navigator.HasDestination)
            {
                Navigator.MoveTo(nextIntent);
            }

            SpeakChangedIntentLine(changed, nextIntent.Kind, resolveIntentLine);
        }

        public void TickCurrentIntent()
        {
            if (Mind == null || Mind.CurrentIntent == null || !Mind.CurrentIntent.UsesNavigation)
            {
                if (Navigator.HasDestination)
                {
                    Navigator.Clear();
                }

                CampusNpcActionOpportunityExecutor.TryCompleteCurrentOpportunity(this);
                return;
            }

            if (!Navigator.HasDestination)
            {
                Navigator.MoveTo(Mind.CurrentIntent);
            }

            Navigator.Tick();
            CampusNpcActionOpportunityExecutor.TryCompleteCurrentOpportunity(this);
        }

        public Vector3 RoomTarget(CampusRoomType roomType, float radius)
        {
            int radiusKey = Mathf.RoundToInt(Mathf.Max(0f, radius) * 100f);
            if (roomTargets.TryGetValue(roomType, out CachedRoomTarget cached) &&
                cached.RadiusKey == radiusKey &&
                Time < cached.ExpiresAt)
            {
                return cached.Position;
            }

            Vector3 target = ResolveRoomTarget(roomType, radius);
            roomTargets[roomType] = new CachedRoomTarget
            {
                RadiusKey = radiusKey,
                Position = target,
                ExpiresAt = Time + 1.25f + ResolveSenseOffset(PersonalSeed + (int)roomType)
            };
            return target;
        }

        private void RefreshSenses(bool force)
        {
            float now = UnityEngine.Time.time;
            Time = now;
            if (!force)
            {
                if (now < nextSenseTime)
                {
                    return;
                }

                if (UnityEngine.Time.frameCount % SenseFrameSlots != senseFrameSlot)
                {
                    return;
                }
            }

            Segment = timeController != null ? timeController.CurrentSegment : Segment;
            UpdateKnownRoom();
            nextSenseTime = now + SenseIntervalSeconds + ResolveSenseOffset(PersonalSeed + Mathf.FloorToInt(now * 13f));
        }

        private void UpdateKnownRoom()
        {
            if (runtime == null || runtime.Data == null)
            {
                return;
            }

            CampusCharacterCurrentRoomTracker.SyncRuntime(runtime, worldService);
        }

        private void EnsureFacingState()
        {
            if (runtime == null)
            {
                facingState = null;
                return;
            }

            facingState = runtime.GetComponent<CampusCharacterFacingState>();
            if (facingState == null)
            {
                facingState = runtime.gameObject.AddComponent<CampusCharacterFacingState>();
            }
        }

        private Vector3 ResolveRoomTarget(CampusRoomType roomType, float radius)
        {
            CampusGameplayRoom room = worldService != null ? worldService.FindFirstUsableRoom(roomType) : null;
            if (room == null && worldService != null)
            {
                room = worldService.FindFirstRoom(roomType);
            }

            if (room == null)
            {
                return ownerTransform != null ? ownerTransform.position : Vector3.zero;
            }

            int seed = PersonalSeed;
            float angle = CampusNpcStableIds.PositiveModulo(seed * 89, 360) * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * Mathf.Max(0f, radius);
            Vector3 target = room.WorldCenter + offset;
            target.z = ownerTransform != null ? ownerTransform.position.z : 0f;
            return target;
        }

        private static float ResolveSenseOffset(int seed)
        {
            return Mathf.Lerp(0.02f, 0.21f, CampusNpcStableIds.PositiveModulo(seed * 37, 100) / 99f);
        }

        private static float ResolveDecisionStartOffset(int seed)
        {
            return Mathf.Lerp(0.04f, 0.48f, CampusNpcStableIds.PositiveModulo(seed * 29, 100) / 99f);
        }

        private static float ResolveDecisionIntervalOffset(int seed)
        {
            return Mathf.Lerp(0.02f, 0.19f, CampusNpcStableIds.PositiveModulo(seed * 17, 100) / 99f);
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private void SpeakChangedIntentLine(
            bool changed,
            CampusNpcIntentKind kind,
            Func<CampusNpcIntentKind, string> resolveIntentLine)
        {
            if (!changed || resolveIntentLine == null)
            {
                return;
            }

            Speak(resolveIntentLine(kind), 1.8f, false);
        }

        private struct CachedRoomTarget
        {
            public int RadiusKey;
            public Vector3 Position;
            public float ExpiresAt;
        }
    }
}
