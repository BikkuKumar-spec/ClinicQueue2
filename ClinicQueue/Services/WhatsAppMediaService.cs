using System.Text.Json;

namespace ClinicQueue.Services
{
    public interface IWhatsAppMediaService
    {
        Task<byte[]> DownloadMediaAsync(string mediaId);
    }

    public class WhatsAppMediaService : IWhatsAppMediaService
    {
        private readonly HttpClient _httpClient;
        private readonly string _accessToken;
        private readonly string _apiVersion;
        private readonly ILogger<WhatsAppMediaService> _logger;

        public WhatsAppMediaService(
            HttpClient httpClient,
            string accessToken,
            string apiVersion,
            ILogger<WhatsAppMediaService> logger)
        {
            _httpClient = httpClient;
            _accessToken = accessToken;
            _apiVersion = apiVersion;
            _logger = logger;
        }

        /// <summary>
        /// Downloads media from Meta's WhatsApp Cloud API using the 2-step process:
        /// 1. GET /v{version}/{mediaId} → returns JSON with a "url" field
        /// 2. GET {url} with Bearer token → returns binary file bytes
        /// </summary>
        public async Task<byte[]> DownloadMediaAsync(string mediaId)
        {
            // Step 1: Retrieve the media URL from Meta Graph API
            var metadataUrl = $"https://graph.facebook.com/{_apiVersion}/{mediaId}";

            using var metadataRequest = new HttpRequestMessage(HttpMethod.Get, metadataUrl);
            metadataRequest.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

            var metadataResponse = await _httpClient.SendAsync(metadataRequest);
            metadataResponse.EnsureSuccessStatusCode();

            var metadataJson = await metadataResponse.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(metadataJson);
            var downloadUrl = doc.RootElement.GetProperty("url").GetString()
                ?? throw new InvalidOperationException("Meta API returned null media URL");

            _logger.LogInformation("Media {MediaId}: resolved download URL", mediaId);

            // Step 2: Download the actual binary file from the resolved URL
            using var downloadRequest = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            downloadRequest.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

            var downloadResponse = await _httpClient.SendAsync(downloadRequest);
            downloadResponse.EnsureSuccessStatusCode();

            var bytes = await downloadResponse.Content.ReadAsByteArrayAsync();
            _logger.LogInformation("Media {MediaId}: downloaded {ByteCount} bytes", mediaId, bytes.Length);

            return bytes;
        }
    }
}
