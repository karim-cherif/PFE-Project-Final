<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { AuthService } from '@/services/auth-service'
import { admin, documents, ocrStatistics, type AdminUpdateUserDto } from '@/services/api'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Checkbox } from '@/components/ui/checkbox'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { useToast } from '@/components/ui/toast/use-toast'
import {
  Users, UserPlus, UserMinus, Shield, Search, MoreHorizontal, Edit, Trash2, RotateCcw,
  FileText, Activity, BarChart3, Settings, Database, TrendingUp, Clock, AlertTriangle
} from 'lucide-vue-next'
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbList,
  BreadcrumbPage,
} from '@/components/ui/breadcrumb'
import { Separator } from '@/components/ui/separator'
import {
  SidebarInset,
  SidebarProvider,
  SidebarTrigger,
} from '@/components/ui/sidebar'
import SidebarLeft from '@/components/SidebarLeft.vue'

interface User {
  id: string
  firstName: string
  lastName: string
  email: string
  role: string
  createdAt: string
  updatedAt?: string
  isDeleted: boolean
  deletedAt?: string
}

interface UserSearchResult {
  items: User[]
  totalCount: number
  page: number
  pageSize: number
}

interface SystemStats {
  totalUsers: number
  totalDocuments: number
  totalProcessedDocuments: number
  totalStorage: number
  averageProcessingTime: string
  successRate: number
  errorRate: number
}

interface OcrStats {
  totalAttempts: number
  successfulAttempts: number
  failedAttempts: number
  averageConfidence: number
  processingTime: number
}

interface ActivityLogItem {
  id: string
  userId: string
  action: string
  timestamp: string
  userEmail?: string
}

const router = useRouter()
const { toast } = useToast()

// Admin access is now controlled by router guard
const currentUser = AuthService.getUser()
console.log('🔍 [AdminDashboard] Current user from AuthService:', currentUser)
console.log('🔍 [AdminDashboard] User role:', currentUser?.role)

// State
const activeTab = ref('overview')
const users = ref<User[]>([])
const loading = ref(false)
const searchTerm = ref('')
const currentPage = ref(1)
const pageSize = ref(10)
const totalCount = ref(0)
const includeDeleted = ref(false)
const selectedUsers = ref<string[]>([])
const autoRefreshInterval = ref<number | null>(null)
const lastUpdated = ref<Date | null>(null)

// System statistics
const systemStats = ref<SystemStats>({
  totalUsers: 0,
  totalDocuments: 0,
  totalProcessedDocuments: 0,
  totalStorage: 0,
  averageProcessingTime: '0s',
  successRate: 0,
  errorRate: 0
})

const ocrStats = ref<OcrStats>({
  totalAttempts: 0,
  successfulAttempts: 0,
  failedAttempts: 0,
  averageConfidence: 0,
  processingTime: 0
})

const activityLogs = ref<ActivityLogItem[]>([])
const documentsData = ref<any[]>([])

// Edit user dialog
const editDialogOpen = ref(false)
const editingUser = ref<User | null>(null)
const editForm = ref({
  firstName: '',
  lastName: '',
  email: '',
  role: ''
})

// Computed
const filteredUsers = computed(() => {
  if (!searchTerm.value) return users.value
  return users.value.filter(user =>
    user.firstName.toLowerCase().includes(searchTerm.value.toLowerCase()) ||
    user.lastName.toLowerCase().includes(searchTerm.value.toLowerCase()) ||
    user.email.toLowerCase().includes(searchTerm.value.toLowerCase())
  )
})

const totalPages = computed(() => Math.ceil(totalCount.value / pageSize.value))

