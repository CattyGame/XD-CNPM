import { StrictMode } from 'react'
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

const mocks = vi.hoisted(() => ({ auth: vi.fn(), observe: vi.fn(), signOut: vi.fn() }))
vi.mock('../firebase/config', () => ({ getFirebaseAuth: mocks.auth }))
vi.mock('../api/client', () => ({ isMockEnabled: false }))
vi.mock('firebase/auth', () => ({ onAuthStateChanged: mocks.observe, signOut: mocks.signOut }))
import BackendSessionProvider from './BackendSessionProvider'
import ProtectedRoute from '../routes/ProtectedRoute'
import { useBackendSession } from './backendSession'

const accountId = '10000000-0000-0000-0000-000000000001'
const user = { uid: 'firebase-a', getIdToken: vi.fn() }
const auth = { currentUser: user }
const account = {
  accountId, firebaseUid: user.uid, roles: ['PATIENT'], accountStatus: 'ACTIVE', profileProvisioningStatus: 'READY',
}
const profile = {
  accountId, patientId: '20000000-0000-0000-0000-000000000002',
  personalTenantId: '30000000-0000-0000-0000-000000000003', displayName: 'Người A', profileVersion: 1,
}
const fetchMock = vi.fn<typeof fetch>()
function response(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
}
function Content() {
  const { state } = useBackendSession()
  return state.status === 'ready' ? <p>Đã xác nhận: {state.context.displayName}</p> : null
}
function app(strict = false) {
  const tree = <BackendSessionProvider><MemoryRouter>
    <ProtectedRoute><Content /></ProtectedRoute>
  </MemoryRouter></BackendSessionProvider>
  return render(strict ? <StrictMode>{tree}</StrictMode> : tree)
}
beforeEach(() => {
  vi.resetAllMocks()
  mocks.auth.mockReturnValue(auth)
  user.getIdToken.mockResolvedValue('fake-id-token')
  vi.stubGlobal('fetch', fetchMock)
  mocks.observe.mockImplementation((_auth, next) => {
    let active = true
    queueMicrotask(() => { if (active) next(user) })
    return () => { active = false }
  })
})
afterEach(() => { cleanup(); vi.unstubAllGlobals() })

describe('Session provider with the actual 8A helper and mocked HTTP', () => {
  it('requires READY bootstrap plus matching Profile before rendering children, also in StrictMode', async () => {
    fetchMock.mockResolvedValueOnce(response(account)).mockResolvedValueOnce(response(profile))
    app(true)
    expect(await screen.findByText('Đã xác nhận: Người A')).toBeTruthy()
    expect(fetchMock.mock.calls.map(([url]) => url)).toEqual([
      '/api/v1/identity/bootstrap', '/api/v1/profiles/me',
    ])
    for (const [, options] of fetchMock.mock.calls) {
      expect(options?.headers).toEqual({ Authorization: 'Bearer fake-id-token' })
    }
    expect(document.body.textContent).not.toContain('fake-id-token')
  })

  it('does not fetch Profile for PENDING and resumes only after the user requests retry', async () => {
    fetchMock.mockResolvedValueOnce(response({ ...account, profileProvisioningStatus: 'PENDING' }))
      .mockResolvedValueOnce(response(account)).mockResolvedValueOnce(response(profile))
    app()
    expect((await screen.findByRole('alert')).textContent).toContain('đang được khởi tạo')
    expect(fetchMock).toHaveBeenCalledTimes(1)
    expect(screen.queryByText('Đã xác nhận: Người A')).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: 'Thử lại xác nhận hồ sơ' }))
    expect(await screen.findByText('Đã xác nhận: Người A')).toBeTruthy()
    expect(fetchMock).toHaveBeenCalledTimes(3)
  })

  it('blocks an account/profile mismatch using the real schema validation', async () => {
    fetchMock.mockResolvedValueOnce(response(account)).mockResolvedValueOnce(response({
      ...profile, accountId: profile.patientId,
    }))
    app()
    await act(async () => { await Promise.resolve() })
    await waitFor(() => expect(screen.getByRole('alert').textContent).toContain('không hợp lệ'))
    expect(screen.queryByText('Đã xác nhận: Người A')).toBeNull()
    expect(fetchMock).toHaveBeenCalledTimes(2)
  })
})
