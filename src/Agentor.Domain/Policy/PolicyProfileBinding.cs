namespace Agentor.Domain.Policy;

/// <summary>Links a PolicyProfile to a specific, published PolicyBundle version.</summary>
public sealed record PolicyProfileBinding(
    Guid BundleId,
    PolicyBundleVersion BundleVersion,
    DateTimeOffset BoundAt);
