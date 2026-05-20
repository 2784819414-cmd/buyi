using UnityEngine;

namespace NtingCampus.Gameplay.Characters
{
    internal static class CampusNpcVisionProfileResolver
    {
        public static CampusNpcVisionProfile Resolve(CampusCharacterData data)
        {
            CampusNpcVisionProfile profile = new CampusNpcVisionProfile();
            if (data == null)
            {
                return profile;
            }

            ApplyRole(profile, data.Role);
            ApplyTraits(profile, data);
            Clamp(profile);
            return profile;
        }

        private static void ApplyRole(CampusNpcVisionProfile profile, CampusCharacterRole role)
        {
            switch (role)
            {
                case CampusCharacterRole.Teacher:
                    profile.ViewDistance = 6.5f;
                    profile.ViewAngle = 115f;
                    profile.HearingDistance = 2.6f;
                    profile.AttentionMultiplier = 1.25f;
                    break;
                case CampusCharacterRole.Staff:
                    profile.ViewDistance = 5.8f;
                    profile.ViewAngle = 100f;
                    profile.HearingDistance = 2.4f;
                    profile.AttentionMultiplier = 1.1f;
                    break;
                default:
                    profile.ViewDistance = 5.0f;
                    profile.ViewAngle = 85f;
                    profile.HearingDistance = 2.0f;
                    profile.AttentionMultiplier = 0.9f;
                    break;
            }
        }

        private static void ApplyTraits(CampusNpcVisionProfile profile, CampusCharacterData data)
        {
            if (data.HasTrait(CampusCharacterTrait.GoodStudent))
            {
                profile.ViewDistance += 0.2f;
                profile.ViewAngle += 5f;
                profile.AttentionMultiplier += 0.1f;
            }

            if (data.HasTrait(CampusCharacterTrait.Tattletale))
            {
                profile.ViewDistance += 0.8f;
                profile.ViewAngle += 15f;
                profile.AttentionMultiplier += 0.35f;
            }

            if (data.HasTrait(CampusCharacterTrait.Troublemaker))
            {
                profile.ViewDistance -= 0.4f;
                profile.ViewAngle -= 8f;
                profile.AttentionMultiplier -= 0.15f;
            }

            if (data.HasTrait(CampusCharacterTrait.Sleepyhead))
            {
                profile.ViewDistance -= 1.2f;
                profile.ViewAngle -= 15f;
                profile.HearingDistance -= 0.4f;
                profile.AttentionMultiplier -= 0.35f;
            }
        }

        private static void Clamp(CampusNpcVisionProfile profile)
        {
            profile.ViewDistance = Mathf.Clamp(profile.ViewDistance, 1.5f, 9f);
            profile.ViewAngle = Mathf.Clamp(profile.ViewAngle, 35f, 160f);
            profile.PeripheralDistance = Mathf.Clamp(profile.PeripheralDistance, 0.5f, profile.ViewDistance);
            profile.HearingDistance = Mathf.Clamp(profile.HearingDistance, 0f, profile.ViewDistance);
            profile.AttentionMultiplier = Mathf.Clamp(profile.AttentionMultiplier, 0.15f, 2.2f);
        }
    }
}
