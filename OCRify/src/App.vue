<script setup lang="ts">
import Button from '@/components/ui/Button.vue'
import Toast from '@/components/ui/toast/Toast.vue'
import { TooltipProvider } from 'reka-ui'
import CommandPalette from '@/components/CommandPalette.vue'
import KeyboardShortcutsHint from '@/components/KeyboardShortcutsHint.vue'
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRoute } from 'vue-router'
import { useNotificationStore } from '@/stores/notification'

const commandPaletteOpen = ref(false)
const route = useRoute()
const notificationStore = useNotificationStore()

// Hide keyboard shortcuts hint on landing, login, and signup pages
const showKeyboardShortcutsHint = computed(() => {
  const currentPath = route.path
  return !['/', '/sign-in', '/sign-up'].includes(currentPath)
})

// Initialize SignalR connection when app starts
onMounted(async () => {
  await notificationStore.initializeSignalR()
})

// Cleanup SignalR connection when app unmounts
onUnmounted(async () => {
  await notificationStore.cleanup()
})
</script>

<template>
  <TooltipProvider>
    <div class="min-h-screen bg-background">
      <!-- Router View -->
      <router-view></router-view>
      <Toast />
      <CommandPalette :open="commandPaletteOpen" @update:open="commandPaletteOpen = $event" />
      <KeyboardShortcutsHint v-if="showKeyboardShortcutsHint" @openCommandPalette="commandPaletteOpen = true" />
    </div>
  </TooltipProvider>
</template>
