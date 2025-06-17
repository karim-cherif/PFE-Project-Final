<script setup lang="ts">
import { Dialog, DialogContent } from '@/components/ui/dialog'

defineProps<{
  open: boolean
}>()

defineEmits<{
  'update:open': [value: boolean]
}>()
</script>

<template>
  <Dialog :open="open" @update:open="$emit('update:open', $event)">
    <DialogContent class="overflow-hidden p-0">
      <slot />
    </DialogContent>
  </Dialog>
</template> 