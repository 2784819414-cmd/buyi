using System;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    public static class CampusNpcIntentRunner
    {
        public static void Apply(
            CampusNpcMindState mind,
            CampusNpcIntent nextIntent,
            CampusGridNavigationAgent navigationAgent,
            CampusCharacterBodyController bodyController,
            float currentTime,
            Action<CampusNpcIntentKind> onIntentChanged)
        {
            if (mind == null)
            {
                return;
            }

            if (nextIntent == null)
            {
                nextIntent = CampusNpcIntent.Idle("Idle");
            }

            CampusNpcIntent previousIntent = mind.CurrentIntent;
            bool changed = previousIntent == null || !previousIntent.SameTargetAs(nextIntent);
            mind.CurrentIntent = nextIntent;

            if (nextIntent.Kind == CampusNpcIntentKind.UsePhoneForDelivery && mind.IntentHoldUntil < currentTime)
            {
                mind.IntentHoldUntil = currentTime + nextIntent.HoldSeconds;
            }

            if (!nextIntent.UsesNavigation)
            {
                navigationAgent?.ClearDestination();
                bodyController?.StopMovement();
                if (changed)
                {
                    onIntentChanged?.Invoke(nextIntent.Kind);
                }

                return;
            }

            if (navigationAgent != null && changed)
            {
                navigationAgent.SetDestination(nextIntent.TargetPosition, nextIntent.StopDistance, nextIntent.Label);
                onIntentChanged?.Invoke(nextIntent.Kind);
            }
        }

        public static void TickNavigation(
            CampusNpcMindState mind,
            CampusGridNavigationAgent navigationAgent,
            CampusCharacterBodyController bodyController,
            float moveSpeed,
            int floorIndex,
            int personalSeed)
        {
            if (navigationAgent == null || mind == null || mind.CurrentIntent == null || !mind.CurrentIntent.UsesNavigation)
            {
                bodyController?.StopMovement();
                return;
            }

            navigationAgent.Configure(
                moveSpeed,
                floorIndex,
                personalSeed,
                0.8f,
                0.16f,
                0.9f,
                0.035f);

            if (!navigationAgent.HasDestination)
            {
                navigationAgent.SetDestination(
                    mind.CurrentIntent.TargetPosition,
                    mind.CurrentIntent.StopDistance,
                    mind.CurrentIntent.Label);
            }

            navigationAgent.TickNavigation();
        }

        public static bool HasArrived(Transform transform, CampusNpcIntent intent, float arrivalDistance)
        {
            if (transform == null || intent == null)
            {
                return false;
            }

            return Vector2.Distance(transform.position, intent.TargetPosition) <=
                   Mathf.Max(arrivalDistance, intent.StopDistance + 0.08f);
        }
    }
}
