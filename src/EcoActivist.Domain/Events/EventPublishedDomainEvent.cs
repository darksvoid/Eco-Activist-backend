using EcoActivist.Domain.Common;

namespace EcoActivist.Domain.Events;

public record EventPublishedDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredOn
) : IDomainEvent;
