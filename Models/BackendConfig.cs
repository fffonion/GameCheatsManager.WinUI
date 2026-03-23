namespace GameCheatsManager.WinUI.Models;

public sealed class BackendConfig
{
    public string SignedUrlDownloadEndpoint { get; set; } = string.Empty;

    public string SignedUrlUploadEndpoint { get; set; } = string.Empty;

    public string VersionCheckerEndpoint { get; set; } = string.Empty;

    public string PatchPatternsEndpoint { get; set; } = string.Empty;

    public string ClientApiKey { get; set; } = string.Empty;

    public bool HasSignedDownloadConfig =>
        !string.IsNullOrWhiteSpace(SignedUrlDownloadEndpoint) &&
        !string.IsNullOrWhiteSpace(ClientApiKey);

    public bool HasSignedUploadConfig =>
        !string.IsNullOrWhiteSpace(SignedUrlUploadEndpoint) &&
        !string.IsNullOrWhiteSpace(ClientApiKey);

    public bool HasVersionConfig =>
        !string.IsNullOrWhiteSpace(VersionCheckerEndpoint) &&
        !string.IsNullOrWhiteSpace(ClientApiKey);

    public bool HasPatchConfig =>
        !string.IsNullOrWhiteSpace(PatchPatternsEndpoint) &&
        !string.IsNullOrWhiteSpace(ClientApiKey);
}
