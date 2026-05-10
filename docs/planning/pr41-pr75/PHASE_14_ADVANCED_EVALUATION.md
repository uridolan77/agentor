# Phase 14 — Advanced evaluation and coordination science

## Purpose

Upgrade Agentor’s evaluation layer so it can compare coordination strategies, not merely check that runs complete.

This phase operationalizes the coordination-layer doctrine: coordination configurations must be traceable, configurable, and evaluable.

## Doctrine

```text
Coordination strategies must be evaluated, not assumed superior.
More agents, more debate, or more consensus does not automatically mean better output.
Evaluation should control model/tool/prompt/context where possible.
Cost, latency, token use, review burden, and failure isolation are architectural outputs.
```

## PR66 — Evaluation fixture registry

### Goal

Formalize versioned evaluation assets.

### Suggested types

```text
EvaluationFixture
EvaluationDataset
EvaluationCase
EvaluationExpectedSnapshot
EvaluationFixtureRegistry
```

### Acceptance

- JSON fixture discovery exists.
- Fixture schema is versioned.
- Deterministic comparison exists.
- Existing evaluation fixtures migrate to registry format.

## PR67 — Coordination profile evaluation

### Goal

Run the same fixture under multiple coordination profiles.

### Initial profiles

```text
SequentialPipeline
SkillWrappedSequential
McpToolBoundPlan
ExternalAgentTool
ReviewGatedPlan
```

### Acceptance

- Same fixture can run under multiple profiles.
- Model/tool/prompt/context are controlled where possible.
- Results record cost, latency, quality, trace count, tool/model/external call counts.

## PR68 — Quality rule set DSL-lite

### Goal

Add declarative quality rules without arbitrary code execution.

### Suggested types

```text
QualityRuleSet
QualityRule
QualityPredicateKind
QualityViolation
QualityWarning
```

### Acceptance

- JSON-configured rule sets.
- Built-in predicates only.
- No arbitrary scripting.
- Tests cover bad rule config and deterministic evaluation.

## PR69 — Coordination evaluation metrics

### Goal

Add coordination-specific metrics derived from traces/manifests.

### Metrics

```text
Reliability
Resolution
Cost
Latency
Token usage
Review burden
Failure isolation
Escalation rate
Diversity/collapse signal where applicable
```

### Acceptance

- Metrics derived from existing runtime artifacts.
- Summary supports per-run and aggregate metrics.
- No raw provider secrets/logs used.

## PR70 — Evaluation report generator

### Goal

Generate deterministic reports suitable for CI and review.

### Outputs

```text
Markdown report
JSON report
CSV summary
CI artifact folder
```

### Acceptance

- Report includes fixture set, coordination profile, quality gates, costs, warnings, failures.
- Output is deterministic.
- CI can upload report artifacts.

## Phase 14 exit criteria

- Evaluation fixture registry exists.
- Coordination profile comparisons exist.
- Quality rule sets exist.
- Coordination metrics exist.
- Deterministic reports exist.
