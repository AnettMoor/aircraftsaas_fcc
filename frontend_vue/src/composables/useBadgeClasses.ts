const badgeMap: Record<string, string> = {
  approved: 'bg-emerald-100 text-emerald-800',
  available: 'bg-emerald-100 text-emerald-800',
  success: 'bg-emerald-100 text-emerald-800',
  valid: 'bg-emerald-100 text-emerald-800',
  ok: 'bg-emerald-100 text-emerald-800',
  pending: 'bg-amber-100 text-amber-800',
  requested: 'bg-amber-100 text-amber-800',
  warning: 'bg-amber-100 text-amber-800',
  cancelled: 'bg-red-100 text-red-800',
  rejected: 'bg-red-100 text-red-800',
  expired: 'bg-red-100 text-red-800',
  danger: 'bg-red-100 text-red-800',
  unavailable: 'bg-red-100 text-red-800',
  off: 'bg-red-100 text-red-800',
  paid: 'bg-blue-100 text-blue-800',
  completed: 'bg-blue-100 text-blue-800',
  maintenance: 'bg-blue-100 text-blue-800',
  info: 'bg-blue-100 text-blue-800',
  insurance: 'bg-amber-100 text-amber-800',
}

const base = 'inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium whitespace-nowrap'

export function badgeClasses(status: string): string {
  return `${base} ${badgeMap[status.toLowerCase()] || 'bg-slate-100 text-slate-600'}`
}
