// The one place that decides what a browser-facing API URL looks like.
//
// The public contract is fixed and the same everywhere - dev, tests, IIS:
//
//     <base><path>          e.g.  /api  +  /Auth/login  ->  /api/Auth/login
//
// The `api` segment belongs to the base alone. Nothing downstream adds it, and
// the backend does not repeat it in its route templates either: on the server
// the API is an IIS Application mounted at /api, so IIS owns that prefix.
//
// This lives apart from httpClient.ts so it can be tested without a browser or
// a bundler - it is a pure string function with no import.meta, fetch or DOM.

/**
 * Joins the configured API base with a request path, with exactly one slash
 * between them.
 *
 * The trimming is not cosmetic. A base written as `/api/` used to produce
 * `/api//Auth/login`, which IIS treats as a different path than `/api/Auth/login`
 * and answers with 404 - a whole environment broken by one trailing character in
 * a `.env` file, with nothing in the error to point at it.
 */
export function joinApiPath(baseUrl: string, path: string): string {
  const base = baseUrl.replace(/\/+$/, '')
  const suffix = path.replace(/^\/+/, '')
  return suffix ? `${base}/${suffix}` : base
}

/**
 * Absolute URL for a request. A relative base (`/api`, the same-origin IIS
 * layout) is resolved against the page origin; an absolute base
 * (`https://host/api`, API on another host) ignores that origin argument.
 * Keeping the relative form working is what lets one production build serve
 * any hostname without being rebuilt.
 */
export function resolveApiUrl(baseUrl: string, path: string, origin: string): URL {
  return new URL(joinApiPath(baseUrl, path), origin)
}
