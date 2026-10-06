// Source: https://github.com/Team-Sellora/sellora-web/blob/6e3118f3154ff22b3a8071b4150d58a1d02ddf29/src/auth/oidcConfig.ts
// Copied unchanged for reference. Team-Sellora, SLIIT SE3022.

import type { UserManagerSettings } from "oidc-client-ts";
import { InMemoryWebStorage, WebStorageStateStore } from "oidc-client-ts";
import { env } from "@/config/env";

export const oidcConfig: UserManagerSettings = {
  authority: `${env.isBaseUrl}/oauth2/token`,
  metadataUrl: `${env.isBaseUrl}/oauth2/token/.well-known/openid-configuration`,
  client_id: env.oidcClientId,
  redirect_uri: env.appOrigin,
  post_logout_redirect_uri: env.appOrigin,
  response_type: "code",
  scope: "openid profile roles offline_access sellora:ref:read",
  automaticSilentRenew: true,
  // Fire the renewal 2 minutes before expiry, so there's time to retry
  // if the first attempt fails (CSP-45 requirement).
  accessTokenExpiringNotificationTimeInSeconds: 120,
  // Token held in memory only (not localStorage/sessionStorage), per CSP-44.
  // Cleared on refresh; silent renewal (CSP-45) restores it from the still-
  // valid Identity Server session.
  userStore: new WebStorageStateStore({ store: new InMemoryWebStorage() }),
};
