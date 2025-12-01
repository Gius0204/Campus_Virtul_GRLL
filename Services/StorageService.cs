using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Campus_Virtul_GRLL.Services
{
    public record StorageOptions(string Url, string AnonKey, string ServiceRoleKey)
    {
        public bool HasServiceRole => !string.IsNullOrWhiteSpace(ServiceRoleKey);
    }

    public class StorageService
    {
        private readonly HttpClient _http;
        private readonly ILogger<StorageService> _logger;
        private readonly StorageOptions _options;

        public StorageService(HttpClient httpClient, ILogger<StorageService> logger, StorageOptions options)
        {
            _http = httpClient;
            _logger = logger;
            _options = options;
        }

        private string BaseStorageUrl => _options.Url?.TrimEnd('/') + "/storage/v1";

        private string AuthToken => _options.ServiceRoleKey ?? _options.AnonKey; // Prefer service role for mutations

        private static string EncodePathSegments(string objectPath)
        {
            var trimmed = objectPath?.TrimStart('/') ?? string.Empty;
            var parts = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++) parts[i] = Uri.EscapeDataString(parts[i]);
            return string.Join('/', parts);
        }

        public async Task<(bool ok, string? path, string? error)> UploadAsync(string bucket, string objectPath, Stream content, string contentType, bool upsert = true)
        {
            if (string.IsNullOrWhiteSpace(bucket) || string.IsNullOrWhiteSpace(objectPath))
                return (false, null, "Bucket o ruta vacíos");
            if (string.IsNullOrWhiteSpace(BaseStorageUrl) || string.IsNullOrWhiteSpace(AuthToken))
                return (false, null, "Storage URL o token no configurados");

            var encodedPath = EncodePathSegments(objectPath);
            var url = $"{BaseStorageUrl}/object/{bucket}/{encodedPath}";
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StreamContent(content)
            };
            req.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AuthToken);
            if (upsert) req.Headers.Add("x-upsert", "true");

            var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                _logger.LogWarning("Fallo upload {Bucket}/{Path}: {Status} {Body}", bucket, objectPath, resp.StatusCode, body);
                return (false, null, $"Error upload: {resp.StatusCode}");
            }
            return (true, objectPath, null);
        }

        public async Task<(bool ok, string? signedUrl, string? error)> GetSignedUrlAsync(string bucket, string objectPath, int expiresInSeconds = 3600)
        {
            if (string.IsNullOrWhiteSpace(bucket) || string.IsNullOrWhiteSpace(objectPath))
                return (false, null, "Bucket o ruta vacíos");
            var encodedPath = EncodePathSegments(objectPath);
            var url = $"{BaseStorageUrl}/object/sign/{bucket}/{encodedPath}";
            var payload = JsonSerializer.Serialize(new { expiresIn = expiresInSeconds });
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AuthToken);
            var resp = await _http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Fallo sign URL {Bucket}/{Path}: {Status} {Body}", bucket, objectPath, resp.StatusCode, body);
                return (false, null, $"Error signed URL: {resp.StatusCode}");
            }
            try
            {
                var json = JsonSerializer.Deserialize<SignUrlResponse>(body);
                if (json != null && !string.IsNullOrWhiteSpace(json.signedURL))
                {
                    var signed = json.signedURL.Trim();
                    string full;
                    if (signed.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        full = signed;
                    }
                    else if (signed.StartsWith("/storage/v1", StringComparison.OrdinalIgnoreCase))
                    {
                        full = (_options.Url?.TrimEnd('/') ?? string.Empty) + signed;
                    }
                    else if (signed.StartsWith("/object/", StringComparison.OrdinalIgnoreCase) || signed.StartsWith("object/", StringComparison.OrdinalIgnoreCase))
                    {
                        var rel = signed.StartsWith("/") ? signed : "/" + signed;
                        full = BaseStorageUrl + rel; // ensure /storage/v1 prefix
                    }
                    else
                    {
                        // Fallback: ensure we prepend /storage/v1 if missing
                        var rel = signed.StartsWith("/") ? signed : "/" + signed;
                        if (!rel.StartsWith("/storage/v1", StringComparison.OrdinalIgnoreCase))
                        {
                            rel = "/storage/v1" + rel;
                        }
                        full = (_options.Url?.TrimEnd('/') ?? string.Empty) + rel;
                    }
                    return (true, full, null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parseando signedURL");
            }
            return (false, null, "No se encontró signedURL en respuesta");
        }

        public async Task<(bool ok, string? error)> DeleteAsync(string bucket, string objectPath)
        {
            if (string.IsNullOrWhiteSpace(bucket) || string.IsNullOrWhiteSpace(objectPath))
                return (false, "Bucket o ruta vacíos");
            var encodedPath = EncodePathSegments(objectPath);
            var url = $"{BaseStorageUrl}/object/{bucket}/{encodedPath}";
            using var req = new HttpRequestMessage(HttpMethod.Delete, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AuthToken);
            var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                _logger.LogWarning("Fallo delete {Bucket}/{Path}: {Status} {Body}", bucket, objectPath, resp.StatusCode, body);
                return (false, $"Error delete: {resp.StatusCode}");
            }
            return (true, null);
        }

        private class SignUrlResponse
        {
            public string signedURL { get; set; } = string.Empty;
        }
    }
}
