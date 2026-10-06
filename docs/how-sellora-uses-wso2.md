# How Sellora uses WSO2 Identity Server and API Manager

This walkthrough describes the configuration and code as they are. Each section points to the screenshot or source file that shows it.

**Sources:** WSO2 IS Console and APIM Admin Portal, Publisher and Developer Portal (October 2026); a decoded access token from a real sign-in; the `dev` branches of the Sellora repositories (web: `develop`).

---

## 1. Components

| Component | Details |
|---|---|
| WSO2 Identity Server 7.3 | Root organization *Super* (`carbon.super`). Single tenant: a Sellora company is **not** a WSO2 tenant, it is the `companyId` claim. |
| WSO2 API Manager 4.7.0 | One gateway environment, *Default* (Regular, public), port 8244. IS and APIM share one AWS EC2 instance, so APIM runs with port offset 1. |
| Sellora services | .NET 8 on Azure App Service: Organization, Catalog, Inventory, Order, Notification. Each has its own PostgreSQL database. |
| Web app | React single-page app on Vercel. |

## 2. Request flow

1. **Sign in.** The web app redirects the user to the WSO2 IS login page (Authorization Code + PKCE).
2. **Token.** IS issues a signed JWT access token (1 hour) to the *Sellora Gateway Client* application, carrying `sub`, `roles` and `companyId`.
3. **API call.** The web app calls `https://<gateway>:8244/sellora-<service>/1.0.0/...` with `Authorization: Bearer <JWT>`.
4. **Gateway.** APIM validates the JWT itself with the IS signing keys (JWKS), maps the token's `azp` to the subscribed application, and applies the rate-limit plan.
5. **Service.** The .NET service validates the same JWT again, applies a role policy from `roles`, and uses `companyId` as the tenant for every database query.
6. **Scope.** Where the user sits in the hierarchy (agency, rep, shop, provinces) is looked up from the Organization service by `sub`, not stored in the token.

Validation is local at both the gateway and the services. Signing keys are fetched from IS and cached; there is no call to IS for each request.

Service-to-service calls (for example Order → Inventory) go directly between App Services, not through APIM. The calling service forwards the user's JWT, and the receiving service validates it again.

## 3. WSO2 Identity Server

### 3.1 Roles
Five business roles with **Organization** audience: `CompanyAdmin`, `AreaManager`, `AgencyOperator`, `SalesRep`, `ShopOwner`.
IS also adds the built-in `everyone` role to every user, so every authorization check looks for a specific business role.
→ [screenshot](../screenshots/identity-server/01-roles-organization-audience.png) · [RolePolicies.cs](../code/services/RolePolicies.cs)

### 3.2 Custom claim
The user attribute `http://wso2.org/claims/companyId` is mapped to the OIDC claim `companyId`. It holds a GUID, one per Sellora company.

### 3.3 Application: Sellora Gateway Client (web sign-in)

| Setting | Value |
|---|---|
| Type | Standard-Based Application |
| Grant types | Authorization Code, Refresh Token |
| Client authentication | Public client: no client secret, because a browser cannot keep one |
| PKCE | Sent by the web app on every sign-in; *Mandatory* is not enabled in IS |
| Access token | JWT, 3600 seconds |
| Role audience | Organization: this is what puts the business roles into the `roles` claim |

→ screenshots: [grant types](../screenshots/identity-server/02-gateway-client-grant-types.png) · [public client and PKCE](../screenshots/identity-server/03-gateway-client-public-client.png) · [role audience](../screenshots/identity-server/04-gateway-client-role-audience.png)

### 3.4 Application: sellora-provisioning (machine client)

| Setting | Value |
|---|---|
| Type | Standard-Based Application, confidential (client secret kept only in App Service settings) |
| Grant type | Client Credentials only |
| Access token | Opaque, 3600 seconds |
| Authorized APIs | SCIM2 Users API and SCIM2 Roles API only |
| Scopes | `internal_user_mgt_create`, `internal_user_mgt_delete`, `internal_user_mgt_list`, `internal_role_mgt_view`, `internal_role_mgt_update`, `internal_role_mgt_users_update` |

→ screenshots: [client credentials](../screenshots/identity-server/05-provisioning-client-credentials.png) · [SCIM2 APIs](../screenshots/identity-server/06-provisioning-scim2-apis.png) · [access token](../screenshots/identity-server/07-provisioning-access-token.png)

### 3.5 A real access token (IDs shortened)

```json
{
  "sub": "d8f8f8b7-…",
  "aut": "APPLICATION_USER",
  "roles": ["AgencyOperator", "everyone"],
  "companyId": "f62a41eb-…",
  "iss": "https://<is-host>:9443/oauth2/token",
  "aud": "<gateway-client-id>",
  "azp": "<gateway-client-id>",
  "client_id": "<gateway-client-id>",
  "scope": "openid profile roles",
  "org_handle": "carbon.super",
  "iat": 1791317492,
  "exp": 1791321092
}
```

## 4. Sign-in in the web app

The web app uses `oidc-client-ts` and `react-oidc-context`:

- **IS endpoints:** authority `<IS>/oauth2/token`; discovery at `<IS>/oauth2/token/.well-known/openid-configuration`.
- **Flow:** `response_type: "code"`, with PKCE added by the library.
- **Scopes:** `openid profile roles offline_access` (plus one API scope).
- **Token storage:** in memory only (`InMemoryWebStorage`), never in `localStorage`.
- **Renewal:** silent, 120 seconds before the token expires.
- **API calls:** every call through the gateway carries `Authorization: Bearer <token>`; a 401 sends the user back to sign in.

