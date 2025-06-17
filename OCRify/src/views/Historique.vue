<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { History, FileText, Clock, Calendar, Search, ChevronLeft, ChevronRight, LogIn, Upload, Trash2, Edit2, Chrome } from 'lucide-vue-next'
import { Input } from '@/components/ui/input'
import { SidebarProvider, SidebarInset } from '@/components/ui/sidebar'
import { Separator } from '@/components/ui/separator'
import { Breadcrumb, BreadcrumbItem, BreadcrumbList, BreadcrumbPage } from '@/components/ui/breadcrumb'
import SidebarLeft from '@/components/SidebarLeft.vue'
import { SidebarTrigger } from '@/components/ui/sidebar'
import { users } from '@/services/api'

interface HistoryItem {
  id: string
  action: string
  timestamp: string
  icon: any
}

const searchQuery = ref('')
const historyItems = ref<HistoryItem[]>([])
const currentPage = ref(1)
const pageSize = ref(10)
const totalItems = ref(0)
const isLoading = ref(false)

// Format timestamp to readable format
const formatTimestamp = (dateString: string) => {
  try {
    const date = new Date(dateString)
    if (isNaN(date.getTime())) {
      return 'Date invalide'
    }
    return date.toLocaleString('fr-FR', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    })
  } catch (error) {
    return 'Date invalide'
  }
}

// Translate actions to French and get appropriate icon
const getActionDetails = (action: string) => {
  const actionMap: Record<string, { text: string; icon: any }> = {
    'User logged in': { text: 'Connexion utilisateur', icon: LogIn },
    'User logged out': { text: 'Déconnexion utilisateur', icon: LogIn },
    'Updated document': { text: 'Document modifié', icon: Edit2 },
    'Soft deleted document': { text: 'Document supprimé', icon: Trash2 },
    'Bulk uploaded': { text: 'Import en masse', icon: Upload },
    'Updated profile': { text: 'Mis à jour du profil', icon: Edit2 },
    'Password changed': { text: 'Mot de passe modifié', icon: Edit2 },
    'Restored document': { text: 'Document restauré', icon: Edit2 },
    'Uploaded document': { text: 'Document importé', icon: Upload },
    'Bulk soft deleted': { text: 'Documents supprimés en masse', icon: Trash2 },
    'User registered via Google': { text: 'Inscription via Google', icon: Chrome },
    'default': { text: action, icon: FileText }
  }

  // Extract document name if present
  const documentNameMatch = action.match(/(?:document|documents) "?([^"]+)"?/)
  const documentName = documentNameMatch ? documentNameMatch[1] : null

  // Extract number of documents for bulk actions
  const bulkCountMatch = action.match(/Bulk soft deleted (\d+) documents/)
  const bulkCount = bulkCountMatch ? bulkCountMatch[1] : null

  // Find matching action
  for (const [key, value] of Object.entries(actionMap)) {
    if (action.includes(key)) {
      let translatedText = value.text
      
      // Handle bulk delete with count
      if (key === 'Bulk soft deleted' && bulkCount) {
        translatedText = `${bulkCount} documents supprimés en masse`
      }
      // If there's a document name, include it in the translation
      else if (documentName) {
        translatedText = translatedText.replace('Document', `Document "${documentName}"`)
      }
      
      return {
        text: translatedText,
        icon: value.icon
      }
    }
  }

  // If no match found but there's a document name, include it in the default text
  if (documentName) {
    return {
      text: `Document "${documentName}" ${action}`,
      icon: FileText
    }
  }

  return actionMap.default
}

// Fetch activities from API
const fetchHistoryItems = async () => {
  try {
    isLoading.value = true
    
    const response = await users.getActivity({
      page: currentPage.value,
      pageSize: pageSize.value
    })
    
    
    if (response?.data?.success && response.data.data?.items) {
      
      const activities = response.data.data.items
      totalItems.value = response.data.data.totalCount || 0
      
      // Transform API data to match our simplified interface
      historyItems.value = activities.map((activity: any) => {
        const actionDetails = getActionDetails(activity.action)
        return {
          id: activity.id || 'unknown',
          action: actionDetails.text,
          timestamp: activity.timestamp ? formatTimestamp(activity.timestamp) : 'Date non disponible',
          icon: actionDetails.icon
        }
      })
      
    } else {
      historyItems.value = []
      totalItems.value = 0
    }
  } catch (err: unknown) {
    const error = err as Error
    historyItems.value = []
    totalItems.value = 0
  } finally {
    isLoading.value = false
  }
}

