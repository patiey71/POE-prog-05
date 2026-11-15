using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Everything under /organiser is only for users with the Organiser role.</summary>
[SessionAuthorize(Role = SessionUserExtensions.Organiser)]
public abstract class OrganiserControllerBase : Controller
{
    protected readonly ApiClient Api;
    protected OrganiserControllerBase(ApiClient api) => Api = api;

    /// <summary>Loads an event and makes sure it belongs to the logged-in organiser.</summary>
    protected async Task<EventDto?> GetOwnEventAsync(int id)
    {
        var ev = await Api.GetEventAsync(id);
        return ev.OrganiserId == HttpContext.Session.GetUserId() ? ev : null;
    }

    protected IActionResult NotYours() => RedirectToAction("AccessDenied", "Home");

    protected bool BannerTooLarge(IFormFile? file) => file is not null && file.Length > 5 * 1024 * 1024;
}

[Route("organiser")]
public class DashboardController : OrganiserControllerBase
{
    public DashboardController(ApiClient api) : base(api) { }

    [HttpGet("")]
    public async Task<IActionResult> Index() =>
        View(new DashboardViewModel { Events = await Api.GetMyEventsAsync() });
}

[Route("organiser/events")]
public class OrganiserEventsController : OrganiserControllerBase
{
    public OrganiserEventsController(ApiClient api) : base(api) { }

    [HttpGet("")]
    public async Task<IActionResult> Index() => View(await Api.GetMyEventsAsync());

    [HttpGet("create")]
    public IActionResult Create() => View(new EventFormViewModel { Date = DateTime.Today.AddDays(30) });

    [HttpPost("create")]
    public async Task<IActionResult> Create(EventFormViewModel model)
    {
        if (BannerTooLarge(model.Banner)) ModelState.AddModelError(nameof(model.Banner), "The banner is too large. The maximum size is 5 MB.");
        if (!ModelState.IsValid) return View(model);

        var created = await Api.CreateEventAsync(model);
        if (model.Banner is { Length: > 0 })
        {
            try { await Api.UploadBannerAsync(created.Id, model.Banner); }
            catch (ApiException ex) when (ex is not ApiUnauthorizedException)
            {
                TempData["Warning"] = "The event was created, but the banner could not be uploaded: " + ex.Message;
                return RedirectToAction(nameof(Edit), new { id = created.Id });
            }
        }

        TempData["Success"] = "Event created. Now add the categories people can enter.";
        return RedirectToAction("Index", "Categories", new { eventId = created.Id });
    }

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var ev = await GetOwnEventAsync(id);
        if (ev is null) return NotYours();
        return View(new EventFormViewModel
        {
            Id = ev.Id, Name = ev.Name, Description = ev.Description, Date = ev.Date,
            Location = ev.Location, DistanceKm = ev.DistanceKm, EventType = ev.EventType, ExistingBanner = ev.BannerImage
        });
    }

    [HttpPost("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, EventFormViewModel model)
    {
        if (BannerTooLarge(model.Banner)) ModelState.AddModelError(nameof(model.Banner), "The banner is too large. The maximum size is 5 MB.");
        if (!ModelState.IsValid) { model.Id = id; return View(model); }

        await Api.UpdateEventAsync(id, model);
        if (model.Banner is { Length: > 0 })
        {
            try { await Api.UploadBannerAsync(id, model.Banner); }
            catch (ApiException ex) when (ex is not ApiUnauthorizedException)
            {
                TempData["Warning"] = "Your changes were saved, but the banner could not be uploaded: " + ex.Message;
                return RedirectToAction(nameof(Edit), new { id });
            }
        }

        TempData["Success"] = "Event updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id)
    {
        await Api.DeleteEventAsync(id);
        TempData["Success"] = "Event deleted.";
        return RedirectToAction(nameof(Index));
    }
}

[Route("organiser/events/{eventId:int}/categories")]
public class CategoriesController : OrganiserControllerBase
{
    public CategoriesController(ApiClient api) : base(api) { }

    [HttpGet("")]
    public async Task<IActionResult> Index(int eventId)
    {
        var ev = await GetOwnEventAsync(eventId);
        if (ev is null) return NotYours();
        ViewBag.Event = ev;
        ViewBag.Categories = await Api.GetCategoriesAsync(eventId);
        return View(new CategoryFormViewModel());
    }

    [HttpPost("add")]
    public async Task<IActionResult> Add(int eventId, CategoryFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Event = await Api.GetEventAsync(eventId);
            ViewBag.Categories = await Api.GetCategoriesAsync(eventId);
            return View("Index", model);
        }
        await Api.AddCategoryAsync(eventId, model.Name.Trim());
        TempData["Success"] = "Category added.";
        return RedirectToAction(nameof(Index), new { eventId });
    }

    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int eventId, int id)
    {
        await Api.DeleteCategoryAsync(id);
        TempData["Success"] = "Category removed.";
        return RedirectToAction(nameof(Index), new { eventId });
    }
}

[Route("organiser/events/{eventId:int}/enrolments")]
public class OrganiserEnrolmentsController : OrganiserControllerBase
{
    public OrganiserEnrolmentsController(ApiClient api) : base(api) { }

    [HttpGet("")]
    public async Task<IActionResult> Index(int eventId)
    {
        var ev = await GetOwnEventAsync(eventId);
        if (ev is null) return NotYours();
        ViewBag.Event = ev;
        return View(await Api.GetEventEnrolmentsAsync(eventId));
    }

    [HttpPost("{id:int}/status")]
    public async Task<IActionResult> SetStatus(int eventId, int id, string status)
    {
        if (status is not ("Confirmed" or "Pending" or "Cancelled"))
        {
            TempData["Error"] = "That status is not valid.";
            return RedirectToAction(nameof(Index), new { eventId });
        }
        await Api.SetEnrolmentStatusAsync(id, status);
        TempData["Success"] = $"Enrolment marked as {status}.";
        return RedirectToAction(nameof(Index), new { eventId });
    }
}

[Route("organiser/events/{eventId:int}/results")]
public class ResultsEntryController : OrganiserControllerBase
{
    public ResultsEntryController(ApiClient api) : base(api) { }

    [HttpGet("")]
    public async Task<IActionResult> Index(int eventId)
    {
        var ev = await GetOwnEventAsync(eventId);
        if (ev is null) return NotYours();

        var enrolments = await Api.GetEventEnrolmentsAsync(eventId);
        var results = await Api.GetEventResultsAsync(eventId);

        var model = new ResultsEntryViewModel
        {
            Event = ev,
            Rows = enrolments
                .Where(e => e.Status != "Cancelled")
                .Select(e => new ResultRowViewModel { Enrolment = e, Result = results.FirstOrDefault(r => r.EnrolmentId == e.Id) })
                .OrderBy(r => r.Result?.Position ?? int.MaxValue).ThenBy(r => r.Enrolment.ParticipantName)
                .ToList()
        };
        return View(model);
    }

    [HttpPost("save")]
    public async Task<IActionResult> Save(int eventId, ResultSaveViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault()
                                ?? "Please check the finish time and position.";
            return RedirectToAction(nameof(Index), new { eventId });
        }

        await Api.SaveResultAsync(eventId, model.EnrolmentId, model.FinishTime, model.Position);
        TempData["Success"] = "Result saved.";
        return RedirectToAction(nameof(Index), new { eventId });
    }
}
