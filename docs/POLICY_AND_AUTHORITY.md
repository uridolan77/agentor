# Policy and Authority

Agentor policy is execution policy.

Athanor authority is epistemic/canonical authority.

Do not confuse them.

## Agentor policy answers

- May this tool execute?
- Is this run allowed under runtime constraints?
- Should this operation require human review?
- Should this result be submitted as candidate material?

## Athanor authority answers

- Does this evidence support this knowledge object?
- May this object version become accepted?
- Is this contradiction resolved?
- May this snapshot become canonical?

## PR1

PR1 uses `AllowAllPolicyEvaluator`, but still records a `PolicyDecision`.

This preserves the shape for later policy hardening.
