using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Browsing events and enrolling (shared by both roles; enrolling is Participant only).</summary>
[SessionAuthorize]
[Route("events")]
public class EventsController : Controller
{
    private readonly ApiClient _api;
    public EventsController(ApiClient api) => _api = api;

    [HttpGet("")]
    public async Task<IActionResult> Index(string? search, string? type, string when = "upcoming", string sort = "date")
    {
        var events = await _api.GetEventsAsync();
        IEnumerable<EventDto> query = events;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(e => e.Name.Contains(s, StringComparison.OrdinalIgnoreCase)
                                     || e.Location.Contains(s, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(e => e.EventType == type);

        query = when switch
        {
            "past" => query.Where(e => e.Date.Date < DateTime.Today),
            "all" => query,
            _ => query.Where(e => e.Date.Date >= DateTime.Today)
        };

        query = sort switch
        {
            "name" => query.OrderBy(e => e.Name),
            "distance" => query.OrderBy(e => e.DistanceKm),
            _ => when == "past" ? query.OrderByDescending(e => e.Date) : query.OrderBy(e => e.Date)
        };

        return View(new EventListViewModel { Events = query.ToList(), Search = search, Type = type, When = when, Sort = sort });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var model = new EventDetailsViewModel
        {
            Event = await _api.GetEventAsync(id),
            Categories = await _api.GetCategoriesAsync(id)
        };

        if (HttpContext.Session.GetRole() == SessionUserExtensions.Participant)
            model.MyEnrolment = (await _api.GetMyEnrolmentsAsync()).FirstOrDefault(e => e.EventId == id);

        return View(model);
    }

    [HttpPost("{id:int}/enrol")]
    [SessionAuthorize(Role = SessionUserExtensions.Participant)]
    public async Task<IActionResult> Enrol(int id, int? categoryId)
    {
        if (categoryId is null)
        {
            TempData["Error"] = "Please choose a category before enrolling.";
            return RedirectToAction(nameof(Details), new { id });
        }

        await _api.EnrolAsync(id, categoryId.Value);
        TempData["Success"] = "You are enrolled! Your enrolment is Pending until the organiser confirms it.";
        return RedirectToAction("Index", "Enrolments");
    }
}
