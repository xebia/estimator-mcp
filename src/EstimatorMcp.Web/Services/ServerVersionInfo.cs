using System.Reflection;

namespace EstimatorMcp.Web.Services;

/// <summary>
/// The running build's version, split out of the assembly's InformationalVersion.
/// </summary>
/// <param name="Semantic">Semantic version without build metadata, e.g. "0.1.0" or "0.2.0-rc.1".</param>
/// <param name="Commit">Commit SHA from the "+sha" build metadata, or null for a local build.</param>
public readonly record struct ServerVersionInfo(string Semantic, string? Commit)
{
    /// <summary>Full informational version, e.g. "0.1.0+abc1234".</summary>
    public string Full => Commit is null ? Semantic : $"{Semantic}+{Commit}";

    /// <summary>
    /// Splits an InformationalVersion into its semantic version and build metadata.
    /// CI passes SourceRevisionId, so released builds carry "+&lt;sha&gt;"; local
    /// builds have no metadata. Only the first '+' separates the two — a
    /// prerelease label ("-rc.1") stays part of the semantic version.
    /// </summary>
    public static ServerVersionInfo Parse(string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
            return new ServerVersionInfo("unknown", null);

        var plus = informationalVersion.IndexOf('+');
        if (plus < 0)
            return new ServerVersionInfo(informationalVersion, null);

        var semantic = informationalVersion[..plus];
        var metadata = informationalVersion[(plus + 1)..];

        return new ServerVersionInfo(
            semantic.Length == 0 ? "unknown" : semantic,
            metadata.Length == 0 ? null : metadata);
    }

    /// <summary>Reads the version off the entry assembly for this app.</summary>
    public static ServerVersionInfo Current { get; } = Parse(
        typeof(ServerVersionInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
}
