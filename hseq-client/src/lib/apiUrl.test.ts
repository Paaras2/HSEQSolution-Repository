// The browser-facing API contract, asserted on the client side.
//
// Runs on Node's built-in test runner and TypeScript support - no test
// framework, no bundler, no new dependency:
//
//     npm test
//
// Why this file exists: the login call is assembled from two pieces that live
// far apart - VITE_API_BASE_URL in a .env file, and the path passed by
// authApi.login. Nothing checked what they produce together, so a deployment
// once ended up calling a URL the server answered with 404 while nothing in
// either half looked wrong on its own.

import assert from 'node:assert/strict'
import { test } from 'node:test'

import { joinApiPath, resolveApiUrl } from './apiUrl.ts'

// The value in hseq-client/.env.production - the same-origin IIS layout.
const PRODUCTION_BASE = '/api'

test('login posts to exactly /api/Auth/login in the production build', () => {
  assert.equal(joinApiPath(PRODUCTION_BASE, '/Auth/login'), '/api/Auth/login')
})

test('the api segment is never doubled', () => {
  const url = joinApiPath(PRODUCTION_BASE, '/Auth/login')

  assert.equal(url.startsWith('/api/api'), false)
  assert.equal(url.split('/').filter((segment) => segment === 'api').length, 1)
})

test('a trailing slash on the base does not produce a doubled slash', () => {
  // `/api//Auth/login` is a different path to IIS, and answers 404.
  assert.equal(joinApiPath('/api/', '/Auth/login'), '/api/Auth/login')
  assert.equal(joinApiPath('/api//', '/Auth/login'), '/api/Auth/login')
})

test('an absolute base keeps its host and still lands on /api/Auth/login', () => {
  assert.equal(
    joinApiPath('https://hseq.odcc.local/api', '/Auth/login'),
    'https://hseq.odcc.local/api/Auth/login',
  )
})

test('a relative base resolves against the page origin, not a fixed domain', () => {
  // One production build has to work on every hostname the site is served from.
  const onIp = resolveApiUrl(PRODUCTION_BASE, '/Auth/login', 'http://172.17.0.254:2525')
  const onName = resolveApiUrl(PRODUCTION_BASE, '/Auth/login', 'https://hseq.odcc.local')

  assert.equal(onIp.href, 'http://172.17.0.254:2525/api/Auth/login')
  assert.equal(onName.href, 'https://hseq.odcc.local/api/Auth/login')
  assert.equal(onIp.pathname, '/api/Auth/login')
})

test('an absolute base ignores the page origin', () => {
  const url = resolveApiUrl('https://api.example.test/api', '/Auth/login', 'https://hseq.odcc.local')

  assert.equal(url.href, 'https://api.example.test/api/Auth/login')
})

test('the dev base points at the API port and keeps the same public path', () => {
  // hseq-client/.env.development
  const url = resolveApiUrl('http://localhost:6270/api', '/Auth/login', 'http://localhost:5173')

  assert.equal(url.href, 'http://localhost:6270/api/Auth/login')
  assert.equal(url.pathname, '/api/Auth/login')
})

test('every route the client calls sits under the single /api base', () => {
  const paths = [
    '/Auth/login',
    '/Health',
    '/Search/documents',
    '/MasterData/projects',
    '/Document/Get',
    '/Admin/users',
    '/Dashboard',
  ]

  for (const path of paths) {
    const url = joinApiPath(PRODUCTION_BASE, path)
    assert.equal(url, `/api${path}`)
    assert.equal(url.includes('/api/api'), false)
  }
})
