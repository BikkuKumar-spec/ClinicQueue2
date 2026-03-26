using System.Text;
using ClinicQueue.Api.Hubs;
using ClinicQueue.Api.Middleware;
using ClinicQueue.Application;
using ClinicQueue.Infrastructure;
using ClinicQueue.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ClinicQueue.Infrastructure.ExternalServices.AI;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.Retry;
using Polly.Timeout;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = "frontend"
});

var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "YourSuperSecretKey32CharactersLong!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "ClinicQueueSystem";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "ClinicDashboard";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
            return;
        }

        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Encoder =
            System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
// Use this version to avoid the ValidationException
builder.Services.AddHttpClient<IConversationAiGateway, ConversationAiGateway>()
    .RemoveAllLoggers()
    .AddResilienceHandler("ai-handler", pipeline => {
        // We skip AddRetry entirely so it defaults to 0 retries (standard HttpClient behavior)

        // Only keep the Timeout so the request doesn't hang forever
        pipeline.AddTimeout(TimeSpan.FromSeconds(100));
    });

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider
        .GetRequiredService<DatabaseSeeder>();
    await seeder.SeedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<DashboardHub>("/hubs/dashboard");

app.Run();
