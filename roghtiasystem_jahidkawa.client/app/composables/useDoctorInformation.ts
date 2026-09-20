import type { DoctorInformation } from '#shared/types/doctor-information'

export function useDoctorInformation() {
  const auth = useAuth()
  const requestFetch = useRequestFetch()
  const stored = useState<DoctorInformation | null>('doctor.information', () => null)
  const information = computed(() => stored.value?.loggedInUserId === auth.user.value?.id ? stored.value : null)

  function accept(value: DoctorInformation) {
    // A request finishing after logout must never populate another user's profile.
    if (value.loggedInUserId === auth.user.value?.id) stored.value = value
  }

  async function load(force = false) {
    if (!auth.user.value) { stored.value = null; return null }
    if (!force && information.value) return information.value
    const value = await requestFetch<DoctorInformation>('/api/doctor-information', { retry: 0 })
    accept(value)
    return information.value
  }

  async function save(body: FormData) {
    const value = await $fetch<DoctorInformation>('/api/doctor-information', {
      method: information.value?.exists ? 'PUT' : 'POST', body,
      headers: { 'x-roghtia-request': '1' }, retry: 0
    })
    accept(value)
    return value
  }

  async function reset() {
    const value = await $fetch<DoctorInformation>('/api/doctor-information/reset', {
      method: 'POST', headers: { 'x-roghtia-request': '1' }, retry: 0
    })
    accept(value)
    return value
  }

  return { information, load, save, reset }
}
