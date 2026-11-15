namespace RaceDay.Web.Models;

// Shapes of the JSON the RaceDay API returns. The web app only ever talks to the API.

public record ProfileDto(int Id, string FirstName, string LastName, string Email, string Role,
    DateTime? DateOfBirth, string? PhoneNumber, string? ProfilePicture);

public record EventDto(int Id, string Name, string Description, DateTime Date, string Location,
    decimal DistanceKm, string EventType, int OrganiserId, string OrganiserName,
    string? BannerImage, int EnrolmentCount);

public record CategoryDto(int Id, int EventId, string Name);

public record EnrolmentDto(int Id, int EventId, string EventName, int ParticipantId, string ParticipantName,
    int CategoryId, string CategoryName, DateTime EnrolledAt, string Status, DateTime EventDate);

public record ResultDto(int Id, int EnrolmentId, int EventId, string EventName, int ParticipantId,
    string ParticipantName, string CategoryName, TimeSpan FinishTime, int Position,
    int TotalFinishers, DateTime EventDate);
