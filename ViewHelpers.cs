using Microsoft.AspNetCore.Mvc;

namespace RaceDay.Web.Services;

/// <summary>Small formatting helpers used by the Razor views.</summary>
public static class ViewHelpers
{
    public static string Banner(this IUrlHelper url, string? name) =>
        string.IsNullOrEmpty(name) ? url.Content("~/img/banner-default.svg") : url.Content("~/files/" + name);

    public static string Avatar(this IUrlHelper url, string? name) =>
        string.IsNullOrEmpty(name) ? url.Content("~/img/avatar-default.svg") : url.Content("~/files/" + name);

    /// <summary>1 -> 1st, 2 -> 2nd, 47 -> 47th.</summary>
    public static string Ordinal(int n)
    {
        var suffix = (n % 100) is >= 11 and <= 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return n + suffix;
    }

    public static string FinishTime(TimeSpan t) => $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";
    public static string ShortDate(DateTime d) => d.ToString("dd MMM yyyy");
    public static string LongDate(DateTime d) => d.ToString("dddd, d MMMM yyyy");

    /// <summary>CSS class for an enrolment status: green = Confirmed, amber = Pending, grey = Cancelled.</summary>
    public static string StatusClass(string status) => status switch
    {
        "Confirmed" => "status-confirmed",
        "Cancelled" => "status-cancelled",
        _ => "status-pending"
    };

    public static string ChipClass(string type) => type switch
    {
        "Walk" => "chip-walk",
        "Cycle" => "chip-cycle",
        _ => "chip-run"
    };
}
