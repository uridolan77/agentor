using Agentor.Infrastructure.Options;

namespace Agentor.Infrastructure.Smoke;

/// <summary>Validates CLI / script <c>--target</c> values against <see cref="SmokeTarget"/> names.</summary>
public static class IntegrationSmokeTargetValidation
{
    public static IReadOnlyList<string> ValidTargetNames { get; } = Enum.GetNames<SmokeTarget>();

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> when any name is not a <see cref="SmokeTarget"/> member.
    /// </summary>
    public static void Validate(IReadOnlyCollection<string> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        foreach (var raw in targets)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                throw new InvalidOperationException("Smoke --target value cannot be empty.");
            }

            var t = raw.Trim();
            if (!Enum.TryParse<SmokeTarget>(t, ignoreCase: true, out _))
            {
                throw new InvalidOperationException(
                    $"Unknown smoke target '{t}'. Valid targets: {string.Join(", ", ValidTargetNames)}.");
            }
        }
    }
}