→ [oidcConfig.ts](../code/web/oidcConfig.ts) · [TokenSync.tsx](../code/web/TokenSync.tsx) · [client.ts](../code/web/client.ts)

## 5. WSO2 API Manager

### 5.1 Key manager: `key_manager_sellora`

| Setting | Value |
|---|---|
| Type | WSO2 Identity Server 7 |
| API invocation | Direct Token: the IS token is sent to the gateway as-is |
| Token validation | Self-validate JWT |
| Certificates | JWKS from `<IS>/oauth2/jwks` |
| Claim URIs | consumer key ← `azp`, scopes ← `scope` |
| Advanced | Token Generation, Out-of-Band Provisioning, OAuth App Creation |

→ screenshots: [key managers](../screenshots/api-manager/01-key-managers.png) · [type and invocation](../screenshots/api-manager/02-key-manager-wso2-is7-direct-token.png) · [claims and JWKS](../screenshots/api-manager/03-key-manager-claims-grants-jwks.png) · [self-validate JWT](../screenshots/api-manager/04-key-manager-self-validate-jwt.png)

### 5.2 Published APIs
REST, version 1.0.0, OAuth2 security, subscription validation enabled, plans `Sellora100PerMin`, `LoadTest`, `Unlimited`. Each API's backend is that service's Azure App Service.

| API | Context |
|---|---|
| Sellora Organization API | `/sellora-organization` |
| Sellora Catalog API | `/sellora-catalog` |
| Sellora Inventory API | `/sellora-inventory` |
| Sellora Order API | `/sellora-order` |
| Sellora Notification API | `/sellora-notification` |

→ screenshots: [gateway](../screenshots/api-manager/05-gateway-environment.png) · [Publisher](../screenshots/api-manager/06-publisher-sellora-apis.png) · [Catalog API settings](../screenshots/api-manager/07-catalog-api-settings.png)

### 5.3 How the browser's token is accepted
The Developer Portal application **Sellora Gateway Client v2** is subscribed to the Sellora APIs. Under the `KEY_MANAGER_SELLORA` tab, its production keys are the **existing IS client**, linked with out-of-band provisioning, so its consumer key is the Sellora Gateway Client's client ID.

That is the same value as `azp` in every user token. When a request arrives, the gateway reads `azp`, finds this application, and checks its subscription and rate-limit plan.

→ screenshots: [subscription](../screenshots/api-manager/08-devportal-inventory-api-subscription.png) · [linked IS keys](../screenshots/api-manager/09-devportal-app-linked-is-keys.png)

## 6. Token validation in the services

Every service configures `AddJwtBearer` against IS:

- **Validation:** signature, issuer, audience (the Gateway Client) and lifetime, with 30 seconds clock skew.
- **Roles:** `RoleClaimType = "roles"`, the claim name WSO2 IS uses; policies such as `RequireCompanyAdmin` check it.
- **Claim names:** Organization, Order and Notification also set `MapInboundClaims = false`, so WSO2's claim names (`roles`, `companyId`, `sub`) arrive unchanged.
- **Tenant isolation:** `companyId` is parsed from the token, and EF Core global query filters limit every query to that company. The tenant never comes from the request body or URL.

→ [Program.JwtBearer.excerpt.cs](../code/services/Program.JwtBearer.excerpt.cs) · [HttpTenantContext.cs](../code/services/HttpTenantContext.cs) · [OrderDbContext.TenantFilter.excerpt.cs](../code/services/OrderDbContext.TenantFilter.excerpt.cs)

## 7. Identity in the token, hierarchy in Organization

| Question | Answered by |
|---|---|
| Who is this user? (`sub`, `roles`, `companyId`) | the WSO2 IS token |
| Where do they sit? (agency, rep, shop, provinces) | Organization `GET /api/me/scope`, by the token's `sub` |

Order and Inventory call `/api/me/scope` with the user's own token and cache the answer for 5 minutes. If Organization is unavailable, the caller gets 503 rather than a guessed scope.

Hierarchy IDs used to be custom claims typed into IS by hand, where a wrong value silently showed a user the wrong data.
→ [CallerScopeMiddleware.cs](../code/services/CallerScopeMiddleware.cs)

## 8. User provisioning over SCIM2

The Organization service creates WSO2 IS logins, so nobody opens the IS Console to add a user:

1. **Machine token.** Gets a client-credentials token for *sellora-provisioning* (cached).
2. **Create the login.** `POST /scim2/Users` with the email as username and `companyId` in the custom schema `urn:scim:schemas:extension:custom:User`.
3. **Assign the role.** `GET /scim2/v2/Roles`, then `PATCH /scim2/v2/Roles/{id}` to add the user to their role.
4. **Rollback.** If the role step or saving the staff profile fails, the new login is deleted (`DELETE /scim2/Users/{id}`).
5. **Temporary password.** Generated, returned once with `Cache-Control: no-store`, and never stored.

**Who may create whom:** a Company Admin can create any staff role; an Agency Operator can create Sales Reps only. The new user's `companyId` always comes from the creating admin's token.

→ [Wso2ScimIdentityProvisioner.cs](../code/provisioning/Wso2ScimIdentityProvisioner.cs) · [ScimAuthenticationHandler.cs](../code/provisioning/ScimAuthenticationHandler.cs) · [IdentityProvisioningOptions.cs](../code/provisioning/IdentityProvisioningOptions.cs) · [TemporaryPasswordGenerator.cs](../code/provisioning/TemporaryPasswordGenerator.cs)
