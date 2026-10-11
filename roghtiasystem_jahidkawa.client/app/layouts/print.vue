<template><main class="print-layout"><slot /></main></template>
<style scoped>
.print-layout { min-height: 100vh; padding: 24px; background: #e8eeeb; }
@media (max-width: 640px) { .print-layout { padding: 10px; } }
@media print { .print-layout { min-height: 0; padding: 0; background: #fff; } }
</style>
