namespace Agentor.Application.Abstractions;

public enum ActorRole
{
    System,
    HumanOperator,
    /// <summary>May approve human reviews that were escalated (Phase 28).</summary>
    HumanGovernanceApprover,
    Service
}

public sealed record ActorContext(Guid ActorId, string DisplayName, ActorRole Role);

/// <summary>Supplies the acting principal for mutating governance operations (PR54).</summary>
public interface ICurrentActorAccessor
{
    ActorContext Current { get; }
}
