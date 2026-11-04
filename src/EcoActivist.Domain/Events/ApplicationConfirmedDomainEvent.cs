using EcoActivist.Domain.Common;

namespace EcoActivist.Domain.Events;

public record ApplicationConfirmedDomainEvent(
    Guid ApplicationId,
    Guid EventId,
    Guid UserId,
    DateTimeOffset OccurredOn
) : IDomainEvent;
