<script setup lang="ts">
defineProps<{
  heading: string
}>()
</script>

<template>
  <div class="overflow-hidden p-1">
    <p class="px-2 py-1.5 text-xs font-medium text-muted-foreground">
      {{ heading }}
    </p>
    <slot />
  </div>
</template> 