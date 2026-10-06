// Source: https://github.com/Team-Sellora/sellora-organization/blob/9772eb48d4a05a6a037c408e5c5ab850ba5f2fbe/src/Sellora.CoreService.Infrastructure/IdentityProvisioning/Wso2ScimIdentityProvisioner.cs
// Copied unchanged for reference. Team-Sellora, SLIIT SE3022.

using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sellora.CoreService.Application.IdentityProvisioning;

namespace Sellora.CoreService.Infrastructure.IdentityProvisioning;

/// <summary>
/// Creates users in WSO2 IS through SCIM2 — the same three calls
/// sellora-infra/seed-users.sh makes, so they are proven against our IS:
///   1. POST  /scim2/Users            (with the custom companyId attribute)
///   2. GET   /scim2/v2/Roles         (find the role's ID by name)
///   3. PATCH /scim2/v2/Roles/{id}    (add the user to the role)
///
/// Schema URNs are deliberately inconsistent, as seed-users.sh warns:
/// core uses "urn:ietf:params:scim:…", the custom extension does NOT.
/// "Correcting" the custom one makes IS silently drop companyId.
/// </summary>
public sealed class Wso2ScimIdentityProvisioner : IIdentityProvisioner
{
  public const string HttpClientName = "IdentityProvisioning";

  private const string CoreUserSchema = "urn:ietf:params:scim:schemas:core:2.0:User";
  private const string CustomUserSchema = "urn:scim:schemas:extension:custom:User";
  private const string Wso2UserSchema = "urn:scim:wso2:schema";
  private const string PatchOpSchema = "urn:ietf:params:scim:api:messages:2.0:PatchOp";
  private const string ScimJson = "application/scim+json";

  // Role IDs never change once created; look each up once per process.
  private static readonly ConcurrentDictionary<string, string> RoleIds = new();

  private readonly HttpClient _http;
  private readonly IdentityProvisioningOptions _options;
  private readonly ILogger<Wso2ScimIdentityProvisioner> _logger;

  public Wso2ScimIdentityProvisioner(
    IHttpClientFactory httpClientFactory,
    IOptions<IdentityProvisioningOptions> options,
    ILogger<Wso2ScimIdentityProvisioner> logger)
  {
    _http = httpClientFactory.CreateClient(HttpClientName);
    _options = options.Value;
    _logger = logger;
  }

  public async Task<ProvisionedIdentity> CreateUserAsync(
    NewIdentityUser user,
    CancellationToken cancellationToken)
  {
    var userName = user.Email.Trim().ToLowerInvariant();
    var temporaryPassword = _options.AskPassword ? null : TemporaryPasswordGenerator.Generate();

    var body = new JsonObject
    {
      ["schemas"] = new JsonArray(CoreUserSchema),
      ["userName"] = userName,
      ["name"] = NameOf(user.DisplayName),
      ["emails"] = new JsonArray(new JsonObject { ["primary"] = true, ["value"] = userName }),
      [CustomUserSchema] = new JsonObject { ["companyId"] = user.CompanyId.ToString() }
    };

    if (!string.IsNullOrWhiteSpace(user.Phone))
    {
      body["phoneNumbers"] = new JsonArray(new JsonObject { ["type"] = "mobile", ["value"] = user.Phone.Trim() });
    }

    if (temporaryPassword is null)
    {
      body[Wso2UserSchema] = new JsonObject { ["askPassword"] = true };
    }
    else
    {
      body["password"] = temporaryPassword;
    }

    using var createResponse = await SendAsync(HttpMethod.Post, "scim2/Users", body, cancellationToken);

    if (createResponse.StatusCode == HttpStatusCode.Conflict)
    {
      throw new IdentityUserAlreadyExistsException(userName);
    }

    await EnsureSuccessAsync(createResponse, "create the user", cancellationToken);

    var identityId = (await ReadJsonAsync(createResponse, cancellationToken))?["id"]?.GetValue<string>()
      ?? throw new IdentityProvisioningUnavailableException("The identity provider created the user but returned no id.");

    try
    {
      var roleId = await FindRoleIdAsync(user.Role, cancellationToken);

      var patch = new JsonObject
      {
        ["schemas"] = new JsonArray(PatchOpSchema),
        ["Operations"] = new JsonArray(new JsonObject
        {
          ["op"] = "add",
          ["path"] = "users",
          ["value"] = new JsonArray(new JsonObject { ["value"] = identityId })
        })
      };

      using var roleResponse = await SendAsync(HttpMethod.Patch, $"scim2/v2/Roles/{roleId}", patch, cancellationToken);
      await EnsureSuccessAsync(roleResponse, $"add the user to role {user.Role}", cancellationToken);
    }
    catch
    {
      // A login without its role is useless and confusing; remove it.
      await DeleteUserAsync(identityId, CancellationToken.None);
      throw;
    }

    _logger.LogInformation(
      "Provisioned {Role} login {UserName} ({IdentityId}) for company {CompanyId}",
      user.Role, userName, identityId, user.CompanyId);

    return new ProvisionedIdentity(identityId, userName, temporaryPassword);
  }

