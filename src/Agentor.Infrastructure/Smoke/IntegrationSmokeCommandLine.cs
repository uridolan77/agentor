namespace Agentor.Infrastructure.Smoke;

/// <summary>
/// Parses the <c>Agentor.IntegrationSmoke</c> CLI arguments and validates targets.
/// </summary>
public static class IntegrationSmokeCommandLine
{
    public const string DefaultOutputSubpath = "artifacts/integration-smoke";

    /// <summary>
    /// Parses <paramref name="args"/>. Throws <see cref="InvalidOperationException"/> when any flag is
    /// unknown, has a missing value, or names an unknown smoke target. The CLI maps that exception to exit code 2.
    /// </summary>
    public static IntegrationSmokeParsedArgs Parse(IReadOnlyList<string> args, string? currentDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? output = null;
        var targets = new List<string>();
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--output":
                case "-o":
                    if (i + 1 >= args.Count || IsFlag(args[i + 1]))
                    {
                        throw new InvalidOperationException(
                            $"CLI flag '{arg}' requires a directory path argument.");
                    }

                    output = args[++i];
                    break;

                case "--target":
                case "-t":
                    if (i + 1 >= args.Count || IsFlag(args[i + 1]))
                    {
                        throw new InvalidOperationException(
                            $"CLI flag '{arg}' requires a target name argument (valid: {string.Join(", ", IntegrationSmokeTargetValidation.ValidTargetNames)}).");
                    }

                    targets.Add(args[++i]);
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unknown CLI argument '{arg}'. Supported: --target|-t <name>, --output|-o <directory>.");
            }
        }

        IntegrationSmokeTargetValidation.Validate(targets);

        var resolvedOutput = string.IsNullOrWhiteSpace(output)
            ? Path.Combine(currentDirectory ?? Environment.CurrentDirectory, "artifacts", "integration-smoke")
            : output.Trim();

        IReadOnlySet<string>? only = targets.Count == 0
            ? null
            : new HashSet<string>(targets, StringComparer.OrdinalIgnoreCase);

        return new IntegrationSmokeParsedArgs(resolvedOutput, only);
    }

    private static bool IsFlag(string candidate) =>
        candidate.StartsWith("--", StringComparison.Ordinal) || candidate.StartsWith('-');
}

/// <summary>Parsed CLI shape: resolved output directory and optional target filter.</summary>
public sealed record IntegrationSmokeParsedArgs(string OutputDirectory, IReadOnlySet<string>? OnlyTargets);
