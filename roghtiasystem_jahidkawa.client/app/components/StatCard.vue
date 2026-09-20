<script setup>
defineProps(['label', 'value', 'change', 'icon', 'tone', 'points', 'unit'])
</script>
<template><article class="stat-card panel"><div class="stat-top"><span>{{ label }}</span><span class="stat-icon" :class="tone"><UIcon :name="icon" /></span></div><div class="stat-value">{{ localNumber(value) }}<span class="stat-unit">{{ unit || 'تنه' }}</span></div><div class="stat-bottom"><span><b>↗ {{ localNumber(change) }}٪</b> <small>د تېرې مودې په پرتله</small></span><svg viewBox="0 0 105 32" aria-hidden="true"><polyline :points="points" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" /></svg></div></article></template>


