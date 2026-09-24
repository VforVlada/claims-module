interface ClaimsRuntimeConfig {
  apiOrigin?: string;
}

/** Set by public/config.js, which each deployment overwrites with its own API origin. */
const runtimeConfig: ClaimsRuntimeConfig =
  (globalThis as { __CLAIMS_CONFIG__?: ClaimsRuntimeConfig }).__CLAIMS_CONFIG__ ?? {};

export const API_ORIGIN = (runtimeConfig.apiOrigin ?? 'http://localhost:5299').replace(/\/+$/, '');
export const API_BASE_URL = `${API_ORIGIN}/api`;

/** LocalFileSystemStorageService returns a relative download URL against the API origin; AzureBlobStorageService returns an absolute SAS URL. */
export function resolveDocumentUrl(downloadUrl: string): string {
  return downloadUrl.startsWith('http') ? downloadUrl : `${API_ORIGIN}${downloadUrl}`;
}
