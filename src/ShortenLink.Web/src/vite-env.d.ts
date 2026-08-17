/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_SHORTENLINK_API_BASE_URL?: string;
  readonly VITE_SHORTENLINK_ADMIN_API_KEY?: string;
  readonly VITE_SHORTENLINK_ADMIN_API_KEY_HEADER?: string;
  readonly VITE_SHORTENLINK_ADMIN_ROLE?: string;
  readonly VITE_SHORTENLINK_ADMIN_PERMISSIONS?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}

interface Window {
  readonly __SHORTENLINK_CONFIG__?: {
    readonly apiBaseUrl?: string;
  };
}
