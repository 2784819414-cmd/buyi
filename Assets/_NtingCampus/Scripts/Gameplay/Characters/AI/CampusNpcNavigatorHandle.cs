using System;
using NtingCampusMapEditor;
using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    internal sealed class CampusNpcNavigatorHandle
    {
        private CampusGridNavigationAgent navigationAgent;
        private CampusCharacterBodyController bodyController;
        private Transform ownerTransform;
        private Func<float> resolveMoveSpeed;
        private Func<int> resolveFloorIndex;
        private Func<int> resolvePersonalSeed;

        public bool HasDestination => navigationAgent != null && navigationAgent.HasDestination;
        public bool HasReachablePath => navigationAgent == null || navigationAgent.HasReachablePath;
        public bool IsMoving => navigationAgent != null && navigationAgent.IsMoving;
        public Vector3 Destination => navigationAgent != null ? navigationAgent.Destination : Vector3.zero;

        public void Bind(
            CampusGridNavigationAgent targetNavigationAgent,
            CampusCharacterBodyController targetBodyController,
            Transform targetTransform,
            Func<float> moveSpeedProvider,
            Func<int> floorIndexProvider,
            Func<int> personalSeedProvider)
        {
            navigationAgent = targetNavigationAgent;
            bodyController = targetBodyController;
            ownerTransform = targetTransform;
            resolveMoveSpeed = moveSpeedProvider;
            resolveFloorIndex = floorIndexProvider;
            resolvePersonalSeed = personalSeedProvider;
        }

        public void MoveTo(CampusNpcIntent intent)
        {
            if (intent == null || !intent.UsesNavigation || navigationAgent == null)
            {
                return;
            }

            navigationAgent.SetDestination(
                intent.TargetPosition,
                intent.StopDistance,
                intent.Label,
                intent.RequireExactDestination);
        }

        public void Clear()
        {
            navigationAgent?.ClearDestination();
            bodyController?.StopMovement();
        }

        public void Tick()
        {
            if (navigationAgent == null)
            {
                bodyController?.StopMovement();
                return;
            }

            navigationAgent.Configure(
                resolveMoveSpeed != null ? resolveMoveSpeed() : 1f,
                resolveFloorIndex != null ? resolveFloorIndex() : 1,
                resolvePersonalSeed != null ? resolvePersonalSeed() : 1,
                0.8f,
                0.16f,
                0.9f,
                0.035f);
            navigationAgent.TickNavigation();
        }

        public bool HasArrived(CampusNpcIntent intent, float arrivalDistance)
        {
            if (ownerTransform == null || intent == null)
            {
                return false;
            }

            return Vector2.Distance(ownerTransform.position, intent.TargetPosition) <=
                   Mathf.Max(arrivalDistance, intent.StopDistance + 0.08f);
        }
    }
}
