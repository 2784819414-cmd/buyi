using System;

namespace NtingCampus.Gameplay.Characters
{
    [Serializable]
    public sealed class CampusNpcVisionProfile
    {
        public float ViewDistance = 5.5f;
        public float ViewAngle = 90f;
        public float PeripheralDistance = 1.4f;
        public bool RequireSameRoom = true;
        public bool CanHearBehind = true;
        public float HearingDistance = 2.2f;
        public float AttentionMultiplier = 1f;
    }
}
