/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL: string
  // شناسه‌ی نسخه، فقط در buildِ Publish-Production.ps1 - ر.ک. lib/release.ts
  readonly VITE_RELEASE_ID?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
