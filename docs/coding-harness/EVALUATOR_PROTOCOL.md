# Agentor Evaluator Protocol

The evaluator is a fresh-context reviewer.

It should not edit code.

It should inspect:

- selected PR spec
- git diff
- verification logs
- relevant tests
- service-boundary docs
- framework strategy docs

## Verdict format

The evaluator must start with exactly one of:

```text
PASS
```

or

```text
NEEDS_WORK
```

## PASS requires

- diff matches PR scope
- build/test evidence exists
- evidence files were read
- no service-boundary violation
- no external framework leakage
- no unexplained failing tests
- no future PR scope implemented early

## NEEDS_WORK requires

Specific fixable bullets.

Do not offer to implement fixes inside the evaluator pass.
