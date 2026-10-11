<script setup lang="ts">
useHead({ title: 'تداوي — روغتیا' })
const route = useRoute()
</script>
<template>
  <NuxtPage v-if="route.path !== '/treatments'" />
  <TreatmentListing v-else />
</template>
