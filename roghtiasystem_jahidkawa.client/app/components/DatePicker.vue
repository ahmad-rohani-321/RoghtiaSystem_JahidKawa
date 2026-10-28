<script setup lang="ts">
import type { DateValue } from '@internationalized/date'

const date = defineModel<DateValue>({ required: true })
const open = ref(false)
</script>

<template>
  <UPopover v-model:open="open" :content="{ collisionPadding: 12 }">
    <UButton color="neutral" variant="outline" icon="i-lucide-calendar-days" size="sm" class="date-button">
      <span class="date-label">{{ formatDate(date) }}</span><UIcon name="i-lucide-chevron-down" />
    </UButton>
    <template #content>
      <UCalendar
        v-model="date"
        :locale="DATE_LOCALE"
        :week-starts-on="0"
        :fixed-weeks="false"
        prevent-deselect
        weekday-format="long"
        size="sm"
        class="w-72 max-w-[calc(100vw-2rem)] p-2.5"
        :ui="{ body: 'pt-2', cellTrigger: 'text-sm', headCell: 'py-1 text-xs' }"
        :prev-month="{ 'aria-label': 'Previous month' }"
        :next-month="{ 'aria-label': 'Next month' }"
        :prev-year="{ 'aria-label': 'Previous year' }"
        :next-year="{ 'aria-label': 'Next year' }"
        @update:model-value="open = false"
      >
        <template #heading="{ date: visibleDate, view, value, setView }">
          <UButton color="neutral" variant="ghost" size="sm" block @click="setView(view === 'day' ? 'month' : view === 'month' ? 'year' : 'day')">
            {{ view === 'day' ? `${formatMonth(visibleDate)} ${localNumber(visibleDate.year)}` : view === 'month' ? localNumber(visibleDate.year) : localNumber(value.trim()) }}
          </UButton>
        </template>
        <template #week-day="{ day }"><span :aria-label="day" :title="day">{{ WEEKDAY_SHORT_LABELS[day] || day }}</span></template>
        <template #day="{ day }">{{ localNumber(day.day) }}</template>
        <template #month-cell="{ month }">{{ formatMonth(month) }}</template>
        <template #year-cell="{ year }">{{ localNumber(year.year) }}</template>
      </UCalendar>
    </template>
  </UPopover>
</template>
