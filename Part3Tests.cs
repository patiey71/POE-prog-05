using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Tests;

public class Part3Tests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _f;
    public Part3Tests(ApiFactory f) => _f = f;

    private static readonly byte[] Png =
        { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 1, 2, 3, 4 };

    private static MultipartFormDataContent Upload(byte[] bytes, string fileName, string type)
    {
        var content = new MultipartFormDataContent();
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue(type);
        content.Add(part, "file", fileName);
        return content;
    }

    [Fact]
    public async Task Organiser_can_upload_a_banner_and_it_is_returned_on_the_event()
    {
        var (org, _, _) = await TestHelpers.NewUserAsync(_f, "Organiser");
        var ev = await TestHelpers.CreateEventAsync(org);

        var r = await org.PostAsync($"/api/events/{ev.Id}/banner", Upload(Png, "banner.png", "image/png"));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);

        var updated = await r.Content.ReadFromJsonAsync<EventResponse>(TestHelpers.Json);
        Assert.StartsWith("banners/", updated!.BannerImage);

        var file = await org.GetAsync($"/api/files/{updated.BannerImage}");
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        Assert.Equal("image/png", file.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Banner_upload_rejects_non_images_and_wrong_extensions()
    {
        var (org, _, _) = await TestHelpers.NewUserAsync(_f, "Organiser");
        var ev = await TestHelpers.CreateEventAsync(org);

        var notImage = await org.PostAsync($"/api/events/{ev.Id}/banner",
            Upload(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, "banner.png", "image/png"));
        Assert.Equal(HttpStatusCode.BadRequest, notImage.StatusCode);

        var wrongExt = await org.PostAsync($"/api/events/{ev.Id}/banner", Upload(Png, "banner.exe", "image/png"));
        Assert.Equal(HttpStatusCode.BadRequest, wrongExt.StatusCode);
    }

    [Fact]
    public async Task Banner_upload_rejects_files_over_5MB()
    {
        var (org, _, _) = await TestHelpers.NewUserAsync(_f, "Organiser");
        var ev = await TestHelpers.CreateEventAsync(org);

        var big = new byte[5 * 1024 * 1024 + 100];
        Array.Copy(Png, big, Png.Length);
        var r = await org.PostAsync($"/api/events/{ev.Id}/banner", Upload(big, "big.png", "image/png"));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Participant_and_other_organisers_cannot_upload_a_banner()
    {
        var (org, _, _) = await TestHelpers.NewUserAsync(_f, "Organiser");
        var (other, _, _) = await TestHelpers.NewUserAsync(_f, "Organiser");
        var (part, _, _) = await TestHelpers.NewUserAsync(_f, "Participant");
        var ev = await TestHelpers.CreateEventAsync(org);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await part.PostAsync($"/api/events/{ev.Id}/banner", Upload(Png, "a.png", "image/png"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await other.PostAsync($"/api/events/{ev.Id}/banner", Upload(Png, "a.png", "image/png"))).StatusCode);
    }

    [Fact]
    public async Task Files_require_login()
    {
        var anonymous = _f.CreateClient();
        var r = await anonymous.GetAsync("/api/files/banners/anything.png");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task User_can_upload_a_profile_picture()
    {
        var (part, _, _) = await TestHelpers.NewUserAsync(_f, "Participant");
        var r = await part.PostAsync("/api/profile/picture", Upload(Png, "me.png", "image/png"));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);

        var profile = await r.Content.ReadFromJsonAsync<ProfileResponse>(TestHelpers.Json);
        Assert.StartsWith("profiles/", profile!.ProfilePicture);
    }

    [Fact]
    public async Task New_enrolments_are_pending_and_the_organiser_can_confirm_them()
    {
        var (org, _, _) = await TestHelpers.NewUserAsync(_f, "Organiser");
        var (part, _, _) = await TestHelpers.NewUserAsync(_f, "Participant");
        var ev = await TestHelpers.CreateEventAsync(org);
        var cat = await TestHelpers.CreateCategoryAsync(org, ev.Id);
        var enrolment = await TestHelpers.EnrolAsync(part, ev.Id, cat.Id);
        Assert.Equal(EnrolmentStatus.Pending, enrolment.Status);

        var confirm = await org.PutAsJsonAsync($"/api/enrolments/{enrolment.Id}/status", new { status = "Confirmed" }, TestHelpers.Json);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        var updated = await confirm.Content.ReadFromJsonAsync<EnrolmentResponse>(TestHelpers.Json);
        Assert.Equal(EnrolmentStatus.Confirmed, updated!.Status);
    }

    [Fact]
    public async Task Only_the_owning_organiser_can_change_an_enrolment_status()
    {
        var (org, _, _) = await TestHelpers.NewUserAsync(_f, "Organiser");
        var (other, _, _) = await TestHelpers.NewUserAsync(_f, "Organiser");
        var (part, _, _) = await TestHelpers.NewUserAsync(_f, "Participant");
        var ev = await TestHelpers.CreateEventAsync(org);
        var cat = await TestHelpers.CreateCategoryAsync(org, ev.Id);
        var enrolment = await TestHelpers.EnrolAsync(part, ev.Id, cat.Id);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await other.PutAsJsonAsync($"/api/enrolments/{enrolment.Id}/status", new { status = "Confirmed" }, TestHelpers.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await part.PutAsJsonAsync($"/api/enrolments/{enrolment.Id}/status", new { status = "Confirmed" }, TestHelpers.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await org.PutAsJsonAsync($"/api/enrolments/{enrolment.Id}/status", new { status = "Nonsense" }, TestHelpers.Json)).StatusCode);
    }

    [Fact]
    public async Task My_events_lists_only_own_events_with_enrolment_counts()
    {
        var (org, _, _) = await TestHelpers.NewUserAsync(_f, "Organiser");
        var (other, _, _) = await TestHelpers.NewUserAsync(_f, "Organiser");
        var (part, _, _) = await TestHelpers.NewUserAsync(_f, "Participant");
        var ev = await TestHelpers.CreateEventAsync(org, "Mine");
        await TestHelpers.CreateEventAsync(other, "Theirs");
        var cat = await TestHelpers.CreateCategoryAsync(org, ev.Id);
        await TestHelpers.EnrolAsync(part, ev.Id, cat.Id);

        var mine = await org.GetFromJsonAsync<List<EventResponse>>("/api/events/mine", TestHelpers.Json);
        Assert.Single(mine!);
        Assert.Equal(1, mine![0].EnrolmentCount);
        Assert.Equal(HttpStatusCode.Forbidden, (await part.GetAsync("/api/events/mine")).StatusCode);
    }

    [Fact]
    public async Task Results_include_the_total_number_of_finishers()
    {
        var (org, _, _) = await TestHelpers.NewUserAsync(_f, "Organiser");
        var (p1, _, _) = await TestHelpers.NewUserAsync(_f, "Participant");
        var (p2, _, _) = await TestHelpers.NewUserAsync(_f, "Participant");
        var ev = await TestHelpers.CreateEventAsync(org);
        var cat = await TestHelpers.CreateCategoryAsync(org, ev.Id);
        var e1 = await TestHelpers.EnrolAsync(p1, ev.Id, cat.Id);
        var e2 = await TestHelpers.EnrolAsync(p2, ev.Id, cat.Id);

        (await org.PostAsJsonAsync($"/api/events/{ev.Id}/results", new { enrolmentId = e1.Id, finishTime = "00:25:00", position = 1 }, TestHelpers.Json)).EnsureSuccessStatusCode();
        (await org.PostAsJsonAsync($"/api/events/{ev.Id}/results", new { enrolmentId = e2.Id, finishTime = "00:31:10", position = 2 }, TestHelpers.Json)).EnsureSuccessStatusCode();

        var mine = await p2.GetFromJsonAsync<List<ResultResponse>>("/api/results/mine", TestHelpers.Json);
        Assert.Single(mine!);
        Assert.Equal(2, mine![0].Position);
        Assert.Equal(2, mine[0].TotalFinishers);
    }
}
