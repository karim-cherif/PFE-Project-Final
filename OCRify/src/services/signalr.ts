import * as signalR from '@microsoft/signalr'
import { useToast } from '@/components/ui/toast/use-toast'

class SignalRService {
  private connection: signalR.HubConnection | null = null
  private isConnected = false

  constructor() {
    console.log('🔌 [SignalR] Initializing connection...')
    const apiUrl = import.meta.env.VITE_API_URL
    console.log('🌐 [SignalR] API URL:', apiUrl)
    
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(`${apiUrl}/notificationHub`, {
        accessTokenFactory: () => {
          const token = localStorage.getItem('token') || ''
          console.log('🔑 [SignalR] Using token:', token ? 'Token exists' : 'No token found')
          return token
        },
        skipNegotiation: true,
        transport: signalR.HttpTransportType.WebSockets
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 20000]) // Retry with increasing delays
      .configureLogging(signalR.LogLevel.Debug)
      .build()

    this.setupConnectionHandlers()
  }

  private setupConnectionHandlers() {
    if (!this.connection) return

    this.connection.onreconnecting((error) => {
      console.log('🔄 [SignalR] Reconnecting to SignalR...', error)
    })

    this.connection.onreconnected((connectionId) => {
      console.log('✅ [SignalR] Reconnected to SignalR with connection ID:', connectionId)
      this.isConnected = true
    })

    this.connection.onclose((error) => {
      console.log('❌ [SignalR] Connection closed:', error)
      this.isConnected = false
    })

    // Add error handler
    this.connection.onreconnecting((error) => {
      console.error('⚠️ [SignalR] Connection error:', error)
    })

    // Log all available methods on the connection
    console.log('🔍 [SignalR] Available methods on connection:', Object.keys(this.connection))
  }

  public async start() {
    if (!this.connection) return

    try {
      console.log('🚀 [SignalR] Starting connection...')
      await this.connection.start()
      this.isConnected = true
      console.log('✅ [SignalR] Connected successfully')
    } catch (err) {
      console.error('❌ [SignalR] Error starting connection:', err)
      this.isConnected = false
    }
  }

  public async stop() {
    if (!this.connection) return

    try {
      await this.connection.stop()
      this.isConnected = false
      console.log('🛑 [SignalR] Disconnected')
    } catch (err) {
      console.error('❌ [SignalR] Error stopping connection:', err)
    }
  }

  public onNotification(callback: (message: string) => void) {
    if (!this.connection) {
      console.error('❌ [SignalR] Cannot register notification handler - no connection')
      return
    }

    console.log('📝 [SignalR] Registering notification handler')
    
    // Remove any existing handlers first
    this.connection.off('ReceiveNotification')
    
    // Add the new handler
    this.connection.on('ReceiveNotification', (message: string) => {
      console.log('📬 [SignalR] Received notification:', message)
      try {
        callback(message)
        console.log('✅ [SignalR] Notification callback executed successfully')
      } catch (error) {
        console.error('❌ [SignalR] Error in notification callback:', error)
      }
    })

    // Add error handler for the notification event
    this.connection.on('Error', (error: any) => {
      console.error('❌ [SignalR] Error in notification event:', error)
    })

    // Log all available methods on the connection
    console.log('🔍 [SignalR] Available methods on connection:', Object.keys(this.connection))
    console.log('🔍 [SignalR] Connection state:', this.connection.state)
    console.log('🔍 [SignalR] Connection ID:', this.connection.connectionId)

    // Verify the handler was registered by checking if the connection is active
    if (this.isConnectionActive()) {
      console.log('✅ [SignalR] Notification handler registered successfully')
    } else {
      console.error('❌ [SignalR] Failed to register notification handler - connection not active')
    }
  }

  public isConnectionActive(): boolean {
    const state = this.connection?.state
    console.log('🔍 [SignalR] Current connection state:', state)
    return this.isConnected && state === signalR.HubConnectionState.Connected
  }
}

// Create a singleton instance
export const signalRService = new SignalRService() 