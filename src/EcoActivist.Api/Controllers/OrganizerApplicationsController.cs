using EcoActivist.Api.Extensions;
using EcoActivist.Application.Applications;
using EcoActivist.Application.Common.Security;
using EcoActivist.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcoActivist.Api.Controllers;

[ApiController]
[Route("api/v1/organizer")]
[Authorize(Policy = AuthorizationPolicies.RequireApprovedOrganizer)]
[Produces("application/json")]
public sealed class OrganizerApplicationsController : ControllerBase
{
    private readonly OrganizerApplicationService _applications;

    public OrganizerApplicationsController(OrganizerApplicationService applications)
    {
        _applications = applications;
    }

    [HttpGet("events/{eventId:guid}/applications")]
    [ProducesResponseType<IReadOnlyList<OrganizerApplicationDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<OrganizerApplicationDto>>> GetApplications(
        Guid eventId,
        [FromQuery] ApplicationStatus? status,
        CancellationToken ct)
    {
        return Ok(await _applications.GetEventApplicationsAsync(User.GetUserId(), eventId, status, ct));
    }

    [HttpPost("applications/{id:guid}/confirm")]
    [ProducesResponseType<ApplicationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApplicationDto>> Confirm(Guid id, CancellationToken ct)
    {
        return Ok(await _applications.ConfirmAsync(User.GetUserId(), id, ct));
    }

    [HttpPost("applications/{id:guid}/reject")]
    [ProducesResponseType<ApplicationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApplicationDto>> Reject(Guid id, [FromBody] RejectApplicationRequest request, CancellationToken ct)
    {
        return Ok(await _applications.RejectAsync(User.GetUserId(), id, request.Reason, ct));
    }
}
