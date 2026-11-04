using EcoActivist.Domain.Events;
using EcoActivist.Application.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EcoActivist.Application.EventHandlers;

public class EventPublishedDomainEventHandler : INotificationHandler<DomainEventNotification<EventPublishedDomainEvent>>
{
    private readonly ILogger<EventPublishedDomainEventHandler> _logger;

    public EventPublishedDomainEventHandler(ILogger<EventPublishedDomainEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(DomainEventNotification<EventPublishedDomainEvent> notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;
        _logger.LogInformation("Доменное событие: Мероприятие {EventId} опубликовано в {OccurredOn}",
            domainEvent.EventId, domainEvent.OccurredOn);

        return Task.CompletedTask;
    }
}