// Methods
const fetchSystemStats = async () => {
  try {
    console.log('🔄 Fetching system stats...')

    // Fetch dashboard stats
    let dashboardData = null
    try {
      const dashboardResponse = await admin.getDashboardStats()
      console.log('📊 Dashboard response:', dashboardResponse.data)
      if (dashboardResponse.data?.success && dashboardResponse.data.data) {
        dashboardData = dashboardResponse.data.data
      }
    } catch (dashError) {
      console.warn('⚠️ Dashboard stats failed:', dashError)
    }

    // Fetch OCR stats
    let ocrData = null
    try {
      const ocrResponse = await admin.getOcrStatistics()
      console.log('🤖 OCR response:', ocrResponse.data)
      if (ocrResponse.data?.success && ocrResponse.data.data) {
        ocrData = ocrResponse.data.data
      }
    } catch (ocrError) {
      console.warn('⚠️ OCR stats failed:', ocrError)
    }

    // Update system stats with available data
    systemStats.value = {
      totalUsers: dashboardData?.totalUsers || totalCount.value || 0,
      totalDocuments: dashboardData?.totalDocuments || 0,
      totalProcessedDocuments: dashboardData?.processedDocuments || 0,
      totalStorage: dashboardData?.totalStorage || 0,
      averageProcessingTime: dashboardData?.averageProcessingTime ? `${dashboardData.averageProcessingTime.toFixed(2)}s` : '0s',
      successRate: dashboardData?.overallSuccessRate || 0, // Keep as percentage
      errorRate: dashboardData?.overallErrorRate || 0 // Keep as percentage
    }

    // Update OCR stats with available data
    ocrStats.value = {
      totalAttempts: ocrData?.totalAttempts || 0,
      successfulAttempts: ocrData?.successfulAttempts || 0,
      failedAttempts: ocrData?.failedAttempts || 0,
      averageConfidence: ocrData?.averageConfidence || 0,
      processingTime: ocrData?.averageProcessingTime || 0
    }

    console.log('✅ System stats updated:', { systemStats: systemStats.value, ocrStats: ocrStats.value })
  } catch (error: any) {
    console.error('❌ Error fetching system stats:', error)
    // Keep existing values or set defaults
    if (!systemStats.value.totalUsers) {
      systemStats.value = {
        totalUsers: totalCount.value || 0,
        totalDocuments: 0,
        totalProcessedDocuments: 0,
        totalStorage: 0,
        averageProcessingTime: '0s',
        successRate: 0,
        errorRate: 0
      }
    }
    if (!ocrStats.value.totalAttempts) {
      ocrStats.value = {
        totalAttempts: 0,
        successfulAttempts: 0,
        failedAttempts: 0,
        averageConfidence: 0,
        processingTime: 0
      }
    }
  }
}

const fetchUsers = async () => {
  try {
    loading.value = true
    console.log('🔄 Fetching users...', { page: currentPage.value, pageSize: pageSize.value, includeDeleted: includeDeleted.value })

    const response = await admin.getAllUsers({
      page: currentPage.value,
      pageSize: pageSize.value,
      includeDeleted: includeDeleted.value
    })

    console.log('👥 Users response:', response.data)

    if (response.data?.success && response.data.data) {
      users.value = response.data.data.items || []
      totalCount.value = response.data.data.totalCount || 0
      console.log('✅ Users loaded:', users.value.length, 'total:', totalCount.value)
    } else {
      console.warn('⚠️ Invalid users response structure:', response.data)
      users.value = []
      totalCount.value = 0
    }
  } catch (error: any) {
    console.error('❌ Error fetching users:', error)
    toast({
      title: 'Erreur',
      description: error.response?.data?.message || 'Impossible de charger les utilisateurs',
      variant: 'destructive'
    })
    users.value = []
    totalCount.value = 0
  } finally {
    loading.value = false
  }
}

const fetchDocuments = async () => {
  try {
    const response = await admin.getAllDocuments({
      page: 1,
      pageSize: 50
    })

    if (response.data?.success) {
      documentsData.value = response.data.data.items || []
    }
  } catch (error: any) {
    console.error('Error fetching documents:', error)
  }
}

const fetchActivityLogs = async () => {
  try {
    console.log('🔄 Fetching system activity logs...')
    const response = await admin.getSystemActivity({ page: 1, pageSize: 50 })
    console.log('📝 System activity response:', response.data)

    if (response.data?.success && response.data.data) {
      activityLogs.value = response.data.data.items || []
      console.log('✅ System activity logs loaded:', activityLogs.value.length)
    } else {
      activityLogs.value = []
    }
  } catch (error: any) {
    console.warn('⚠️ System activity logs not available:', error)
    activityLogs.value = []
  }
}

const openEditDialog = (user: User) => {
  editingUser.value = user
  editForm.value = {
    firstName: user.firstName,
    lastName: user.lastName,
    email: user.email,
    role: user.role
  }
  editDialogOpen.value = true
}

const saveUser = async () => {
  if (!editingUser.value) return

  try {
    await admin.updateUser(editingUser.value.id, editForm.value)
    toast({
      title: 'Succès',
      description: 'Utilisateur mis à jour avec succès'
    })
    editDialogOpen.value = false
    await fetchUsers()
  } catch (error: any) {
    toast({
      title: 'Erreur',
      description: 'Impossible de mettre à jour l\'utilisateur',
      variant: 'destructive'
    })
  }
}

const deleteUser = async (userId: string) => {
  // Prevent admin from deleting themselves
  if (userId === currentUser?.id) {
    toast({
      title: 'Erreur',
      description: 'Vous ne pouvez pas supprimer votre propre compte',
      variant: 'destructive'
    })
    return
  }

  try {
    await admin.deleteUser(userId)

    toast({
      title: 'Succès',
      description: 'Utilisateur supprimé avec succès (suppression temporaire)'
    })
    await fetchUsers()
    await fetchSystemStats() // Refresh stats
  } catch (error: any) {
    toast({
      title: 'Erreur',
      description: error.response?.data?.message || 'Impossible de supprimer l\'utilisateur',
      variant: 'destructive'
    })
  }
}

