using EcoActivist.Api.Extensions;
using EcoActivist.Application.Common.Models;
using EcoActivist.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcoActivist.Api.Controllers;

[ApiController]
[Route("api/v1/notifications")]
[Authorize]
[Produces("application/json")]
public sealed class NotificationsController : ControllerBase
{
    private readonly NotificationService _notifications;

    public NotificationsController(NotificationService notifications)
    {
        _notifications = notifications;
    }

    [HttpGet]
    [ProducesResponseType<PagedResult<NotificationDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<NotificationDto>>> GetMy([FromQuery] NotificationListQuery query, CancellationToken ct)
    {
        return Ok(await _notifications.GetMyAsync(User.GetUserId(), query, ct));
    }

    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken ct)
    {
        await _notifications.MarkAsReadAsync(User.GetUserId(), id, ct);
        return NoContent();
    }
}
