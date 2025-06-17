<script lang="ts">
export const iframeHeight = '800px'
export const description = 'A left and right sidebar.'
</script>
<script setup lang="ts">
import { ref, watch, onMounted, onUnmounted, inject, computed } from 'vue'
import { useRoute } from 'vue-router'
import type { AxiosResponse } from 'axios'
import type { DocumentSearchDto, DashboardResponse } from '@/types/documents'
import {
  FileText,
  Loader2,
  FileType2,
  BarChart3,
  Clock,
  CheckCircle2,
  Languages,
  LayoutDashboard,
  Info,
} from 'lucide-vue-next'
import Progress from '@/components/ui/progress/Progress.vue'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import SidebarLeft from '@/components/SidebarLeft.vue'
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
import { documents } from '@/services/api'
import { useToast } from '@/components/ui/toast/use-toast'

import AreaChart from '@/components/ui/chart-area/AreaChart.vue'
import BarChart from '@/components/ui/chart-bar/BarChart.vue'
import DonutChart from '@/components/ui/chart-donut/DonutChart.vue'
import LineChart from '@/components/ui/chart-line/LineChart.vue'
import { formatFileSize } from '@/lib/utils'
import type { DocumentTypeDistribution } from '@/types/documents'
import axios from 'axios'
import { AuthService } from '@/services/auth-service'

const route = useRoute()
const { toast } = useToast()

// Add interface for dashboard response
interface DashboardResponse {
  documentsProcessed: number;
  processedDocuments: number;
  processingTime: string;
  averageAccuracy: number;
  successRate: number;
  totalStorage: number;
  processingQueue: number;
  languagesDetected: string[];
  totalCharacters: string;
  documentsToday: number;
  errorRate: number;
  recoveryRate: number;
  storageGrowth: number;
  characterExtractionRate: number;
  weeklyActivity: {
    day: string;
    documents: number;
    précision: number;
  }[];
  monthlyTrends: {
    month: string;
    actifs: number;
    supprimés: number;
  }[];
  languageDistribution: {
    name: string;
    docs: number;
  }[];
  processingMetrics: {
    metric: string;
    valeur: number;
  }[];
  documentTypeDistribution: {
    type: string;
    nombre: number;
  }[];
}

// Add interface for weekly activity data point
interface WeeklyActivityDataPoint {
  day: string;
  documents: number;
  accuracy: number;
}

// Add interface for monthly trend data point
interface MonthlyTrendDataPoint {
  month: string;
  active: number;
  deleted: number;
}

// Add interface for language distribution data point
interface LanguageDistributionDataPoint {
  name: string;
  docs: number;
}

// Add interface for processing metric data point
interface ProcessingMetricDataPoint {
  metric: string;
  value: number;
}

// Add interface for document type distribution
interface DocumentTypeDistribution {
  type: string;
  nombre: number;
}

// Add new function to get current day name in French
const getCurrentDayName = () => {
  const days = ['Dim', 'Lun', 'Mar', 'Mer', 'Jeu', 'Ven', 'Sam']
  return days[new Date().getDay()]
}

// Add new function to get current month name in French
const getCurrentMonthName = () => {
  const months = ['Jan', 'Fév', 'Mar', 'Avr', 'Mai', 'Jun', 'Jul', 'Aoû', 'Sep', 'Oct', 'Nov', 'Déc']
  return months[new Date().getMonth()]
}

// Create sample response data
const sampleResponse: DashboardResponse = {
  documentsProcessed: 156,
  processedDocuments: 156,
  processingTime: "1m 45s",
  averageAccuracy: 87,
  successRate: 92,
  totalStorage: 2.3,
  processingQueue: 4,
  languagesDetected: ["Français", "English", "العربية"],
  totalCharacters: "1,234,567",
  documentsToday: 12,
  errorRate: 5,
  recoveryRate: 88,
  storageGrowth: 8,
  characterExtractionRate: 96,
  weeklyActivity: [
    { day: "Lun", documents: 18, précision: 70.00 },
    { day: "Mar", documents: 22, précision: 80.00 },
    { day: "Mer", documents: 15, précision: 90.00 },
    { day: "Jeu", documents: 28, précision: 95.00 },
    { day: "Ven", documents: 20, précision: 82.00 },
    { day: "Sam", documents: 12, précision: 85.00 },
    { day: getCurrentDayName(), documents: 8, précision: 69.00 }
  ],
  monthlyTrends: [
    { month: "Jan", actifs: 45, supprimés: 5 },
    { month: "Fév", actifs: 52, supprimés: 8 },
    { month: "Mar", actifs: 48, supprimés: 12 },
    { month: "Avr", actifs: 65, supprimés: 15 },
    { month: "Mai", actifs: 58, supprimés: 10 },
    { month: getCurrentMonthName(), actifs: 72, supprimés: 18 }
  ],
  languageDistribution: [
    { name: "Français", docs: 96 },
    { name: "Anglais", docs: 76 },
    { name: "Arabe", docs: 48 }
  ],
  processingMetrics: [
    { metric: "Temps moyen par page", valeur: 45 },
    { metric: "Taille moyenne", valeur: 2.5 },
    { metric: "Pages par document", valeur: 4 },
    { metric: "Taux de compression", valeur: 65 }
  ],
  documentTypeDistribution: [
    { type: "PDF", nombre: 45 },
    { type: "Images", nombre: 30 },
    { type: "Scannés", nombre: 25 },
    { type: "Autres", nombre: 10 }
  ]
}



