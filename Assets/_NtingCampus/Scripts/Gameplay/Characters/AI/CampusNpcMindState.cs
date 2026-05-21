using System;
namespace NtingCampus.Gameplay.Characters
{
    [Serializable]
    public sealed class CampusNpcMindState
    {
        public CampusNpcIntent CurrentIntent = CampusNpcIntent.Idle("Idle");
        public float IntentHoldUntil = -1f;
    }
}
