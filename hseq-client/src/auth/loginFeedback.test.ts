// صفحه‌ی ورود، بدون مرورگر: عادی‌سازی ورودی و ترجمه‌ی پاسخ سرور به پیام.
//
//     npm test

import test from 'node:test'
import assert from 'node:assert/strict'
import {
  describeLoginFailure,
  looksLikePersianKeyboard,
  normalizeUsername,
  validateLogin,
} from './loginFeedback.ts'

test('Persian and Arabic digits become Latin digits', () => {
  assert.equal(normalizeUsername('۳۲۵۶'), '3256')
  assert.equal(normalizeUsername('٣٢٥٦'), '3256')
})

test('invisible marks and spaces copied from a Persian document are dropped', () => {
  assert.equal(normalizeUsername('‏3256'), '3256')
  assert.equal(normalizeUsername(' 32‌56 '), '3256')
  assert.equal(normalizeUsername('‫۳۲۵۶‬'), '3256')
  assert.equal(normalizeUsername('﻿3256'), '3256')
})

test('empty fields are reported per field', () => {
  assert.deepEqual(Object.keys(validateLogin('', '')).sort(), ['password', 'username'])
  assert.deepEqual(Object.keys(validateLogin('3256', '')), ['password'])
  assert.deepEqual(validateLogin('3256', 'secret'), {})
})

test('a Persian keyboard is detected from letters or digits', () => {
  assert.equal(looksLikePersianKeyboard('secret123'), false)
  assert.equal(looksLikePersianKeyboard('سثزقثف'), true)
  assert.equal(looksLikePersianKeyboard('pass۱۲۳'), true)
})

test('a wrong password shows the message user management sent', () => {
  const failure = describeLoginFailure({
    status: 400,
    code: 410,
    reason: 'invalid_credentials',
    message: 'اطلاعات وارد شده صحیح نمی باشد',
  })
  assert.equal(failure.kind, 'invalid_credentials')
  assert.equal(failure.title, 'اطلاعات وارد شده صحیح نمی باشد')
  assert.equal(failure.canRetry, false)
})

test('an unavailable service is a warning with retry, never a wrong password', () => {
  const failure = describeLoginFailure({ status: 400, code: 503, reason: 'unavailable', message: 'x' })
  assert.equal(failure.kind, 'unavailable')
  assert.equal(failure.tone, 'warning')
  assert.equal(failure.canRetry, true)
})

test('an inactive account has its own message', () => {
  const failure = describeLoginFailure({ status: 403, reason: 'inactive', message: 'x' })
  assert.equal(failure.kind, 'inactive')
  assert.equal(failure.canRetry, false)
})

test('no response at all is a network failure', () => {
  assert.equal(describeLoginFailure(new TypeError('Failed to fetch')).kind, 'network')
})

test('a server without reason is still classified from its codes', () => {
  assert.equal(describeLoginFailure({ status: 400, code: 503, message: 'x' }).kind, 'unavailable')
  assert.equal(describeLoginFailure({ status: 400, code: 410, message: 'x' }).kind, 'invalid_credentials')
  assert.equal(describeLoginFailure({ status: 403, message: 'x' }).kind, 'inactive')
  assert.equal(describeLoginFailure({ status: 500 }).kind, 'unavailable')
})

test('anything unexpected still produces a readable message', () => {
  const failure = describeLoginFailure('boom')
  assert.equal(failure.kind, 'unknown')
  assert.ok(failure.title.length > 0)
  assert.equal(failure.canRetry, true)
})
