<script setup lang="ts">
const props = defineProps<{ name: string, label: string, current?: string | null, disabled?: boolean, portrait?: boolean }>()
const file = defineModel<File | null>({ default: null })
const removed = defineModel<boolean>('removed', { default: false })
const localError = ref('')
const objectUrl = ref('')
const imageFailed = ref(false)
const errorId = useId()
const preview = computed(() => objectUrl.value || (!removed.value ? props.current : null))

function releasePreview() {
  if (objectUrl.value) URL.revokeObjectURL(objectUrl.value)
  objectUrl.value = ''
}

function select(value: File | null | undefined) {
  if (props.disabled) return
  localError.value = ''
  if (!value) return
  if (!['image/png', 'image/jpeg', 'image/webp'].includes(value.type)) {
    localError.value = 'Only PNG, JPEG and WebP images are accepted.'
    return
  }
  if (value.size > 2 * 1024 * 1024 || value.size === 0) {
    localError.value = 'Image must not be empty or larger than 2 MB.'
    return
  }
  file.value = value
  removed.value = false
}

function remove() {
  file.value = null
  removed.value = !!props.current
  localError.value = ''
}

function undo() {
  file.value = null
  removed.value = false
  localError.value = ''
}

watch(file, (value) => {
  releasePreview()
  if (value && import.meta.client) objectUrl.value = URL.createObjectURL(value)
}, { immediate: true })
watch(preview, () => { imageFailed.value = false })
onBeforeUnmount(releasePreview)
</script>

<template>
  <UFormField :label="label" :name="name" :error="localError || undefined" size="lg" class="settings-image-field" :ui="{ label: 'text-base' }">
    <div class="image-editor">
      <div class="image-preview" :class="{ portrait }">
        <img v-if="preview && !imageFailed" :src="preview" :alt="label" @error="imageFailed = true">
        <div v-else class="image-placeholder">
          <UIcon :name="portrait ? 'i-lucide-user-round' : 'i-lucide-image'" aria-hidden="true" />
          <span>{{ removed ? 'Image will be removed' : 'No image selected' }}</span>
        </div>
      </div>
      <p class="image-description">PNG, JPEG or WebP · up to 2 MB</p>
      <p v-if="file" class="image-filename" dir="auto">{{ file.name }}</p>
      <UFileUpload :model-value="file" :name="name" :disabled="disabled" accept="image/png,image/jpeg,image/webp" :multiple="false" :preview="false" :dropzone="false" class="w-full min-w-0" reset @update:model-value="select">
        <template #default="{ open: chooseFile }">
          <UButton type="button" color="neutral" variant="outline" size="lg" block icon="i-lucide-upload" :disabled="disabled" :aria-label="`${label}: Choose image`" :aria-invalid="!!localError" :aria-describedby="localError ? errorId : undefined" @click="chooseFile()">Choose image</UButton>
        </template>
      </UFileUpload>
      <span :id="errorId" class="sr-only" role="status" aria-live="polite">{{ localError }}</span>
      <div v-if="preview || file || removed" class="image-actions">
        <UButton v-if="preview" type="button" color="neutral" variant="ghost" size="sm" icon="i-lucide-trash-2" :disabled="disabled" :aria-label="`${label}: Remove image`" @click="remove">Remove</UButton>
        <UButton v-if="file || removed" type="button" color="neutral" variant="ghost" size="sm" icon="i-lucide-undo-2" :disabled="disabled" @click="undo">Restore</UButton>
      </div>
    </div>
  </UFormField>
</template>

<style scoped>
.settings-image-field { min-width: 0; }
.image-editor { min-width: 0; padding: 18px; border: 1px solid var(--line); border-radius: 12px; background: var(--canvas); }
.image-preview { width: 100%; height: 140px; display: grid; place-items: center; overflow: hidden; border: 1px solid var(--line); border-radius: 9px; background: var(--surface); }
.image-preview img { width: 100%; height: 100%; object-fit: contain; padding: 12px; }
.image-preview.portrait img { padding: 0; object-fit: cover; }
.image-placeholder { min-width: 0; padding: 10px; display: flex; flex-direction: column; align-items: center; gap: 9px; color: var(--muted); font-size: 0.875rem; text-align: center; }
.image-placeholder .iconify { width: 30px; height: 30px; color: var(--teal); }
.image-description { color: var(--muted); font-size: 0.875rem; margin: 13px 0; line-height: 1.9; }
.image-filename { overflow-wrap: anywhere; color: var(--ink); font-size: 0.875rem; margin: 0 0 12px; }
.image-actions { display: flex; flex-wrap: wrap; justify-content: center; gap: 6px; margin-top: 8px; }
.image-editor :deep(button) { min-width: 0; max-width: 100%; min-height: 44px; white-space: normal; }
@media (max-width: 480px) { .image-editor { padding: 14px; } }
</style>
