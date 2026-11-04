using EcoActivist.Domain.Common;

namespace EcoActivist.Domain.Events;

public record EventCancelledDomainEvent(
    Guid EventId,
    string Reason,
    DateTimeOffset OccurredOn
) : IDomainEvent;
