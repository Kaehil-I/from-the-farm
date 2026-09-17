using FromTheFarm.Api.Models;
using FromTheFarm.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

// ---- Hosting ----
// Render (and most container hosts) assign a port via the PORT environment
// variable and expect the process to listen on it on all interfaces. Without
// this the app binds localhost:5000 inside the container and every health
// check fails.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

var firebaseProjectId = builder.Configuration["Firebase:ProjectId"]
    ?? throw new InvalidOperationException("Firebase:ProjectId is not configured.");

// ---- BSON conventions ----
// Store documents in camelCase so the persisted shape matches both the schema
// documented in Section 7 and the JSON the API returns, rather than the
// driver's default PascalCase.
ConventionRegistry.Register(
    "camelCase",
    new ConventionPack { new CamelCaseElementNameConvention() },
    _ => true);

// harvestDate, deadline and relevantDate are DateOnly, which has no dependable
// built-in BSON mapping. See DateOnlySerializer for why these are stored as
// "yyyy-MM-dd" strings.
BsonSerializer.RegisterSerializer(new DateOnlySerializer());

// ---- Mongo client (one client for the whole app's lifetime, per driver guidance) ----
var mongoOptions = builder.Configuration.GetSection("MongoDb").Get<MongoDbOptions>()
    ?? throw new InvalidOperationException("MongoDb configuration section is missing.");

if (string.IsNullOrWhiteSpace(mongoOptions.ConnectionString))
{
    throw new InvalidOperationException(
        "MongoDb:ConnectionString is not configured. Set it with user-secrets locally, " +
        "or as an environment variable in the deployed environment.");
}

builder.Services.AddSingleton<IMongoClient>(new MongoClient(mongoOptions.ConnectionString));
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IMongoClient>().GetDatabase(mongoOptions.DatabaseName));

// One repository per collection. Mongo creates a collection on first write, so
// there is nothing to provision up front.
builder.Services.AddSingleton(sp => new MongoRepository<UserProfile>(sp.GetRequiredService<IMongoDatabase>(), "Users"));
builder.Services.AddSingleton(sp => new MongoRepository<Listing>(sp.GetRequiredService<IMongoDatabase>(), "Listings"));
builder.Services.AddSingleton(sp => new MongoRepository<DemandRequest>(sp.GetRequiredService<IMongoDatabase>(), "Demands"));
builder.Services.AddSingleton(sp => new MongoRepository<MatchDocument>(sp.GetRequiredService<IMongoDatabase>(), "Matches"));
builder.Services.AddSingleton(sp => new MongoRepository<Rating>(sp.GetRequiredService<IMongoDatabase>(), "Ratings"));

builder.Services.AddSingleton<MatchingService>();

// ---- Firebase-issued ID token validation ----
// Every request (aside from POST /auth/session's first-contact case) carries
// a Firebase ID token in the Authorization header. This validates it against
// Google's published signing keys rather than trusting the client.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"https://securetoken.google.com/{firebaseProjectId}";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = $"https://securetoken.google.com/{firebaseProjectId}",
            ValidateAudience = true,
            ValidAudience = firebaseProjectId,
            ValidateLifetime = true
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    // The API has no route at the root, so a browser pointed at the base URL
    // would otherwise get a bare 404. Send it somewhere useful instead.
    app.MapGet("/", () => Results.Redirect("/swagger"));
    // Only redirect to HTTPS locally. Render terminates TLS at its proxy and
    // forwards plain HTTP, so redirecting in production would bounce every
    // request — and the Android client is built with followRedirects(false),
    // so it would fail outright rather than follow the redirect.
    app.UseHttpsRedirection();
}
else
{
    // In the deployed environment Swagger is not mapped, so point the root at
    // the health check — a browser hitting the base URL then gets live JSON
    // proving the service is up rather than a 404.
    app.MapGet("/", () => Results.Redirect("/api/v1/health"));
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
