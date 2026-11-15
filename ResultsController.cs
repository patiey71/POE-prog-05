using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
[Produces("application/json")]
public class ResultsController : ApiControllerBase
{
    private readonly AppDbContext _db;
    public ResultsController(AppDbContext db) => _db = db;

    private static ResultResponse ToResponse(Result r, int totalFinishers) =>
        new(r.Id, r.EnrolmentId, r.Enrolment.EventId, r.Enrolment.Event.Name, r.Enrolment.UserId,
            r.Enrolment.User.FirstName + " " + r.Enrolment.User.LastName,
            r.Enrolment.Category.Name, r.FinishTime, r.Position, totalFinishers, r.Enrolment.Event.Date);

    /// <summary>Turns results into responses, adding how many finishers each event has (for "47th out of 312").</summary>
    private async Task<List<ResultResponse>> ProjectAsync(List<Result> results)
    {
        var eventIds = results.Select(r => r.Enrolment.EventId).Distinct().ToList();
        var totals = await _db.Results.AsNoTracking()
            .Where(r => eventIds.Contains(r.Enrolment.EventId))
            .GroupBy(r => r.Enrolment.EventId)
            .Select(g => new { EventId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EventId, x => x.Count);
        return results.Select(r => ToResponse(r, totals.GetValueOrDefault(r.Enrolment.EventId))).ToList();
    }

    private IQueryable<Result> Full() =>
        _db.Results.AsNoTracking()
            .Include(r => r.Enrolment).ThenInclude(e => e.Event)
            .Include(r => r.Enrolment).ThenInclude(e => e.User)
            .Include(r => r.Enrolment).ThenInclude(e => e.Category);

    /// <summary>Capture (or correct) a participant's finish time and position for your own event (Organiser only).</summary>
    /// <response code="201">Result captured.</response>
    /// <response code="200">Existing result updated.</response>
    /// <response code="400">Enrolment does not belong to this event.</response>
    /// <response code="403">Not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">Event not found.</response>
    [HttpPost("events/{eventId:int}/results")]
    [Authorize(Roles = nameof(UserRole.Organiser))]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Capture(int eventId, [FromBody] ResultUpsertDto dto)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId);
        if (ev is null) return NotFound();
        if (ev.OrganiserId != CurrentUserId) return Forbid();

        if (dto.FinishTime!.Value <= TimeSpan.Zero)
            return BadRequest(new { error = "Finish time must be greater than zero." });

        var enrolmentOk = await _db.Enrolments.AnyAsync(e => e.Id == dto.EnrolmentId && e.EventId == eventId);
        if (!enrolmentOk)
            return BadRequest(new { error = "The enrolment does not belong to this event." });

        var result = await _db.Results.FirstOrDefaultAsync(r => r.EnrolmentId == dto.EnrolmentId);
        var created = result is null;
        if (created)
        {
            result = new Result { EnrolmentId = dto.EnrolmentId!.Value };
            _db.Results.Add(result);
        }
        result!.FinishTime = dto.FinishTime.Value;
        result.Position = dto.Position;
        await _db.SaveChangesAsync();

        var saved = (await ProjectAsync(new List<Result> { await Full().FirstAsync(r => r.Id == result.Id) }))[0];
        return created ? Created($"/api/events/{eventId}/results", saved) : Ok(saved);
    }

    /// <summary>View all results for your own event, ordered by position (Organiser only).</summary>
    [HttpGet("events/{eventId:int}/results")]
    [Authorize(Roles = nameof(UserRole.Organiser))]
    [ProducesResponseType(typeof(IEnumerable<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ForEvent(int eventId)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId);
        if (ev is null) return NotFound();
        if (ev.OrganiserId != CurrentUserId) return Forbid();

        var list = await Full().Where(r => r.Enrolment.EventId == eventId).OrderBy(r => r.Position).ToListAsync();
        return Ok(await ProjectAsync(list));
    }

    /// <summary>View your own results (Participant only).</summary>
    [HttpGet("results/mine")]
    [Authorize(Roles = nameof(UserRole.Participant))]
    [ProducesResponseType(typeof(IEnumerable<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Mine()
    {
        var userId = CurrentUserId;
        var list = await Full().Where(r => r.Enrolment.UserId == userId).ToListAsync();
        list = list.OrderByDescending(r => r.Enrolment.Event.Date).ToList();
        return Ok(await ProjectAsync(list));
    }
}
