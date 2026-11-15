using System.ComponentModel.DataAnnotations;

namespace RaceDay.Web.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Please enter your email address."), EmailAddress(ErrorMessage = "That does not look like a valid email address.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Please enter your password.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "First name is required."), StringLength(100)]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = "";

    [Required(ErrorMessage = "Last name is required."), StringLength(100)]
    [Display(Name = "Last name")]
    public string LastName { get; set; } = "";

    [Required(ErrorMessage = "Email is required."), EmailAddress(ErrorMessage = "That does not look like a valid email address."), StringLength(256)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Password is required."), StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Please confirm your password.")]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = "";

    [Required(ErrorMessage = "Please choose a role.")]
    public string Role { get; set; } = "Participant";

    [Display(Name = "Date of birth")]
    [DataType(DataType.Date)]
    public DateTime? DateOfBirth { get; set; }

    [Display(Name = "Phone number"), StringLength(30)]
    public string? PhoneNumber { get; set; }

    [Display(Name = "Organiser code")]
    public string? OrganiserCode { get; set; }
}

public class EventFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Event name is required."), StringLength(200)]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Please describe the event."), StringLength(2000)]
    public string Description { get; set; } = "";

    [Required(ErrorMessage = "Please choose the event date.")]
    [DataType(DataType.Date)]
    public DateTime? Date { get; set; }

    [Required(ErrorMessage = "Location is required."), StringLength(300)]
    public string Location { get; set; } = "";

    [Range(0.01, 1000, ErrorMessage = "Distance must be between 0.01 and 1000 km.")]
    [Display(Name = "Distance (km)")]
    public decimal DistanceKm { get; set; } = 5;

    [Required(ErrorMessage = "Please choose an event type.")]
    [Display(Name = "Event type")]
    public string EventType { get; set; } = "Run";

    /// <summary>Optional banner image (jpg, png, gif, webp up to 5 MB). Sent on to the API, which stores it in Azure Blob Storage.</summary>
    public IFormFile? Banner { get; set; }

    public string? ExistingBanner { get; set; }
}

public class CategoryFormViewModel
{
    [Required(ErrorMessage = "Category name is required."), StringLength(100)]
    public string Name { get; set; } = "";
}

public class ProfileViewModel
{
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public string? ProfilePicture { get; set; }

    [Required(ErrorMessage = "First name is required."), StringLength(100)]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = "";

    [Required(ErrorMessage = "Last name is required."), StringLength(100)]
    [Display(Name = "Last name")]
    public string LastName { get; set; } = "";

    [Display(Name = "Date of birth")]
    [DataType(DataType.Date)]
    public DateTime? DateOfBirth { get; set; }

    [Display(Name = "Phone number"), StringLength(30)]
    public string? PhoneNumber { get; set; }
}

public class EventListViewModel
{
    public List<EventDto> Events { get; set; } = new();
    public string? Search { get; set; }
    public string? Type { get; set; }
    public string When { get; set; } = "upcoming";
    public string Sort { get; set; } = "date";
}

public class EventDetailsViewModel
{
    public EventDto Event { get; set; } = null!;
    public List<CategoryDto> Categories { get; set; } = new();
    public EnrolmentDto? MyEnrolment { get; set; }
}

public class DashboardViewModel
{
    public List<EventDto> Events { get; set; } = new();
    public int TotalEvents => Events.Count;
    public int UpcomingEvents => Events.Count(e => e.Date.Date >= DateTime.Today);
    public int TotalEnrolments => Events.Sum(e => e.EnrolmentCount);
}

public class ResultRowViewModel
{
    public EnrolmentDto Enrolment { get; set; } = null!;
    public ResultDto? Result { get; set; }
}

public class ResultsEntryViewModel
{
    public EventDto Event { get; set; } = null!;
    public List<ResultRowViewModel> Rows { get; set; } = new();
}

public class ResultSaveViewModel
{
    public int EnrolmentId { get; set; }

    [Required(ErrorMessage = "Enter the finish time as hh:mm:ss.")]
    [RegularExpression(@"^\d{1,2}:[0-5]\d:[0-5]\d$", ErrorMessage = "Finish time must look like 00:45:30 (hh:mm:ss).")]
    public string FinishTime { get; set; } = "";

    [Range(1, 100000, ErrorMessage = "Position must be 1 or higher.")]
    public int Position { get; set; }
}

public class ErrorViewModel
{
    public int Code { get; set; } = 500;
    public string Title { get; set; } = "Something went wrong";
    public string Message { get; set; } = "An unexpected problem occurred. Please try again.";
}
