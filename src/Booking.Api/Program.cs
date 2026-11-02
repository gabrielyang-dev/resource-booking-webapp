using Booking.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Render tells us which port to listen on through PORT.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://+:{port}");
}

builder.Services.AddControllers();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();

// Any route that is not /api/... and not a file (nonfile) gets the React app, so client-side routes work on refresh.
app.MapFallbackToFile("/{**path:nonfile:regex(^(?!api(/|$)).*$)}", "index.html");

app.Run();

// Lets the integration tests start the app with WebApplicationFactory.
public partial class Program { }
