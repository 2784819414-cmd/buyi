using System;

namespace Nting.AIPrefabGuard.Editor
{
    [Serializable]
    public sealed class BaselineSnapshot
    {
        public int version = 1;
        public string projectPath = string.Empty;
        public string createdAtUtc = string.Empty;
        public string updatedAtUtc = string.Empty;
        public BaselineFileEntry[] files = new BaselineFileEntry[0];

        public int FileCount
        {
            get { return files == null ? 0 : files.Length; }
        }
    }
}
