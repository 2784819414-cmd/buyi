namespace Nting.AIPrefabGuard.Editor
{
    public sealed class GitChangedFile
    {
        public GitChangedFile(string relativePath, string originalPath, string statusCode, GitChangeKind changeKind, bool existsOnDisk)
        {
            RelativePath = relativePath ?? string.Empty;
            OriginalPath = originalPath ?? string.Empty;
            StatusCode = statusCode ?? string.Empty;
            ChangeKind = changeKind;
            ExistsOnDisk = existsOnDisk;
        }

        public string RelativePath { get; }

        public string OriginalPath { get; }

        public string StatusCode { get; }

        public GitChangeKind ChangeKind { get; }

        public bool ExistsOnDisk { get; }
    }
}
