using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[SessionAuthorize(Role = SessionUserExtensions.Participant)]
[Route("my/enrolments")]
public class EnrolmentsController : Controller
{
    private readonly ApiClient _api;
    public EnrolmentsController(ApiClient api) => _api = api;

    [HttpGet("")]
    public async Task<IActionResult> Index() => View(await _api.GetMyEnrolmentsAsync());

    [HttpPost("{id:int}/withdraw")]
    public async Task<IActionResult> Withdraw(int id)
    {
        await _api.WithdrawAsync(id);
        TempData["Success"] = "You have withdrawn from the event.";
        return RedirectToAction(nameof(Index));
    }
}

[SessionAuthorize(Role = SessionUserExtensions.Participant)]
[Route("my/results")]
public class MyResultsController : Controller
{
    private readonly ApiClient _api;
    public MyResultsController(ApiClient api) => _api = api;

    [HttpGet("")]
    public async Task<IActionResult> Index() => View(await _api.GetMyResultsAsync());
}