// Update the dashboard data structure with proper typing
const dashboardData = ref<DashboardResponse>({
  documentsProcessed: 0,
  processedDocuments: 0,
  documentsToday: 0,
  processingTime: "0s",
  processingQueue: 0,
  averageAccuracy: 0,
  successRate: 0,
  errorRate: 0,
  recoveryRate: 0,
  characterExtractionRate: 0,
  totalStorage: 0,
  storageGrowth: 0,
  languagesDetected: [],
  totalCharacters: "0",
  weeklyActivity: [],
  monthlyTrends: [],
  languageDistribution: [],
  processingMetrics: [],
  documentTypeDistribution: []
})

// Update stats ref to use sample response data
const stats = ref({
  documentsProcessed: sampleResponse.documentsProcessed,
  processedDocuments: sampleResponse.processedDocuments,
  averageAccuracy: sampleResponse.averageAccuracy,
  totalStorage: sampleResponse.totalStorage,
  processingTime: sampleResponse.processingTime,
  totalCharacters: sampleResponse.totalCharacters,
  languagesDetected: sampleResponse.languagesDetected,
  successRate: sampleResponse.successRate,
  documentsToday: sampleResponse.documentsToday,
  errorRate: sampleResponse.errorRate,
  recoveryRate: sampleResponse.recoveryRate,
  processingQueue: sampleResponse.processingQueue,
  storageGrowth: sampleResponse.storageGrowth,
  characterExtractionRate: sampleResponse.characterExtractionRate
})

// Add types at the top of the script section
interface Document {
  id: number
  name: string
  action: string
  status: string
  accuracy?: number
  timestamp: string
  type: string
  details: string
  actionDescription: string
  actionResult: string
  thumbnail?: string
}

