<script setup>
defineProps(['label', 'value', 'icon', 'tone', 'unit'])
</script>
<template><article class="stat-card panel"><div class="stat-top"><span>{{ label }}</span><span class="stat-icon" :class="tone"><UIcon :name="icon" /></span></div><div class="stat-value">{{ localNumber(value) }}<span class="stat-unit">{{ unit || '' }}</span></div><div class="stat-bottom"><span><small>Updated from your clinic records</small></span></div></article></template>


