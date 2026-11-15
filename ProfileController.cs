using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[SessionAuthorize]
[Route("profile")]
public class ProfileController : Controller
{
    private readonly ApiClient _api;
    public ProfileController(ApiClient api) => _api = api;

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var p = await _api.GetProfileAsync();
        return View(ToModel(p));
    }

    [HttpPost("update")]
    public async Task<IActionResult> Update(ProfileViewModel model)
    {
        if (!ModelState.IsValid)
        {
            // Email, role and picture are not posted back, so reload them for display.
            var current = await _api.GetProfileAsync();
            model.Email = current.Email;
            model.Role = current.Role;
            model.ProfilePicture = current.ProfilePicture;
            return View("Index", model);
        }

        var updated = await _api.UpdateProfileAsync(model);
        HttpContext.Session.UpdateDisplay(updated);
        TempData["Success"] = "Your profile has been updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("picture")]
    public async Task<IActionResult> Picture(IFormFile? picture)
    {
        if (picture is null || picture.Length == 0)
        {
            TempData["Error"] = "Please choose an image to upload.";
            return RedirectToAction(nameof(Index));
        }
        if (picture.Length > 5 * 1024 * 1024)
        {
            TempData["Error"] = "That image is too large. The maximum size is 5 MB.";
            return RedirectToAction(nameof(Index));
        }

        var updated = await _api.UploadProfilePictureAsync(picture);
        HttpContext.Session.UpdateDisplay(updated);
        TempData["Success"] = "Your profile picture has been updated.";
        return RedirectToAction(nameof(Index));
    }

    private static ProfileViewModel ToModel(ProfileDto p) => new()
    {
        Email = p.Email, Role = p.Role, ProfilePicture = p.ProfilePicture,
        FirstName = p.FirstName, LastName = p.LastName, DateOfBirth = p.DateOfBirth, PhoneNumber = p.PhoneNumber
    };
}
