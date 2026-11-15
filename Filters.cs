using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using RaceDay.Web.Models;

namespace RaceDay.Web.Services;

/// <summary>Only lets logged-in users in; optionally only users with one role.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class SessionAuthorizeAttribute : Attribute, IAuthorizationFilter
{
    public string? Role { get; set; }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var session = context.HttpContext.Session;

        if (!session.IsSignedIn())
        {
            var returnUrl = context.HttpContext.Request.Method == "GET"
                ? context.HttpContext.Request.Path + context.HttpContext.Request.QueryString
                : null;
            context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl });
            return;
        }

        if (Role is not null && session.GetRole() != Role)
            context.Result = new RedirectToActionResult("AccessDenied", "Home", null);
    }
}

/// <summary>
/// Turns API failures into friendly messages instead of crashes:
/// pages show an error screen, form posts go back with a red alert, expired sessions go to the login page.
/// </summary>
public class ApiExceptionFilter : IExceptionFilter
{
    private readonly ITempDataDictionaryFactory _tempData;
    private readonly ILogger<ApiExceptionFilter> _logger;

    public ApiExceptionFilter(ITempDataDictionaryFactory tempData, ILogger<ApiExceptionFilter> logger)
    {
        _tempData = tempData;
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not ApiException ex) return;

        _logger.LogWarning("API call failed ({Status}): {Message}", ex.StatusCode, ex.Message);
        var http = context.HttpContext;
        var tempData = _tempData.GetTempData(http);

        if (ex is ApiUnauthorizedException)
        {
            http.Session.SignOut();
            tempData["Warning"] = ex.Message;
            context.Result = new RedirectToActionResult("Login", "Account", null);
            context.ExceptionHandled = true;
            return;
        }

        if (http.Request.Method == "GET")
        {
            // Showing the error page directly avoids redirect loops when the API is down.
            var model = new ErrorViewModel { Code = ex.StatusCode, Title = Titles(ex.StatusCode), Message = ex.Message };
            context.Result = new ViewResult
            {
                ViewName = "Error",
                StatusCode = ex.StatusCode is >= 400 and < 600 ? ex.StatusCode : 500,
                ViewData = new ViewDataDictionary<ErrorViewModel>(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model },
                TempData = tempData
            };
        }
        else
        {
            tempData["Error"] = ex.Message;
            var referer = http.Request.Headers.Referer.ToString();
            var back = Uri.TryCreate(referer, UriKind.Absolute, out var uri) && uri.Host == http.Request.Host.Host
                ? uri.PathAndQuery
                : "/";
            context.Result = new LocalRedirectResult(back);
        }
        context.ExceptionHandled = true;
    }

    private static string Titles(int code) => code switch
    {
        403 => "Not allowed",
        404 => "Not found",
        503 => "Service unavailable",
        _ => "Something went wrong"
    };
}
