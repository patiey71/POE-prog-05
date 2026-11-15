# RaceDay (PROG6212 POE – Part 3: Automation of Web Application)

RaceDay is a platform for running, walking and cycling events. This repository contains:

| Project | What it is |
|---------|------------|
| `RaceDay.Api` | ASP.NET Core 8 Web API (Part 2, extended in Part 3). EF Core code-first on SQL Server, cookie session auth, **Azure Blob Storage** for images, Swagger. |
| `RaceDay.Web` | **New in Part 3.** ASP.NET Core MVC front end. It talks to the API only (it never touches the database or Azure). Bootstrap 5 with a sporting RaceDay theme. |
| `RaceDay.Tests` | xUnit tests for the API (run in CI, using an in-memory database and an in-memory image store – no Azure needed). |

[![CI](https://github.com/<your-username>/<your-repo>/actions/workflows/ci.yml/badge.svg)](https://github.com/<your-username>/<your-repo>/actions)

## Run everything with one command (Docker)

Prerequisite: Docker Desktop.

```
docker compose up --build
```

Then open:

* **Web app:** http://localhost:8081
* **API / Swagger:** http://localhost:5080/swagger

The compose file starts SQL Server, Azurite (a local Azure Blob Storage emulator), the API and the MVC web app,
already wired together. Stop with `Ctrl+C`, remove with `docker compose down` (add `-v` to wipe data).

To use a **real Azure Storage account** instead of Azurite:

```
# PowerShell
$env:AZURE_STORAGE_CONNECTION_STRING = "<your storage account connection string>"
docker compose up --build
```

### Run only the MVC web app in Docker

```
docker build -t raceday-web ./RaceDay.Web
docker run -p 8081:8080 -e Api__BaseUrl=http://host.docker.internal:5080 raceday-web
```

`Api__BaseUrl` must point at a running RaceDay API (for example the API started from Visual Studio on port 5080).

## Run from Visual Studio (without Docker)

1. Open `RaceDay.sln` (needs the .NET 8 SDK and SQL Server LocalDB).
2. **Create the database.** The model gained new columns in Part 3 (`BannerImage`, `ProfilePicture`, enrolment `Status`).
   If you already created migrations in Part 2, add a new one; otherwise the API creates the schema on first start:
   ```
   dotnet tool install --global dotnet-ef
   cd RaceDay.Api
   dotnet ef migrations add Part3Fields
   dotnet ef database update
   ```
3. **Azure Blob Storage (optional locally).** If `AzureBlob:ConnectionString` is empty the API saves images to
   `RaceDay.Api/App_Data/uploads` so everything still works offline. To use Azure, create a *Storage account* in the
   Azure portal, copy *Access keys → Connection string*, and store it as a user secret (never commit it):
   ```
   cd RaceDay.Api
   dotnet user-secrets set "AzureBlob:ConnectionString" "<connection string>"
   ```
   The container `raceday-images` is created automatically and is **private**; images are only served through the API.
4. Right-click the solution → **Configure Startup Projects → Multiple startup projects** → set `RaceDay.Api` and
   `RaceDay.Web` to **Start**. Press F5.
   * API: http://localhost:5080/swagger
   * Web: http://localhost:5090 (reads the API address from `RaceDay.Web/appsettings.json` → `Api:BaseUrl`)
5. Run the tests: `dotnet test RaceDay.sln`

## What the web app does

**Participant**
* Register / log in, edit profile, upload a profile picture
* Browse events with search, type filter, upcoming/past filter and sorting
* View event details and enrol by choosing a category
* *My enrolments* with colour-coded status: **green = Confirmed, amber = Pending, grey = Cancelled**; withdraw an enrolment
* *My results* showing finish time and position, e.g. "47th out of 312 finishers"

**Organiser**
* Dashboard: number of events, upcoming events, total enrolments, and enrolments per event
* Create / edit / delete events and upload an event banner image
* Add and remove categories
* Confirm or cancel enrolments
* Capture finish times (hh:mm:ss) and positions

Participants cannot open organiser pages and organisers cannot open participant pages (they get an *Access denied* page);
unauthenticated users are sent to the login page.

## How the pieces fit together

```
Browser ──► RaceDay.Web (MVC) ──HTTP──► RaceDay.Api ──► SQL Server
                                            └────────► Azure Blob Storage (images)
```

* **Session management.** After login the web app keeps the user id, name, role and picture (and the API's own session
  cookie) in a server-side ASP.NET session cookie (HttpOnly, 30 minute idle timeout). A custom `SessionAuthorize`
  filter enforces login and role on every controller. Anti-forgery tokens protect every POST.
* **Images.** Uploads go from the browser to the web app, which forwards them to the API
  (`POST /api/events/{id}/banner`, `POST /api/profile/picture`). The API validates them (JPG/PNG/GIF/WEBP, max 5 MB,
  real file signature checked) and stores them in Azure Blob Storage. Pages show images via the web app's `/files/...`
  route, which fetches them from the API (`GET /api/files/...`). The browser never contacts Azure directly.
* **Error handling.** API failures (timeouts, 400/403/404/409/500) are translated into friendly messages by
  `ApiExceptionFilter`: pages show an error screen, forms return with a red alert, expired sessions go to the login page.
  Unknown URLs show a styled 404 page.

## Part 3 API additions

| Method | Route | Access |
|--------|-------|--------|
| GET | /api/events/mine | Organiser – own events with enrolment counts |
| POST | /api/events/{id}/banner | Organiser (owner) – multipart `file` |
| POST | /api/profile/picture | Authenticated – multipart `file` |
| GET | /api/files/{name} | Authenticated – banner / profile images |
| PUT | /api/enrolments/{id}/status | Organiser (owner) – `{ "status": "Confirmed" }` |

Enrolments now have a `Status` (Pending → Confirmed / Cancelled). New enrolments start as **Pending**.
Results include `TotalFinishers` so the web app can show "47th out of 312".

## Part 2 endpoints (unchanged)

| Method | Route | Access |
|--------|-------|--------|
| POST | /api/auth/register, /api/auth/login | Public |
| POST | /api/auth/logout | Authenticated |
| GET / PUT | /api/profile | Authenticated |
| GET | /api/events, /api/events/{id} | Authenticated |
| POST / PUT / DELETE | /api/events, /api/events/{id} | Organiser (owner) |
| GET / POST | /api/events/{eventId}/categories | Authenticated / Organiser (owner) |
| DELETE | /api/categories/{id} | Organiser (owner) |
| POST | /api/events/{eventId}/enrolments | Participant |
| GET | /api/enrolments/mine | Participant |
| DELETE | /api/enrolments/{id} | Participant (own) |
| GET | /api/events/{eventId}/enrolments | Organiser (owner) |
| POST / GET | /api/events/{eventId}/results | Organiser (owner) |
| GET | /api/results/mine | Participant |

Optional: set `Registration:OrganiserCode` so only people with the code can sign up as Organisers.

## Security

* Passwords are hashed with BCrypt; plain-text passwords are never stored.
* HttpOnly session cookies; role checks with `[Authorize(Roles = ...)]` (API) and `SessionAuthorize` (web).
* Ownership checks stop one organiser editing another organiser's events.
* Secrets (Azure connection string) come from user secrets / environment variables, never from source control.

## CI/CD

`.github/workflows/ci.yml` runs on every push: restores and builds **all three projects** (API, MVC web app, tests),
runs the unit tests, then builds both Docker images.

![Green build](docs/ci-green-build.png)

*(Add your screenshot of the passing GitHub Actions run at `docs/ci-green-build.png`.)*

## Demo video

YouTube (unlisted): <paste link here>