// Update the fetchRecentDocuments function
const fetchRecentDocuments = async () => {
  if (loading.value) return // Prevent multiple simultaneous calls

  try {
    loading.value = true
    console.log('🔵 [Dashboard View] Fetching dashboard data')
    const response = await documents.getRecent()
    const data = response.data

    console.log('📊 [Dashboard View] Raw API response:', data)

    if (!data) {
      throw new Error('No data received from API')
    }

    // Update dashboard data with real response and proper type conversion
    dashboardData.value = {
      documentsProcessed: Number(data.documentsProcessed) || 0,
      processedDocuments: Number(data.processedDocuments) || 0,
      documentsToday: Number(data.documentsToday) || 0,
      processingTime: String(data.processingTime || "0s"),
      processingQueue: Number(data.processingQueue) || 0,
      averageAccuracy: Number(data.averageAccuracy) || 0,
      successRate: Number(data.successRate) || 0,
      errorRate: Number(data.errorRate) || 0,
      recoveryRate: Number(data.recoveryRate) || 0,
      characterExtractionRate: Number(data.characterExtractionRate) || 0,
      totalStorage: Number(data.totalStorage) || 0,
      storageGrowth: Number(data.storageGrowth) || 0,
      languagesDetected: Array.isArray(data.languagesDetected) ? data.languagesDetected : [],
      totalCharacters: String(data.totalCharacters || "0"),
      weeklyActivity: Array.isArray(data.weeklyActivity) && data.weeklyActivity.length > 0
        ? data.weeklyActivity.map(item => ({
            day: String(item.day || ''),
            documents: Number(item.documents) || 0,
            précision: Number((item.accuracy || 0).toFixed(2))
          }))
        : [{ day: 'Lun', documents: 0, précision: 0 }],
      monthlyTrends: Array.isArray(data.monthlyTrends) && data.monthlyTrends.length > 0
        ? data.monthlyTrends.map(item => ({
            month: String(item.month || ''),
            actifs: Number(item.active) || 0,
            supprimés: Number(item.deleted) || 0
          }))
        : [{ month: 'Jan', actifs: 0, supprimés: 0 }],
      languageDistribution: Array.isArray(data.languageDistribution) && data.languageDistribution.length > 0
        ? data.languageDistribution.map(item => ({
            name: item.name === 'English' ? 'Anglais' :
                  item.name === 'French' ? 'Français' :
                  item.name === 'Arabic' ? 'Arabe' : String(item.name || ''),
            docs: Number(item.docs) || 0
          }))
        : [{ name: 'Aucune', docs: 0 }],
      processingMetrics: Array.isArray(data.processingMetrics) && data.processingMetrics.length > 0
        ? data.processingMetrics.map(item => ({
            metric: String(item.metric || ''),
            valeur: Number(item.value) || 0
          }))
        : [{ metric: 'Aucune métrique', valeur: 0 }],
      documentTypeDistribution: Array.isArray(data.documentTypeDistribution) && data.documentTypeDistribution.length > 0
        ? data.documentTypeDistribution.map((item: any) => ({
            type: String(item.type || ''),
            nombre: Number(item.nombre) || 0
          }))
        : [{ type: 'Aucun', nombre: 0 }]
    }

    // Update stats with real data and proper type conversion
    stats.value = {
      documentsProcessed: Number(data.documentsProcessed) || 0,
      processedDocuments: Number(data.processedDocuments) || 0,
      averageAccuracy: Number(data.averageAccuracy) || 0,
      totalStorage: Number(data.totalStorage) || 0,
      processingTime: String(data.processingTime || "0s"),
      totalCharacters: String(data.totalCharacters || "0"),
      languagesDetected: Array.isArray(data.languagesDetected) ? data.languagesDetected : [],
      successRate: Number(data.successRate) || 0,
      documentsToday: Number(data.documentsToday) || 0,
      errorRate: Number(data.errorRate) || 0,
      recoveryRate: Number(data.recoveryRate) || 0,
      processingQueue: Number(data.processingQueue) || 0,
      storageGrowth: Number(data.storageGrowth) || 0,
      characterExtractionRate: Number(data.characterExtractionRate) || 0
    }

    // Force chart re-render
    chartKey.value++

    console.log('📈 [Dashboard View] Updated dashboard data:', dashboardData.value)
  } catch (error) {
    console.error('❌ [Dashboard View] Error:', error)

    // Reset all data to empty/default values
    dashboardData.value = {
      documentsProcessed: 0,
      processedDocuments: 0,
      documentsToday: 0,
      processingTime: "0s",
      processingQueue: 0,
      averageAccuracy: 0,
      successRate: 0,
      errorRate: 0,
      recoveryRate: 0,
      characterExtractionRate: 0,
      totalStorage: 0,
      storageGrowth: 0,
      languagesDetected: [],
      totalCharacters: "0",
      weeklyActivity: [{ day: 'Lun', documents: 0, précision: 0 }],
      monthlyTrends: [{ month: 'Jan', actifs: 0, supprimés: 0 }],
      languageDistribution: [{ name: 'Aucune', docs: 0 }],
      processingMetrics: [{ metric: 'Aucune métrique', valeur: 0 }],
      documentTypeDistribution: [{ type: 'Aucun', nombre: 0 }]
    }

    stats.value = {
      documentsProcessed: 0,
      processedDocuments: 0,
      averageAccuracy: 0,
      totalStorage: 0,
      processingTime: "0s",
      totalCharacters: "0",
      languagesDetected: [],
      successRate: 0,
      documentsToday: 0,
      errorRate: 0,
      recoveryRate: 0,
      processingQueue: 0,
      storageGrowth: 0,
      characterExtractionRate: 0
    }

    // Show error toast in French
    toast({
      title: "Erreur",
      description: "Impossible de récupérer les données du tableau de bord",
      variant: "destructive"
    })
  } finally {
    loading.value = false
  }
}

// Update the formatFileSize function
const formatFileSize = (bytes: number) => {
  if (!bytes) return '0 o'

  // Convert to KB
  const kb = bytes / 1024
  if (kb < 1) return '0 Ko'

  // Convert to MB if KB is large enough
  if (kb >= 1024) {
    const mb = kb / 1024
    return `${mb.toFixed(2)} Mo`
  }

  return `${kb.toFixed(2)} Ko`
}

