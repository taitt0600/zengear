import { adminRoutes } from '@/features/identity/access'

/** SCR-STF-02 New staff account, which "Thêm nhân viên" opens. */
export const newStaffPath = `${adminRoutes.staff}/new`

/** SCR-STF-03 of one account, which its name in the list opens. */
export const staffPath = (id: string) => `${adminRoutes.staff}/${id}`
