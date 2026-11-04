using EcoActivist.Domain.Common;

namespace EcoActivist.Domain.Events;

public record ApplicationRejectedDomainEvent(
    Guid ApplicationId,
    Guid EventId,
    Guid UserId,
    string Reason,
    DateTimeOffset OccurredOn
) : IDomainEvent;
