<script setup>
import { fa_ir } from '@nuxt/ui/locale'
const locale = { ...fa_ir, code: 'fa-IR-u-nu-latn' }
</script>
<template><UApp :locale="locale"><NuxtLayout><NuxtPage /></NuxtLayout></UApp></template>
