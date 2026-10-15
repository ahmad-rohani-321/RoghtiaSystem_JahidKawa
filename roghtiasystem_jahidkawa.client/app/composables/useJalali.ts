import { today, toCalendar, PersianCalendar } from '@internationalized/date'
import type { DateValue } from '@internationalized/date'
export const JALALI_LOCALE = 'en-US-u-ca-persian-nu-latn'
export const JALALI_WEEKDAY_LABELS: Record<string, string> = {
  'Saturday': 'Sat',
  'Sunday': 'Sun',
  'Monday': 'Mon',
  'Tuesday': 'Tue',
  'Wednesday': 'Wed',
  'Thursday': 'Thu',
  'Friday': 'Fri'
}
// English transliterations of Afghan solar calendar months.
export const JALALI_MONTH_NAMES = [
  'Hamal', 'Sawr', 'Jawza', 'Saratan', 'Asad', 'Sunbula',
  'Mizan', 'Aqrab', 'Qaws', 'Jadi', 'Dalwa', 'Hut'
] as const
type JalaliDate = DateValue
export const localNumber = (value: number | string) => String(value).replace(/[۰-۹]/g, digit => '0123456789'['۰۱۲۳۴۵۶۷۸۹'.indexOf(digit)]!)
export const jalaliToday = () => toCalendar(today('Asia/Kabul'), new PersianCalendar())
export const formatJalaliMonth = (date: JalaliDate) => JALALI_MONTH_NAMES[toCalendar(date, new PersianCalendar()).month - 1]!
export const formatJalali = (date: JalaliDate, weekday = false) => {
  const parts = new Intl.DateTimeFormat(JALALI_LOCALE, { year: 'numeric', month: 'long', day: 'numeric', ...(weekday ? { weekday: 'long' as const } : {}), timeZone: 'Asia/Kabul' }).formatToParts(date.toDate('Asia/Kabul'))
  const part = (type: Intl.DateTimeFormatPartTypes) => parts.find(value => value.type === type)?.value ?? ''
  // Keep the shared display compact and omit Intl's Latin "AP" era marker.
  return `${weekday ? `${part('weekday')}, ` : ''}${part('day')} ${formatJalaliMonth(date)} ${part('year')}`
}
export const formatJalaliDateTime = (utc: string) => {
  const date = new Date(utc)
  if (Number.isNaN(date.getTime())) return ''
  const parts = new Intl.DateTimeFormat(JALALI_LOCALE, { year: 'numeric', month: 'numeric', day: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false, timeZone: 'Asia/Kabul' }).formatToParts(date)
  const part = (type: Intl.DateTimeFormatPartTypes) => parts.find(value => value.type === type)?.value ?? ''
  return `${localNumber(part('day'))} ${JALALI_MONTH_NAMES[Number(part('month')) - 1]} ${localNumber(part('year'))}, ${localNumber(part('hour'))}:${localNumber(part('minute'))}`
}
