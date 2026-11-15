using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>
/// Serves event banners and profile pictures. The bytes come from the API (which reads them from Azure
/// Blob Storage), so the browser never needs credentials for - or a direct connection to - Azure.
/// </summary>
[SessionAuthorize]
public class FilesController : Controller
{
    private readonly ApiClient _api;
    public FilesController(ApiClient api) => _api = api;

    [HttpGet("files/{**name}")]
    public async Task<IActionResult> Get(string name)
    {
        try
        {
            var (bytes, contentType) = await _api.GetFileAsync(name);
            Response.Headers["Cache-Control"] = "private, max-age=3600";
            return File(bytes, contentType);
        }
        catch (ApiException ex) when (ex is not ApiUnauthorizedException)
        {
            // Missing or unreadable image: show a default picture rather than a broken icon.
            var fallback = name.StartsWith("profiles/") ? "~/img/avatar-default.svg" : "~/img/banner-default.svg";
            return LocalRedirect(fallback);
        }
    }
}
