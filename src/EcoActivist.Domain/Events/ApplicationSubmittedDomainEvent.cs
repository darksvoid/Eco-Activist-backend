using EcoActivist.Domain.Common;

namespace EcoActivist.Domain.Events;

public record ApplicationSubmittedDomainEvent(
    Guid ApplicationId,
    Guid EventId,
    Guid UserId,
    DateTimeOffset OccurredOn
) : IDomainEvent;
