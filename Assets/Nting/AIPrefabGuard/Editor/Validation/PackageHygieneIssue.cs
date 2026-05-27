namespace Nting.AIPrefabGuard.Editor
{
    public sealed class PackageHygieneIssue
    {
        public PackageHygieneIssue(string path, string message)
        {
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Path { get; }

        public string Message { get; }
    }
}
