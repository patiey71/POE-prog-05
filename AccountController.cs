using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[Route("account")]
public class AccountController : Controller
{
    private readonly ApiClient _api;
    public AccountController(ApiClient api) => _api = api;

    [HttpGet("login")]
    public IActionResult Login(string? returnUrl)
    {
        if (HttpContext.Session.IsSignedIn()) return RedirectToAction("Index", "Home");
        ViewBag.ReturnUrl = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl)
    {
        ViewBag.ReturnUrl = returnUrl;
        if (!ModelState.IsValid) return View(model);

        try
        {
            var profile = await _api.LoginAsync(model.Email.Trim(), model.Password);
            HttpContext.Session.SignIn(profile);
        }
        catch (ApiException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View(model);
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet("register")]
    public IActionResult Register()
    {
        if (HttpContext.Session.IsSignedIn()) return RedirectToAction("Index", "Home");
        return View(new RegisterViewModel());
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (model.Role != SessionUserExtensions.Organiser && model.Role != SessionUserExtensions.Participant)
            ModelState.AddModelError(nameof(model.Role), "Please choose Organiser or Participant.");
        if (!ModelState.IsValid) return View(model);

        try
        {
            model.Email = model.Email.Trim();
            await _api.RegisterAsync(model);
            var profile = await _api.LoginAsync(model.Email, model.Password);   // log in straight after registering
            HttpContext.Session.SignIn(profile);
        }
        catch (ApiException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View(model);
        }

        TempData["Success"] = "Welcome to RaceDay! Your account has been created.";
        return RedirectToAction("Index", "Home");
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        try { await _api.LogoutAsync(); } catch (ApiException) { /* logging out locally is what matters */ }
        HttpContext.Session.SignOut();
        TempData["Success"] = "You have been logged out.";
        return RedirectToAction("Login");
    }
}
