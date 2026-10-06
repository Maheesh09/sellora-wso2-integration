// Source: https://github.com/Team-Sellora/sellora-organization/blob/9772eb48d4a05a6a037c408e5c5ab850ba5f2fbe/src/Sellora.CoreService.Infrastructure/IdentityProvisioning/ScimAuthenticationHandler.cs
// Copied unchanged for reference. Team-Sellora, SLIIT SE3022.

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Sellora.CoreService.Application.IdentityProvisioning;

namespace Sellora.CoreService.Infrastructure.IdentityProvisioning;

/// <summary>
/// Adds credentials to every SCIM2 call: an OAuth client-credentials token
/// (cached until shortly before it expires) or, for local development,
/// the IS admin's basic credentials — the same ones seed-users.sh uses.
/// </summary>
public sealed class ScimAuthenticationHandler : DelegatingHandler
{
  private readonly IdentityProvisioningOptions _options;
  private readonly IHttpClientFactory _httpClientFactory;
  private readonly ScimTokenCache _cache;

  public const string TokenClientName = "IdentityProvisioningToken";

  // Transient (IHttpClientFactory rebuilds handler chains), so the token
  // lives in a singleton cache instead of on the handler.
  public ScimAuthenticationHandler(
    IOptions<IdentityProvisioningOptions> options,
    IHttpClientFactory httpClientFactory,
    ScimTokenCache cache)
  {
    _options = options.Value;
    _httpClientFactory = httpClientFactory;
    _cache = cache;
  }

  protected override async Task<HttpResponseMessage> SendAsync(
    HttpRequestMessage request,
    CancellationToken cancellationToken)
  {
    request.Headers.Authorization = _options.IsBasic
      ? new AuthenticationHeaderValue(
        "Basic",
        Convert.ToBase64String(Encoding.UTF8.GetBytes(
          $"{_options.AdminUserName}:{_options.AdminPassword}")))
      : new AuthenticationHeaderValue("Bearer", await GetTokenAsync(cancellationToken));

    return await base.SendAsync(request, cancellationToken);
  }

  private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
  {
    if (_cache.Token is { } cached && DateTimeOffset.UtcNow < _cache.ExpiresAt)
    {
      return cached;
    }

    await _cache.Lock.WaitAsync(cancellationToken);
    try
    {
      if (_cache.Token is { } fresh && DateTimeOffset.UtcNow < _cache.ExpiresAt)
      {
        return fresh;
      }

      using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, _options.ResolvedTokenEndpoint)
      {
        Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
          ["grant_type"] = "client_credentials",
          ["scope"] = _options.Scopes
        })
      };

      tokenRequest.Headers.Authorization = new AuthenticationHeaderValue(
        "Basic",
        Convert.ToBase64String(Encoding.UTF8.GetBytes(
          $"{Uri.EscapeDataString(_options.ClientId)}:{Uri.EscapeDataString(_options.ClientSecret)}")));

      using var response = await _httpClientFactory
        .CreateClient(TokenClientName)
        .SendAsync(tokenRequest, cancellationToken);

      if (!response.IsSuccessStatusCode)
      {
        throw new IdentityProvisioningUnavailableException(
          $"The identity provider refused the provisioning client's credentials (HTTP {(int)response.StatusCode}).");
      }

      using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
      var token = json.RootElement.GetProperty("access_token").GetString()
        ?? throw new IdentityProvisioningUnavailableException("The identity provider returned no access token.");

      var lifetime = json.RootElement.TryGetProperty("expires_in", out var expiresIn)
        ? expiresIn.GetInt32()
        : 300;

      // Renew a minute early so a request never carries an expired token.
      _cache.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, lifetime - 60));
      _cache.Token = token;
      return token;
    }
    finally
    {
      _cache.Lock.Release();
    }
  }
}

/// <summary>The provisioning client's access token, shared across handler instances.</summary>
public sealed class ScimTokenCache
{
  internal SemaphoreSlim Lock { get; } = new(1, 1);

  internal string? Token { get; set; }

  internal DateTimeOffset ExpiresAt { get; set; }
}
