using Booking.Api.Abstractions;
using Booking.Api.Errors;
using Booking.Api.Features.AddOns;
using Booking.Api.Features.Bookings;
using Booking.Api.Features.Catalog;
using Booking.Api.Features.Demo;
using Booking.Api.Features.Insights;
using Booking.Api.Features.Rules;
using Booking.Api.Features.Session;
using Booking.Api.Features.Users;
using Booking.Api.Infrastructure;
using Booking.Api.Jobs;
using Booking.Api.Rules;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Render tells us which port to listen on through PORT.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://+:{port}");
}

builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
{
    // Malformed bodies use the same error shape as everything else.
    options.InvalidModelStateResponseFactory = context => new MalformedRequestResult(context.ModelState);
});
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IClock, Booking.Api.Infrastructure.SystemClock>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<SessionManager>();
builder.Services.AddScoped<UsersManager>();
builder.Services.AddScoped<LocationsManager>();
builder.Services.AddScoped<ResourceTypesManager>();
builder.Services.AddScoped<ResourcesManager>();
builder.Services.AddScoped<BookingManager>();
builder.Services.AddScoped<BookingJudge>();
builder.Services.AddScoped<AddOnsManager>();
builder.Services.AddScoped<RulesManager>();
builder.Services.AddScoped<InsightsManager>();

// The rule engine runs every always-on check and every configurable rule registered here.
builder.Services.AddSingleton<IBookingCheck, ValidTimeRangeCheck>();
builder.Services.AddSingleton<IBookingCheck, CapacityCheck>();
builder.Services.AddSingleton<IBookingCheck, AddOnLeadTimeCheck>();
builder.Services.AddSingleton<IBookingCheck, AddOnHoursCheck>();
builder.Services.AddSingleton<IBookingRule, OpeningHoursRule>();
builder.Services.AddSingleton<IBookingRule, MaxDurationRule>();
builder.Services.AddSingleton<IBookingRule, BookingWindowRule>();
builder.Services.AddSingleton<IBookingRule, DailyQuotaRule>();
builder.Services.AddSingleton<RuleEngine>();

// Moves bookings that have ended to Completed. Off in the integration tests, which call it directly.
if (builder.Configuration.GetValue("Jobs:Enabled", true))
{
    builder.Services.AddHostedService<CompleteEndedBookings>();
}
builder.Services.AddScoped<DemoManager>();

// Errors that MVC builds itself (415, 405, ...) get a code too; ApiExceptionHandler covers thrown errors.
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ProblemWriter.AddCode);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

// Render ends TLS and forwards plain http, so trust X-Forwarded-Proto: the cookie is then Secure there.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "booking.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);

        // API calls get ProblemDetails, never a redirect to a login page.
        options.Events.OnRedirectToLogin = context => ProblemWriter.WriteAsync(
            context.HttpContext, 401, "NOT_SIGNED_IN", "You are not signed in. Pick a user to sign in and try again.");
        options.Events.OnRedirectToAccessDenied = context => ProblemWriter.WriteAsync(
            context.HttpContext, 403, "FORBIDDEN", "You are not allowed to do this. Ask an admin if you need access.");

        // Reject a cookie whose user was deleted (demo reset) or whose role changed.
        options.Events.OnValidatePrincipal = async context =>
        {
            var session = context.HttpContext.RequestServices.GetRequiredService<SessionManager>();
            if (!await session.IsSessionValidAsync(context.Principal))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// Apply migrations and seed the demo users (only when there are none) before serving.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DemoData.SeedIfEmptyAsync(db, scope.ServiceProvider.GetRequiredService<IClock>().UtcNow);
}

app.UseForwardedHeaders();
app.UseExceptionHandler();

// Empty error responses under /api (unknown route, 405, 415) get ProblemDetails with a code too.
app.UseStatusCodePages(context => context.HttpContext.Request.Path.StartsWithSegments("/api")
    ? ProblemWriter.WriteForStatusAsync(context.HttpContext)
    : Task.CompletedTask);

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Any route that is not /api/... and not a file (nonfile) gets the React app, so client-side routes work on refresh.
app.MapFallbackToFile("/{**path:nonfile:regex(^(?!api(/|$)).*$)}", "index.html");

app.Run();

// Lets the integration tests start the app with WebApplicationFactory.
public partial class Program { }
