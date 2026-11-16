using EcoActivist.Application.Admin;
using EcoActivist.Application.Common.Security;
using EcoActivist.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcoActivist.Api.Controllers;

[ApiController]
[Route("api/v1/admin/organizers")]
[Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
[Produces("application/json")]
public sealed class AdminOrganizersController : ControllerBase
{
    private readonly OrganizerModerationService _moderation;

    public AdminOrganizersController(OrganizerModerationService moderation)
    {
        _moderation = moderation;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<OrganizerDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OrganizerDto>>> Get([FromQuery] OrganizerStatus? status, CancellationToken ct)
    {
        return Ok(await _moderation.GetOrganizersAsync(status, ct));
    }

    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType<OrganizerDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OrganizerDto>> Approve(Guid id, CancellationToken ct) =>
        Ok(await _moderation.ApproveAsync(id, ct));

    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType<OrganizerDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OrganizerDto>> Reject(Guid id, CancellationToken ct) =>
        Ok(await _moderation.RejectAsync(id, ct));

    [HttpPost("{id:guid}/revision")]
    [ProducesResponseType<OrganizerDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OrganizerDto>> SendToRevision(Guid id, CancellationToken ct) =>
        Ok(await _moderation.SendToRevisionAsync(id, ct));
}
