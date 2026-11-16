using EcoActivist.Api.Extensions;
using EcoActivist.Application.Attendance;
using EcoActivist.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcoActivist.Api.Controllers;

[ApiController]
[Route("api/v1/events/{id:guid}/attendance")]
[Authorize(Policy = AuthorizationPolicies.RequireApprovedOrganizer)]
[Produces("application/json")]
public sealed class AttendanceController : ControllerBase
{
    private readonly AttendanceService _attendance;

    public AttendanceController(AttendanceService attendance)
    {
        _attendance = attendance;
    }

    [HttpPost("qr")]
    [ProducesResponseType<AttendanceResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AttendanceResultDto>> MarkByQr(Guid id, [FromBody] MarkAttendanceByQrRequest request, CancellationToken ct)
    {
        return Ok(await _attendance.MarkByQrAsync(User.GetUserId(), id, request.Token, ct));
    }

    [HttpPost("manual")]
    [ProducesResponseType<AttendanceResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AttendanceResultDto>> MarkManual(Guid id, [FromBody] MarkAttendanceManualRequest request, CancellationToken ct)
    {
        return Ok(await _attendance.MarkManualAsync(User.GetUserId(), id, request.UserId, ct));
    }
}
