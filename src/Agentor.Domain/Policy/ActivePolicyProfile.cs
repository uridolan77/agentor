namespace Agentor.Domain.Policy;

/// <summary>
/// Runtime marker: which PolicyProfile (and via its binding, which PolicyBundle version) is active for evaluation.
/// Stored and retrieved by <c>IPolicyProfileRepository</c>.
/// </summary>
public sealed record ActivePolicyProfile(
    Guid ProfileId,
    string ProfileName,
    Guid BundleId,
    PolicyBundleVersion BundleVersion,
    DateTimeOffset ActivatedAt,
    Guid ActivatedBy);
