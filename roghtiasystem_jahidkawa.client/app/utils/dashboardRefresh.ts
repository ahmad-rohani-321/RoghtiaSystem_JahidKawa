export function notifyDashboardDataChanged() {
  if (import.meta.client) window.dispatchEvent(new Event('roghtia:data-changed'))
}
