<script setup lang="ts">
import { Command } from 'lucide-vue-next'
</script>

<template>
  <div class="fixed top-4 right-4 z-50">
    <button
      class="flex items-center gap-2 px-3 py-1.5 text-sm text-muted-foreground hover:text-foreground transition-colors rounded-md border bg-background/80 backdrop-blur-sm"
      @click="$emit('openCommandPalette')"
    >
      <Command class="h-4 w-4" />
      <span>Raccourcis clavier</span>
      <kbd class="pointer-events-none h-5 select-none items-center gap-1 rounded border bg-muted px-1.5 font-mono text-[10px] font-medium opacity-100">
        <span class="text-xs">⌘</span>K
      </kbd>
    </button>
  </div>
</template> 