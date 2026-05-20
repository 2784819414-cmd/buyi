namespace NtingCampus.Gameplay.Characters
{
    internal static class CampusNpcStableIds
    {
        public static string CharacterKey(CampusCharacterRuntime runtime)
        {
            if (runtime == null)
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(runtime.CharacterId)
                ? runtime.GetInstanceID().ToString()
                : runtime.CharacterId;
        }

        public static int Hash(string value)
        {
            unchecked
            {
                int hash = 23;
                string normalized = value ?? string.Empty;
                for (int i = 0; i < normalized.Length; i++)
                {
                    hash = hash * 31 + char.ToUpperInvariant(normalized[i]);
                }

                return hash;
            }
        }

        public static int PositiveModulo(int value, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            int result = value % count;
            return result < 0 ? result + count : result;
        }
    }
}
