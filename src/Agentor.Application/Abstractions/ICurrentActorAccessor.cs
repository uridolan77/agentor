namespace Agentor.Application.Abstractions;

public enum ActorRole
{
    System,
    HumanOperator,
    Service
}

public sealed record ActorContext(Guid ActorId, string DisplayName, ActorRole Role);

/// <summary>Supplies the acting principal for mutating governance operations (PR54).</summary>
public interface ICurrentActorAccessor
{
    ActorContext Current { get; }
}
