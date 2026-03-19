using ClinicQueue.Application.Common;
using ClinicQueue.Application.DTOs;

namespace ClinicQueue.Application.Interfaces;

public interface IDocumentProcessingGateway
{
    Task<Result<MedicalDocumentExtractionDto>> ExtractAsync(byte[] fileBytes, string fileName, CancellationToken cancellationToken = default);
}
