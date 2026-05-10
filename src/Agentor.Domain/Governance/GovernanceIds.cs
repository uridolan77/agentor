namespace Agentor.Domain.Governance;

public readonly record struct TenantId(Guid Value)
{
    public static TenantId From(Guid value) => new(value);
}

public readonly record struct WorkspaceId(Guid Value)
{
    public static WorkspaceId From(Guid value) => new(value);
}

public readonly record struct ProjectId(Guid Value)
{
    public static ProjectId From(Guid value) => new(value);
}

public readonly record struct KnowledgeScopeId(Guid Value)
{
    public static KnowledgeScopeId From(Guid value) => new(value);
}

public readonly record struct ActorId(Guid Value)
{
    public static ActorId From(Guid value) => new(value);
}