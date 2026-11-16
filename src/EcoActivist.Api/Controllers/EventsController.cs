using EcoActivist.Application.Common.Models;
using EcoActivist.Application.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcoActivist.Api.Controllers;

[ApiController]
[Route("api/v1/events")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class EventsController : ControllerBase
{
    private readonly EventCatalogService _catalog;

    public EventsController(EventCatalogService catalog)
    {
        _catalog = catalog;
    }

    /// <summary>Catalog of published events with filters, sorting (date, availablePlaces, distance) and pagination.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<EventListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<EventListItemDto>>> GetEvents([FromQuery] EventListQuery query, CancellationToken ct)
    {
        return Ok(await _catalog.GetPublishedEventsAsync(query, ct));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<EventDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDetailsDto>> GetEvent(Guid id, CancellationToken ct)
    {
        return Ok(await _catalog.GetEventAsync(id, ct));
    }
}
