# Framework Decision Matrix

| Framework/protocol | Use in core? | When | How |
|---|---:|---|---|
| MCP | No | PR36–PR37 | Tool discovery/binding adapter |
| Microsoft Agent Framework | No | PR41+ if useful | External adapter |
| Semantic Kernel | No | PR41+ if useful | Prompt/function adapter, model calls still through Conexus |
| A2A | No | PR41+ | External-agent communication adapter |
| LangGraph | No | Later, optional | External runtime adapter |
| AutoGen | No | Later, optional | External runtime adapter |
| CrewAI | No | Later, optional | External runtime adapter |

Rule: frameworks adapt to Agentor. Agentor does not adapt itself into a framework clone.
