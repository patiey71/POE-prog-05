using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/files")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly IFileStorage _files;
    public FilesController(IFileStorage files) => _files = files;

    /// <summary>Download an event banner or profile picture by its stored name (logged-in users only).</summary>
    /// <response code="200">The image.</response>
    /// <response code="404">No such image.</response>
    [HttpGet("{**name}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string name)
    {
        if (!(name.StartsWith("banners/") || name.StartsWith("profiles/")) || name.Contains("..")) return NotFound();
        var file = await _files.OpenAsync(name);
        return file is null ? NotFound() : File(file.Content, file.ContentType);
    }
}