const permanentlyDeleteUser = async (userId: string) => {
  // Prevent admin from deleting themselves
  if (userId === currentUser?.id) {
    toast({
      title: 'Erreur',
      description: 'Vous ne pouvez pas supprimer définitivement votre propre compte',
      variant: 'destructive'
    })
    return
  }

  const user = users.value.find(u => u.id === userId)
  const userName = user ? `${user.firstName} ${user.lastName}` : 'cet utilisateur'

  const confirmed = confirm(
    `⚠️ ATTENTION: Suppression définitive!\n\n` +
    `Êtes-vous sûr de vouloir supprimer définitivement ${userName}?\n\n` +
    `Cette action:\n` +
    `• Supprimera TOUTES les données de l'utilisateur\n` +
    `• Supprimera TOUS ses documents\n` +
    `• Ne peut PAS être annulée\n\n` +
    `Cette action est IRRÉVERSIBLE!`
  )

  if (!confirmed) return

  try {
    await admin.permanentlyDeleteUser(userId)

    toast({
      title: 'Succès',
      description: 'Utilisateur supprimé définitivement avec toutes ses données'
    })
    await fetchUsers()
    await fetchSystemStats() // Refresh stats
    await fetchDocuments() // Refresh documents as they might be affected
  } catch (error: any) {
    toast({
      title: 'Erreur',
      description: error.response?.data?.message || 'Impossible de supprimer définitivement l\'utilisateur',
      variant: 'destructive'
    })
  }
}

const bulkPermanentlyDelete = async () => {
  if (selectedUsers.value.length === 0) return

  // Check if admin is trying to delete themselves
  if (selectedUsers.value.includes(currentUser?.id || '')) {
    toast({
      title: 'Erreur',
      description: 'Vous ne pouvez pas vous inclure dans une suppression en masse',
      variant: 'destructive'
    })
    return
  }

  const confirmed = confirm(
    `⚠️ ATTENTION: Suppression définitive en masse!\n\n` +
    `Êtes-vous sûr de vouloir supprimer définitivement ${selectedUsers.value.length} utilisateurs?\n\n` +
    `Cette action:\n` +
    `• Supprimera TOUTES les données de ces utilisateurs\n` +
    `• Supprimera TOUS leurs documents\n` +
    `• Ne peut PAS être annulée\n\n` +
    `Cette action est IRRÉVERSIBLE!`
  )

  if (!confirmed) return

  try {
    // Process deletions individually since bulk endpoint might not exist
    for (const userId of selectedUsers.value) {
      await admin.permanentlyDeleteUser(userId)
    }

    toast({
      title: 'Succès',
      description: `${selectedUsers.value.length} utilisateurs supprimés définitivement`
    })
    selectedUsers.value = []
    await fetchUsers()
    await fetchSystemStats() // Refresh stats
    await fetchDocuments() // Refresh documents as they might be affected
  } catch (error: any) {
    toast({
      title: 'Erreur',
      description: error.response?.data?.message || 'Impossible de supprimer définitivement les utilisateurs',
      variant: 'destructive'
    })
  }
}

const restoreUser = async (userId: string) => {
  try {
    await admin.restoreUser(userId)
    toast({
      title: 'Succès',
      description: 'Utilisateur restauré avec succès'
    })
    await fetchUsers()
    await fetchSystemStats() // Refresh stats
  } catch (error: any) {
    toast({
      title: 'Erreur',
      description: error.response?.data?.message || 'Impossible de restaurer l\'utilisateur',
      variant: 'destructive'
    })
  }
}

const bulkDelete = async () => {
  if (selectedUsers.value.length === 0) return

  // Check if admin is trying to delete themselves
  if (selectedUsers.value.includes(currentUser?.id || '')) {
    toast({
      title: 'Erreur',
      description: 'Vous ne pouvez pas vous inclure dans une suppression en masse',
      variant: 'destructive'
    })
    return
  }

  try {
    await admin.bulkDeleteUsers(selectedUsers.value)
    toast({
      title: 'Succès',
      description: `${selectedUsers.value.length} utilisateurs supprimés (suppression temporaire)`
    })
    selectedUsers.value = []
    await fetchUsers()
    await fetchSystemStats() // Refresh stats
  } catch (error: any) {
    toast({
      title: 'Erreur',
      description: error.response?.data?.message || 'Impossible de supprimer les utilisateurs',
      variant: 'destructive'
    })
  }
}

