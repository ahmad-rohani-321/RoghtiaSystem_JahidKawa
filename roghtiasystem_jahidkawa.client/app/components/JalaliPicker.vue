<script setup>
const date = defineModel()
const open = ref(false)
</script>
<template>
  <UPopover v-model:open="open" :content="{ collisionPadding: 12 }">
    <UButton color="neutral" variant="outline" icon="i-lucide-calendar-days" size="sm" class="date-button">
      <span class="date-label">{{ formatJalali(date) }}</span><UIcon name="i-lucide-chevron-down" />
    </UButton>
    <template #content>
      <UCalendar
        v-model="date"
        :locale="JALALI_LOCALE"
        :week-starts-on="6"
        :fixed-weeks="false"
        prevent-deselect
        weekday-format="long"
        size="sm"
        class="w-72 max-w-[calc(100vw-2rem)] p-2.5"
        :ui="{ body: 'pt-2', cellTrigger: 'text-sm', headCell: 'py-1 text-xs' }"
        :prev-month="{ 'aria-label': 'تېره میاشت' }"
        :next-month="{ 'aria-label': 'راتلونکې میاشت' }"
        :prev-year="{ 'aria-label': 'تېر کال' }"
        :next-year="{ 'aria-label': 'راتلونکی کال' }"
        @update:model-value="open = false"
      >
        <template #heading="{ date: visibleDate, view, value, setView }">
          <UButton color="neutral" variant="ghost" size="sm" block @click="setView(view === 'day' ? 'month' : view === 'month' ? 'year' : 'day')">
            {{ view === 'day' ? `${formatJalaliMonth(visibleDate)} ${localNumber(visibleDate.year)}` : view === 'month' ? localNumber(visibleDate.year) : localNumber(value.replaceAll('AP', '').trim()) }}
          </UButton>
        </template>
        <template #week-day="{ day }"><span :aria-label="day" :title="day">{{ JALALI_WEEKDAY_LABELS[day] || day }}</span></template>
        <template #day="{ day }">{{ localNumber(day.day) }}</template>
        <template #month-cell="{ month }">{{ formatJalaliMonth(month) }}</template>
        <template #year-cell="{ year }">{{ localNumber(year.year) }}</template>
      </UCalendar>
    </template>
  </UPopover>
</template>
