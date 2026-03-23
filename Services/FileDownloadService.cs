using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using GameCheatsManager.WinUI.Models;

namespace GameCheatsManager.WinUI.Services;

public sealed class FileDownloadService
{
    private readonly BackendConfig _config;
    private readonly HttpClient _httpClient;

    public FileDownloadService(BackendConfig config)
    {
        _config = config;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/132.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
    }

    public bool HasSignedDownloadBackend => _config.HasSignedDownloadConfig;

    public async Task<bool> IsInternetConnectedAsync(CancellationToken cancellationToken = default)
    {
        var urls = new[]
        {
            "https://www.bing.com/",
            "https://www.baidu.com/",
            "http://www.google.com/",
            "https://www.apple.com/",
            "https://www.wechat.com/"
        };

        foreach (var url in urls)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, url);
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
            }
            catch
            {
            }
        }

        return false;
    }

    public async Task<string> GetWebPageContentAsync(string url, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public async Task<string?> DownloadFileAsync(
        string url,
        string downloadDirectory,
        IProgress<DownloadProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(downloadDirectory);

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var fileName = GetDownloadFileName(response);
        var fullPath = Path.Combine(downloadDirectory, fileName);
        var totalBytes = response.Content.Headers.ContentLength ?? 0;
        long downloadedBytes = 0;

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = File.Create(fullPath);
        var buffer = new byte[81920];
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            downloadedBytes += read;
            progress?.Report(new DownloadProgressInfo(downloadedBytes, totalBytes));
        }

        return fullPath;
    }

    public async Task<string?> GetSignedDownloadUrlAsync(string filePathOnS3, CancellationToken cancellationToken = default)
    {
        if (!_config.HasSignedDownloadConfig)
        {
            return null;
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{_config.SignedUrlDownloadEndpoint}?filePath={Uri.EscapeDataString(filePathOnS3)}");
        request.Headers.Add("x-api-key", _config.ClientApiKey);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);
        return json.RootElement.TryGetProperty("signedUrl", out var signedUrl)
            ? signedUrl.GetString()
            : null;
    }

    public async Task<JsonDocument?> GetSignedUploadUrlAsync(string filePathOnS3, string metadataJson, CancellationToken cancellationToken = default)
    {
        if (!_config.HasSignedUploadConfig)
        {
            return null;
        }

        var name = Path.GetFileNameWithoutExtension(filePathOnS3);
        var extension = Path.GetExtension(filePathOnS3);
        var uniquePath = $"trainers/{name}_{Guid.NewGuid():N}{extension}".Replace("\\", "/", StringComparison.Ordinal);
        var requestUrl = $"{_config.SignedUrlUploadEndpoint}?filePath={Uri.EscapeDataString(uniquePath)}&metadata={Uri.EscapeDataString(metadataJson)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        request.Headers.Add("x-api-key", _config.ClientApiKey);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);
    }

    public async Task<bool> UploadFileAsync(
        string uploadUrl,
        IDictionary<string, string> requiredHeaders,
        string filePath,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await using var fileStream = File.OpenRead(filePath);
        using var content = new ProgressStreamContent(fileStream, progress, cancellationToken);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        using var request = new HttpRequestMessage(HttpMethod.Put, uploadUrl)
        {
            Content = content
        };
        foreach (var (key, value) in requiredHeaders)
        {
            request.Headers.TryAddWithoutValidation(key, value);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<string?> GetLatestVersionAsync(string appName, CancellationToken cancellationToken = default)
    {
        if (!_config.HasVersionConfig)
        {
            return null;
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{_config.VersionCheckerEndpoint}?appName={Uri.EscapeDataString(appName)}");
        request.Headers.Add("x-api-key", _config.ClientApiKey);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);
        return json.RootElement.TryGetProperty("latest_version", out var latestVersion)
            ? latestVersion.GetString()
            : null;
    }

    public async Task<Dictionary<string, string>?> GetPatchPatternsAsync(string patchMethod, bool enableDev, CancellationToken cancellationToken = default)
    {
        if (!_config.HasPatchConfig)
        {
            return null;
        }

        var requestUrl = $"{_config.PatchPatternsEndpoint}?patchMethod={Uri.EscapeDataString(patchMethod)}&enableDev={(enableDev ? "true" : "false")}";
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        request.Headers.Add("x-api-key", _config.ClientApiKey);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);
    }

    public static async Task SaveTextAsync(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
    }

    public static async Task<T?> LoadJsonAsync<T>(string path)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream);
    }

    public static string GetDownloadFileName(HttpResponseMessage response)
    {
        var disposition = response.Content.Headers.ContentDisposition;
        if (disposition is not null)
        {
            if (!string.IsNullOrWhiteSpace(disposition.FileNameStar))
            {
                return disposition.FileNameStar.Trim('"');
            }

            if (!string.IsNullOrWhiteSpace(disposition.FileName))
            {
                return disposition.FileName.Trim('"');
            }
        }

        return Path.GetFileName(response.RequestMessage?.RequestUri?.LocalPath) ?? "download.bin";
    }

    private sealed class ProgressStreamContent : HttpContent
    {
        private readonly Stream _source;
        private readonly IProgress<int>? _progress;
        private readonly CancellationToken _cancellationToken;

        public ProgressStreamContent(Stream source, IProgress<int>? progress, CancellationToken cancellationToken)
        {
            _source = source;
            _progress = progress;
            _cancellationToken = cancellationToken;
            Headers.ContentLength = _source.Length;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            var buffer = new byte[81920];
            long uploaded = 0;
            while (true)
            {
                var read = await _source.ReadAsync(buffer, _cancellationToken);
                if (read == 0)
                {
                    break;
                }

                await stream.WriteAsync(buffer.AsMemory(0, read), _cancellationToken);
                uploaded += read;
                if (_source.Length > 0)
                {
                    _progress?.Report((int)(uploaded * 100 / _source.Length));
                }
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _source.Length;
            return true;
        }
    }
}
