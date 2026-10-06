// Source: https://github.com/Team-Sellora/sellora-organization/blob/9772eb48d4a05a6a037c408e5c5ab850ba5f2fbe/src/Sellora.CoreService.Infrastructure/IdentityProvisioning/IdentityProvisioningOptions.cs
// Copied unchanged for reference. Team-Sellora, SLIIT SE3022.

namespace Sellora.CoreService.Infrastructure.IdentityProvisioning;

/// <summary>
/// Bound from the "IdentityProvisioning" section. Secrets come from App
/// Service settings (IdentityProvisioning__ClientSecret etc.), never git.
/// </summary>
public sealed class IdentityProvisioningOptions
{
  public const string SectionName = "IdentityProvisioning";

  /// <summary>WSO2 IS base URL, e.g. https://is.selloralk.tech. Empty disables provisioning.</summary>
  public string BaseUrl { get; set; } = string.Empty;

  /// <summary>"ClientCredentials" (recommended) or "Basic" (local development only).</summary>
  public string AuthMode { get; set; } = "ClientCredentials";

  public string ClientId { get; set; } = string.Empty;

  public string ClientSecret { get; set; } = string.Empty;

  /// <summary>Defaults to {BaseUrl}/oauth2/token.</summary>
  public string TokenEndpoint { get; set; } = string.Empty;

  /// <summary>IS 7 scopes for the SCIM2 Users and Roles APIs.</summary>
  public string Scopes { get; set; } =
    "internal_user_mgt_create internal_user_mgt_delete internal_user_mgt_list " +
    "internal_role_mgt_view internal_role_mgt_update internal_role_mgt_users_update";

  /// <summary>Basic mode only.</summary>
  public string AdminUserName { get; set; } = string.Empty;

  public string AdminPassword { get; set; } = string.Empty;

  /// <summary>
  /// "TemporaryPassword" (default): a strong password is generated and shown
  /// to the admin once. "AskPassword": IS emails the user a set-password
  /// link, which needs an email sender configured in IS.
  /// </summary>
  public string InvitationMode { get; set; } = "TemporaryPassword";

  /// <summary>For an IS with a self-signed certificate on staging only.</summary>
  public bool AllowUntrustedCertificate { get; set; }

  public int TimeoutSeconds { get; set; } = 10;

  public bool IsConfigured =>
    !string.IsNullOrWhiteSpace(BaseUrl) &&
    (IsBasic
      ? !string.IsNullOrWhiteSpace(AdminUserName) && !string.IsNullOrWhiteSpace(AdminPassword)
      : !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret));

  public bool IsBasic => string.Equals(AuthMode, "Basic", StringComparison.OrdinalIgnoreCase);

  public bool AskPassword => string.Equals(InvitationMode, "AskPassword", StringComparison.OrdinalIgnoreCase);

  public string ResolvedTokenEndpoint =>
    string.IsNullOrWhiteSpace(TokenEndpoint)
      ? $"{BaseUrl.TrimEnd('/')}/oauth2/token"
      : TokenEndpoint;
}
