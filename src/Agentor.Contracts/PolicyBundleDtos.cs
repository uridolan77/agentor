using Agentor.Domain.Policy;

namespace Agentor.Contracts;

// ---- requests ----

public sealed record CreatePolicyRuleDto(
    PolicyRuleKind Kind,
    PolicyRuleScope Scope,
    PolicyRuleEffect Effect,
    string? TargetKey = null,
    string? ThresholdValue = null,
    string Description = "");

public sealed record CreatePolicyBundleRequestDto(
    string Name,
    string Version,
    IReadOnlyList<CreatePolicyRuleDto> Rules);

public sealed record ActivatePolicyProfileRequestDto(
    Guid BundleId,
    string BundleVersion);

// ---- responses ----

public sealed record PolicyRuleDto(
    Guid Id,
    PolicyRuleKind Kind,
    PolicyRuleScope Scope,
    PolicyRuleEffect Effect,
    string? TargetKey,
    string? ThresholdValue,
    string Description);

public sealed record PolicyBundleSummaryDto(
    Guid Id,
    string Name,
    string Version,
    bool IsPublished,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt);

public sealed record PolicyBundleDetailDto(
    Guid Id,
    string Name,
    string Version,
    bool IsPublished,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<PolicyRuleDto> Rules);

public sealed record PolicyBundleListDto(IReadOnlyList<PolicyBundleSummaryDto> Bundles);

public sealed record ActivePolicyProfileDto(
    Guid ProfileId,
    string ProfileName,
    Guid BundleId,
    string BundleVersion,
    DateTimeOffset ActivatedAt);
