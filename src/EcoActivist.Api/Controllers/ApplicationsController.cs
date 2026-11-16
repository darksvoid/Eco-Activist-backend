using EcoActivist.Api.Extensions;
using EcoActivist.Application.Applications;
using EcoActivist.Application.Common.Security;
using EcoActivist.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcoActivist.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize(Policy = AuthorizationPolicies.RequireVolunteer)]
[Produces("application/json")]
public sealed class ApplicationsController : ControllerBase
{
    private readonly VolunteerApplicationService _applications;

    public ApplicationsController(VolunteerApplicationService applications)
    {
        _applications = applications;
    }

    [HttpPost("events/{id:guid}/apply")]
    [ProducesResponseType<ApplicationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApplicationDto>> Apply(Guid id, CancellationToken ct)
    {
        var application = await _applications.ApplyAsync(User.GetUserId(), id, ct);
        return StatusCode(StatusCodes.Status201Created, application);
    }

    [HttpPost("applications/{id:guid}/cancel")]
    [ProducesResponseType<ApplicationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ApplicationDto>> Cancel(Guid id, CancellationToken ct)
    {
        return Ok(await _applications.CancelAsync(User.GetUserId(), id, ct));
    }

    [HttpGet("applications/my")]
    [ProducesResponseType<IReadOnlyList<MyApplicationDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MyApplicationDto>>> GetMy([FromQuery] ApplicationStatus? status, CancellationToken ct)
    {
        return Ok(await _applications.GetMyApplicationsAsync(User.GetUserId(), status, ct));
    }

    [HttpGet("applications/{id:guid}/qr")]
    [ProducesResponseType<QrCodeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<QrCodeDto>> GetQr(Guid id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await _applications.GetQrCodeAsync(User.GetUserId(), id, ct));
    }
}
