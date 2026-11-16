using EcoActivist.Api.Extensions;
using EcoActivist.Application.Common.Models;
using EcoActivist.Application.Common.Security;
using EcoActivist.Application.Volunteers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcoActivist.Api.Controllers;

[ApiController]
[Route("api/v1/volunteer/profile")]
[Authorize(Policy = AuthorizationPolicies.RequireVolunteer)]
[Produces("application/json")]
public sealed class VolunteerProfileController : ControllerBase
{
    private readonly VolunteerProfileService _profile;

    public VolunteerProfileController(VolunteerProfileService profile)
    {
        _profile = profile;
    }

    /// <summary>EcoPoints balance, accrual history (paged) and attendance statistics of the current volunteer.</summary>
    [HttpGet]
    [ProducesResponseType<VolunteerProfileDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<VolunteerProfileDto>> Get([FromQuery] PageRequest transactions, CancellationToken ct)
    {
        return Ok(await _profile.GetProfileAsync(User.GetUserId(), transactions, ct));
    }
}
