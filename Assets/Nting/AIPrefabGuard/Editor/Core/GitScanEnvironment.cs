namespace Nting.AIPrefabGuard.Editor
{
    public sealed class GitScanEnvironment
    {
        public static GitScanEnvironment Unknown(string projectPath)
        {
            return new GitScanEnvironment(false, false, projectPath, string.Empty, "Not scanned yet.");
        }

        public static GitScanEnvironment Baseline(
            string projectPath,
            string baselinePath,
            bool hasBaseline,
            string baselineCreatedAtUtc,
            string baselineUpdatedAtUtc,
            int baselineFileCount,
            string message)
        {
            return new GitScanEnvironment(
                true,
                true,
                projectPath,
                string.Empty,
                message,
                baselinePath,
                hasBaseline,
                baselineCreatedAtUtc,
                baselineUpdatedAtUtc,
                baselineFileCount);
        }

        public GitScanEnvironment(bool gitAvailable, bool isRepository, string projectPath, string repositoryPath, string message)
            : this(gitAvailable, isRepository, projectPath, repositoryPath, message, string.Empty, false, string.Empty, string.Empty, 0)
        {
        }

        private GitScanEnvironment(
            bool gitAvailable,
            bool isRepository,
            string projectPath,
            string repositoryPath,
            string message,
            string baselinePath,
            bool hasBaseline,
            string baselineCreatedAtUtc,
            string baselineUpdatedAtUtc,
            int baselineFileCount)
        {
            GitAvailable = gitAvailable;
            IsRepository = isRepository;
            ProjectPath = projectPath ?? string.Empty;
            RepositoryPath = repositoryPath ?? string.Empty;
            Message = message ?? string.Empty;
            BaselinePath = baselinePath ?? string.Empty;
            HasBaseline = hasBaseline;
            BaselineCreatedAtUtc = baselineCreatedAtUtc ?? string.Empty;
            BaselineUpdatedAtUtc = baselineUpdatedAtUtc ?? string.Empty;
            BaselineFileCount = baselineFileCount;
        }

        public bool GitAvailable { get; }

        public bool IsRepository { get; }

        public string ProjectPath { get; }

        public string RepositoryPath { get; }

        public string Message { get; }

        public string BaselinePath { get; }

        public bool HasBaseline { get; }

        public string BaselineCreatedAtUtc { get; }

        public string BaselineUpdatedAtUtc { get; }

        public int BaselineFileCount { get; }
    }
}
