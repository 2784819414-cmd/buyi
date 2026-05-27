using System;

namespace Nting.AIPrefabGuard.Editor
{
    [Serializable]
    public sealed class BaselineFileEntry
    {
        public string relativePath;
        public long size;
        public long lastWriteTimeUtcTicks;
        public string sha256;

        public BaselineFileEntry()
        {
        }

        public BaselineFileEntry(string relativePath, long size, long lastWriteTimeUtcTicks, string sha256)
        {
            this.relativePath = relativePath ?? string.Empty;
            this.size = size;
            this.lastWriteTimeUtcTicks = lastWriteTimeUtcTicks;
            this.sha256 = sha256 ?? string.Empty;
        }
    }
}
