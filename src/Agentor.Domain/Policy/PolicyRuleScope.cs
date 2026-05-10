namespace Agentor.Domain.Policy;

public enum PolicyRuleScope
{
    Global,
    Tenant,
    Workspace,
    Project,

    /// <summary>Rule applies when <see cref="PolicyRule.ScopeKnowledgeScopeId"/> matches the run's knowledge scope.</summary>
    KnowledgeScope
}