  public async Task DeleteUserAsync(string identityId, CancellationToken cancellationToken)
  {
    try
    {
      using var response = await SendAsync(HttpMethod.Delete, $"scim2/Users/{Uri.EscapeDataString(identityId)}", null, cancellationToken);

      if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
      {
        _logger.LogError(
          "Could not remove orphan login {IdentityId} (HTTP {Status}); delete it in the IS console",
          identityId, (int)response.StatusCode);
      }
    }
    catch (Exception exception)
    {
      _logger.LogError(exception, "Could not remove orphan login {IdentityId}; delete it in the IS console", identityId);
    }
  }

  private async Task<string> FindRoleIdAsync(string role, CancellationToken cancellationToken)
  {
    if (RoleIds.TryGetValue(role, out var cached))
    {
      return cached;
    }

    using var response = await SendAsync(HttpMethod.Get, "scim2/v2/Roles?count=200", null, cancellationToken);
    await EnsureSuccessAsync(response, "list roles", cancellationToken);

    var resources = (await ReadJsonAsync(response, cancellationToken))?["Resources"]?.AsArray() ?? new JsonArray();

    foreach (var resource in resources)
    {
      if (resource?["displayName"]?.GetValue<string>() == role &&
          resource["id"]?.GetValue<string>() is { } id)
      {
        RoleIds[role] = id;
        return id;
      }
    }

    throw new IdentityProvisioningUnavailableException(
      $"Role '{role}' does not exist in the identity provider. Run sellora-infra/seed-roles.sh.");
  }

  private async Task<HttpResponseMessage> SendAsync(
    HttpMethod method,
    string path,
    JsonObject? body,
    CancellationToken cancellationToken)
  {
    using var request = new HttpRequestMessage(method, path);

    if (body is not null)
    {
      request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, ScimJson);
    }

    try
    {
      return await _http.SendAsync(request, cancellationToken);
    }
    catch (IdentityProvisioningUnavailableException)
    {
      throw;
    }
    catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
    {
      throw new IdentityProvisioningUnavailableException("The identity provider could not be reached.", exception);
    }
  }

  private async Task EnsureSuccessAsync(
    HttpResponseMessage response,
    string action,
    CancellationToken cancellationToken)
  {
    if (response.IsSuccessStatusCode)
    {
      return;
    }

    var detail = await response.Content.ReadAsStringAsync(cancellationToken);
    _logger.LogError("SCIM2 call to {Action} failed (HTTP {Status}): {Detail}", action, (int)response.StatusCode, detail);

    throw new IdentityProvisioningUnavailableException(
      $"The identity provider could not {action} (HTTP {(int)response.StatusCode}).");
  }

  private static async Task<JsonNode?> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
  {
    var text = await response.Content.ReadAsStringAsync(cancellationToken);
    return string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
  }

  private static JsonObject NameOf(string displayName)
  {
    var parts = displayName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

    return new JsonObject
    {
      ["givenName"] = parts.Length > 0 ? parts[0] : displayName.Trim(),
      ["familyName"] = parts.Length > 1 ? parts[1] : "-"
    };
  }
}

/// <summary>Used when IdentityProvisioning is not configured: fails clearly instead of half-working.</summary>
public sealed class DisabledIdentityProvisioner : IIdentityProvisioner
{
  public Task<ProvisionedIdentity> CreateUserAsync(NewIdentityUser user, CancellationToken cancellationToken) =>
    throw new IdentityProvisioningUnavailableException(
      "User provisioning is not configured on this server (IdentityProvisioning settings are missing).");

  public Task DeleteUserAsync(string identityId, CancellationToken cancellationToken) => Task.CompletedTask;
}
