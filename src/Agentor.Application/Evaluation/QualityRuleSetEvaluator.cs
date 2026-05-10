using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agentor.Domain;
using Agentor.Domain.Enums;

namespace Agentor.Application.Evaluation;

public enum QualityPredicateKind
{
    RunStatusEquals,
    MinTraceEventCount,
    TraceContainsKind,
    MaxPolicyDeniedDecisions
}

public sealed record QualityRule(string Id, QualityPredicateKind Predicate, IReadOnlyList<string> Arguments);

public sealed record QualityRuleSet(int SchemaVersion, IReadOnlyList<QualityRule> Rules);

public sealed record QualityRuleEvaluationResult(
    bool Passed,
    IReadOnlyList<QualityViolation> Violations,
    IReadOnlyList<QualityWarning> Warnings);

public sealed record QualityViolation(string RuleId, string Code, string Message);

public sealed record QualityWarning(string RuleId, string Code, string Message);

public static class QualityRuleSetEvaluator
{
    private sealed class Dto
    {
        public int SchemaVersion { get; set; }
        public List<RuleDto>? Rules { get; set; }
    }

    private sealed class RuleDto
    {
        public string? Id { get; set; }
        public string? Predicate { get; set; }
        public List<string>? Arguments { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static QualityRuleSet Parse(string json)
    {
        var dto = JsonSerializer.Deserialize<Dto>(json, JsonOptions)
                  ?? throw new InvalidDataException("Quality rule set JSON deserialized to null.");

        if (dto.SchemaVersion < 1)
        {
            throw new InvalidDataException("Quality rule set schemaVersion must be >= 1.");
        }

        var rules = new List<QualityRule>();
        foreach (var r in dto.Rules ?? [])
        {
            if (string.IsNullOrWhiteSpace(r.Id))
            {
                throw new InvalidDataException("Each rule requires a non-empty id.");
            }

            if (!Enum.TryParse<QualityPredicateKind>(r.Predicate, ignoreCase: true, out var pred))
            {
                throw new InvalidDataException($"Unknown predicate '{r.Predicate}' on rule '{r.Id}'.");
            }

            rules.Add(new QualityRule(r.Id.Trim(), pred, r.Arguments ?? []));
        }

        return new QualityRuleSet(dto.SchemaVersion, rules);
    }

    public static QualityRuleEvaluationResult Evaluate(QualityRuleSet ruleSet, AgentRun run)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);
        ArgumentNullException.ThrowIfNull(run);

        var violations = new List<QualityViolation>();
        var warnings = new List<QualityWarning>();

        foreach (var rule in ruleSet.Rules)
        {
            switch (rule.Predicate)
            {
                case QualityPredicateKind.RunStatusEquals:
                {
                    var expected = rule.Arguments.FirstOrDefault();
                    if (string.IsNullOrWhiteSpace(expected))
                    {
                        violations.Add(new QualityViolation(rule.Id, "RULE_CONFIG", "RunStatusEquals requires one argument (status name)."));
                        break;
                    }

                    if (!Enum.TryParse<AgentRunStatus>(expected, ignoreCase: true, out var st))
                    {
                        violations.Add(new QualityViolation(rule.Id, "RULE_CONFIG", $"Unknown run status '{expected}'."));
                        break;
                    }

                    if (run.Status != st)
                    {
                        violations.Add(new QualityViolation(
                            rule.Id,
                            "RUN_STATUS_MISMATCH",
                            $"Expected status {st}, actual {run.Status}."));
                    }

                    break;
                }
                case QualityPredicateKind.MinTraceEventCount:
                {
                    if (!TryParseIntArg(rule, out var min, out var err))
                    {
                        violations.Add(new QualityViolation(rule.Id, "RULE_CONFIG", err));
                        break;
                    }

                    if (run.Trace.Count < min)
                    {
                        violations.Add(new QualityViolation(
                            rule.Id,
                            "TRACE_COUNT_TOO_LOW",
                            $"Trace events {run.Trace.Count} < required {min}."));
                    }

                    break;
                }
                case QualityPredicateKind.TraceContainsKind:
                {
                    var kindName = rule.Arguments.FirstOrDefault();
                    if (string.IsNullOrWhiteSpace(kindName))
                    {
                        violations.Add(new QualityViolation(rule.Id, "RULE_CONFIG", "TraceContainsKind requires trace kind name."));
                        break;
                    }

                    if (!Enum.TryParse<TraceEventKind>(kindName, ignoreCase: true, out var kind))
                    {
                        violations.Add(new QualityViolation(rule.Id, "RULE_CONFIG", $"Unknown trace kind '{kindName}'."));
                        break;
                    }

                    if (!run.Trace.Any(e => e.Kind == kind))
                    {
                        violations.Add(new QualityViolation(
                            rule.Id,
                            "MISSING_TRACE_KIND",
                            $"Trace does not contain {kind}."));
                    }

                    break;
                }
                case QualityPredicateKind.MaxPolicyDeniedDecisions:
                {
                    if (!TryParseIntArg(rule, out var maxDenied, out var err))
                    {
                        violations.Add(new QualityViolation(rule.Id, "RULE_CONFIG", err));
                        break;
                    }

                    var denied = run.Steps.Sum(s => s.PolicyDecisions.Count(d => d.Outcome == PolicyDecisionOutcome.Deny));
                    if (denied > maxDenied)
                    {
                        violations.Add(new QualityViolation(
                            rule.Id,
                            "TOO_MANY_POLICY_DENIALS",
                            $"Policy denials {denied} exceed maximum {maxDenied}."));
                    }

                    break;
                }
                default:
                    violations.Add(new QualityViolation(rule.Id, "RULE_CONFIG", $"Unhandled predicate {rule.Predicate}."));
                    break;
            }
        }

        return new QualityRuleEvaluationResult(violations.Count == 0, violations, warnings);
    }

    private static bool TryParseIntArg(QualityRule rule, out int value, out string error)
    {
        value = 0;
        error = "";
        var raw = rule.Arguments.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "Predicate requires one integer argument.";
            return false;
        }

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            error = $"Argument '{raw}' is not an integer.";
            return false;
        }

        return true;
    }
}
