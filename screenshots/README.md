# Screenshots

Taken from the running WSO2 consoles in October 2026. Client IDs, hostnames, IP addresses and backend URLs are covered with dark boxes; secrets were never revealed.

## WSO2 Identity Server
| Screenshot | Shows |
|---|---|
| [01-roles-organization-audience](identity-server/01-roles-organization-audience.png) | The five Sellora business roles, Organization audience |
| [02-gateway-client-grant-types](identity-server/02-gateway-client-grant-types.png) | Sellora Gateway Client: Code and Refresh Token grants |
| [03-gateway-client-public-client](identity-server/03-gateway-client-public-client.png) | Allowed origins, PKCE section, public client |
| [04-gateway-client-role-audience](identity-server/04-gateway-client-role-audience.png) | Role audience set to Organization |
| [05-provisioning-client-credentials](identity-server/05-provisioning-client-credentials.png) | sellora-provisioning: confidential client, Client Credentials |
| [06-provisioning-scim2-apis](identity-server/06-provisioning-scim2-apis.png) | Authorized only for the SCIM2 Users and Roles APIs |
| [07-provisioning-access-token](identity-server/07-provisioning-access-token.png) | Opaque access token, 3600 s |

## WSO2 API Manager
| Screenshot | Shows |
|---|---|
| [01-key-managers](api-manager/01-key-managers.png) | IS 7 key manager next to the Resident Key Manager |
| [02-key-manager-wso2-is7-direct-token](api-manager/02-key-manager-wso2-is7-direct-token.png) | Type WSO2 Identity Server 7, Direct Token invocation |
| [03-key-manager-claims-grants-jwks](api-manager/03-key-manager-claims-grants-jwks.png) | Consumer key from `azp`, grant types, JWKS certificates |
| [04-key-manager-self-validate-jwt](api-manager/04-key-manager-self-validate-jwt.png) | Self-validate JWT, out-of-band provisioning |
| [05-gateway-environment](api-manager/05-gateway-environment.png) | Gateway *Default*, Regular, public |
| [06-publisher-sellora-apis](api-manager/06-publisher-sellora-apis.png) | The Sellora APIs in the Publisher |
| [07-catalog-api-settings](api-manager/07-catalog-api-settings.png) | Catalog API: context, OAuth2, plans, subscription validation, resources |
| [08-devportal-inventory-api-subscription](api-manager/08-devportal-inventory-api-subscription.png) | Inventory API subscribed by Sellora Gateway Client v2 |
| [09-devportal-app-linked-is-keys](api-manager/09-devportal-app-linked-is-keys.png) | The app's production keys linked to the existing IS client |
