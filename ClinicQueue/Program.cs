using ClinicQueue.Data;
using ClinicQueue.Services;
using ClinicQueue.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Hangfire;
using Hangfire.Storage.SQLite;

// ✅ FIX: Force UTF-8 so Hindi/Marathi/etc text is NOT corrupted to '????' in console or string processing
Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

var builder = WebApplication.CreateBuilder(args);

// Configuration
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "YourSuperSecretKey32CharactersLong!";
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=clinic_queue.db";

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add Services
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // ✅ FIX: Ensure JSON serialization never escapes Unicode characters (Hindi, Marathi etc.)
        // Without this, Hindi text gets escaped to \uXXXX in HTTP bodies sent to your microservice
        options.JsonSerializerOptions.Encoder =
            System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

var mySqlConnection = builder.Configuration.GetConnectionString("MySQLConnection")
    ?? "Server=localhost;Database=clinic_queue;Uid=root;Pwd=;";

// Database
builder.Services.AddSingleton(new DatabaseService(mySqlConnection));

// Meta WhatsApp Service
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IMetaWhatsAppService>(sp =>
{
    var config = builder.Configuration.GetSection("Meta:WhatsApp");
    var db = sp.GetRequiredService<DatabaseService>();
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient();
    var logger = sp.GetRequiredService<ILogger<MetaWhatsAppService>>();

    return new MetaWhatsAppService(
        phoneNumberId: config["PhoneNumberId"] ?? "YOUR_PHONE_NUMBER_ID",
        accessToken: config["AccessToken"] ?? "",
        apiVersion: config["ApiVersion"] ?? "v21.0",
        db: db,
        httpClient: httpClient,
        logger: logger
    );
});

// ✅ Register BotSessionStore as Singleton so language and session state persists across requests
builder.Services.AddSingleton<BotSessionStore>();

// Application Services
builder.Services.AddScoped<IPatientService, PatientService>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();
builder.Services.AddScoped<IQueueService, QueueService>();
builder.Services.AddScoped<IReminderService, ReminderService>();
builder.Services.AddScoped<WhatsAppBotService>();

// ✅ FIX: TranslationService needs its own named HttpClient so it sends proper UTF-8 JSON
// Previously it used a plain HttpClient which didn't guarantee UTF-8 encoding on POST bodies
builder.Services.AddHttpClient<ITranslationService, TranslationService>(client =>
{
    var translationUrl = builder.Configuration["AI:TranslationServiceUrl"] ?? "http://localhost:5001";
    client.BaseAddress = new Uri(translationUrl);
    client.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
});

builder.Services.AddHttpClient<IAISymptomService, AISymptomService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5001/");
    // ✅ Timeout set to 150s — must be GREATER than the Python/Ollama timeout (120s)
    // so that Python's own fallback fires first before C# cancels the request.
    // On CPU-only machines, Llama-3.1-8B-Instruct can take 60-120s to respond.
    client.Timeout = TimeSpan.FromSeconds(150);
});

builder.Services.AddScoped<NotificationCronJobs>();
builder.Services.AddScoped<NotificationBackgroundService>();

// JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "ClinicQueueSystem",
            ValidAudience = "ClinicDashboard",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

// Hangfire for Cron Jobs
builder.Services.AddHangfire(config =>
    config.UseSQLiteStorage(connectionString));
builder.Services.AddHangfireServer();

var app = builder.Build();

// Configure Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowReactApp");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<DashboardHub>("/hubs/dashboard");

app.UseHangfireDashboard("/hangfire");

RecurringJob.AddOrUpdate<NotificationBackgroundService>(
    "queue-position-notifier",
    service => service.NotifyNextInQueue(),
    "*/1 * * * *"
);

app.Run();