using ClinicQueue.Application.Common;

namespace ClinicQueue.Application.Interfaces;

public interface IWhatsAppGateway
{
    Task<Result<bool>> SendTextAsync(string to, string message, CancellationToken cancellationToken = default);
}