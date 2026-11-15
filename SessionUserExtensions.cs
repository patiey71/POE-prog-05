using RaceDay.Web.Models;

namespace RaceDay.Web.Services;

/// <summary>
/// Session management for the web app. After login the user's id, name, role and picture are kept in the
/// server-side ASP.NET session, together with the API's own session cookie (forwarded on every API call).
/// </summary>
public static class SessionUserExtensions
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";

    private const string IdKey = "user.id";
    private const string NameKey = "user.name";
    private const string RoleKey = "user.role";
    private const string PictureKey = "user.picture";
    private const string CookieKey = "api.cookie";

    public static void SignIn(this ISession s, ProfileDto p)
    {
        var cookie = s.GetApiCookie();
        s.Clear();                       // fresh session on every login
        s.SetApiCookie(cookie);
        s.SetInt32(IdKey, p.Id);
        s.SetString(NameKey, p.FirstName + " " + p.LastName);
        s.SetString(RoleKey, p.Role);
        s.SetString(PictureKey, p.ProfilePicture ?? "");
    }

    public static void UpdateDisplay(this ISession s, ProfileDto p)
    {
        s.SetString(NameKey, p.FirstName + " " + p.LastName);
        s.SetString(PictureKey, p.ProfilePicture ?? "");
    }

    public static void SignOut(this ISession s) => s.Clear();

    public static bool IsSignedIn(this ISession s) => s.GetInt32(IdKey).HasValue;
    public static int GetUserId(this ISession s) => s.GetInt32(IdKey) ?? 0;
    public static string GetDisplayName(this ISession s) => s.GetString(NameKey) ?? "";
    public static string GetRole(this ISession s) => s.GetString(RoleKey) ?? "";
    public static string? GetPicture(this ISession s)
    {
        var p = s.GetString(PictureKey);
        return string.IsNullOrEmpty(p) ? null : p;
    }

    public static string? GetApiCookie(this ISession s) => s.GetString(CookieKey);
    public static void SetApiCookie(this ISession s, string? cookie)
    {
        if (string.IsNullOrEmpty(cookie)) s.Remove(CookieKey);
        else s.SetString(CookieKey, cookie);
    }
}
