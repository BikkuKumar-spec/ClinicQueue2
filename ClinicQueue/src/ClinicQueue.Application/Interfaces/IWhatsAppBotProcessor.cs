namespace ClinicQueue.Application.Interfaces;

public interface IWhatsAppBotProcessor
{
    Task ProcessMessageAsync(string from, string input, CancellationToken cancellationToken = default);
}
