import { today } from '@internationalized/date'
import type { DateValue } from '@internationalized/date'

export const DATE_LOCALE = 'en-US-u-ca-gregory-nu-latn'
export const WEEKDAY_SHORT_LABELS: Record<string, string> = {
  Sunday: 'Sun',
  Monday: 'Mon',
  Tuesday: 'Tue',
  Wednesday: 'Wed',
  Thursday: 'Thu',
  Friday: 'Fri',
  Saturday: 'Sat'
}

export const localNumber = (value: number | string) => String(value).replace(/[۰-۹]/g, digit => '0123456789'['۰۱۲۳۴۵۶۷۸۹'.indexOf(digit)]!)
export const gregorianToday = () => today('Asia/Kabul')

export const formatMonth = (date: DateValue) => new Intl.DateTimeFormat(DATE_LOCALE, {
  month: 'long',
  timeZone: 'Asia/Kabul'
}).format(date.toDate('Asia/Kabul'))

export const formatDate = (date: DateValue, weekday = false) => {
  const options: Intl.DateTimeFormatOptions = {
    year: 'numeric',
    month: 'numeric',
    day: 'numeric',
    ...(weekday ? { weekday: 'long' as const } : {}),
    timeZone: 'Asia/Kabul'
  }
  const parts = new Intl.DateTimeFormat(DATE_LOCALE, options).formatToParts(date.toDate('Asia/Kabul'))
  const part = (type: Intl.DateTimeFormatPartTypes) => parts.find(value => value.type === type)?.value ?? ''
  const dateText = `${localNumber(part('year'))}/${localNumber(part('month'))}/${localNumber(part('day'))}`
  return weekday ? `${part('weekday')}, ${dateText}` : dateText
}

export const formatDateTime = (utc: string) => {
  const date = new Date(utc)
  if (Number.isNaN(date.getTime())) return ''
  const parts = new Intl.DateTimeFormat(DATE_LOCALE, {
    year: 'numeric',
    month: 'numeric',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
    timeZone: 'Asia/Kabul'
  }).formatToParts(date)
  const part = (type: Intl.DateTimeFormatPartTypes) => parts.find(value => value.type === type)?.value ?? ''
  const dateText = `${localNumber(part('year'))}/${localNumber(part('month'))}/${localNumber(part('day'))}`
  return `${dateText}, ${localNumber(part('hour'))}:${localNumber(part('minute'))}`
}

export const formatTime = (date: Date) => new Intl.DateTimeFormat(DATE_LOCALE, {
  hour: '2-digit',
  minute: '2-digit',
  hour12: false,
  timeZone: 'Asia/Kabul'
}).format(date)
