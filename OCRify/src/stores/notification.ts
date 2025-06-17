import { defineStore } from 'pinia'
import { ref } from 'vue'
import { useToast } from '@/components/ui/toast/use-toast'
import { signalRService } from '@/services/signalr'

export const useNotificationStore = defineStore('notification', () => {
  const { toast } = useToast()
  const isConnected = ref(false)

  // Initialize SignalR connection
  const initializeSignalR = async () => {
    console.log('🔄 [NotificationStore] Initializing SignalR connection...')
    try {
      await signalRService.start()
      isConnected.value = signalRService.isConnectionActive()
      console.log('✅ [NotificationStore] SignalR connection status:', isConnected.value)

      // Set up notification handler
      console.log('📝 [NotificationStore] Setting up notification handler')
      signalRService.onNotification((message: string) => {
        console.log('📬 [NotificationStore] Received notification:', message)
        try {
          showNotification(message)
          console.log('✅ [NotificationStore] Notification displayed successfully')
        } catch (error) {
          console.error('❌ [NotificationStore] Error displaying notification:', error)
        }
      })
    } catch (error) {
      console.error('❌ [NotificationStore] Failed to initialize SignalR:', error)
    }
  }

  // Show notification using toast
  const showNotification = (message: string) => {
    console.log('🎯 [NotificationStore] Showing notification:', message)
    try {
      toast({
        title: 'Notification',
        description: message,
        duration: 5000,
        variant: 'default',
      })
      console.log('✅ [NotificationStore] Toast notification created')
    } catch (error) {
      console.error('❌ [NotificationStore] Error creating toast notification:', error)
    }
  }

  // Cleanup SignalR connection
  const cleanup = async () => {
    console.log('🧹 [NotificationStore] Cleaning up SignalR connection')
    await signalRService.stop()
    isConnected.value = false
  }

  return {
    isConnected,
    initializeSignalR,
    showNotification,
    cleanup
  }
}) 