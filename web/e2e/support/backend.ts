// Set-up of the screen flows through the API and the Firebase Auth Emulator, on the development environment
// (docs/ACCEPTANCE.md) or the acceptance environment of CI. Test values only: the emulator accepts any API key, and
// its admin endpoints take the token "owner"; nothing here is a real credential.
const API = process.env.E2E_API_BASE_URL ?? 'http://localhost:5256'
const EMULATOR = `http://${process.env.E2E_FIREBASE_AUTH_EMULATOR_HOST ?? '127.0.0.1:9099'}`
const PROJECT = process.env.E2E_FIREBASE_PROJECT_ID ?? 'demo-zengear'

/** The emulator password of the Admin and the fixture accounts: dev-reset-only after tools/dev_reset.ps1, ci-only in CI. */
export const PASSWORD = process.env.E2E_PASSWORD ?? 'dev-reset-only'

export const accounts = {
  admin: 'owner@zengear.test',
  manager: 'manager.fixture@zengear.test',
  customer: 'customer.fixture@zengear.test',
}

// The fixed id of the role Sales Consultant (api/seed/01_roles.sql), which holds no permission by default.
const SALES_CONSULTANT_ROLE = '0199a000-0000-7000-8000-000000000003'

async function post(url: string, body: unknown, headers: Record<string, string> = {}) {
  const response = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...headers },
    body: JSON.stringify(body),
  })
  const text = await response.text()
  if (!response.ok) throw new Error(`POST ${new URL(url).pathname}: ${response.status} ${text}`)
  return text ? JSON.parse(text) : undefined
}

const emulator = (method: string, body: unknown) =>
  post(`${EMULATOR}/identitytoolkit.googleapis.com/v1/${method}?key=e2e`, body, {
    Authorization: 'Bearer owner',
  })

/** An ID token of an emulator account. */
export async function idToken(email: string, password: string): Promise<string> {
  const result = await emulator('accounts:signInWithPassword', {
    email,
    password,
    returnSecureToken: true,
  })
  return result.idToken
}

const api = async (token: string, path: string, body: unknown) =>
  post(`${API}${path}`, body, { Authorization: `Bearer ${token}` })

/** Today in Vietnam time, as the API's dates (yyyy-MM-dd). */
function today(): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date())
}

/**
 * A new Sales Consultant created by the Admin (STF-01), with a password set through the emulator: the API still holds
 * must_change_password = true, as for any new staff account. Unique per run, so the flows need no reset.
 */
export async function newSalesConsultant(password: string): Promise<{ id: string; email: string }> {
  const stamp = `${Date.now()}${Math.floor(Math.random() * 1000)}`
  const email = `e2e.${stamp}@zengear.test`
  const admin = await idToken(accounts.admin, PASSWORD)
  const staff = await api(admin, '/api/v1/admin/staff', {
    email,
    name: `E2E ${stamp}`,
    employeeCode: `E2E${stamp}`,
    joinedDate: today(),
    workPhone: null,
    roleIds: [SALES_CONSULTANT_ROLE],
  })
  const lookup = await emulator(`projects/${PROJECT}/accounts:lookup`, { email: [email] })
  await emulator(`projects/${PROJECT}/accounts:update`, {
    localId: lookup.users[0].localId,
    password,
  })
  return { id: staff.id, email }
}

/** The required password change of SCR-AUT-02, through the API (DEC-094); it clears must_change_password. */
export async function changePassword(email: string, current: string, next: string) {
  await api(await idToken(email, current), '/api/v1/admin/me/change-password', {
    newPassword: next,
  })
}

/** Renames an account as its owner (PATCH /me with the ETag of GET /me), as another tab or device would. */
export async function renameMe(email: string, password: string, name: string) {
  const headers = { Authorization: `Bearer ${await idToken(email, password)}` }
  const me = await fetch(`${API}/api/v1/admin/me`, { headers })
  const etag = me.headers.get('etag')
  if (!me.ok || !etag) throw new Error(`GET /api/v1/admin/me: ${me.status} ${await me.text()}`)
  const response = await fetch(`${API}/api/v1/admin/me`, {
    method: 'PATCH',
    headers: { ...headers, 'Content-Type': 'application/json', 'If-Match': etag },
    body: JSON.stringify({ name }),
  })
  if (!response.ok) {
    throw new Error(`PATCH /api/v1/admin/me: ${response.status} ${await response.text()}`)
  }
}

/** Disables a staff account as the Admin (STF-02), with today as the last working day. */
export async function disableStaff(id: string) {
  const admin = await idToken(accounts.admin, PASSWORD)
  await api(admin, `/api/v1/admin/staff/${id}/disable`, { leftDate: today() })
}
