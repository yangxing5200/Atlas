<script setup lang="ts">
defineProps<{
  title: string
  description?: string
}>()
</script>

<template>
  <section class="page-container">
    <header class="page-header">
      <div>
        <h1>{{ title }}</h1>
        <p v-if="description">{{ description }}</p>
      </div>
      <div class="page-actions">
        <slot name="actions" />
      </div>
    </header>
    <main>
      <slot />
    </main>
  </section>
</template>

<style scoped>
.page-container {
  min-width: 0;
}

.page-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
  margin-bottom: 16px;
}

.page-header h1 {
  margin: 0;
  color: #17202a;
  font-size: 22px;
  font-weight: 650;
  line-height: 1.3;
}

.page-header p {
  margin: 6px 0 0;
  color: #687385;
  font-size: 14px;
}

.page-actions {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: 8px;
}

@media (max-width: 720px) {
  .page-header {
    display: block;
  }

  .page-actions {
    justify-content: flex-start;
    margin-top: 12px;
  }
}
</style>
