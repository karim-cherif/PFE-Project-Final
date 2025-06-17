<script setup lang="ts">
import { cn } from '@/lib/utils'

defineProps<{
  class?: string
}>()

defineEmits<{
  select: []
}>()
</script>

<template>
  <div
    :class="cn(
      'relative flex cursor-default select-none items-center rounded-sm px-2 py-1.5 text-sm outline-none hover:bg-accent hover:text-accent-foreground data-[disabled]:pointer-events-none data-[disabled]:opacity-50',
      $props.class
    )"
    @click="$emit('select')"
  >
    <slot />
  </div>
</template> 