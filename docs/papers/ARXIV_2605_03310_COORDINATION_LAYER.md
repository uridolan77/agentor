# arXiv:2605.03310 — Coordination as an Architectural Layer for Agentor

**Paper:** *Coordination as an Architectural Layer for LLM-Based Multi-Agent Systems*  
**Authors:** Maksym Nechepurenko, Pavel Shuvalov  
**arXiv:** [2605.03310](https://arxiv.org/abs/2605.03310)  
**Agentor status:** Architecture doctrine (PR12.5); not a runtime implementation mandate by itself.

**Category (arXiv):** Multiagent Systems / Machine Learning / Trading and Market Microstructure

---

## 1. Why this paper matters for Agentor

This paper is directly relevant to Agentor because it gives a formal architectural justification for separating **coordination** from:

1. model/agent logic,
2. information access,
3. tool access,
4. external orchestration frameworks.

Agentor already separates several service roles:

```text
Agentor = governed runtime / coordination layer
Athanor = canonical knowledge-state and evidence authority
Conexus = model execution and model-routing gateway
MCP / A2A / Semantic Kernel / Microsoft Agent Framework = future adapters
```

The paper strengthens this separation by arguing that coordination is not merely an implementation detail or prompt pattern. It is an architectural layer with its own configuration, failure modes, evaluation signatures, and compute consequences.

For Agentor, this means:

```text
Agentor should not merely execute agent runs.
Agentor should own the governed coordination/runtime layer.
```

This does **not** mean Agentor should immediately implement multi-agent orchestration.

It means Agentor’s architecture, roadmap, docs, and future ontology should clearly distinguish:

```text
information access
model execution
coordination
tool execution
runtime policy
evaluation
```

---

## 2. Core thesis of the paper

The paper’s central thesis can be stated as:

> Coordination in LLM-based multi-agent systems should be treated as a configurable architectural layer, separable from agent logic and information access.

This is important because many failures in multi-agent LLM systems are not caused only by weak base models. They are caused by coordination defects:

```text
bad delegation
bad synchronization
bad aggregation
premature consensus
uncontrolled debate
unclear authority
bad stopping rules
failure propagation
diversity collapse
excessive compute
```

The architectural lesson is:

```text
Do not hide coordination inside prompts.
Do not delegate coordination ontology to a framework.
Do not assume more agents means better results.
Do not assume debate or consensus improves quality.
Make coordination explicit, traceable, configurable, and evaluable.
```

---

## 3. Three-layer separation

The paper separates multi-agent systems into three conceptual layers.

For Agentor, we should translate them as follows.

### 3.1 Information layer

The information layer contains data, tools, external sources, retrieval systems, and evidence.

In Agentor’s ecosystem:

```text
Information layer:
- Athanor canonical snapshots
- Athanor evidence search
- tool outputs
- external data sources
- bounded session memory
- future MCP tool results
```

Important distinction:

```text
Tool output is not canonical knowledge.
LLM output is not canonical knowledge.
Session memory is not Athanor.
```

Athanor remains the authority for canonical knowledge, evidence binding, review events, contradiction handling, and snapshots.

---

### 3.2 Agent/model layer

The agent/model layer contains the concrete LLM calls, prompt profiles, model profiles, and provider behavior.

In Agentor’s ecosystem:

```text
Agent/model layer:
- Conexus
- model profiles
- prompt profiles
- model-call tools
- provider routing
- cost/latency/model selection
```

Important distinction:

```text
Agentor should not call OpenAI, Anthropic, local models, or other providers directly from Domain/Application.
Model execution belongs behind Conexus.
```

---

### 3.3 Coordination layer

The coordination layer determines how work is organized.

In Agentor:

```text
Coordination layer:
- AgentRun
- AgentPlan
- AgentStep
- ToolCall
- PolicyDecision
- ExecutionTraceEvent
- RunManifest
- EvaluationResult
- future CoordinationProfile
```

Coordination answers questions such as:

```text
Who acts first?
Which step follows which?
Which actor has authority?
Which tools may execute?
When is review required?
How are outputs aggregated?
When does the run stop?
How are failures isolated?
When is human escalation required?
How is the coordination pattern evaluated?
```

Agentor owns this layer.

---

## 4. Coordination is not the same as runtime policy

After PR12, Agentor has a runtime policy engine.

That is good, but runtime policy is only one part of coordination.

Runtime policy answers:

```text
May this tool execute automatically?
Is this tool denied?
Does this action require review?
Is this tool allowed under current risk settings?
```

Coordination answers broader questions:

```text
What is the topology of the work?
What authority model governs the run?
How are agent/tool/model outputs aggregated?
What synchronization regime is used?
What stopping condition applies?
How do failures propagate or get isolated?
What evaluation signature is expected?
```

Therefore:

```text
RuntimePolicyEvaluator ∈ coordination layer
RuntimePolicyEvaluator ≠ full coordination layer
```

This distinction should be preserved before PR13 introduces tool execution pipeline semantics.

---

## 5. Reference coordination configurations

The paper compares several coordination configurations. Agentor should treat these as **reference configurations**, not as built-in assumptions.

### 5.1 Single Agent

A single agent/tool/model path handles the task.

Agentor interpretation:

```text
Topology: SingleAgent
Aggregation: None
Synchronization: Sequential
Authority: SingleController
```

Use when:

* task is simple,
* cost must be low,
* auditability matters,
* no clear benefit from multiple agents exists.

Risk:

* narrow perspective,
* no independent error checking,
* brittle if first reasoning path is wrong.

---

### 5.2 Sequential Pipeline

Stages execute in fixed order. Later stages consume earlier outputs.

Agentor interpretation:

```text
Topology: SequentialPipeline
Synchronization: Sequential
Aggregation: StageOutput
Termination: FinalStageCompleted
```

Use when:

* task naturally decomposes into stages,
* each step has clear input/output,
* auditability and traceability matter.

Risk:

* early-stage errors propagate downstream,
* stage 1 quality may dominate final quality.

Agentor relevance:

* This is the most natural first coordination pattern for PR16–PR20.
* It fits AgentPlan / AgentRecipe / AgentStep well.

---

### 5.3 Independent Ensemble

Multiple agents independently solve the same problem, then outputs are aggregated.

Agentor interpretation:

```text
Topology: IndependentEnsemble
Synchronization: Parallel
Aggregation: Mean / Vote / WeightedVote
Authority: Aggregator
```

Use when:

* diverse independent estimates matter,
* parallel cost is acceptable,
* aggregation is well-defined.

Risk:

* higher compute,
* correlated errors if agents use same model/prompt/context,
* aggregation may hide important minority signals.

Agentor relevance:

* Should not be implemented before plan execution and evaluation harness exist.
* Requires explicit cost/latency accounting.

---

### 5.4 Peer-Critique Debate

Agents observe and critique each other across rounds.

Agentor interpretation:

```text
Topology: PeerCritique
Synchronization: RoundBased
Aggregation: RevisedAnswer / CriticMerge
Termination: FixedRounds / DisagreementThreshold
```

Use when:

* critique may expose hidden assumptions,
* reasoning diversity is valuable,
* latency/cost budget allows multiple rounds.

Risk:

* diversity collapse,
* social convergence,
* overfitting to critic phrasing,
* higher cost without proportional quality gain.

Agentor relevance:

* Should be treated as an evaluable coordination profile, not assumed superior.

---

### 5.5 Orchestrator-Specialist

A central planner decomposes work, dispatches to specialists, and integrates outputs.

Agentor interpretation:

```text
Topology: OrchestratorSpecialist
Authority: Orchestrator
Aggregation: OrchestratorSynthesis
Synchronization: Sequential or Parallel
```

Use when:

* task decomposes into specialized subproblems,
* central control improves coherence,
* specialists can be independently evaluated.

Risk:

* orchestrator bottleneck,
* bad decomposition,
* specialist hallucination,
* integration error.

Agentor relevance:

* Good future fit after AgentPlan, ToolRegistry, Conexus, and evaluation harness mature.
* Should not be PR13 scope.

---

### 5.6 Consensus Alignment

Agents iterate until disagreement falls below a threshold.

Agentor interpretation:

```text
Topology: ConsensusAlignment
Synchronization: RoundBased
Aggregation: Consensus
Termination: DisagreementThreshold
```

Use when:

* agreement itself is meaningful,
* uncertainty must be surfaced,
* disagreement is measurable.

Risk:

* false consensus,
* diversity collapse,
* low resolution,
* convergence without correctness.

Agentor warning:

```text
Consensus is not automatically quality.
Agreement can destroy signal.
```

---

## 6. Coordination evaluation signatures

The paper’s deeper lesson is not only that coordination is configurable.

The deeper lesson is:

```text
Coordination configurations have observable failure signatures.
```

Agentor should eventually represent this explicitly.

### Future concept: CoordinationEvaluationSignature

A future `CoordinationEvaluationSignature` should describe the expected and observed behavior of a coordination configuration.

Possible dimensions:

```text
Reliability / calibration behavior
Resolution / discriminative behavior
Cost behavior
Latency behavior
Token / compute behavior
Diversity collapse (and broader diversity behavior)
Escalation behavior
Failure propagation
Failure isolation
Termination quality
Review burden
```

### Why this matters

Agentor should not evaluate coordination only by final task success.

It should ask:

```text
Was the system calibrated?
Did coordination improve discrimination?
Did it collapse diversity?
Did it increase cost without quality gain?
Did it escalate appropriately?
Did it stop too early?
Did it loop too long?
Did it hide uncertainty?
```

---

## 7. Controlled coordination evaluation

The paper uses an information-controlled methodology: keep model, tools, prompt structure, output cap, and task set fixed while changing coordination configuration.

Agentor should adopt this methodological principle.

When evaluating coordination patterns, hold these constant where possible:

```text
model profile
prompt profile
tool set
information access
retrieval scope
task fixture
output budget
temperature / decoding parameters
evaluation metric
```

Then vary:

```text
coordination topology
authority mode
aggregation mode
synchronization mode
termination policy
failure handling policy
```

Reason:

```text
If model, tools, prompts, and information access all change together,
we cannot tell whether improvement came from coordination.
```

This is especially important in Agentor because Athanor, Conexus, tools, memory, and coordination are intentionally separate layers.

---

## 8. Compute is an architectural output

The paper treats total compute per question as an endogenous architectural output.

Agentor should adopt the same principle:

```text
Cost is not merely billing.
Latency is not merely infrastructure.
Token use is not merely logging.
Compute is a consequence of coordination architecture.
```

Future run manifests should include:

```text
coordinationProfileId
topology
stepCount
agentCallCount
toolCallCount
modelCallCount
inputTokens
outputTokens
estimatedCost
latencyMs
coordinationOverhead
reviewEscalationCount
retryCount
terminationReason
```

This does not need implementation in PR12.5.

But it should influence PR30, PR34, and PR35.

---

## 9. Agentor coordination ontology

Future Agentor coordination should include these concepts.

### CoordinationProfile

Describes the coordination configuration of a run or plan.

```text
CoordinationProfile
  Id
  Name
  Topology
  AuthorityMode
  AggregationMode
  SynchronizationMode
  TerminationPolicy
  FailureHandlingPolicy
  EvaluationSignature
  ComputeBudgetProfile
```

### CoordinationTopology

Possible values:

```text
SingleAgent
SequentialPipeline
IndependentEnsemble
PeerCritiqueDebate
OrchestratorSpecialist
ConsensusAlignment
```

### AuthorityMode

Possible values:

```text
SingleController
PolicyGated
HumanFinalApproval
Distributed
OrchestratorAuthority
```

### AggregationMode

Possible values:

```text
None
FirstValid
Mean
Vote
WeightedVote
CriticMerge
OrchestratorSynthesis
Consensus
```

### SynchronizationMode

Possible values:

```text
Sequential
Parallel
RoundBased
ThresholdConvergence
```

### TerminationPolicy

Possible values:

```text
FixedSteps
FinalStageCompleted
SuccessCondition
BudgetLimit
Timeout
DisagreementThreshold
ConfidenceThreshold
HumanStop
```

### FailureHandlingPolicy

Possible values:

```text
FailFast
Retry
Degrade
Compensate
EscalateToReview
IsolateFailedBranch
```

### ComputeBudgetProfile

Possible values / fields:

```text
MaxSteps
MaxToolCalls
MaxModelCalls
MaxTokens
MaxCost
MaxLatency
MaxReviewEscalations
```

### CoordinationEvaluationSignature

Possible fields:

```text
ExpectedReliabilityBehavior
ExpectedResolutionBehavior
ExpectedCostBehavior
ExpectedLatencyBehavior
ExpectedDiversityBehavior
ExpectedEscalationBehavior
ExpectedFailureModes
ObservedReliabilityBehavior
ObservedResolutionBehavior
ObservedCostBehavior
ObservedLatencyBehavior
ObservedFailureModes
```

---

## 10. Forbidden ontology collapses

Agentor must not collapse these concepts:

```text
Coordination ≠ model routing
Coordination ≠ tool execution
Coordination ≠ runtime policy alone
Coordination ≠ Athanor authority
Coordination ≠ MCP
Coordination ≠ A2A
Coordination ≠ Semantic Kernel planner
Coordination ≠ Microsoft Agent Framework orchestration
Coordination ≠ LangGraph graph
Coordination ≠ AutoGen conversation
Coordination ≠ CrewAI crew
```

External frameworks may provide coordination mechanisms.

They must not define Agentor’s coordination ontology.

---

## 11. Impact on existing Agentor roadmap

### PR12 / PR12.1

Runtime policy engine must distinguish:

```text
Allow
Deny
RequiresReview
```

This is coordination-relevant because review gating is an authority and execution-control decision.

However:

```text
RequiresReview ≠ Deny
Runtime policy ≠ full coordination
```

### PR13

Tool execution pipeline should add timeout, retry, cancellation, and structured failure.

PR13 should not introduce multi-agent coordination.

It should, however, preserve trace data needed for future coordination evaluation:

```text
attempt count
timeout
retry count
duration
failure reason
cancellation reason
```

### PR16

AgentPlan and AgentRecipe should become the first concrete expression of coordination structure.

PR16 should avoid importing ontology from external frameworks.

### PR17

Sequential plan executor should be treated as the first real coordination implementation:

```text
CoordinationTopology = SequentialPipeline
```

### PR18

Conditional and guarded step execution introduces coordination guards.

### PR19

Failure handling, retries, and compensation hooks begin implementing coordination failure semantics.

### PR20

Run state machine hardening should ensure coordination states are explicit and valid.

### PR30

Model-call telemetry should contribute to compute-as-architecture tracking.

### PR34–PR35

Evaluation harness and quality gates should eventually support coordination evaluation signatures.

---

## 12. Summary doctrine

Agentor should adopt the following doctrine:

```text
Agentor owns the governed coordination/runtime layer.

Coordination is explicit, configurable, traceable, and evaluable.

Coordination is separate from:
- canonical knowledge,
- model execution,
- raw tool access,
- external framework orchestration.

Coordination strategies must be compared by observed signatures,
not assumed superior because they use more agents, more debate,
or more consensus.
```

This doctrine should guide PR13 onward, especially PR16–PR20 and PR34–PR35, without collapsing coordination into any single subsystem (for example runtime policy alone or a vendor orchestration graph).

## 13. See also

- `docs/COORDINATION_LAYER.md` — canonical short summary
- `decisions/ADR-008-coordination-is-agentor-runtime-layer.md` — ADR
- Paper: [arXiv:2605.03310](https://arxiv.org/abs/2605.03310) — *Coordination as an Architectural Layer for LLM-Based Multi-Agent Systems* (Nechepurenko, Shuvalov)
