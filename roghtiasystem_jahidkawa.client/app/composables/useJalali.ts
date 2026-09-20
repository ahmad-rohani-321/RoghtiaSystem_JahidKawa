import { today, toCalendar, PersianCalendar } from '@internationalized/date'
import type { DateValue } from '@internationalized/date'
export const JALALI_LOCALE = 'fa-IR-u-ca-persian-nu-latn'
export const JALALI_WEEKDAY_LABELS: Record<string, string> = {
  'شنبه': 'شنبه',
  'یکشنبه': 'یک',
  'دوشنبه': 'دو',
  'سه‌شنبه': 'سه',
  'چهارشنبه': 'چهار',
  'پنجشنبه': 'پنج',
  'جمعه': 'جمعه'
}
// Explicit labels keep the requested Pashto spelling identical in every browser.
export const JALALI_MONTH_NAMES = [
  'وری', 'غویی', 'غبرګولی', 'چنګاښ', 'زمری', 'وږی',
  'تله', 'لړم', 'لیندۍ', 'مرغومی', 'سلواغه', 'کب'
] as const
type JalaliDate = DateValue
export const localNumber = (value: number | string) => String(value).replace(/[۰-۹]/g, digit => '0123456789'['۰۱۲۳۴۵۶۷۸۹'.indexOf(digit)]!)
export const jalaliToday = () => toCalendar(today('Asia/Kabul'), new PersianCalendar())
export const formatJalaliMonth = (date: JalaliDate) => JALALI_MONTH_NAMES[toCalendar(date, new PersianCalendar()).month - 1]!
export const formatJalali = (date: JalaliDate, weekday = false) => {
  const parts = new Intl.DateTimeFormat(JALALI_LOCALE, { year: 'numeric', month: 'long', day: 'numeric', ...(weekday ? { weekday: 'long' as const } : {}), timeZone: 'Asia/Kabul' }).formatToParts(date.toDate('Asia/Kabul'))
  const part = (type: Intl.DateTimeFormatPartTypes) => parts.find(value => value.type === type)?.value ?? ''
  // Keep the shared display compact and omit Intl's Latin "AP" era marker.
  return `${weekday ? `${part('weekday')}، ` : ''}${part('day')} ${formatJalaliMonth(date)} ${part('year')}`
}
