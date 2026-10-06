// Source: https://github.com/Team-Sellora/sellora-web/blob/6e3118f3154ff22b3a8071b4150d58a1d02ddf29/src/api/client.ts
// Copied unchanged for reference. Team-Sellora, SLIIT SE3022.

import { env } from "@/config/env";
import { getAccessToken } from "./tokenStore";

// Called when a request comes back 401 despite the token/renewal — the token
// was rejected, so send the user to log in again, preserving where they were.
let onUnauthorized: (() => void) | null = null;

export function setUnauthorizedHandler(handler: () => void): void {
  onUnauthorized = handler;
}

async function fetchWithBase(
  baseUrl: string,
  path: string,
  options: RequestInit = {},
  redirectOnUnauthorized = true,
): Promise<Response> {
  const token = getAccessToken();

  const headers = new Headers(options.headers);
  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }
  headers.set("Accept", "application/json");

  const url = `${baseUrl}${path}`;
  const response = await fetch(url, { ...options, headers });

  if (response.status === 401 && redirectOnUnauthorized) {
    // Token rejected — trigger the login redirect wired up by the auth layer.
    onUnauthorized?.();
  }

  return response;
}

/**
 * Gateway HTTP client for the organization service. Automatically attaches
 * the bearer token to every request, so callers never handle auth headers.
 */
export function apiFetch(path: string, options: RequestInit = {}): Promise<Response> {
  return fetchWithBase(env.gatewayBaseUrl, path, options);
}

/** Gateway HTTP client for the catalog service (products). */
export function catalogApiFetch(path: string, options: RequestInit = {}): Promise<Response> {
  return fetchWithBase(env.catalogGatewayBaseUrl, path, options);
}

/** Gateway HTTP client for the Inventory service (stock). */
export function inventoryApiFetch(path: string, options: RequestInit = {}): Promise<Response> {
  // Inventory can return 401 when APIM/backend role or tenant mapping is
  // incomplete. Keep the user on the page so its actionable error is shown;
  // a 401 from the identity or other APIs still follows the normal sign-in flow.
  return fetchWithBase(env.inventoryGatewayBaseUrl, path, options, false);
}

/** Gateway HTTP client for the Order service. */
export function orderApiFetch(path: string, options: RequestInit = {}): Promise<Response> {
  // Like Inventory: keep the user on the page on 401 so the order screens can
  // show an actionable message while token scope claims are still being wired.
  return fetchWithBase(env.orderGatewayBaseUrl, path, options, false);
}

/** Gateway HTTP client for the Notification service (US-E5-3). */
export function notificationApiFetch(path: string, options: RequestInit = {}): Promise<Response> {
  // Like Order: stay on the page on 401 so the screen can say what is missing.
  return fetchWithBase(env.notificationGatewayBaseUrl, path, options, false);
}
