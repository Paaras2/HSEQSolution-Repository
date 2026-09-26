import test from 'node:test'
import assert from 'node:assert/strict'
import {
  initialReservationState,
  reservationReducer,
  reservationFlags,
  isMetadataComplete,
  type ReservationState,
} from './documentReservation.ts'

const completeMetadata = {
  name: 'خط‌مشی',
  category: 'Headquarters' as const,
  isEnglishVersion: false,
  projectId: '',
  managementId: 'm1',
  activityId: 'a1',
  documentTypeId: 't1',
}

function draftReady(): ReservationState {
  return { ...initialReservationState, metadata: { ...completeMetadata } }
}

function reservedState(): ReservationState {
  return {
    ...draftReady(),
    status: 'reserved',
    reservedKey: 'k1',
    reservedCode: 'MGEFM-001-A',
    reservationExpiresAt: '2026-09-29T00:00:00Z',
  }
}

test('a headquarters metadata set is complete without a project', () => {
  assert.equal(isMetadataComplete(completeMetadata), true)
})

test('a project category is incomplete until a project is chosen', () => {
  const meta = { ...completeMetadata, category: 'Project' as const }
  assert.equal(isMetadataComplete(meta), false)
  assert.equal(isMetadataComplete({ ...meta, projectId: 'p1' }), true)
})

test('canReserve requires complete metadata and the draft state', () => {
  assert.equal(reservationFlags(initialReservationState).canReserve, false)
  assert.equal(reservationFlags(draftReady()).canReserve, true)
})

test('reserving then succeeding moves to reserved and keeps the code', () => {
  let s = reservationReducer(draftReady(), { type: 'RESERVE_STARTED' })
  assert.equal(s.status, 'reserving')
  s = reservationReducer(s, { type: 'RESERVE_SUCCEEDED', key: 'k1', code: 'MGEFM-001-A', expiresAt: null })
  assert.equal(s.status, 'reserved')
  assert.equal(s.reservedCode, 'MGEFM-001-A')
})

test('a reserve failure returns to draft with the metadata intact', () => {
  const started = reservationReducer(draftReady(), { type: 'RESERVE_STARTED' })
  const failed = reservationReducer(started, { type: 'RESERVE_FAILED', message: 'خطا' })
  assert.equal(failed.status, 'draft')
  assert.equal(failed.error, 'خطا')
  assert.equal(failed.metadata.name, 'خط‌مشی')
})

test('metadata is frozen once a code is reserved', () => {
  const s = reservationReducer(reservedState(), { type: 'FIELD_CHANGED', field: 'name', value: 'دیگر' })
  assert.equal(s.metadata.name, 'خط‌مشی') // unchanged
})

test('finalize needs a file; the flag and the guard both enforce it', () => {
  const noFile = reservedState()
  assert.equal(reservationFlags(noFile).canFinalize, false)
  // UPLOAD_STARTED is ignored without a file
  assert.equal(reservationReducer(noFile, { type: 'UPLOAD_STARTED' }).status, 'reserved')

  const withFile: ReservationState = { ...noFile, file: new File(['x'], 'a.pdf') }
  assert.equal(reservationFlags(withFile).canFinalize, true)
  assert.equal(reservationReducer(withFile, { type: 'UPLOAD_STARTED' }).status, 'uploading')
})

test('an upload failure keeps the reserved code and lets the user retry', () => {
  const uploading: ReservationState = {
    ...reservedState(),
    status: 'uploading',
    file: new File(['x'], 'a.pdf'),
  }
  const failed = reservationReducer(uploading, { type: 'UPLOAD_FAILED', message: 'شبکه' })
  assert.equal(failed.status, 'reserved')
  assert.equal(failed.reservedCode, 'MGEFM-001-A') // code preserved
  assert.equal(failed.error, 'شبکه')
})

test('RESET returns to the initial draft state', () => {
  assert.deepEqual(reservationReducer(reservedState(), { type: 'RESET' }), initialReservationState)
})
