<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Loader2, Scan, FileText, Layers, Hash } from 'lucide-vue-next'

const props = defineProps<{
  isLoading: boolean
  totalPages: number
  isPdf: boolean
}>()

const emit = defineEmits<{
  (e: 'select', pageNumber: number): void
  (e: 'close'): void
}>()

const pageSelectionMode = ref('first')
const customPageNumber = ref(1)

const handleSelect = () => {
  let pageNumber = 0 // Default to first page
  if (pageSelectionMode.value === 'all') {
    pageNumber = -1 // -1 indicates all pages
  } else if (pageSelectionMode.value === 'custom') {
    pageNumber = customPageNumber.value
  }
  emit('select', pageNumber)
  emit('close') // Close the panel immediately after selection
}

const isCustomPageValid = computed(() => {
  if (pageSelectionMode.value !== 'custom') return true
  return customPageNumber.value >= 1 && customPageNumber.value <= props.totalPages
})

const getPageRangeText = computed(() => {
  if (props.totalPages === 1) return '1 page'
  return `1-${props.totalPages} pages`
})

// Add click outside handler
const handleClickOutside = (event: MouseEvent) => {
  const target = event.target as HTMLElement
  // Don't close if clicking on buttons or their children
  if (target.closest('button')) return
  // Don't close if clicking inside the panel
  if (target.closest('.page-selector-panel')) return
  emit('close')
}

onMounted(() => {
  document.addEventListener('click', handleClickOutside)
})

onUnmounted(() => {
  document.removeEventListener('click', handleClickOutside)
})
</script>

<template>
  <div v-if="isPdf" class="fixed inset-0 bg-background/80 backdrop-blur-sm z-50">
    <div class="fixed left-[50%] top-[50%] translate-x-[-50%] translate-y-[-50%] w-full max-w-lg">
      <div class="page-selector-panel bg-background border rounded-lg shadow-lg p-6 space-y-6">
        <div class="space-y-2">
          <h4 class="text-lg font-semibold leading-none tracking-tight">Sélectionner les pages</h4>
          <p class="text-sm text-muted-foreground">
            Document de {{ getPageRangeText }}. Choisissez les pages à traiter.
          </p>
        </div>

        <div class="space-y-4">
          <div class="grid gap-4">
            <div class="flex items-center space-x-2 rounded-lg border border-border p-4 hover:bg-muted/50 cursor-pointer transition-colors"
                 :class="{ 'bg-muted': pageSelectionMode === 'first' }"
                 @click="pageSelectionMode = 'first'">
              <FileText class="h-5 w-5 text-muted-foreground" />
              <div class="flex-1">
                <p class="text-sm font-medium">Première page uniquement</p>
                <p class="text-xs text-muted-foreground">Traiter uniquement la première page du document</p>
              </div>
            </div>

            <div class="flex items-center space-x-2 rounded-lg border border-border p-4 hover:bg-muted/50 cursor-pointer transition-colors"
                 :class="{ 'bg-muted': pageSelectionMode === 'all' }"
                 @click="pageSelectionMode = 'all'">
              <Layers class="h-5 w-5 text-muted-foreground" />
              <div class="flex-1">
                <p class="text-sm font-medium">Toutes les pages</p>
                <p class="text-xs text-muted-foreground">Traiter l'intégralité du document</p>
              </div>
            </div>

            <div class="flex items-center space-x-2 rounded-lg border border-border p-4 hover:bg-muted/50 cursor-pointer transition-colors"
                 :class="{ 'bg-muted': pageSelectionMode === 'custom' }"
                 @click="pageSelectionMode = 'custom'">
              <Hash class="h-5 w-5 text-muted-foreground" />
              <div class="flex-1">
                <p class="text-sm font-medium">Page spécifique</p>
                <p class="text-xs text-muted-foreground">Choisir une page particulière à traiter</p>
              </div>
            </div>
          </div>

          <div v-if="pageSelectionMode === 'custom'" class="space-y-2">
            <Label for="pageNumber" class="text-sm font-medium">Numéro de page</Label>
            <div class="flex items-center gap-2">
              <Input
                id="pageNumber"
                v-model="customPageNumber"
                type="number"
                min="1"
                :max="totalPages"
                class="w-24"
                :class="{ 'border-destructive': !isCustomPageValid }"
              />
              <span class="text-sm text-muted-foreground">sur {{ totalPages }}</span>
            </div>
            <p v-if="!isCustomPageValid" class="text-xs text-destructive">
              Le numéro de page doit être entre 1 et {{ totalPages }}
            </p>
          </div>
        </div>

        <div class="flex justify-end gap-2">
          <Button 
            variant="outline"
            @click="$emit('close')"
          >
            Annuler
          </Button>
          <Button 
            @click="handleSelect" 
            :disabled="isLoading || (pageSelectionMode === 'custom' && !isCustomPageValid)"
          >
            <Loader2 v-if="isLoading" class="h-4 w-4 animate-spin mr-2" />
            <Scan v-else class="h-4 w-4 mr-2" />
            {{ isLoading ? 'Traitement en cours...' : 'Lancer OCR' }}
          </Button>
        </div>
      </div>
    </div>
  </div>
</template> 