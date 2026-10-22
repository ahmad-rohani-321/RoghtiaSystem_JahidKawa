<script setup lang="ts">
const a5 = defineModel('a5', { default: false })
const grayscale = defineModel('grayscale', { default: false })
</script>

<template>
  <div class="prescription-print-options" role="group" aria-label="Prescription print options">
    <UCheckbox v-model="a5" label="Print on A5 paper" />
    <UCheckbox v-model="grayscale" label="Print in grayscale (black and white)" />
  </div>
</template>

<style scoped>
.prescription-print-options { display: flex; align-items: center; flex-wrap: wrap; gap: 10px 20px; min-width: 0; padding: 10px 14px; border: 1px solid var(--line, #d7e5e1); border-radius: 12px; background: var(--surface, #fff); }
@media (max-width: 640px) { .prescription-print-options { align-items: flex-start; flex-direction: column; gap: 10px; } }
</style>
