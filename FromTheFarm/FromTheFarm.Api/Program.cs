using FromTheFarm.Api.Models;
using FromTheFarm.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Azure.Cosmos;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ---- Configuration ----
builder.Services.Configure<CosmosDbOptions>(builder.Configuration.GetSection("CosmosDb"));
var firebaseProjectId = builder.Configuration["Firebase:ProjectId"]
    ?? throw new InvalidOperationException("Firebase:ProjectId is not configured.");

// ---- Cosmos DB client (one client for the whole app's lifetime, per Microsoft guidance) ----
var cosmosOptions = builder.Configuration.GetSection("CosmosDb").Get<CosmosDbOptions>()
    ?? throw new InvalidOperationException("CosmosDb configuration section is missing.");

builder.Services.AddSingleton(new CosmosClient(cosmosOptions.Endpoint, cosmosOptions.Key));

// One repository per container, each with its documented partition key path
// (see Models/*.cs for the reasoning behind each choice).
builder.Services.AddSingleton(sp => new CosmosRepository<UserProfile>(
    sp.GetRequiredService<CosmosClient>(), sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CosmosDbOptions>>(),
    "Users", "/userId"));
builder.Services.AddSingleton(sp => new CosmosRepository<Listing>(
    sp.GetRequiredService<CosmosClient>(), sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CosmosDbOptions>>(),
    "Listings", "/farmerId"));
builder.Services.AddSingleton(sp => new CosmosRepository<DemandRequest>(
    sp.GetRequiredService<CosmosClient>(), sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CosmosDbOptions>>(),
    "Demands", "/buyerId"));
builder.Services.AddSingleton(sp => new CosmosRepository<MatchDocument>(
    sp.GetRequiredService<CosmosClient>(), sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CosmosDbOptions>>(),
    "Matches", "/id"));
builder.Services.AddSingleton(sp => new CosmosRepository<Rating>(
    sp.GetRequiredService<CosmosClient>(), sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CosmosDbOptions>>(),
    "Ratings", "/matchId"));

builder.Services.AddSingleton<MatchingService>();

// ---- Firebase-issued ID token validation ----
// Every request (aside from POST /auth/session's first-contact case) carries
// a Firebase ID token in the Authorization header. This validates it against
// Google's published signing keys rather than trusting the client.
// NOTE: verify this Authority auto-discovers correctly for your specific
// Firebase project during Week 5 testing — if OIDC discovery doesn't resolve,
// fall back to manually pointing IssuerSigningKeyResolver at
// https://www.googleapis.com/service_accounts/v1/jwk/[email protected]
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
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
