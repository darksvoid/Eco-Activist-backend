namespace EcoActivist.Domain.Common;

public interface IDomainEvent
{
    DateTimeOffset OccurredOn { get; }
}