const filteredHistory = computed(() => {
  const query = searchQuery.value.toLowerCase()
  return historyItems.value.filter(item => 
    item.action.toLowerCase().includes(query) ||
    item.timestamp.toLowerCase().includes(query)
  )
})

const totalPages = computed(() => Math.ceil(totalItems.value / pageSize.value))

const handlePageChange = (newPage: number) => {
  if (newPage >= 1 && newPage <= totalPages.value) {
    currentPage.value = newPage
    fetchHistoryItems()
  }
}

onMounted(() => {
  fetchHistoryItems()
})
</script>

<template>
  <SidebarProvider>
    <SidebarLeft />
    <SidebarInset>
      <header class="sticky top-0 z-10 flex h-14 shrink-0 items-center gap-2 bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60 border-b">
        <div class="flex flex-1 items-center gap-2 px-3">
          <SidebarTrigger />
          <Separator orientation="vertical" class="mr-2 h-4" />
          <Breadcrumb>
            <BreadcrumbList>
              <BreadcrumbItem>
                <BreadcrumbPage>Historique</BreadcrumbPage>
              </BreadcrumbItem>
            </BreadcrumbList>
          </Breadcrumb>
        </div>
      </header>

      <div class="flex-1 space-y-4 p-4 md:p-8 pt-6">
        <!-- Header -->
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-2">
            <History class="h-6 w-6 text-primary" />
            <h1 class="text-2xl font-semibold tracking-tight">Historique</h1>
          </div>
          <div class="flex items-center gap-4">
            <div class="relative w-64">
              <Search class="absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                v-model="searchQuery"
                placeholder="Rechercher dans l'historique..."
                class="pl-8"
              />
            </div>
          </div>
        </div>

        <!-- Main Content -->
        <div class="grid gap-4">
          <Card>
            <CardHeader>
              <CardTitle>Activités récentes</CardTitle>
              <CardDescription>
                Historique de vos actions
              </CardDescription>
            </CardHeader>
            <CardContent>
              <div class="space-y-4">
                <div
                  v-for="item in filteredHistory"
                  :key="item.id"
                  class="flex items-center justify-between p-4 rounded-lg border bg-card hover:bg-accent/5 transition-colors"
                >
                  <div class="flex items-center gap-4">
                    <component :is="item.icon" class="h-5 w-5 text-muted-foreground" />
                    <div>
                      <h3 class="font-medium">{{ item.action }}</h3>
                      <div class="flex items-center gap-1 text-sm text-muted-foreground">
                        <Clock class="h-4 w-4" />
                        <span>{{ item.timestamp }}</span>
                      </div>
                    </div>
                  </div>
                </div>

                <!-- Empty State -->
                <div
                  v-if="filteredHistory.length === 0 && !isLoading"
                  class="flex flex-col items-center justify-center py-12 text-center"
                >
                  <History class="h-12 w-12 text-muted-foreground mb-4" />
                  <h3 class="text-lg font-medium mb-2">Aucun historique trouvé</h3>
                  <p class="text-sm text-muted-foreground">
                    {{ searchQuery ? 'Aucun résultat ne correspond à votre recherche.' : 'Votre historique apparaîtra ici une fois que vous aurez effectué des actions.' }}
                  </p>
                </div>

                <!-- Loading State -->
                <div
                  v-if="isLoading"
                  class="flex flex-col items-center justify-center py-12 text-center"
                >
                  <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-primary mb-4"></div>
                  <p class="text-sm text-muted-foreground">Chargement des activités...</p>
                </div>

                <!-- Pagination -->
                <div
                  v-if="totalPages > 1"
                  class="flex items-center justify-between pt-4 border-t"
                >
                  <div class="text-sm text-muted-foreground">
                    Page {{ currentPage }} sur {{ totalPages }}
                  </div>
                  <div class="flex items-center gap-2">
                    <Button
                      variant="outline"
                      size="sm"
                      :disabled="currentPage === 1"
                      @click="handlePageChange(currentPage - 1)"
                    >
                      <ChevronLeft class="h-4 w-4" />
                    </Button>
                    <Button
                      variant="outline"
                      size="sm"
                      :disabled="currentPage === totalPages"
                      @click="handlePageChange(currentPage + 1)"
                    >
                      <ChevronRight class="h-4 w-4" />
                    </Button>
                  </div>
                </div>
              </div>
            </CardContent>
          </Card>
        </div>
      </div>
    </SidebarInset>
  </SidebarProvider>
</template> 