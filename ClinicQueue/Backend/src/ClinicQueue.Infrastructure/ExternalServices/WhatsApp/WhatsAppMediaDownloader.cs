using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ClinicQueue.Infrastructure.ExternalServices.WhatsApp;

public interface IWhatsAppMediaDownloader
{
    Task<byte[]> DownloadMediaAsync(string mediaId, CancellationToken cancellationToken = default);
}

public sealed class WhatsAppMediaDownloader(HttpClient httpClient, ILogger<WhatsAppMediaDownloader> logger) : IWhatsAppMediaDownloader
{
    public async Task<byte[]> DownloadMediaAsync(string mediaId, CancellationToken cancellationToken = default)
    {
        var metadataResponse = await httpClient.GetAsync(mediaId, cancellationToken);
        metadataResponse.EnsureSuccessStatusCode();

        var metadataJson = await metadataResponse.Content.ReadAsStringAsync(cancellationToken);
        using var metadataDoc = JsonDocument.Parse(metadataJson);
        var downloadUrl = metadataDoc.RootElement.GetProperty("url").GetString();

        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            throw new InvalidOperationException("Meta API returned an empty media download URL.");
        }

        logger.LogInformation("Resolved media {MediaId} URL from Meta API", mediaId);

        using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
        request.Headers.Authorization = httpClient.DefaultRequestHeaders.Authorization;

        var downloadResponse = await httpClient.SendAsync(request, cancellationToken);
        downloadResponse.EnsureSuccessStatusCode();

        var bytes = await downloadResponse.Content.ReadAsByteArrayAsync(cancellationToken);
        logger.LogInformation("Downloaded media {MediaId}, {Size} bytes", mediaId, bytes.Length);

        return bytes;
    }
}