const bulkRestore = async () => {
  if (selectedUsers.value.length === 0) return

  try {
    await admin.bulkRestoreUsers(selectedUsers.value)
    toast({
      title: 'Succès',
      description: `${selectedUsers.value.length} utilisateurs restaurés`
    })
    selectedUsers.value = []
    await fetchUsers()
    await fetchSystemStats() // Refresh stats
  } catch (error: any) {
    toast({
      title: 'Erreur',
      description: error.response?.data?.message || 'Impossible de restaurer les utilisateurs',
      variant: 'destructive'
    })
  }
}

const toggleUserSelection = (userId: string) => {
  const index = selectedUsers.value.indexOf(userId)
  if (index > -1) {
    selectedUsers.value.splice(index, 1)
  } else {
    selectedUsers.value.push(userId)
  }
}

const selectAllUsers = () => {
  if (selectedUsers.value.length === filteredUsers.value.length) {
    selectedUsers.value = []
  } else {
    selectedUsers.value = filteredUsers.value.map(user => user.id)
  }
}

const formatDate = (dateString: string) => {
  return new Date(dateString).toLocaleDateString('fr-FR', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}

const getRoleBadgeVariant = (role: string): 'destructive' | 'secondary' => {
  return role === 'Admin' ? 'destructive' : 'secondary'
}

const getStatusDisplayName = (status: number) => {
  switch (status) {
    case 0:
      return 'Téléchargé'
    case 1:
      return 'En traitement'
    case 2:
      return 'OCR en cours'
    case 3:
      return 'OCR terminé'
    case 4:
      return 'Amélioration en cours'
    case 5:
      return 'Terminé'
    case 6:
      return 'Échec'
    default:
      return 'Inconnu'
  }
}

const getStatusBadgeVariant = (status: number) => {
  switch (status) {
    case 5: // Complete
      return 'default'
    case 6: // Failed
      return 'destructive'
    case 1: // Processing
    case 2: // OcrInProgress
    case 4: // EnhancementInProgress
      return 'secondary'
    case 0: // Uploaded
    case 3: // OcrComplete
      return 'outline'
    default:
      return 'secondary'
  }
}

const refreshAllData = async () => {
  if (loading.value) return // Prevent multiple simultaneous refreshes

  try {
    loading.value = true
    await Promise.all([
      fetchUsers(),
      fetchSystemStats(),
      fetchDocuments(),
      fetchActivityLogs()
    ])
    lastUpdated.value = new Date()
    toast({
      title: 'Succès',
      description: 'Toutes les données ont été actualisées',
      variant: 'default'
    })
  } catch (error) {
    console.error('Error refreshing data:', error)
    toast({
      title: 'Erreur',
      description: 'Impossible d\'actualiser les données',
      variant: 'destructive'
    })
  } finally {
    loading.value = false
  }
}

// Auto-refresh functionality
const startAutoRefresh = () => {
  if (autoRefreshInterval.value) {
    clearInterval(autoRefreshInterval.value)
  }

  // Refresh data every 30 seconds
  autoRefreshInterval.value = window.setInterval(async () => {
    console.log('🔄 [AdminDashboard] Auto-refreshing data...')
    await fetchSystemStats()
    await fetchDocuments()
    await fetchActivityLogs()
    lastUpdated.value = new Date()
  }, 30000)
}

const stopAutoRefresh = () => {
  if (autoRefreshInterval.value) {
    clearInterval(autoRefreshInterval.value)
    autoRefreshInterval.value = null
  }
}

onMounted(async () => {
  console.log('🚀 [AdminDashboard] Component mounted')
  await Promise.all([
    fetchUsers(),
    fetchSystemStats(),
    fetchDocuments(),
    fetchActivityLogs()
  ])
  lastUpdated.value = new Date()
  startAutoRefresh()
})

onUnmounted(() => {
  stopAutoRefresh()
})
</script>

<template>
  <SidebarProvider class="min-h-screen">
    <SidebarLeft />
    <SidebarInset class="flex-1">
      <header class="sticky top-0 z-10 flex h-14 shrink-0 items-center gap-2 bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60 border-b">
        <div class="flex flex-1 items-center gap-2 px-3">
          <SidebarTrigger />
          <Separator orientation="vertical" class="mr-2 h-4" />
          <Breadcrumb>
            <BreadcrumbList>
              <BreadcrumbItem>
                <BreadcrumbPage>Administration</BreadcrumbPage>
              </BreadcrumbItem>
            </BreadcrumbList>
          </Breadcrumb>
        </div>
      </header>

      <div class="flex flex-1 flex-col gap-6 p-6">
        <!-- Header -->
        <div class="flex items-center justify-between">
          <div>
            <h1 class="text-3xl font-bold tracking-tight">Tableau de bord administrateur</h1>
            <p class="text-muted-foreground">Gérez les utilisateurs, documents et surveillez les performances du système</p>
          </div>
          <div class="flex items-center gap-2">
            <Badge variant="outline" class="flex items-center gap-1">
              <Users class="h-4 w-4" />
              {{ totalCount }} utilisateurs
            </Badge>
            <Badge variant="outline" class="flex items-center gap-1">
              <FileText class="h-4 w-4" />
              {{ systemStats.totalDocuments }} documents
            </Badge>
            <Button @click="refreshAllData" variant="outline" size="sm" class="flex items-center gap-1" :disabled="loading">
              <RotateCcw class="h-4 w-4" :class="{ 'animate-spin': loading }" />
              {{ loading ? 'Actualisation...' : 'Actualiser' }}
            </Button>
            <Badge variant="secondary" class="flex items-center gap-1 text-xs">
              <div class="w-2 h-2 bg-green-500 rounded-full animate-pulse"></div>
              Auto-actualisation: 30s
            </Badge>
            <Badge v-if="lastUpdated" variant="outline" class="text-xs">
              Dernière mise à jour: {{ lastUpdated.toLocaleTimeString('fr-FR', { hour: '2-digit', minute: '2-digit', second: '2-digit' }) }}
            </Badge>
          </div>
        </div>

        <!-- Admin Tabs -->
        <Tabs v-model="activeTab" class="w-full">
          <TabsList class="grid w-full grid-cols-5">
            <TabsTrigger value="overview" class="flex items-center gap-2">
              <BarChart3 class="h-4 w-4" />
              Vue d'ensemble
            </TabsTrigger>
            <TabsTrigger value="users" class="flex items-center gap-2">
              <Users class="h-4 w-4" />
              Utilisateurs
            </TabsTrigger>
            <TabsTrigger value="documents" class="flex items-center gap-2">
              <FileText class="h-4 w-4" />
              Documents
            </TabsTrigger>
            <TabsTrigger value="statistics" class="flex items-center gap-2">
              <TrendingUp class="h-4 w-4" />
              Statistiques OCR
            </TabsTrigger>
            <TabsTrigger value="activity" class="flex items-center gap-2">
              <Activity class="h-4 w-4" />
              Activité
            </TabsTrigger>
          </TabsList>

          <!-- Overview Tab -->
          <TabsContent value="overview" class="space-y-6">
            <!-- System Overview Stats -->
            <div class="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
              <Card>
                <CardHeader class="flex flex-row items-center justify-between space-y-0 pb-2">
                  <CardTitle class="text-sm font-medium">Total Utilisateurs</CardTitle>
                  <Users class="h-4 w-4 text-muted-foreground" />
                </CardHeader>
                <CardContent>
                  <div class="text-2xl font-bold">{{ systemStats.totalUsers }}</div>
                  <p class="text-xs text-muted-foreground">
                    {{ users.filter(u => u.role === 'Admin').length }} administrateurs
                  </p>
                </CardContent>
              </Card>

              <Card>
                <CardHeader class="flex flex-row items-center justify-between space-y-0 pb-2">
                  <CardTitle class="text-sm font-medium">Documents Traités</CardTitle>
                  <FileText class="h-4 w-4 text-muted-foreground" />
                </CardHeader>
                <CardContent>
                  <div class="text-2xl font-bold">{{ systemStats.totalProcessedDocuments }}</div>
                  <p class="text-xs text-muted-foreground">
                    sur {{ systemStats.totalDocuments }} total
                  </p>
                </CardContent>
              </Card>

              <Card>
                <CardHeader class="flex flex-row items-center justify-between space-y-0 pb-2">
                  <CardTitle class="text-sm font-medium">Taux de Succès</CardTitle>
                  <TrendingUp class="h-4 w-4 text-muted-foreground" />
                </CardHeader>
                <CardContent>
                  <div class="text-2xl font-bold">{{ systemStats.successRate.toFixed(1) }}%</div>
                  <p class="text-xs text-muted-foreground">
                    {{ systemStats.errorRate.toFixed(1) }}% d'erreurs
                  </p>
                </CardContent>
              </Card>

              <Card>
                <CardHeader class="flex flex-row items-center justify-between space-y-0 pb-2">
                  <CardTitle class="text-sm font-medium">Stockage Total</CardTitle>
                  <Database class="h-4 w-4 text-muted-foreground" />
                </CardHeader>
                <CardContent>
                  <div class="text-2xl font-bold">{{ (systemStats.totalStorage / 1024 / 1024).toFixed(1) }} MB</div>
                  <p class="text-xs text-muted-foreground">
                    Temps moyen: {{ systemStats.averageProcessingTime }}
                  </p>
                </CardContent>
              </Card>
            </div>

            <!-- Quick Actions -->
            <Card>
              <CardHeader>
                <CardTitle>Actions Rapides</CardTitle>
                <CardDescription>Accès rapide aux fonctions d'administration courantes</CardDescription>
              </CardHeader>
              <CardContent class="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
                <Button @click="activeTab = 'users'" variant="outline" class="h-20 flex-col gap-2">
                  <Users class="h-6 w-6" />
                  Gérer les Utilisateurs
                </Button>
                <Button @click="activeTab = 'documents'" variant="outline" class="h-20 flex-col gap-2">
                  <FileText class="h-6 w-6" />
                  Voir les Documents
                </Button>
                <Button @click="activeTab = 'statistics'" variant="outline" class="h-20 flex-col gap-2">
                  <BarChart3 class="h-6 w-6" />
                  Statistiques OCR
                </Button>
                <Button @click="activeTab = 'activity'" variant="outline" class="h-20 flex-col gap-2">
                  <Activity class="h-6 w-6" />
                  Journal d'Activité
                </Button>
              </CardContent>
            </Card>
          </TabsContent>

          <!-- Users Tab -->
          <TabsContent value="users" class="space-y-6">
            <Card>
              <CardHeader>
                <CardTitle>Gestion des utilisateurs</CardTitle>
                <CardDescription>Recherchez, modifiez et gérez les utilisateurs du système</CardDescription>
              </CardHeader>
              <CardContent class="space-y-4">
        <!-- Search and Filters -->
        <div class="flex items-center gap-4">
          <div class="relative flex-1">
            <Search class="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
            <Input
              v-model="searchTerm"
              placeholder="Rechercher par nom ou email..."
              class="pl-8"
            />
          </div>
          <div class="flex items-center space-x-2">
            <Checkbox
              id="include-deleted"
              v-model:checked="includeDeleted"
              @update:checked="fetchUsers"
            />
            <label for="include-deleted" class="text-sm font-medium">
              Inclure les supprimés
            </label>
          </div>
        </div>

        <!-- Bulk Actions -->
        <div v-if="selectedUsers.length > 0" class="flex items-center gap-2 p-2 bg-muted rounded-md">
          <span class="text-sm text-muted-foreground">
            {{ selectedUsers.length }} utilisateur(s) sélectionné(s)
          </span>
          <Button size="sm" variant="outline" @click="bulkDelete">
            <Trash2 class="h-4 w-4 mr-1" />
            Supprimer (temporaire)
          </Button>
          <Button size="sm" variant="destructive" @click="bulkPermanentlyDelete">
            <Trash2 class="h-4 w-4 mr-1" />
            Supprimer définitivement
          </Button>
          <Button size="sm" variant="outline" @click="bulkRestore">
            <RotateCcw class="h-4 w-4 mr-1" />
            Restaurer
          </Button>
        </div>

        <!-- Users Table -->
        <div class="border rounded-md">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead class="w-12">
                  <Checkbox
                    :checked="selectedUsers.length === filteredUsers.length && filteredUsers.length > 0"
                    @update:checked="selectAllUsers"
                  />
                </TableHead>
                <TableHead>Nom</TableHead>
                <TableHead>Email</TableHead>
                <TableHead>Rôle</TableHead>
                <TableHead>Statut</TableHead>
                <TableHead>Créé le</TableHead>
                <TableHead class="w-12">Actions</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              <TableRow v-for="user in filteredUsers" :key="user.id">
                <TableCell>
                  <Checkbox
                    v-if="user.id !== currentUser?.id"
                    :checked="selectedUsers.includes(user.id)"
                    @update:checked="() => toggleUserSelection(user.id)"
                  />
                  <span v-else class="text-muted-foreground text-xs">-</span>
                </TableCell>
                <TableCell class="font-medium">
                  {{ user.firstName }} {{ user.lastName }}
                </TableCell>
                <TableCell>{{ user.email }}</TableCell>
                <TableCell>
                  <Badge :variant="getRoleBadgeVariant(user.role)">
                    {{ user.role }}
                  </Badge>
                </TableCell>
                <TableCell>
                  <Badge :variant="user.isDeleted ? 'destructive' : 'default'">
                    {{ user.isDeleted ? 'Supprimé' : 'Actif' }}
                  </Badge>
                </TableCell>
                <TableCell>{{ formatDate(user.createdAt) }}</TableCell>
                <TableCell>
                  <div class="flex items-center gap-1">
                    <Button size="sm" variant="ghost" @click="openEditDialog(user)">
                      <Edit class="h-4 w-4" />
                    </Button>
                    <Button
                      v-if="!user.isDeleted && user.id !== currentUser?.id"
                      size="sm"
                      variant="ghost"
                      @click="deleteUser(user.id)"
                      title="Suppression temporaire"
                    >
                      <Trash2 class="h-4 w-4" />
                    </Button>
                    <Button
                      v-if="user.isDeleted && user.id !== currentUser?.id"
                      size="sm"
                      variant="destructive"
                      @click="permanentlyDeleteUser(user.id)"
                      title="Suppression définitive"
                    >
                      <Trash2 class="h-4 w-4" />
                    </Button>
                    <Badge
                      v-if="user.id === currentUser?.id"
                      variant="outline"
                      class="text-xs"
                    >
                      Vous
                    </Badge>
                    <Button
                      v-if="user.isDeleted"
                      size="sm"
                      variant="ghost"
                      @click="restoreUser(user.id)"
                      title="Restaurer l'utilisateur"
                    >
                      <RotateCcw class="h-4 w-4" />
                    </Button>
                  </div>
                </TableCell>
              </TableRow>
            </TableBody>
          </Table>
        </div>

        <!-- Pagination -->
        <div class="flex items-center justify-between">
          <div class="text-sm text-muted-foreground">
            Affichage de {{ (currentPage - 1) * pageSize + 1 }} à {{ Math.min(currentPage * pageSize, totalCount) }} sur {{ totalCount }} utilisateurs
          </div>
          <div class="flex items-center gap-2">
            <Button
              size="sm"
              variant="outline"
              :disabled="currentPage === 1"
              @click="currentPage--; fetchUsers()"
            >
              Précédent
            </Button>
            <Button
              size="sm"
              variant="outline"
              :disabled="currentPage === totalPages"
              @click="currentPage++; fetchUsers()"
            >
              Suivant
            </Button>
          </div>
        </div>
              </CardContent>
            </Card>
          </TabsContent>

          <!-- Documents Tab -->
          <TabsContent value="documents" class="space-y-6">
            <Card>
              <CardHeader>
                <CardTitle>Gestion des documents</CardTitle>
                <CardDescription>Vue d'ensemble de tous les documents du système</CardDescription>
              </CardHeader>
              <CardContent>
                <div class="grid gap-4 md:grid-cols-3">
                  <Card>
                    <CardHeader class="pb-2">
                      <CardTitle class="text-sm">Documents Totaux</CardTitle>
                    </CardHeader>
                    <CardContent>
                      <div class="text-2xl font-bold">{{ documentsData.length }}</div>
                    </CardContent>
                  </Card>
                  <Card>
                    <CardHeader class="pb-2">
                      <CardTitle class="text-sm">En Traitement</CardTitle>
                    </CardHeader>
                    <CardContent>
                      <div class="text-2xl font-bold">{{ documentsData.filter(d => d.status === 1 || d.status === 2 || d.status === 4).length }}</div>
                    </CardContent>
                  </Card>
                  <Card>
                    <CardHeader class="pb-2">
                      <CardTitle class="text-sm">Complétés</CardTitle>
                    </CardHeader>
                    <CardContent>
                      <div class="text-2xl font-bold">{{ documentsData.filter(d => d.status === 5).length }}</div>
                    </CardContent>
                  </Card>
                </div>

                <div class="mt-6">
                  <h4 class="text-sm font-medium mb-4">Documents Récents</h4>
                  <div class="border rounded-md">
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead>Nom du fichier</TableHead>
                          <TableHead>Utilisateur</TableHead>
                          <TableHead>Statut</TableHead>
                          <TableHead>Date de création</TableHead>
                          <TableHead>Taille</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        <TableRow v-for="doc in documentsData.slice(0, 10)" :key="doc.id">
                          <TableCell class="font-medium">{{ doc.fileName }}</TableCell>
                          <TableCell>{{ doc.userEmail || 'N/A' }}</TableCell>
                          <TableCell>
                            <Badge :variant="getStatusBadgeVariant(doc.status)">
                              {{ getStatusDisplayName(doc.status) }}
                            </Badge>
                          </TableCell>
                          <TableCell>{{ formatDate(doc.uploadDate) }}</TableCell>
                          <TableCell>{{ (doc.fileSize / 1024).toFixed(1) }} KB</TableCell>
                        </TableRow>
                      </TableBody>
                    </Table>
                  </div>
                </div>
              </CardContent>
            </Card>
          </TabsContent>

          <!-- OCR Statistics Tab -->
          <TabsContent value="statistics" class="space-y-6">
            <div class="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
              <Card>
                <CardHeader class="flex flex-row items-center justify-between space-y-0 pb-2">
                  <CardTitle class="text-sm font-medium">Tentatives Totales</CardTitle>
                  <BarChart3 class="h-4 w-4 text-muted-foreground" />
                </CardHeader>
                <CardContent>
                  <div class="text-2xl font-bold">{{ ocrStats.totalAttempts }}</div>
                </CardContent>
              </Card>

              <Card>
                <CardHeader class="flex flex-row items-center justify-between space-y-0 pb-2">
                  <CardTitle class="text-sm font-medium">Succès</CardTitle>
                  <TrendingUp class="h-4 w-4 text-muted-foreground" />
                </CardHeader>
                <CardContent>
                  <div class="text-2xl font-bold">{{ ocrStats.successfulAttempts }}</div>
                  <p class="text-xs text-muted-foreground">
                    {{ ocrStats.totalAttempts > 0 ? ((ocrStats.successfulAttempts / ocrStats.totalAttempts) * 100).toFixed(1) : 0 }}% de réussite
                  </p>
                </CardContent>
              </Card>

              <Card>
                <CardHeader class="flex flex-row items-center justify-between space-y-0 pb-2">
                  <CardTitle class="text-sm font-medium">Échecs</CardTitle>
                  <AlertTriangle class="h-4 w-4 text-muted-foreground" />
                </CardHeader>
                <CardContent>
                  <div class="text-2xl font-bold">{{ ocrStats.failedAttempts }}</div>
                  <p class="text-xs text-muted-foreground">
                    {{ ocrStats.totalAttempts > 0 ? ((ocrStats.failedAttempts / ocrStats.totalAttempts) * 100).toFixed(1) : 0 }}% d'échecs
                  </p>
                </CardContent>
              </Card>

              <Card>
                <CardHeader class="flex flex-row items-center justify-between space-y-0 pb-2">
                  <CardTitle class="text-sm font-medium">Confiance Moyenne</CardTitle>
                  <Clock class="h-4 w-4 text-muted-foreground" />
                </CardHeader>
                <CardContent>
                  <div class="text-2xl font-bold">{{ ocrStats.averageConfidence > 1 ? ocrStats.averageConfidence.toFixed(1) : (ocrStats.averageConfidence * 100).toFixed(1) }}%</div>
                  <p class="text-xs text-muted-foreground">
                    {{ ocrStats.processingTime.toFixed(2) }}s temps moyen
                  </p>
                </CardContent>
              </Card>
            </div>
          </TabsContent>

          <!-- Activity Tab -->
          <TabsContent value="activity" class="space-y-6">
            <Card>
              <CardHeader>
                <CardTitle>Journal d'activité du système</CardTitle>
                <CardDescription>Surveillance des actions des utilisateurs et des événements système</CardDescription>
              </CardHeader>
              <CardContent>
                <div class="border rounded-md">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Utilisateur</TableHead>
                        <TableHead>Action</TableHead>
                        <TableHead>Horodatage</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      <TableRow v-for="log in activityLogs.slice(0, 20)" :key="log.id">
                        <TableCell>{{ log.userEmail || 'Système' }}</TableCell>
                        <TableCell>{{ log.action }}</TableCell>
                        <TableCell>{{ formatDate(log.timestamp) }}</TableCell>
                      </TableRow>
                    </TableBody>
                  </Table>
                </div>
              </CardContent>
            </Card>
          </TabsContent>
        </Tabs>

        <!-- Edit User Dialog -->
        <Dialog v-model:open="editDialogOpen">
          <DialogContent class="sm:max-w-[425px]">
            <DialogHeader>
              <DialogTitle>Modifier l'utilisateur</DialogTitle>
              <DialogDescription>
                Modifiez les informations de l'utilisateur. Cliquez sur sauvegarder pour confirmer.
              </DialogDescription>
            </DialogHeader>
            <div class="grid gap-4 py-4">
              <div class="grid grid-cols-4 items-center gap-4">
                <label for="firstName" class="text-right">Prénom</label>
                <Input
                  id="firstName"
                  v-model="editForm.firstName"
                  class="col-span-3"
                />
              </div>
              <div class="grid grid-cols-4 items-center gap-4">
                <label for="lastName" class="text-right">Nom</label>
                <Input
                  id="lastName"
                  v-model="editForm.lastName"
                  class="col-span-3"
                />
              </div>
              <div class="grid grid-cols-4 items-center gap-4">
                <label for="email" class="text-right">Email</label>
                <Input
                  id="email"
                  v-model="editForm.email"
                  type="email"
                  class="col-span-3"
                />
              </div>
              <div class="grid grid-cols-4 items-center gap-4">
                <label for="role" class="text-right">Rôle</label>
                <Select v-model="editForm.role">
                  <SelectTrigger class="col-span-3">
                    <SelectValue placeholder="Sélectionner un rôle" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="User">Utilisateur</SelectItem>
                    <SelectItem value="Admin">Administrateur</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </div>
            <DialogFooter>
              <Button type="submit" @click="saveUser">Sauvegarder</Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </div>
    </SidebarInset>
  </SidebarProvider>
</template>
