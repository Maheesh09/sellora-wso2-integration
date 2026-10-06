# Code that integrates with WSO2

Unchanged copies of the files that talk to WSO2 Identity Server, or rely on its tokens. The first line of each file links to the exact commit it was copied from.

## Web app: sign-in with WSO2 IS
| File | What it does |
|---|---|
| [web/oidcConfig.ts](web/oidcConfig.ts) | OIDC settings for WSO2 IS: Authorization Code + PKCE, in-memory token storage, silent renewal |
| [web/TokenSync.tsx](web/TokenSync.tsx) | Keeps the HTTP client's token in step with the sign-in state; redirects to IS on 401 |
| [web/client.ts](web/client.ts) | Sends every API call to the APIM gateway with `Authorization: Bearer <token>` |

## Services: validating WSO2 tokens
| File | What it does |
|---|---|
| [services/Program.JwtBearer.excerpt.cs](services/Program.JwtBearer.excerpt.cs) | JWT bearer validation against IS (issuer, audience, lifetime, signing keys) |
| [services/RolePolicies.cs](services/RolePolicies.cs) | Authorization policies on the raw WSO2 `roles` claim |
| [services/HttpTenantContext.cs](services/HttpTenantContext.cs) | Reads `companyId` from the token as the request's tenant |
| [services/OrderDbContext.TenantFilter.excerpt.cs](services/OrderDbContext.TenantFilter.excerpt.cs) | EF Core query filters that restrict every query to that tenant |
| [services/CallerScopeMiddleware.cs](services/CallerScopeMiddleware.cs) | Looks up the user's hierarchy scope from Organization by the token's `sub` |

## Organization service: SCIM2 provisioning into WSO2 IS
| File | What it does |
|---|---|
| [provisioning/Wso2ScimIdentityProvisioner.cs](provisioning/Wso2ScimIdentityProvisioner.cs) | Creates the IS user, assigns the role, rolls back on failure |
| [provisioning/ScimAuthenticationHandler.cs](provisioning/ScimAuthenticationHandler.cs) | Client-credentials token for the SCIM2 APIs, cached |
| [provisioning/IdentityProvisioningOptions.cs](provisioning/IdentityProvisioningOptions.cs) | Settings and the minimal SCIM2 scopes requested |
| [provisioning/TemporaryPasswordGenerator.cs](provisioning/TemporaryPasswordGenerator.cs) | One-time temporary password for new users |
