using EcoActivist.Api.Extensions;
using EcoActivist.Application.Common.Security;
using EcoActivist.Application.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcoActivist.Api.Controllers;

[ApiController]
[Route("api/v1/organizer/events")]
[Authorize(Policy = AuthorizationPolicies.RequireApprovedOrganizer)]
[Produces("application/json")]
public sealed class OrganizerEventsController : ControllerBase
{
    private readonly OrganizerEventService _events;

    public OrganizerEventsController(OrganizerEventService events)
    {
        _events = events;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<OrganizerEventDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrganizerEventDto>>> GetMyEvents(CancellationToken ct)
    {
        return Ok(await _events.GetMyEventsAsync(User.GetUserId(), ct));
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateEventRequest request, CancellationToken ct)
    {
        var id = await _events.CreateAsync(User.GetUserId(), request, ct);
        return CreatedAtAction(nameof(EventsController.GetEvent), "Events", new { id }, new { id });
    }

    [HttpPost("{id:guid}/publish")]
    [ProducesResponseType<EventDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<EventDetailsDto>> Publish(Guid id, CancellationToken ct)
    {
        return Ok(await _events.PublishAsync(User.GetUserId(), id, ct));
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelEventRequest request, CancellationToken ct)
    {
        await _events.CancelAsync(User.GetUserId(), id, request.Reason, ct);
        return NoContent();
    }
}
