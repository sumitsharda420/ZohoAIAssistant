using System.Text.Json;
using Microsoft.Extensions.Options;
using ZohoAIAssistant.Configuration;
using ZohoAIAssistant.DTOs;
using ZohoAIAssistant.Models;

namespace ZohoAIAssistant.Services;

/// <summary>
/// Thread-safe token store backed by in-memory state and a JSON file on disk.
/// On startup, loads any previously saved tokens; falls back to appsettings RefreshToken.
/// </summary>
public class TokenStorageService : ITokenStorageService
{
    private readonly ZohoSettings _settings;
    private readonly ILogger<TokenStorageService> _logger;
    private readonly string _storageFilePath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    private StoredZohoTokens _tokens = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Match ZohoOAuthService buffer so validity checks stay consistent.
    private static readonly TimeSpan ExpiryBuffer = TimeSpan.FromSeconds(60);

    public TokenStorageService(
        IOptions<ZohoSettings> settings,
        ILogger<TokenStorageService> logger,
        IWebHostEnvironment environment)
    {
        _settings = settings.Value;
        _logger = logger;

        // Demo persistence file — relative to content root.
        var relativePath = string.IsNullOrWhiteSpace(_settings.TokenStorageFilePath)
            ? "Data/zoho-tokens.json"
            : _settings.TokenStorageFilePath;

        _storageFilePath = Path.Combine(environment.ContentRootPath, relativePath);

        LoadFromFile();
    }

    /// <inheritdoc />
    public Task<StoredZohoTokens> GetStoredTokensAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_tokens);
    }

    /// <inheritdoc />
    public async Task SaveTokensAsync(
        string accessToken,
        string? refreshToken,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        _tokens.AccessToken = accessToken;
        _tokens.ExpiresAtUtc = expiresAtUtc;
        _tokens.LastUpdatedUtc = DateTimeOffset.UtcNow;

        // Only overwrite refresh token when Zoho returns a new one.
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            _tokens.RefreshToken = refreshToken;
        }

        _logger.LogInformation(
            "OAuth tokens updated. Access token expires at {ExpiresAtUtc:O}. Refresh token present: {HasRefresh}",
            expiresAtUtc,
            !string.IsNullOrWhiteSpace(GetEffectiveRefreshToken()));

        await PersistToFileAsync(cancellationToken);
    }

    /// <inheritdoc />
    public bool IsAccessTokenValid()
    {
        return !string.IsNullOrWhiteSpace(_tokens.AccessToken)
               && _tokens.ExpiresAtUtc.HasValue
               && DateTimeOffset.UtcNow < _tokens.ExpiresAtUtc.Value;
    }

    /// <inheritdoc />
    public string? GetEffectiveRefreshToken()
    {
        if (!string.IsNullOrWhiteSpace(_tokens.RefreshToken))
        {
            return _tokens.RefreshToken;
        }

        return string.IsNullOrWhiteSpace(_settings.RefreshToken) ? null : _settings.RefreshToken;
    }

    /// <inheritdoc />
    public ZohoTokenStatusDto GetTokenStatus()
    {
        var refreshToken = GetEffectiveRefreshToken();

        return new ZohoTokenStatusDto
        {
            AccessToken = _tokens.AccessToken,
            RefreshToken = refreshToken,
            ExpiresAtUtc = _tokens.ExpiresAtUtc,
            IsAccessTokenValid = IsAccessTokenValid(),
            HasRefreshToken = !string.IsNullOrWhiteSpace(refreshToken),
            Message = IsAccessTokenValid()
                ? "Access token is valid."
                : string.IsNullOrWhiteSpace(refreshToken)
                    ? "No refresh token available. Complete the OAuth flow via /api/oauth/authorize."
                    : "Access token expired or missing. Call POST /api/oauth/refresh or use the Leads API (auto-refresh)."
        };
    }

    /// <summary>Loads token state from the JSON file if it exists.</summary>
    private void LoadFromFile()
    {
        try
        {
            if (!File.Exists(_storageFilePath))
            {
                _logger.LogInformation(
                    "No token storage file found at {Path}. Tokens will be created after OAuth callback.",
                    _storageFilePath);
                return;
            }

            var json = File.ReadAllText(_storageFilePath);
            var loaded = JsonSerializer.Deserialize<StoredZohoTokens>(json, JsonOptions);

            if (loaded is not null)
            {
                _tokens = loaded;
                _logger.LogInformation(
                    "Loaded OAuth tokens from {Path}. Access token valid: {IsValid}",
                    _storageFilePath,
                    IsAccessTokenValid());
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load token storage file from {Path}. Starting with empty tokens.", _storageFilePath);
        }
    }

    /// <summary>Writes the current token state to the JSON file.</summary>
    private async Task PersistToFileAsync(CancellationToken cancellationToken)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            var directory = Path.GetDirectoryName(_storageFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(_tokens, JsonOptions);
            await File.WriteAllTextAsync(_storageFilePath, json, cancellationToken);

            _logger.LogDebug("OAuth tokens persisted to {Path}.", _storageFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist OAuth tokens to {Path}.", _storageFilePath);
        }
        finally
        {
            _fileLock.Release();
        }
    }
}
