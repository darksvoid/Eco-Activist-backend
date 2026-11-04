namespace EcoActivist.Application.Common;

public record DomainEventNotification<TEvent>(TEvent DomainEvent) : MediatR.INotification where TEvent : Domain.Common.IDomainEvent;
