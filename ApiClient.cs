using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using RaceDay.Web.Models;

namespace RaceDay.Web.Services;

/// <summary>
/// The only place the web app talks to the outside world: it calls the RaceDay API over HTTP.
/// (Azure Blob Storage is never touched from here - the API stores and serves the images.)
/// </summary>
public class ApiClient
{
    private const string ApiCookieName = "RaceDay.Session";

    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _context;

    public ApiClient(HttpClient http, IHttpContextAccessor context)
    {
        _http = http;
        _context = context;
    }

    private ISession Session => _context.HttpContext!.Session;

    // ---------- Account ----------

    public async Task<ProfileDto> LoginAsync(string email, string password)
    {
        var res = await SendAsync(HttpMethod.Post, "api/auth/login", JsonBody(new { email, password }), loginCall: true);
        return await ReadAsync<ProfileDto>(res);
    }

    public async Task RegisterAsync(RegisterViewModel m)
    {
        await SendAsync(HttpMethod.Post, "api/auth/register", JsonBody(new
        {
            m.FirstName, m.LastName, m.Email, m.Password, m.Role, m.DateOfBirth, m.PhoneNumber, m.OrganiserCode
        }), loginCall: true);
    }

    public async Task LogoutAsync() => await SendAsync(HttpMethod.Post, "api/auth/logout", null);

    // ---------- Profile ----------

    public async Task<ProfileDto> GetProfileAsync() =>
        await ReadAsync<ProfileDto>(await SendAsync(HttpMethod.Get, "api/profile", null));

    public async Task<ProfileDto> UpdateProfileAsync(ProfileViewModel m) =>
        await ReadAsync<ProfileDto>(await SendAsync(HttpMethod.Put, "api/profile",
            JsonBody(new { m.FirstName, m.LastName, m.DateOfBirth, m.PhoneNumber })));

    public async Task<ProfileDto> UploadProfilePictureAsync(IFormFile file) =>
        await ReadAsync<ProfileDto>(await SendAsync(HttpMethod.Post, "api/profile/picture", FileBody(file)));

    // ---------- Events ----------

    public async Task<List<EventDto>> GetEventsAsync() =>
        await ReadAsync<List<EventDto>>(await SendAsync(HttpMethod.Get, "api/events", null));

    public async Task<List<EventDto>> GetMyEventsAsync() =>
        await ReadAsync<List<EventDto>>(await SendAsync(HttpMethod.Get, "api/events/mine", null));

    public async Task<EventDto> GetEventAsync(int id) =>
        await ReadAsync<EventDto>(await SendAsync(HttpMethod.Get, $"api/events/{id}", null));

    public async Task<EventDto> CreateEventAsync(EventFormViewModel m) =>
        await ReadAsync<EventDto>(await SendAsync(HttpMethod.Post, "api/events", EventBody(m)));

    public async Task<EventDto> UpdateEventAsync(int id, EventFormViewModel m) =>
        await ReadAsync<EventDto>(await SendAsync(HttpMethod.Put, $"api/events/{id}", EventBody(m)));

    public async Task DeleteEventAsync(int id) =>
        await SendAsync(HttpMethod.Delete, $"api/events/{id}", null);

    public async Task<EventDto> UploadBannerAsync(int eventId, IFormFile file) =>
        await ReadAsync<EventDto>(await SendAsync(HttpMethod.Post, $"api/events/{eventId}/banner", FileBody(file)));

    private static HttpContent EventBody(EventFormViewModel m) => JsonBody(new
    {
        m.Name, m.Description, m.Date, m.Location, m.DistanceKm, m.EventType
    });

    // ---------- Categories ----------

    public async Task<List<CategoryDto>> GetCategoriesAsync(int eventId) =>
        await ReadAsync<List<CategoryDto>>(await SendAsync(HttpMethod.Get, $"api/events/{eventId}/categories", null));

    public async Task AddCategoryAsync(int eventId, string name) =>
        await SendAsync(HttpMethod.Post, $"api/events/{eventId}/categories", JsonBody(new { name }));

    public async Task DeleteCategoryAsync(int categoryId) =>
        await SendAsync(HttpMethod.Delete, $"api/categories/{categoryId}", null);

    // ---------- Enrolments ----------

    public async Task EnrolAsync(int eventId, int categoryId) =>
        await SendAsync(HttpMethod.Post, $"api/events/{eventId}/enrolments", JsonBody(new { categoryId }));

