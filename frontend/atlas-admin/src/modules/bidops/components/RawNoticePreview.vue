<script setup lang="ts">
withDefaults(defineProps<{
  text?: string | null
  variant?: 'document' | 'extracted'
}>(), {
  variant: 'document',
})
</script>

<template>
  <article class="raw-notice-preview" :class="`raw-notice-preview--${variant}`">{{ text || '-' }}</article>
</template>

<style scoped>
.raw-notice-preview {
  min-height: 220px;
  max-height: 560px;
  overflow: auto;
  padding: 14px;
  margin: 0;
  border: 1px solid #dce3ee;
  border-radius: 8px;
  background: #fff;
  color: #273444;
  font-size: 13px;
  line-height: 1.75;
  white-space: pre-wrap;
  word-break: break-word;
}

.raw-notice-preview--document {
  font-family: inherit;
}

.raw-notice-preview--extracted {
  background: #f8fafc;
  font-family: "Cascadia Mono", Consolas, monospace;
  line-height: 1.6;
}
</style>
