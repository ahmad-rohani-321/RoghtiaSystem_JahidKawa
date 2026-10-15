<script setup lang="ts">
useHead({ title: 'Treatments — Roghtia' })
const route = useRoute()
</script>
<template>
  <NuxtPage v-if="route.path !== '/treatments'" />
  <TreatmentListing v-else />
</template>
