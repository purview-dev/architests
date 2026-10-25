namespace Purview.Architecture.Testing;

/// <summary>Resolves a repo-relative path the policy supplies, keeping it inside the repository.</summary>
/// <remarks>
/// A policy file is input, not trust. It arrives with a pull request, including one from a fork, so
/// a path it names decides which directories the evaluator reads and - for the API baseline - which
/// file it writes. <see cref="Path.Combine(string, string)"/> alone is not enough: it discards the
/// first argument entirely when the second is rooted, so <c>"C:/Windows"</c> or <c>"/etc"</c> silently
/// escapes the repository, and <c>"../.."</c> walks out of it.
/// <para>
/// The policy's own root-finding already stops deliberately at the <c>.git</c> boundary rather than
/// picking up a sibling checkout's rules. This applies the same thinking to every other path the
/// policy names.
/// </para>
/// </remarks>
public static class PolicyPath
{
	/// <summary>Resolves a policy-supplied path against the repository root.</summary>
	/// <param name="repositoryRoot">The repository root.</param>
	/// <param name="relative">The repo-relative path from the policy.</param>
	/// <param name="setting">The policy field the path came from, named in the failure message.</param>
	/// <returns>The absolute path, which is inside <paramref name="repositoryRoot"/>.</returns>
	/// <exception cref="InvalidOperationException">The path resolves outside the repository.</exception>
	public static string Resolve(string repositoryRoot, string relative, string setting)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
		ArgumentNullException.ThrowIfNull(relative);

		return TryResolve(repositoryRoot, relative, out var resolved)
			? resolved
			: throw new InvalidOperationException(
				$"The policy's '{setting}' resolves to a path outside the repository: '{relative}'. "
					+ "Paths in the policy are relative to the repository root, and may not escape it with "
					+ "'..' or an absolute path."
			);
	}

	/// <summary>Resolves a policy-supplied path, reporting containment rather than throwing.</summary>
	/// <param name="repositoryRoot">The repository root.</param>
	/// <param name="relative">The repo-relative path from the policy.</param>
	/// <param name="resolved">The absolute path, when it is inside the repository.</param>
	/// <returns><see langword="true"/> when the path resolves inside the repository.</returns>
	public static bool TryResolve(string repositoryRoot, string relative, out string resolved)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
		ArgumentNullException.ThrowIfNull(relative);

		resolved = string.Empty;

		var root = Normalise(repositoryRoot);

		// An empty value means "the root itself", which is how an empty sourceRoots entry behaves.
		var candidate = Path.GetFullPath(
			relative.Length == 0 ? repositoryRoot : Path.Combine(repositoryRoot, relative)
		);

		var normalised = Normalise(candidate);

		// Compared with the separator appended, so '/repo-other' is not accepted as being inside
		// '/repo'. Case-insensitively on Windows and macOS, where the filesystem is.
		var comparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

		if (!string.Equals(normalised, root, comparison) && !normalised.StartsWith(root, comparison))
			return false;

		resolved = candidate;
		return true;
	}

	/// <summary>Renders a path in a form two paths can be compared as prefixes.</summary>
	/// <param name="path">The path.</param>
	/// <returns>The full path, ending in a directory separator.</returns>
	static string Normalise(string path)
	{
		var full = Path.GetFullPath(path);

		return full.Length > 0 && full[^1] == Path.DirectorySeparatorChar ? full : full + Path.DirectorySeparatorChar;
	}
}
