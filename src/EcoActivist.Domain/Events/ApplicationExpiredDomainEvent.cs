using EcoActivist.Domain.Common;

namespace EcoActivist.Domain.Events;

public record ApplicationExpiredDomainEvent(
    Guid ApplicationId,
    Guid EventId,
    Guid UserId,
    DateTimeOffset OccurredOn
) : IDomainEvent;
