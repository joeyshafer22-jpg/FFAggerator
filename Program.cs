using FFAggerator.Endpoints;
using FFAggerator.Infrastructure.Data;
using FFAggerator.Infrastructure.Rss;
using FFAggerator.Infrastructure.Sleeper;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
                      ?? ["http://localhost:5173"];
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
    });
});

var dbPath = Path.Combine(builder.Environment.ContentRootPath, "Data", "ffaggerator.db");
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddHttpClient(SleeperClient.HttpClientName, client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Sleeper:BaseUrl"] ?? "https://api.sleeper.app/v1/");
    client.Timeout = TimeSpan.FromMinutes(2);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("FFAggerator/1.0 (+https://localhost)");
});

builder.Services.AddHttpClient(RssFeedReader.HttpClientName, client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("FFAggerator/1.0 (+https://localhost)");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/rss+xml, application/xml, text/xml");
});

builder.Services.AddScoped<SleeperClient>();
builder.Services.AddScoped<IPlayerCatalog>(sp => sp.GetRequiredService<SleeperClient>());
builder.Services.AddScoped<IProjectionProvider>(sp => sp.GetRequiredService<SleeperClient>());
builder.Services.AddScoped<INflStateProvider>(sp => sp.GetRequiredService<SleeperClient>());
builder.Services.AddSingleton<PlayerNewsMatcher>();
builder.Services.AddScoped<INewsFeedReader, RssFeedReader>();
builder.Services.AddScoped<INewsProvider, RssNewsProvider>();
builder.Services.AddHostedService<DataRefreshHostedService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.MapNflEndpoints();
app.MapPlayerEndpoints();
app.Run();