    public async Task<List<EnrolmentDto>> GetMyEnrolmentsAsync() =>
        await ReadAsync<List<EnrolmentDto>>(await SendAsync(HttpMethod.Get, "api/enrolments/mine", null));

    public async Task<List<EnrolmentDto>> GetEventEnrolmentsAsync(int eventId) =>
        await ReadAsync<List<EnrolmentDto>>(await SendAsync(HttpMethod.Get, $"api/events/{eventId}/enrolments", null));

    public async Task WithdrawAsync(int enrolmentId) =>
        await SendAsync(HttpMethod.Delete, $"api/enrolments/{enrolmentId}", null);

    public async Task SetEnrolmentStatusAsync(int enrolmentId, string status) =>
        await SendAsync(HttpMethod.Put, $"api/enrolments/{enrolmentId}/status", JsonBody(new { status }));

    // ---------- Results ----------

    public async Task<List<ResultDto>> GetMyResultsAsync() =>
        await ReadAsync<List<ResultDto>>(await SendAsync(HttpMethod.Get, "api/results/mine", null));

    public async Task<List<ResultDto>> GetEventResultsAsync(int eventId) =>
        await ReadAsync<List<ResultDto>>(await SendAsync(HttpMethod.Get, $"api/events/{eventId}/results", null));

    public async Task SaveResultAsync(int eventId, int enrolmentId, string finishTime, int position) =>
        await SendAsync(HttpMethod.Post, $"api/events/{eventId}/results",
            JsonBody(new { enrolmentId, finishTime, position }));

    // ---------- Images (proxied so the browser never contacts the API or Azure directly) ----------

    public async Task<(byte[] Bytes, string ContentType)> GetFileAsync(string name)
    {
        using var res = await SendAsync(HttpMethod.Get, "api/files/" + name, null);
        var bytes = await res.Content.ReadAsByteArrayAsync();
        return (bytes, res.Content.Headers.ContentType?.ToString() ?? "application/octet-stream");
    }

    // ---------- Plumbing ----------

    private static StringContent JsonBody(object body) =>
        new(JsonSerializer.Serialize(body, Json), System.Text.Encoding.UTF8, "application/json");

    private static MultipartFormDataContent FileBody(IFormFile file)
    {
        var content = new MultipartFormDataContent();
        var part = new StreamContent(file.OpenReadStream());
        part.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
        content.Add(part, "file", file.FileName);
        return content;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, HttpContent? body, bool loginCall = false)
    {
        using var request = new HttpRequestMessage(method, url) { Content = body };

        var cookie = Session.GetApiCookie();
        if (!string.IsNullOrEmpty(cookie)) request.Headers.TryAddWithoutValidation("Cookie", cookie);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new ApiException("The RaceDay service is not reachable right now. Please try again in a moment.", 503);
        }

        RememberCookie(response);

        if (response.IsSuccessStatusCode) return response;

        var message = await ReadErrorAsync(response);
        var status = (int)response.StatusCode;
        response.Dispose();

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            if (loginCall) throw new ApiException(message ?? "Invalid email or password.", 401);
            throw new ApiUnauthorizedException();
        }

        throw new ApiException(message ?? status switch
        {
            400 => "Please check the details you entered and try again.",
            403 => "You do not have permission to do that.",
            404 => "That item could not be found. It may have been removed.",
            409 => "That conflicts with something that already exists.",
            >= 500 => "The RaceDay service ran into a problem. Please try again shortly.",
            _ => "The request could not be completed."
        }, status);
    }

    /// <summary>Keeps the API's session cookie (and its sliding expiry) in this user's web session.</summary>
    private void RememberCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values)) return;
        foreach (var value in values)
        {
            if (!value.StartsWith(ApiCookieName + "=", StringComparison.Ordinal)) continue;
            var pair = value.Split(';')[0];
            Session.SetApiCookie(pair == ApiCookieName + "=" ? null : pair);
        }
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        using (response)
        {
            try
            {
                var value = await response.Content.ReadFromJsonAsync<T>(Json);
                return value ?? throw new ApiException("The RaceDay service sent an empty answer.", 502);
            }
            catch (JsonException)
            {
                throw new ApiException("The RaceDay service sent an answer this page could not understand.", 502);
            }
        }
    }

    private static async Task<string?> ReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(text)) return null;
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
                return error.GetString();

            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                foreach (var field in errors.EnumerateObject())
                    foreach (var msg in field.Value.EnumerateArray())
                        if (msg.ValueKind == JsonValueKind.String) return msg.GetString();
            }
        }
        catch (Exception) { /* not JSON - fall back to the generic message */ }
        return null;
    }
}
