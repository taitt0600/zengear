// Set-up of the screen flows through the API and the Firebase Auth Emulator, on the development environment
// (docs/ACCEPTANCE.md) or the acceptance environment of CI. Test values only: the emulator accepts any API key, and
// its admin endpoints take the token "owner"; nothing here is a real credential.
const API = process.env.E2E_API_BASE_URL ?? 'http://localhost:5256'
const EMULATOR = `http://${process.env.E2E_FIREBASE_AUTH_EMULATOR_HOST ?? '127.0.0.1:9099'}`
const PROJECT = process.env.E2E_FIREBASE_PROJECT_ID ?? 'demo-zengear'

/** The emulator password of the Admin and the fixture accounts: dev-reset-only after tools/dev_reset.ps1, ci-only in CI. */
export const PASSWORD = process.env.E2E_PASSWORD ?? 'dev-reset-only'

export const accounts = {
  admin: 'khoatmzg260001@zengear.test',
  manager: 'haltzg260002@zengear.test',
  customer: 'dothimai@zengear.test',
  itSupport: 'baopqzg260003@zengear.test',
}

// The fixed ids of the roles Sales Consultant and Cashier (api/seed/01_roles.sql), which hold no permission by default.
const SALES_CONSULTANT_ROLE = '0199a000-0000-7000-8000-000000000003'
const CASHIER_ROLE = '0199a000-0000-7000-8000-000000000005'

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
async function idToken(email: string, password: string): Promise<string> {
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
 * A new Cashier created by the IT Support of the fixtures, who holds no staff.approve: the API creates it PENDING, with no
 * Firebase account, until another person approves it (STF-09, R-USR-17). Each flow makes its own, so a rerun needs no
 * reset.
 */
export async function newPendingAccount(name: string): Promise<{ id: string; email: string }> {
  const itSupport = await idToken(accounts.itSupport, PASSWORD)
  const staff = await api(itSupport, '/api/v1/admin/staff', {
    name,
    joinedDate: today(),
    workPhone: null,
    roleIds: [CASHIER_ROLE],
  })
  if (staff.status !== 'PENDING') throw new Error(`The new account is ${staff.status}, not PENDING`)
  return { id: staff.id, email: staff.email }
}

/** The name newSalesConsultant gives when it is given none. */
export const STAFF_NAME = 'Nguyễn Văn Kiểm'

/**
 * A new Sales Consultant created by the Admin (STF-01), with a password set through the emulator: the API still holds
 * must_change_password = true, as for any new staff account. The API generates the employee code, the username and the
 * email from the name (R-USR-24), so every run gets a new account without a reset.
 */
export async function newSalesConsultant(
  password: string,
  name = STAFF_NAME,
): Promise<{ id: string; email: string }> {
  const admin = await idToken(accounts.admin, PASSWORD)
  const staff = await api(admin, '/api/v1/admin/staff', {
    name,
    joinedDate: today(),
    workPhone: null,
    roleIds: [SALES_CONSULTANT_ROLE],
  })
  const lookup = await emulator(`projects/${PROJECT}/accounts:lookup`, { email: [staff.email] })
  await emulator(`projects/${PROJECT}/accounts:update`, {
    localId: lookup.users[0].localId,
    password,
  })
  return { id: staff.id, email: staff.email }
}

/** The required password change of SCR-AUT-02, through the API (DEC-094); it clears must_change_password. */
export async function changePassword(email: string, current: string, next: string) {
  await api(await idToken(email, current), '/api/v1/admin/me/change-password', {
    newPassword: next,
  })
}

/** GET /api/v1/admin/me as the account, with the headers it was sent with. */
async function getMe(email: string, password: string) {
  const headers = { Authorization: `Bearer ${await idToken(email, password)}` }
  const me = await fetch(`${API}/api/v1/admin/me`, { headers })
  if (!me.ok) throw new Error(`GET /api/v1/admin/me: ${me.status} ${await me.text()}`)
  return { me, headers }
}

/** Renames an account as its owner (PATCH /me with the ETag of GET /me), as another tab or device would. */
export async function renameMe(email: string, password: string, name: string) {
  const { me, headers } = await getMe(email, password)
  const etag = me.headers.get('etag')
  if (!etag) throw new Error('GET /api/v1/admin/me: no ETag')
  const response = await fetch(`${API}/api/v1/admin/me`, {
    method: 'PATCH',
    headers: { ...headers, 'Content-Type': 'application/json', 'If-Match': etag },
    body: JSON.stringify({ name }),
  })
  if (!response.ok) {
    throw new Error(`PATCH /api/v1/admin/me: ${response.status} ${await response.text()}`)
  }
}

/**
 * The page of SCR-STF-01, at its 20 rows a page, that shows the account `email` with the filters and sort of
 * `query` (`status=DISABLED&sort=-employeeCode`), read from the API as the Admin. The development database keeps the
 * accounts of every earlier run (the Postman collection's included), so a flow cannot count on page 1.
 */
export async function staffListPage(email: string, query: string): Promise<number> {
  const headers = { Authorization: `Bearer ${await idToken(accounts.admin, PASSWORD)}` }
  for (let page = 1; ; page++) {
    const url = `${API}/api/v1/admin/staff?${query}&pageSize=100&page=${page}`
    const response = await fetch(url, { headers })
    if (!response.ok)
      throw new Error(`GET /api/v1/admin/staff: ${response.status} ${await response.text()}`)
    const { items } = (await response.json()) as { items: { email: string | null }[] }
    const index = items.findIndex((item) => item.email === email)
    if (index >= 0) return Math.floor(((page - 1) * 100 + index) / 20) + 1
    if (items.length < 100) throw new Error(`${email} is not in the staff list of ${query}`)
  }
}

/** The id of an account, from its own GET /api/v1/admin/me. */
export async function meId(email: string, password: string): Promise<string> {
  const { me } = await getMe(email, password)
  return ((await me.json()) as { id: string }).id
}

/** Disables a staff account as the Admin (STF-02), with today as the last working day. */
export async function disableStaff(id: string) {
  const admin = await idToken(accounts.admin, PASSWORD)
  await api(admin, `/api/v1/admin/staff/${id}/disable`, { leftDate: today() })
}
