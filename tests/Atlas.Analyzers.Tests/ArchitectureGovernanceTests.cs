using System.Xml.Linq;

namespace Atlas.Analyzers.Tests;

public class ArchitectureGovernanceTests
{
    [Fact]
    public void DataAbstractions_DoesNotReferenceEntityFramework()
    {
        var projectPath = Path.Combine(RepoRoot, "src", "Atlas.Data.Abstractions", "Atlas.Data.Abstractions.csproj");
        var project = XDocument.Load(projectPath);
        var forbiddenReferences = GetItems(project, "PackageReference")
            .Concat(GetItems(project, "ProjectReference"))
            .Select(x => x.Attribute("Include")?.Value ?? string.Empty)
            .Where(x => x.Contains("EntityFrameworkCore", StringComparison.OrdinalIgnoreCase)
                        || x.Contains("Atlas.Data.EntityFramework", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(forbiddenReferences);

        var forbiddenSourceReferences = Directory
            .EnumerateFiles(Path.GetDirectoryName(projectPath)!, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadAllText(path).Contains("Microsoft.EntityFrameworkCore", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(forbiddenSourceReferences);
    }

    [Fact]
    public void Core_DoesNotReferenceInfrastructureProjects()
    {
        var projectPath = Path.Combine(RepoRoot, "src", "Atlas.Core", "Atlas.Core.csproj");
        var project = XDocument.Load(projectPath);
        var forbiddenReferences = GetItems(project, "ProjectReference")
            .Select(x => x.Attribute("Include")?.Value ?? string.Empty)
            .Where(x => x.Contains("Atlas.Infrastructure", StringComparison.OrdinalIgnoreCase)
                        || x.Contains("Atlas.Data", StringComparison.OrdinalIgnoreCase)
                        || x.Contains("Atlas.Services", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(forbiddenReferences);
    }

    [Fact]
    public void MicrosoftExtensionsPackages_FollowNet8MajorVersionOrDocumentedException()
    {
        var packageVersions = XDocument.Load(Path.Combine(RepoRoot, "Directory.Packages.props"));
        var violations = GetItems(packageVersions, "PackageVersion")
            .Select(x => new
            {
                Name = x.Attribute("Include")?.Value ?? string.Empty,
                Version = x.Attribute("Version")?.Value ?? string.Empty
            })
            .Where(x => x.Name.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal))
            .Where(x => TryGetMajorVersion(x.Version) != 8 && !AllowedMicrosoftExtensionsException(x.Name, x.Version))
            .Select(x => $"{x.Name} {x.Version}")
            .ToList();

        Assert.Empty(violations);
    }

    private static string RepoRoot => FindRepoRoot();

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Directory.Packages.props")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private static IEnumerable<XElement> GetItems(XDocument document, string itemName)
    {
        return document.Descendants().Where(x => x.Name.LocalName == itemName);
    }

    private static int? TryGetMajorVersion(string version)
    {
        var normalized = version.Split('-', StringSplitOptions.RemoveEmptyEntries)[0];
        return Version.TryParse(normalized, out var parsed) ? parsed.Major : null;
    }

    private static bool AllowedMicrosoftExtensionsException(string packageName, string version)
    {
        var normalized = version.Split('-', StringSplitOptions.RemoveEmptyEntries)[0];
        return normalized == "10.0.0"
               && (packageName == "Microsoft.Extensions.Logging.Abstractions"
                   || packageName == "Microsoft.Extensions.DependencyInjection.Abstractions");
    }
}