// Add refresh interval
const refreshInterval = ref<number | null>(null)

// Add loading state
const loading = ref(false)

// Add chart key for forcing re-render
const chartKey = ref(0)

// Computed properties for safe chart data
const safeWeeklyActivity = computed(() => {
  const data = dashboardData.value.weeklyActivity
  return Array.isArray(data) && data.length > 0 ? data : [{ day: 'Lun', documents: 0, précision: 0 }]
})

const safeMonthlyTrends = computed(() => {
  const data = dashboardData.value.monthlyTrends
  return Array.isArray(data) && data.length > 0 ? data : [{ month: 'Jan', actifs: 0, supprimés: 0 }]
})

const safeLanguageDistribution = computed(() => {
  const data = dashboardData.value.languageDistribution
  return Array.isArray(data) && data.length > 0 ? data : [{ name: 'Aucune', docs: 0 }]
})

const safeDocumentTypeDistribution = computed(() => {
  const data = dashboardData.value.documentTypeDistribution
  return Array.isArray(data) && data.length > 0 ? data : [{ type: 'Aucun', nombre: 0 }]
})

// Add force refresh function
const forceRefresh = async () => {
  console.log('🔄 [Dashboard View] Force refresh triggered')
  if (refreshInterval.value) {
    clearInterval(refreshInterval.value)
  }
  await fetchRecentDocuments()
  startPeriodicRefresh()
}

// Add periodic refresh function
const startPeriodicRefresh = () => {
  if (refreshInterval.value) {
    clearInterval(refreshInterval.value)
  }
  refreshInterval.value = window.setInterval(() => {
    console.log('🔄 [Dashboard View] Periodic refresh triggered')
    fetchRecentDocuments()
  }, 30000)
}

// Add cleanup function
const stopPeriodicRefresh = () => {
  if (refreshInterval.value) {
    clearInterval(refreshInterval.value)
    refreshInterval.value = null
  }
}

// Modify onMounted
onMounted(() => {
  console.log('🚀 [Dashboard View] Component mounted')
  fetchRecentDocuments()
  startPeriodicRefresh()
})

// Add onUnmounted
onUnmounted(() => {
  console.log('🛑 [Dashboard View] Component unmounted')
  stopPeriodicRefresh()
})

// Add route change watcher
watch(() => route.path, () => {
  console.log('🔄 [Dashboard View] Route changed, refreshing data')
  forceRefresh()
})

// Update monthlyStats to use real data
const monthlyStats = ref({
  labels: ['Jan', 'Fév', 'Mar', 'Avr', 'Mai', 'Jun'],
  documents: [0, 0, 0, 0, 0, 0],
  storage: [0, 0, 0, 0, 0, 0],
  deleted: [0, 0, 0, 0, 0, 0] // Add deleted documents count
})

// Add language mapping function
const mapLanguageToNative = (lang: string) => {
  const languageMap: Record<string, string> = {
    'English': 'English',
    'French': 'Français',
    'Arabic': 'العربية'
  }
  return languageMap[lang] || lang
}

const documentTypeDistribution = ref<DocumentTypeDistribution[]>([])

