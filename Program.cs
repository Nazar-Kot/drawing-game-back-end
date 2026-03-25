using DrawingApp.Api.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        // ALLOWED_ORIGINS env var: comma-separated list, e.g. "https://my-app.up.railway.app"
        // Falls back to permitting any localhost origin in development.
        var originsEnv = builder.Configuration["ALLOWED_ORIGINS"];
        if (!string.IsNullOrWhiteSpace(originsEnv))
        {
            var origins = originsEnv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            policy.WithOrigins(origins);
        }
        else
        {
            policy.SetIsOriginAllowed(origin =>
            {
                var uri = new Uri(origin);
                return uri.Host == "localhost" || uri.Host == "127.0.0.1";
            });
        }
        policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    });
});

var app = builder.Build();

app.UseCors();
app.MapGet("/health", () => Results.Ok());
app.MapHub<DrawingHub>("/hub/drawing");

app.Run();
