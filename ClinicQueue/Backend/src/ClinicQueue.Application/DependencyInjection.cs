using Microsoft.Extensions.DependencyInjection;
using ClinicQueue.Application.UseCases.Ai;
using ClinicQueue.Application.UseCases.Appointments;
using ClinicQueue.Application.UseCases.Documents;
using ClinicQueue.Application.UseCases.Patients;
using ClinicQueue.Application.UseCases.Queue;
using ClinicQueue.Application.UseCases.Reports;
using ClinicQueue.Application.Validators;
using MediatR;

namespace ClinicQueue.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(typeof(DependencyInjection).Assembly);

        services.AddScoped<IRequestValidator<CreateAppointmentCommand>, CreateAppointmentCommandValidator>();
        services.AddScoped<IRequestValidator<CancelAppointmentCommand>, CancelAppointmentCommandValidator>();
        services.AddScoped<IRequestValidator<GetPatientByIdQuery>, GetPatientByIdQueryValidator>();
        services.AddScoped<IRequestValidator<JoinQueueCommand>, JoinQueueCommandValidator>();
        services.AddScoped<IRequestValidator<GetQueueStatusQuery>, GetQueueStatusQueryValidator>();
        services.AddScoped<IRequestValidator<AnalyzeSymptomsCommand>, AnalyzeSymptomsCommandValidator>();
        services.AddScoped<IRequestValidator<UploadMedicalDocumentCommand>, UploadMedicalDocumentCommandValidator>();
        services.AddScoped<IRequestValidator<GenerateReportSummaryCommand>, GenerateReportSummaryCommandValidator>();

        return services;
    }
}