const fetchDocumentTypeDistribution = async () => {
  try {
    const response = await documents.getRecent()
    if (response.data && response.data.documentTypeDistribution) {
      documentTypeDistribution.value = response.data.documentTypeDistribution
    }
  } catch (error) {
    console.error('Error fetching document type distribution:', error)
  }
}


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
                <BreadcrumbPage>Accueil</BreadcrumbPage>
              </BreadcrumbItem>
            </BreadcrumbList>
          </Breadcrumb>
        </div>
      </header>

      <div class="flex flex-1 flex-col p-6">
        <!-- Welcome Section -->
        <div class="flex items-center justify-between mb-8">
          <div>
            <div class="flex items-center gap-2">
              <LayoutDashboard class="h-6 w-6 text-primary" />
              <h1 class="text-2xl font-semibold">Tableau de bord</h1>
            </div>
            <p class="text-muted-foreground mt-1">Aperçu de vos activités OCR et documents</p>
          </div>
        </div>



        <!-- Statistics Cards -->
        <div class="grid sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
          <!-- Document Processing KPIs -->
          <Card>
            <CardHeader class="flex flex-row items-center justify-between pb-2">
              <CardTitle class="text-sm font-medium">Documents stockés</CardTitle>
              <FileText class="h-4 w-4 text-muted-foreground" />
            </CardHeader>
            <CardContent>
              <div class="text-2xl font-bold">{{ stats.documentsProcessed }}</div>
              <div class="flex items-center gap-2 mt-1">
                <div class="text-xs text-muted-foreground">+{{ stats.documentsToday }} aujourd'hui</div>
                <div class="text-xs text-green-500" v-if="stats.documentsToday > 0">
                  ↑ {{ Math.round((stats.documentsToday / stats.documentsProcessed) * 100) }}%
                </div>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader class="flex flex-row items-center justify-between pb-2">
              <CardTitle class="text-sm font-medium">Temps de traitement</CardTitle>
              <Clock class="h-4 w-4 text-muted-foreground" />
            </CardHeader>
            <CardContent>
              <div class="text-2xl font-bold">{{ stats.processingTime }}</div>
              <div class="flex items-center gap-2 mt-1">
                <div class="text-xs text-muted-foreground">Moyenne par page</div>
                <div class="text-xs text-green-500" v-if="stats.processingTime < '2m'">Rapide</div>
              </div>
            </CardContent>
          </Card>

          <!-- Quality KPIs -->
          <Card>
            <CardHeader class="flex flex-row items-center justify-between pb-2">
              <CardTitle class="text-sm font-medium">Précision OCR</CardTitle>
              <BarChart3 class="h-4 w-4 text-muted-foreground" />
            </CardHeader>
            <CardContent>
              <div class="text-2xl font-bold">{{ Number(stats.averageAccuracy || 0).toFixed(2) }}%</div>
              <Progress
                :value="Number(stats.averageAccuracy || 0)"
                :max="100"
                :class="[
                  'mt-2',
                  Number(stats.averageAccuracy || 0) >= 95 ? 'bg-green-500' :
                  Number(stats.averageAccuracy || 0) >= 85 ? 'bg-yellow-500' :
                  'bg-red-500'
                ]"
              />
              <div class="text-xs text-muted-foreground mt-1">
                Taux d'extraction: {{ stats.characterExtractionRate.toFixed(2) }}%
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader class="flex flex-row items-center justify-between pb-2">
              <div class="flex items-center gap-2">
                <CardTitle class="text-sm font-medium">Taux de réussite</CardTitle>
                <Popover>
                  <PopoverTrigger>
                    <Info class="h-4 w-4 text-muted-foreground cursor-pointer hover:text-primary transition-colors" />
                  </PopoverTrigger>
                  <PopoverContent class="w-80 p-4 bg-background border rounded-lg shadow-lg">
                    <div class="space-y-3">
                      <p class="text-sm text-muted-foreground">
                        Indicateurs de performance du système OCR
                      </p>
                      <div class="space-y-2">
                        <div class="flex items-center gap-2">
                          <div class="w-2 h-2 rounded-full bg-primary"></div>
                          <span class="text-sm font-medium">Taux de réussite</span>
                        </div>
                        <p class="text-sm text-muted-foreground ml-4">Pourcentage de documents traités avec succès</p>
                        <div class="flex items-center gap-2">
                          <div class="w-2 h-2 rounded-full bg-destructive"></div>
                          <span class="text-sm font-medium">Taux d'erreur</span>
                        </div>
                        <p class="text-sm text-muted-foreground ml-4">Pourcentage de documents échoués</p>
                        <div class="flex items-center gap-2">
                          <div class="w-2 h-2 rounded-full bg-warning"></div>
                          <span class="text-sm font-medium">Taux de récupération</span>
                        </div>
                        <p class="text-sm text-muted-foreground ml-4">Pourcentage de documents récupérés après erreur</p>
                      </div>
                    </div>
                  </PopoverContent>
                </Popover>
              </div>
              <CheckCircle2 class="h-4 w-4 text-muted-foreground" />
            </CardHeader>
            <CardContent>
              <div class="text-2xl font-bold">{{ Number(stats.successRate || 0).toFixed(2) }}%</div>
              <Progress :value="Number(stats.successRate || 0)" :max="100" class="mt-2" />
              <div class="flex items-center justify-between text-xs text-muted-foreground mt-1">
                <span>Erreurs: {{ Number(stats.errorRate || 0).toFixed(2) }}%</span>
                <span>Récupération: {{ Number(stats.recoveryRate || 0).toFixed(2) }}%</span>
              </div>
            </CardContent>
          </Card>

          <!-- Storage KPIs -->
          <Card>
            <CardHeader class="flex flex-row items-center justify-between pb-2">
              <CardTitle class="text-sm font-medium">Stockage utilisé</CardTitle>
              <FileType2 class="h-4 w-4 text-muted-foreground" />
            </CardHeader>
            <CardContent>
              <div class="text-2xl font-bold">{{ formatFileSize(stats.totalStorage) }}</div>
              <div class="flex items-center gap-2 mt-1">
                <div class="text-xs text-muted-foreground">
                  Ajouté aujourd'hui: {{ formatFileSize(stats.storageGrowth) }}
                </div>
                <div class="text-xs" :class="stats.storageGrowth > 1024 * 1024 * 10 ? 'text-red-500' : 'text-green-500'">
                  {{ stats.storageGrowth > 1024 * 1024 * 10 ? '↑ Élevée' : '↓ Normale' }}
                </div>
              </div>
            </CardContent>
          </Card>

          <!-- Processing Queue -->
          <Card>
            <CardHeader class="flex flex-row items-center justify-between pb-2">
              <CardTitle class="text-sm font-medium">File en cours</CardTitle>
              <Loader2 class="h-4 w-4 text-muted-foreground" />
            </CardHeader>
            <CardContent>
              <div class="text-2xl font-bold">{{ stats.processingQueue }}</div>
              <div class="text-xs text-muted-foreground mt-1">
                Documents en attente de traitement
              </div>
              <div class="text-xs" :class="stats.processingQueue > 5 ? 'text-yellow-500' : 'text-green-500'">
                {{ stats.processingQueue > 5 ? 'Charge élevée' : 'Charge normale' }}
              </div>
            </CardContent>
          </Card>

          <!-- Language Distribution -->
          <Card>
            <CardHeader class="flex flex-row items-center justify-between pb-2">
              <CardTitle class="text-sm font-medium">Langues détectées</CardTitle>
              <Languages class="h-4 w-4 text-muted-foreground" />
            </CardHeader>
            <CardContent>
              <div class="flex flex-wrap gap-1">
                <span v-for="lang in stats.languagesDetected" :key="lang"
                  class="inline-flex items-center rounded-full bg-primary/10 px-2 py-1 text-xs font-medium">
                  {{ mapLanguageToNative(lang) }}
                </span>
              </div>
              <div class="text-xs text-muted-foreground mt-2">
                {{ stats.languagesDetected.length }} langues détectées
              </div>
            </CardContent>
          </Card>

          <!-- Character Extraction -->
          <Card>
            <CardHeader class="flex flex-row items-center justify-between pb-2">
              <CardTitle class="text-sm font-medium">Caractères extraits</CardTitle>
              <FileType2 class="h-4 w-4 text-muted-foreground" />
            </CardHeader>
            <CardContent>
              <div class="text-2xl font-bold">{{ stats.totalCharacters }}</div>
              <div class="text-xs text-muted-foreground mt-1">
                Taux d'extraction: {{ Number(stats.characterExtractionRate || 0).toFixed(2) }}%
              </div>
              <div class="text-xs" :class="Number(stats.characterExtractionRate || 0) >= 95 ? 'text-green-500' : 'text-yellow-500'">
                {{ Number(stats.characterExtractionRate || 0) >= 95 ? 'Excellent' : 'À améliorer' }}
              </div>
            </CardContent>
          </Card>
        </div>

        <!-- Charts Section -->
        <div class="grid lg:grid-cols-2 gap-6 mb-8">
          <!-- Weekly Activity Area Chart -->
          <Card>
            <CardHeader>
              <CardTitle class="flex items-center gap-2">
                <BarChart3 class="h-5 w-5" />
                Activité hebdomadaire
                <Popover>
                  <PopoverTrigger>
                    <Info class="h-4 w-4 text-muted-foreground cursor-pointer hover:text-primary transition-colors" />
                  </PopoverTrigger>
                  <PopoverContent class="w-80 p-4 bg-background border rounded-lg shadow-lg">
                    <div class="space-y-3">
                      <p class="text-sm text-muted-foreground">
                        Ce graphique montre l'évolution de votre activité sur les 7 derniers jours.
                      </p>
                      <div class="space-y-2">
                        <div class="flex items-center gap-2">
                          <div class="w-2 h-2 rounded-full bg-primary"></div>
                          <span class="text-sm font-medium">Documents</span>
                        </div>
                        <p class="text-sm text-muted-foreground ml-4">Nombre de documents traités par jour</p>
                        <div class="flex items-center gap-2">
                          <div class="w-2 h-2 rounded-full bg-foreground"></div>
                          <span class="text-sm font-medium">Précision</span>
                        </div>
                        <p class="text-sm text-muted-foreground ml-4">Taux de précision moyen de l'OCR par jour (%)</p>
                      </div>
                    </div>
                  </PopoverContent>
                </Popover>
              </CardTitle>
              <CardDescription>Documents traités et précision moyenne</CardDescription>
            </CardHeader>
            <CardContent>
              <AreaChart
                :key="`area-chart-${chartKey}`"
                :data="safeWeeklyActivity"
                index="day"
                :categories="['documents', 'précision']"
                :colors="['hsl(var(--primary))', '#000']"
                :y-formatter="(tick: number | Date) => {
                  if (typeof tick === 'number') {
                    return tick === Math.floor(tick) ? `${tick}` : `${Math.round(tick)}`
                  }
                  return tick.toString()
                }"
                :show-grid-line="true"
                :showXAxis="true"
                :showYAxis="true"
                :showY2Axis="true"
                :yAxisLabel="'Documents'"
                :y2AxisLabel="'Précision (%)'"
                :xAxisLabel="'Jour'"
                :yAxisTickCount="5"
                :y2AxisTickCount="5"
                :xAxisTickCount="7"
                :yAxisMin="0"
                :y2AxisMin="0"
                :yAxisMax="30"
                :y2AxisMax="100"
                :showAxisLabels="true"
                :showAxisTitles="true"
                :axisLabelColor="'black'"
                :axisTitleColor="'black'"
                :axisLineColor="'gray'"
                :axisTickColor="'gray'"
              />

            </CardContent>
          </Card>

          <!-- Monthly Trend Line Chart -->
          <Card>
            <CardHeader>
              <CardTitle class="flex items-center gap-2">
                <BarChart3 class="h-5 w-5" />
                Tendance mensuelle
                <Popover>
                  <PopoverTrigger>
                    <Info class="h-4 w-4 text-muted-foreground cursor-pointer hover:text-primary transition-colors" />
                  </PopoverTrigger>
                  <PopoverContent class="w-80 p-4 bg-background border rounded-lg shadow-lg">
                    <div class="space-y-3">
                      <p class="text-sm text-muted-foreground">
                        Ce graphique présente l'évolution mensuelle de vos documents.
                      </p>
                      <div class="space-y-2">
                        <div class="flex items-center gap-2">
                          <div class="w-2 h-2 rounded-full bg-primary"></div>
                          <span class="text-sm font-medium">Documents actifs</span>
                        </div>
                        <p class="text-sm text-muted-foreground ml-4">Nombre de documents conservés</p>
                        <div class="flex items-center gap-2">
                          <div class="w-2 h-2 rounded-full bg-destructive"></div>
                          <span class="text-sm font-medium">Documents supprimés</span>
                        </div>
                        <p class="text-sm text-muted-foreground ml-4">Nombre de documents supprimés</p>
                      </div>
                    </div>
                  </PopoverContent>
                </Popover>
              </CardTitle>
              <CardDescription>Evolution du nombre de documents</CardDescription>
            </CardHeader>
            <CardContent>
              <LineChart
                :key="`line-chart-${chartKey}`"
                :data="[...safeMonthlyTrends].reverse()"
                index="month"
                :categories="['actifs', 'supprimés']"
                :colors="['hsl(var(--primary))', 'hsl(var(--destructive))']"
                :y-formatter="(value) => `${value} docs`"
                :show-grid-line="true"
                :height="300"
                :show-x-axis="true"
                :show-y-axis="true"
                :y-axis-label="'Nombre de documents'"
                :x-axis-label="'Mois'"
                :y-axis-min="0"
                :y-axis-tick-count="5"
                :show-axis-labels="true"
                :show-axis-titles="true"
                :axis-label-color="'black'"
                :axis-title-color="'black'"
                :axis-line-color="'gray'"
                :axis-tick-color="'gray'"
              />

            </CardContent>
          </Card>

          <!-- Language Distribution Donut Chart -->
          <Card>
            <CardHeader>
              <CardTitle class="flex items-center gap-2">
                <Languages class="h-5 w-5" />
                Distribution des langues
                <Popover>
                  <PopoverTrigger>
                    <Info class="h-4 w-4 text-muted-foreground cursor-pointer hover:text-primary transition-colors" />
                  </PopoverTrigger>
                  <PopoverContent class="w-80 p-4 bg-background border rounded-lg shadow-lg">
                    <div class="space-y-3">
                      <p class="text-sm text-muted-foreground">
                        Ce graphique montre la répartition des documents par langue détectée.
                      </p>
                      <div class="space-y-2">
                        <div class="flex items-center gap-2">
                          <div class="w-2 h-2 rounded-full bg-primary"></div>
                          <span class="text-sm font-medium">Segments</span>
                        </div>
                        <p class="text-sm text-muted-foreground ml-4">Chaque segment représente une langue</p>
                        <div class="flex items-center gap-2">
                          <div class="w-2 h-2 rounded-full bg-foreground"></div>
                          <span class="text-sm font-medium">Pourcentage</span>
                        </div>
                        <p class="text-sm text-muted-foreground ml-4">Proportion de documents dans chaque langue</p>
                      </div>
                    </div>
                  </PopoverContent>
                </Popover>
              </CardTitle>
              <CardDescription>Proportion des documents par langue</CardDescription>
            </CardHeader>
            <CardContent>
              <div class="flex items-center justify-center h-[300px]">
                <DonutChart
                  :key="`donut-chart-${chartKey}`"
                  :data="safeLanguageDistribution"
                  index="name"
                  category="docs"
                  :value-formatter="(value) => `${value} docs`"
                  :show-legend="true"
                  :height="300"
                  :colors="['hsl(var(--primary))', 'hsl(var(--foreground))', 'hsl(var(--destructive))']"
                />

              </div>
            </CardContent>
          </Card>

          <!-- Document Type Distribution -->
          <Card>
            <CardHeader>
              <CardTitle class="flex items-center gap-2">
                <FileType2 class="h-5 w-5" />
                Distribution des types de documents
                <Popover>
                  <PopoverTrigger>
                    <Info class="h-4 w-4 text-muted-foreground cursor-pointer hover:text-primary transition-colors" />
                  </PopoverTrigger>
                  <PopoverContent class="w-80 p-4 bg-background border rounded-lg shadow-lg">
                    <div class="space-y-3">
                      <p class="text-sm text-muted-foreground">
                        Ce graphique montre la répartition des documents par type de fichier.
                      </p>
                      <div class="space-y-2">
                        <div class="flex items-center gap-2">
                          <div class="w-2 h-2 rounded-full bg-primary"></div>
                          <span class="text-sm font-medium">PDF</span>
                        </div>
                        <p class="text-sm text-muted-foreground ml-4">Documents au format PDF</p>
                        <div class="flex items-center gap-2">
                          <div class="w-2 h-2 rounded-full bg-success"></div>
                          <span class="text-sm font-medium">Images</span>
                        </div>
                        <p class="text-sm text-muted-foreground ml-4">Images (JPG, PNG, etc.)</p>
                        <div class="flex items-center gap-2">
                          <div class="w-2 h-2 rounded-full bg-warning"></div>
                          <span class="text-sm font-medium">Scannés</span>
                        </div>
                        <p class="text-sm text-muted-foreground ml-4">Documents numérisés</p>
                        <div class="flex items-center gap-2">
                          <div class="w-2 h-2 rounded-full bg-info"></div>
                          <span class="text-sm font-medium">Autres</span>
                        </div>
                        <p class="text-sm text-muted-foreground ml-4">Autres formats de documents</p>
                      </div>
                    </div>
                  </PopoverContent>
                </Popover>
              </CardTitle>
              <CardDescription>Répartition des documents par format</CardDescription>
            </CardHeader>
            <CardContent>
              <BarChart
                :key="`bar-chart-${chartKey}`"
                :data="safeDocumentTypeDistribution"
                index="type"
                :categories="['nombre']"
                :colors="['hsl(var(--primary))']"
                :value-formatter="(value: number) => `${value} docs`"
                :show-grid-line="true"
                :height="300"
                :show-x-axis="true"
                :show-y-axis="true"
                :y-axis-label="'Nombre de documents'"
                :x-axis-label="'Type de document'"
                :y-axis-min="0"
                :y-axis-tick-count="5"
                :show-axis-labels="true"
                :show-axis-titles="true"
                :axis-label-color="'black'"
                :axis-title-color="'black'"
                :axis-line-color="'gray'"
                :axis-tick-color="'gray'"
              />

            </CardContent>
          </Card>
        </div>
      </div>
    </SidebarInset>
  </SidebarProvider>
</template>