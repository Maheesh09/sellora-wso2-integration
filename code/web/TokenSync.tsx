// Source: https://github.com/Team-Sellora/sellora-web/blob/6e3118f3154ff22b3a8071b4150d58a1d02ddf29/src/auth/TokenSync.tsx
// Copied unchanged for reference. Team-Sellora, SLIIT SE3022.

import { useEffect } from "react";
import { useAuth } from "react-oidc-context";
import { setAccessToken } from "@/api/tokenStore";
import { setUnauthorizedHandler } from "@/api/client";

/**
 * Keeps the in-memory token store in sync with the OIDC auth state.
 * Renders nothing — it's a side-effect bridge between React auth and the
 * non-React HTTP client.
 */
export function TokenSync() {
  const auth = useAuth();

  useEffect(() => {
    setAccessToken(auth.user?.access_token ?? null);
  }, [auth.user?.access_token]);

  useEffect(() => {
    setUnauthorizedHandler(() => {
      // Preserve where the user was, then send to login.
      const redirectTo = window.location.pathname + window.location.search;
      void auth.signinRedirect({ state: { redirectTo } });
    });
  }, [auth]);

  return null;
}
