using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize]
[Produces("application/json")]
public class ProfileController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly IFileStorage _files;
    public ProfileController(AppDbContext db, IFileStorage files) { _db = db; _files = files; }

    internal static ProfileResponse ToResponse(User u) =>
        new(u.Id, u.FirstName, u.LastName, u.Email, u.Role.ToString(), u.DateOfBirth, u.PhoneNumber, u.ProfilePicture);

    /// <summary>View your own profile (Organiser or Participant).</summary>
    /// <response code="200">The logged-in user's profile.</response>
    /// <response code="401">Not logged in.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Get()
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == CurrentUserId);
        return user is null ? Unauthorized() : Ok(ToResponse(user));
    }

    /// <summary>Update your own profile. Email and role cannot be changed here.</summary>
    /// <response code="200">Updated profile.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    [HttpPut]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Update([FromBody] UpdateProfileDto dto)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == CurrentUserId);
        if (user is null) return Unauthorized();

        user.FirstName = dto.FirstName.Trim();
        user.LastName = dto.LastName.Trim();
        user.DateOfBirth = dto.DateOfBirth;
        user.PhoneNumber = dto.PhoneNumber;
        await _db.SaveChangesAsync();

        return Ok(ToResponse(user));
    }

    /// <summary>Upload or replace your profile picture (jpg, png, gif or webp, max 5 MB). Stored in Azure Blob Storage.</summary>
    /// <response code="200">Updated profile.</response>
    /// <response code="400">Missing or invalid image.</response>
    [HttpPost("picture")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UploadPicture(IFormFile? file)
    {
        var check = await ImageRules.ValidateAsync(file);
        if (!check.Ok) return BadRequest(new { error = check.Error });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == CurrentUserId);
        if (user is null) return Unauthorized();

        await using var stream = file!.OpenReadStream();
        var name = await _files.SaveAsync(stream, "profiles", check.Extension!, check.ContentType!);

        var old = user.ProfilePicture;
        user.ProfilePicture = name;
        await _db.SaveChangesAsync();
        if (!string.IsNullOrEmpty(old)) await _files.DeleteAsync(old);

        return Ok(ToResponse(user));
    }
}
