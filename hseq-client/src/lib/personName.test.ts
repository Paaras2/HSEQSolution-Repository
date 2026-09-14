// نام نمایشی و حروف آواتار.
//
//     npm test

import test from 'node:test'
import assert from 'node:assert/strict'
import { displayNameOf, initialsOf } from './personName.ts'

test('first and last name are joined with single spaces', () => {
  assert.equal(displayNameOf({ firstName: ' مریم ', lastName: 'رضایی  احمدی' }), 'مریم رضایی احمدی')
})

test('a missing half still produces a name', () => {
  assert.equal(displayNameOf({ firstName: 'مریم' }), 'مریم')
  assert.equal(displayNameOf({ lastName: 'رضایی' }), 'رضایی')
})

test('no name at all falls back to null', () => {
  assert.equal(displayNameOf({}), null)
  assert.equal(displayNameOf({ firstName: '  ', lastName: null }), null)
  assert.equal(initialsOf({}), null)
})

test('initials are separated so Persian letters do not join', () => {
  assert.equal(initialsOf({ firstName: 'مریم', lastName: 'رضایی' }), 'م\u200Cر')
  assert.equal(initialsOf({ firstName: 'Maryam' }), 'M')
})
