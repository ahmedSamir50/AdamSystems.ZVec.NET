namespace ZVec.NET.Samples.Shared;

/// <summary>Resolves collection and dataset paths for sample hosts.</summary>
public static class SamplePaths
{
    public static string RepoSamplesRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, "samples");
                if (Directory.Exists(Path.Combine(candidate, "datasets")))
                    return candidate;
                if (dir.Name.Equals("samples", StringComparison.OrdinalIgnoreCase) &&
                    Directory.Exists(Path.Combine(dir.FullName, "datasets")))
                    return dir.FullName;
                dir = dir.Parent;
            }

            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        }
    }

    public static string DatasetsRoot => Path.Combine(RepoSamplesRoot, "datasets");
    public static string FixturesRoot => Path.Combine(DatasetsRoot, "fixtures");
    public static string CacheRoot => Path.Combine(DatasetsRoot, "cache");

    public static string CollectionPath(string folderName, string? rootOverride = null)
    {
        var root = rootOverride ?? Path.Combine(Path.GetTempPath(), "ZVec.NET.Samples");
        Directory.CreateDirectory(root);
        return Path.Combine(root, folderName);
    }

    public static void EnsureCacheDirectory() => Directory.CreateDirectory(CacheRoot);
}
