using ClinicQueue.Application.Interfaces;
using ClinicQueue.Infrastructure.BackgroundServices;
using ClinicQueue.Infrastructure.ExternalServices;
using ClinicQueue.Infrastructure.ExternalServices.AI;
using ClinicQueue.Infrastructure.ExternalServices.Documents;
using ClinicQueue.Infrastructure.ExternalServices.OCR;
using ClinicQueue.Infrastructure.ExternalServices.PDF;
using ClinicQueue.Infrastructure.ExternalServices.Translation;
using ClinicQueue.Infrastructure.ExternalServices.WhatsApp;
using ClinicQueue.Infrastructure.Persistence;
using ClinicQueue.Infrastructure.Persistence.Identity;
using ClinicQueue.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClinicQueue.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? "Data Source=clinic_queue.db";

        services.AddDbContext<ClinicDbContext>(options =>
            options.UseSqlite(connectionString));

        services
            .AddIdentityCore<ClinicIdentityUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ClinicDbContext>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IQueueRepository, QueueRepository>();
        services.AddScoped<IDoctorRepository, DoctorRepository>();
        services.AddScoped<IMedicalReportRepository, MedicalReportRepository>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITokenProvider, JwtTokenProvider>();
        services.AddScoped<DatabaseSeeder>();

        services.AddHttpClient<ISymptomAnalysisGateway, SymptomAnalysisGateway>(client =>
        {
            var baseUrl = configuration["AI:TranslationServiceUrl"] ?? "http://localhost:8000";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(150);
        });

        services.AddHttpClient<IConversationAiGateway, ConversationAiGateway>(client =>
        {
            var baseUrl = configuration["AI:TranslationServiceUrl"] ?? "http://localhost:8000";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(150);
        });

        services.AddHttpClient<OcrDocumentProcessingGateway>(client =>
        {
            var baseUrl = configuration["AI:OcrServiceUrl"] ?? "http://localhost:8001";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        services.AddHttpClient<IOcrService, OcrService>(client =>
        {
            var baseUrl = configuration["AI:OcrServiceUrl"] ?? "http://localhost:8001";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        services.AddScoped<IPdfExtractionService, PdfExtractionService>();
        services.AddScoped<IDocumentExtractionService, DocumentExtractionService>();

        services.AddScoped<PdfTextExtractionService>();
        services.AddScoped<IDocumentProcessingGateway, DocumentProcessingGateway>();

        services.AddHttpClient<IReportSummaryGateway, ReportSummaryGateway>(client =>
        {
            var baseUrl = configuration["AI:TranslationServiceUrl"] ?? "http://localhost:8000";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(90);
        });

        services.AddHttpClient<ITranslationClient, TranslationClient>(client =>
        {
            var baseUrl = configuration["AI:TranslationServiceUrl"] ?? "http://localhost:8000";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        services.AddHttpClient<IWhatsAppGateway, WhatsAppClient>(client =>
        {
            var version = configuration["Meta:WhatsApp:ApiVersion"] ?? "v21.0";
            var phoneNumberId = configuration["Meta:WhatsApp:PhoneNumberId"] ?? string.Empty;
            client.BaseAddress = new Uri($"https://graph.facebook.com/{version}/{phoneNumberId}/");

            var token = configuration["Meta:WhatsApp:AccessToken"];
            if (!string.IsNullOrWhiteSpace(token))
            {
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        });

        services.AddHttpClient<IWhatsAppMediaDownloader, WhatsAppMediaDownloader>(client =>
        {
            var version = configuration["Meta:WhatsApp:ApiVersion"] ?? "v21.0";
            client.BaseAddress = new Uri($"https://graph.facebook.com/{version}/");

            var token = configuration["Meta:WhatsApp:AccessToken"];
            if (!string.IsNullOrWhiteSpace(token))
            {
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        });

        services.AddSingleton<WhatsAppConversationSessionStore>();
        services.AddScoped<IWhatsAppBotProcessor, WhatsAppBotProcessor>();

        services.AddHostedService<NotificationReminderDispatcher>();

        return services;
    }
}
