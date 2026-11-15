using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class HomeController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        var session = HttpContext.Session;
        if (session.IsSignedIn())
            return session.GetRole() == SessionUserExtensions.Organiser
                ? RedirectToAction("Index", "Dashboard")
                : RedirectToAction("Index", "Events");
        return View();
    }

    [HttpGet("access-denied")]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = 403;
        return View();
    }

    [Route("error")]
    [IgnoreAntiforgeryToken]
    public IActionResult Error() =>
        View("Error", new ErrorViewModel());

    [Route("status/{code:int}")]
    [IgnoreAntiforgeryToken]
    public IActionResult StatusPage(int code)
    {
        var model = code switch
        {
            404 => new ErrorViewModel { Code = 404, Title = "Page not found", Message = "We could not find the page you were looking for." },
            403 => new ErrorViewModel { Code = 403, Title = "Not allowed", Message = "You do not have access to that page." },
            _ => new ErrorViewModel { Code = code }
        };
        Response.StatusCode = code;
        return View("Error", model);
    }
}
