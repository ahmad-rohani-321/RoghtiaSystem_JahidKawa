<script setup lang="ts">
const route = useRoute()
const id = Number(route.params.id)
if (!Number.isInteger(id) || id < 1 || id > 2147483647) throw createError({ statusCode: 404 })
</script>
<template><TreatmentEditor :treatment-id="id" /></template>
