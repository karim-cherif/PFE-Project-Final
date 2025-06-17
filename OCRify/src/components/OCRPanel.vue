<template>
  <Dialog :open="true" @update:open="handleClose">
    <DialogContent class="max-w-[90vw] h-[90vh] flex flex-col">
      <!-- Toast Container -->
      <div class="absolute top-4 right-4 z-50">
        <div v-if="toastMessage" class="bg-background border rounded-lg shadow-lg p-4 mb-2" :class="{
          'border-green-500': toastType === 'success',
          'border-red-500': toastType === 'error',
          'border-yellow-500': toastType === 'warning'
        }">
          <div class="flex items-center gap-2">
            <Check v-if="toastType === 'success'" class="h-4 w-4 text-green-500" />
            <AlertCircle v-else-if="toastType === 'error'" class="h-4 w-4 text-destructive" />
            <AlertTriangle v-else-if="toastType === 'warning'" class="h-4 w-4 text-yellow-500" />
            <span :class="{
              'text-green-500': toastType === 'success',
              'text-destructive': toastType === 'error',
              'text-yellow-500': toastType === 'warning'
            }">{{ toastMessage }}</span>
          </div>
          <!-- Add acknowledgment button for persistent warnings -->
          <div v-if="isPersistentToast" class="mt-2 flex justify-end">
            <Button 
              variant="outline" 
              size="sm" 
              class="text-yellow-500 hover:text-yellow-600 hover:bg-yellow-50"
              @click="dismissToast"
            >
              J'ai compris
            </Button>
          </div>
        </div>
      </div>
      <DialogHeader>
        <DialogTitle>Éditeur de texte OCR</DialogTitle>
        <DialogDescription>
          Modifiez et révisez le texte OCR avec les suggestions générées par l'IA.
        </DialogDescription>
        <div class="flex justify-start mt-2">
          <Button 
            variant="secondary"  
            class="gap-2"
            :disabled="isLoading"
            @click="startOcr"
          >
            <Loader2 v-if="isLoading" class="h-4 w-4 animate-spin" />
            <Scan v-else class="h-4 w-4" />
            {{ isLoading ? 'Traitement en cours...' : 'Lancer OCR' }}
          </Button>
        </div>
      </DialogHeader>

      <!-- Page Selector Panel -->
      <PageSelector
        v-if="showPageSelection"
        :is-loading="isLoading"
        :total-pages="totalPages"
        :is-pdf="isPdf"
        @select="handlePageSelection"
        @close="showPageSelection = false"
      />

      <div class="flex-1 flex gap-6 overflow-hidden">
        <!-- Document Preview Panel (Left) -->
        <div class="w-1/3 h-full flex flex-col border rounded-lg overflow-hidden">
          <div class="flex-1 overflow-auto p-4">
            <div class="bg-muted rounded-lg overflow-hidden h-full flex flex-col">
              <!-- Zoom Controls -->
              <div class="flex justify-end gap-2 p-2 border-b">
                <Button
                  variant="outline"
                  size="sm"
                  @click="zoomOut"
                  :disabled="zoomLevel <= 0.5"
                >
                  <Minus class="h-4 w-4" />
                </Button>
                <span class="flex items-center text-sm">{{ Math.round(zoomLevel * 100) }}%</span>
                <Button
                  variant="outline"
                  size="sm"
                  @click="zoomIn"
                  :disabled="zoomLevel >= 2"
                >
                  <Plus class="h-4 w-4" />
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  @click="resetZoom"
                >
                  <RefreshCw class="h-4 w-4" />
                </Button>
              </div>
              
              <!-- PDF Preview -->
              <div v-if="selectedDocument.type?.toLowerCase().includes('pdf')" class="w-full h-full overflow-auto">
                <iframe
                  :src="documentUrl"
                  class="w-full h-full"
                  :style="{ transform: `scale(${zoomLevel})`, transformOrigin: '0 0' }"
                  frameborder="0"
                ></iframe>
              </div>
              
              <!-- Image Preview -->
              <div v-else-if="selectedDocument.type?.toLowerCase().includes('image')" class="w-full h-full overflow-auto">
                <div class="relative w-full h-full">
                  <img
                    :src="documentUrl"
                    :alt="selectedDocument.name"
                    class="w-full h-full object-contain"
                    :style="{ transform: `scale(${zoomLevel})`, transformOrigin: '0 0' }"
                    @load="imageLoading = false"
                  />
                  <div v-if="imageLoading" class="absolute inset-0 flex items-center justify-center bg-muted/50">
                    <Loader2 class="h-8 w-8 animate-spin text-muted-foreground" />
                  </div>
                  <!-- Fallback icon only shown on error -->
                  <div class="absolute inset-0 items-center justify-center bg-muted/50 hidden">
                    <div class="flex flex-col items-center">
                      <ImageIcon class="h-12 w-12 text-muted-foreground mb-2" />
                    </div>
                  </div>
                </div>
              </div>
              
              <!-- Default Preview -->
              <div v-else class="w-full h-full flex flex-col items-center justify-center p-8">
                <div class="w-24 h-24 bg-gray-100 dark:bg-gray-800 rounded-lg flex items-center justify-center mb-4">
                  <FileText class="h-12 w-12 text-gray-600 dark:text-gray-400" />
                </div>
                <span class="text-sm text-muted-foreground">{{ selectedDocument.type || 'Document' }}</span>
              </div>
            </div>
          </div>
        </div>

        <!-- OCR Text Editor Panel (Middle) -->
        <div class="w-1/3 h-full flex flex-col relative">
          <!-- Action Buttons -->
          <div class="flex justify-end gap-2 p-2 border-b">
            <div class="relative">
              <Button 
                variant="outline" 
                size="sm"
                @click.stop="showExportMenu = !showExportMenu"
                :disabled="!editorContent"
              >
                <FileText class="h-4 w-4 mr-2" />
                Exporter
                <ChevronDown class="h-4 w-4 ml-2" />
              </Button>
              <!-- Export Menu -->
              <div 
                v-if="showExportMenu" 
                class="absolute right-0 mt-2 w-48 bg-popover text-popover-foreground rounded-lg shadow-lg border z-[9999]"
              >
                <button 
                  class="w-full text-left px-4 py-2 text-sm hover:bg-muted rounded-t-lg flex items-center gap-2"
                  @click.stop="() => { exportToDoc(); showExportMenu = false; }"
                >
                  <FileText class="h-4 w-4" />
                  Exporter en DOC
                </button>
                <button 
                  class="w-full text-left px-4 py-2 text-sm hover:bg-muted flex items-center gap-2"
                  @click.stop="() => { exportToPdf(); showExportMenu = false; }"
                >
                  <FileText class="h-4 w-4" />
                  Exporter en PDF
                </button>
                <button 
                  class="w-full text-left px-4 py-2 text-sm hover:bg-muted rounded-b-lg flex items-center gap-2"
                  @click.stop="() => { copyToClipboard(); showExportMenu = false; }"
                >
                  <Copy class="h-4 w-4" />
                  Copier
                </button>
              </div>
            </div>
            <!-- Add toggle button for markdown preview -->
            <Button 
              variant="outline" 
              size="sm" 
              @click="toggleMarkdownPreview"
              :title="showMarkdown ? 'Voir le mode édition' : 'Voir la version markdown'"
            >
              <Eye v-if="!showMarkdown" class="h-4 w-4 mr-2" />
              <Edit v-else class="h-4 w-4 mr-2" />
              {{ showMarkdown ? 'Mode édition' : 'Markdown' }}
            </Button>
          </div>

          <!-- OCR Text Editor -->
          <div class="flex-1 overflow-auto border rounded-lg bg-muted/30 relative h-[calc(100%-40px)]">
            <!-- Loading State -->
            <div v-if="isLoading" class="w-full h-full flex flex-col items-center justify-center p-8">
              <Loader2 class="h-8 w-8 animate-spin text-muted-foreground mb-4" />
              <span class="text-sm text-muted-foreground">{{ loadingMessage }}</span>
              <span class="text-xs text-muted-foreground mt-2">{{ formattedElapsedTime }}</span>
            </div>

            <!-- Markdown Preview -->
            <div 
              v-else-if="showMarkdown" 
              class="w-full h-full p-4 text-sm prose dark:prose-invert max-w-none"
              :dir="isRTL(editorContent) ? 'rtl' : 'ltr'"
              v-html="markdownContent"
            ></div>

            <!-- Editable Text Div -->
            <div
              v-else
              :contenteditable="!!editorContent"
              ref="editableTextRef"
              @input="syncEditorContent"
              @click="handleWordClick"
              v-html="editorContent ? renderedContent : 'Le texte extrait par OCR sera affiché ici...'"
              class="w-full h-full p-4 text-sm font-mono border border-transparent bg-transparent focus:outline-none relative"
              :class="{ 
                'text-muted-foreground': !editorContent,
                'cursor-not-allowed': !editorContent,
                'bg-muted/20': !editorContent
              }"
            ></div>

            <!-- Floating Action Button -->
            <div v-if="editorContent && !isLoading && !showPageSelection" class="fixed bottom-8 left-8 z-50">
              <div class="relative floating-action-button flex items-center gap-6">
                <button 
                  class="rounded-full shadow-lg hover:shadow-xl transition-all bg-primary text-primary-foreground w-12 h-12 relative flex items-center justify-center"
                  @click="toggleActionMenu"
                >
                  <AlertCircle class="h-6 w-6" />
                  <span v-if="errorCount > 0" class="absolute -top-2 -right-2 bg-destructive text-destructive-foreground text-xs rounded-full h-6 w-6 flex items-center justify-center error-badge">
                    {{ errorCount }}
                  </span>
                </button>

                <!-- Legend for corrected words -->
                <div class="flex items-center gap-2 text-xs text-muted-foreground">
                  <div class="flex items-center gap-1">
                    <span class="w-4 h-0.5 bg-destructive"></span>
                    <span>Mot corrigé</span>
                  </div>
                </div>

                <!-- Action Menu -->
                <div v-if="showActionMenu" class="absolute bottom-full left-0 mb-2 bg-popover text-popover-foreground rounded-lg shadow-lg p-2 min-w-[200px] border">
                  <div class="text-sm font-medium px-2 py-1 mb-2 border-b">
                    {{ errorCount }} erreur(s) détectée(s)
                  </div>
                  <button 
                    v-if="errorCount > 0"
                    class="w-full text-left px-2 py-1.5 text-sm hover:bg-muted rounded flex items-center gap-2"
                    @click="ignoreAllCorrections"
                  >
                    <Trash2 class="h-4 w-4" />
                    Ignorer tout
                  </button>
                </div>
              </div>
            </div>

            <!-- Suggestions Dropdown -->
            <div v-if="selectedWord && dropdownPosition" 
                class="suggestion-dropdown" 
                :style="dropdownStyle"
                @mouseenter="isHoveringDropdown = true"
                @mouseleave="isHoveringDropdown = false">
              <div class="p-1 text-xs text-muted-foreground border-b mb-1">
                Cliquez pour remplacer le mot
              </div>
              <div class="max-h-[200px] overflow-y-auto">
                <button
                  v-for="suggestion in getSuggestionsForWord(selectedWord)"
                  :key="suggestion.text"
                  @click="applySuggestion(suggestion.text)"
                  class="suggestion-item"
                  :class="{ 'tesseract-suggestion': suggestion.text.startsWith('(tes)') }"
                  :dir="isRTL(suggestion.text) ? 'rtl' : 'ltr'"
                >
                  <span class="suggestion-text">
                    {{ suggestion.text }}
                  </span>
                  <span class="confidence-badge">
                    {{ suggestion.confidence === 1.0 && suggestion.text === selectedSuggestion?.word ? 'mot original' : Math.round(suggestion.confidence * 100) + '%' }}
                  </span>
                </button>
                <button @click="ignoreSuggestion()" class="suggestion-item text-gray-500">
                  <div class="flex items-center gap-2 w-full">
                    <Trash2 class="h-4 w-4" />
                    <span>Ignorer</span>
                  </div>
                </button>
              </div>
            </div>
          </div>
        </div>

        <!-- Summary Panel (Right) -->
        <div class="w-1/3 h-full flex flex-col">
          <div class="p-2 border-b flex items-center justify-end gap-1">
            <Button
              variant="outline"
              size="xs"
              @click="generateSummary"
              :disabled="isLoading || isSummaryLoading || !editorContent"
            >
              <Text class="h-3 w-3 mr-1" />
              <span class="text-xs">Générer</span>
            </Button>
            <div class="relative">
              <Button 
                variant="outline" 
                size="xs"
                @click.stop="showSummaryExportMenu = !showSummaryExportMenu"
                :disabled="!summaryContent"
              >
                <FileText class="h-3 w-3 mr-1" />
                <span class="text-xs">Exporter</span>
                <ChevronDown class="h-3 w-3 ml-1" />
              </Button>
              <!-- Summary Export Menu -->
              <div 
                v-if="showSummaryExportMenu" 
                class="absolute right-0 mt-2 w-48 bg-popover text-popover-foreground rounded-lg shadow-lg border z-[9999]"
              >
                <button 
                  class="w-full text-left px-4 py-2 text-sm hover:bg-muted rounded-t-lg flex items-center gap-2"
                  @click.stop="() => { exportSummaryToDoc(); showSummaryExportMenu = false; }"
                >
                  <FileText class="h-4 w-4" />
                  Exporter en DOC
                </button>
                <button 
                  class="w-full text-left px-4 py-2 text-sm hover:bg-muted flex items-center gap-2"
                  @click.stop="() => { exportSummaryToPdf(); showSummaryExportMenu = false; }"
                >
                  <FileText class="h-4 w-4" />
                  Exporter en PDF
                </button>
                <button 
                  class="w-full text-left px-4 py-2 text-sm hover:bg-muted rounded-b-lg flex items-center gap-2"
                  @click.stop="() => { copySummaryToClipboard(); showSummaryExportMenu = false; }"
                >
                  <Copy class="h-4 w-4" />
                  Copier
                </button>
              </div>
            </div>
            <Button
              variant="outline"
              size="xs"
              @click="toggleSummaryMarkdown"
              :title="showSummaryMarkdown ? 'Voir le mode édition' : 'Voir la version markdown'"
            >
              <Eye v-if="!showSummaryMarkdown" class="h-3 w-3 mr-1" />
              <Edit v-else class="h-3 w-3 mr-1" />
              <span class="text-xs">{{ showSummaryMarkdown ? 'Édition' : 'Markdown' }}</span>
            </Button>
          </div>
          
          <div class="flex-1 overflow-hidden">
            <div v-if="isSummaryLoading" class="w-full h-full flex flex-col items-center justify-center p-8">
              <Loader2 class="h-8 w-8 animate-spin text-muted-foreground mb-4" />
              <span class="text-sm text-muted-foreground">Génération du résumé en cours...</span>
              <span class="text-xs text-muted-foreground mt-2">{{ formattedSummaryElapsedTime }}</span>
            </div>
            
            <div v-else-if="error" class="w-full h-full flex flex-col items-center justify-center p-8">
              <AlertCircle class="h-8 w-8 text-destructive mb-4" />
              <span class="text-sm text-destructive">{{ error }}</span>
            </div>
            
            <div v-else-if="!summaryContent" class="w-full h-full border rounded-lg bg-muted/30">
              <div
                class="w-full h-full p-4 text-sm font-mono border border-transparent bg-transparent focus:outline-none relative text-muted-foreground cursor-not-allowed"
              >
                Le résumé généré sera affiché ici...
              </div>
            </div>
            
            <div v-else class="w-full h-full border rounded-lg bg-muted/30 relative">
              <!-- Markdown Preview -->
              <div 
                v-if="showSummaryMarkdown" 
                class="w-full h-full p-4 text-sm prose dark:prose-invert max-w-none overflow-auto"
                :dir="isRTL(summaryContent) ? 'rtl' : 'ltr'"
                v-html="summaryMarkdownContent"
              ></div>
              
              <!-- Editable Text Div -->
              <div
                v-else
                :contenteditable="!!summaryContent"
                ref="summaryEditorRef"
                @input="syncSummaryContent"
                v-html="summaryContent ? summaryContent : 'Le résumé généré sera affiché ici...'"
                class="w-full h-full p-4 text-sm font-mono border border-transparent bg-transparent focus:outline-none relative overflow-auto"
                :class="{ 
                  'text-muted-foreground': !summaryContent,
                  'cursor-not-allowed': !summaryContent,
                  'bg-muted/20': !summaryContent
                }"
                :dir="isRTL(summaryContent) ? 'rtl' : 'ltr'"
                :style="{
                  unicodeBidi: isRTL(summaryContent) ? 'bidi-override' : 'normal',
                  direction: isRTL(summaryContent) ? 'rtl' : 'ltr'
                }"
              ></div>
            </div>
          </div>
        </div>
      </div>

      <DialogFooter class="mt-4 flex justify-between items-center">
        <div v-if="hasStartedOcr" class="flex items-center gap-4 text-sm text-muted-foreground">
          <div class="flex items-center gap-2">
            <Popover>
              <PopoverTrigger asChild>
                <button class="p-0 hover:bg-transparent flex items-center gap-2">
                  <Clock class="h-4 w-4" />
                  <span>{{ formattedProcessingTime }}</span>
                </button>
              </PopoverTrigger>
              <PopoverContent 
                side="top" 
                align="start" 
                :sideOffset="5" 
                class="w-60 p-3 bg-background border rounded-lg shadow-lg z-[9999]"
                :style="{ bottom: 'calc(100% + 5px)', left: '0px' }"
              >
                <div class="space-y-2 text-left">
                  <div class="flex items-center gap-2">
                     <Clock class="h-4 w-4 text-primary" />
                     <h4 class="font-semibold text-sm">Temps de traitement</h4>
                  </div>
                  <p class="text-xs text-muted-foreground">
                    Durée écoulée pour le traitement OCR du document.
                  </p>
                </div>
              </PopoverContent>
            </Popover>
          </div>
          <div class="flex items-center gap-2 group relative">
            <Target class="h-4 w-4" />
            <div class="relative inline-block">
              <Popover>
                <PopoverTrigger asChild>
                  <button class="p-0 hover:bg-transparent flex items-center gap-1 cursor-pointer">
                    <span>Confiance: {{ precision }}%</span>
                  </button>
                </PopoverTrigger>
                <PopoverContent 
                  side="top" 
                  align="end" 
                  :sideOffset="5"
                  class="w-80 p-4 bg-background border rounded-lg shadow-lg z-[9999] text-left"
                  :style="{ bottom: 'calc(100% + 5px)', right: '0px' }"
                >
                  <div class="space-y-4 text-left">
                    <div class="flex items-center gap-2 border-b pb-2 mb-3">
                      <Info class="h-4 w-4 text-primary" />
                      <h4 class="font-semibold text-base">Score de confiance</h4>
                    </div>
                    <p class="text-sm text-muted-foreground">
                      Ce score indique la fiabilité globale de la reconnaissance de texte.
                    </p>
                    <div class="space-y-3">
                      <div class="space-y-1">
                        <div class="flex items-center gap-2">
                          <div class="w-2 h-2 rounded-full bg-primary flex-shrink-0"></div>
                          <span class="text-sm font-medium">Qualité de l'image</span>
                        </div>
                        <p class="text-sm text-muted-foreground ml-4">Clarté et résolution de l'image source</p>
                      </div>
                      <div class="space-y-1">
                        <div class="flex items-center gap-2">
                          <div class="w-2 h-2 rounded-full bg-destructive flex-shrink-0"></div>
                          <span class="text-sm font-medium">Erreurs détectées</span>
                        </div>
                        <p class="text-sm text-muted-foreground ml-4">Nombre de mots nécessitant une correction</p>
                      </div>
                      <div class="space-y-1">
                        <div class="flex items-center gap-2">
                          <div class="w-2 h-2 rounded-full bg-warning flex-shrink-0" style="background-color: yellow !important;"></div>
                          <span class="text-sm font-medium">Complexité du texte</span>
                        </div>
                        <p class="text-sm text-muted-foreground ml-4">Difficulté de reconnaissance du contenu</p>
                      </div>
                    </div>
                  </div>
                </PopoverContent>
              </Popover>
            </div>
          </div>
        </div>
        <div class="flex gap-2">
          <Button variant="outline" @click="$emit('close')">Fermer</Button>
          <Button @click="handleSave">Enregistrer</Button>
        </div>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>

<script setup lang="ts">
import { ref, computed, watch, nextTick, onMounted, onUnmounted } from 'vue'
import { useToast } from '@/components/ui/toast/use-toast'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { FileText, Image as ImageIcon, Loader2, X, Scan, Copy, Trash2, AlertCircle, Check, Languages, Clock, Target, Eye, Edit, ChevronDown, Text, Minus, Plus, RefreshCw, AlertTriangle, Info } from 'lucide-vue-next'
import { documents, ocrStatistics } from '@/services/api'
import { jsPDF } from 'jspdf'
import type { CSSProperties } from 'vue';
import { ocr } from '@/services/api'
import { marked } from 'marked'
import PageSelector from '@/components/ui/page-selector/PageSelector.vue'
import { PDFDocument } from 'pdf-lib'
import html2pdf from 'html2pdf.js'
import { api } from '@/services/api'
import * as katex from 'katex'
import 'katex/dist/katex.min.css'
import MathRenderer from './MathRenderer.vue'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'

// Type Definitions
interface Suggestion {
  text: string;
  confidence: number;
}

interface WordSuggestion {
  word: string;
  suggestions: Suggestion[];
  confidence: number;
  start: number;
  end: number;
}

interface Document {
  id: string;
  name: string;
  status: string;
  type: string;
  documentUrl: string;
  language?: string;
}

interface Props {
  initialContent: string
  ocrSuggestions?: WordSuggestion[]
  selectedDocument: Document
  onUpdateContent?: (content: string) => void
  onSave?: () => void
  onClose?: () => void
  onRefresh?: () => void
}

const props = withDefaults(defineProps<Props>(), {
  initialContent: '',
})

// Emits
const emit = defineEmits<{
  'update:content': [value: string]
  'save': []
  'close': []
  'refresh': []
  'launchOcr': []
}>()

// State
const editorContent = ref(props.initialContent)
const selectedWord = ref<string | null>(null)
const selectedSuggestion = ref<{ word: string; start: number; end: number } | null>(null)
const dropdownPosition = ref<{ top: number; left: number } | null>(null)
const editableTextRef = ref<HTMLElement | null>(null)
const { toast } = useToast()
const imageLoading = ref(true)
const documentUrl = ref('')
const localSuggestions = ref<WordSuggestion[]>([]);
const originalSuggestions = ref<Array<{
  word: string
  suggestions: string[]
  start: number
  end: number
  confidence: number
}>>([])
const correctedWords = ref<Array<{
  originalWord: string
  correctedWord: string
  start: number
  end: number
}>>([])
const isLoading = ref(false)
const isHoveringDropdown = ref(false)
const showActionMenu = ref(false)
const errorCount = computed(() => localSuggestions.value.length)
const startTime = ref<number>(0)
const processingTime = ref<string>('0.00')
const precision = ref<string>('0.00')
const hasStartedOcr = ref(false)
const toastMessage = ref<string | null>(null);
const toastType = ref<'success' | 'error' | 'warning'>('success');
const isPersistentToast = ref(false);
const showMarkdown = ref(true)
const markdownContent = ref('')
const showPageSelection = ref(false)
const totalPages = ref(1)
const showExportMenu = ref(false)
const loadingMessage = ref('Traitement du document en cours...')
const messageTimeout = ref<number | null>(null)
const showSummary = ref(false)
const summaryEditorRef = ref<HTMLElement | null>(null)
const summaryContent = ref<string>('')
const isSummaryLoading = ref(false)
const error = ref<string | null>(null)
const elapsedTime = ref(0)
const summaryElapsedTime = ref(0)
let timerInterval: number | null = null
let summaryTimerInterval: number | null = null
const showSummaryMarkdown = ref(true)
const showSummaryExportMenu = ref(false)
const summaryMarkdownContent = ref('')

// Add zoom state
const zoomLevel = ref(1)

// Add zoom methods
const zoomIn = () => {
  if (zoomLevel.value < 2) {
    zoomLevel.value += 0.1
  }
}

const zoomOut = () => {
  if (zoomLevel.value > 0.5) {
    zoomLevel.value -= 0.1
  }
}

const resetZoom = () => {
  zoomLevel.value = 1
}

// Define language mappings at component level
const backendLanguageMap: { [key: string]: number } = {
  'English': 0,
  'French': 1,
  'Arabic': 2
};

const ocrLanguageMap: { [key: string]: string } = {
  'english': 'eng',
  'french': 'fra',
  'arabic': 'ara',
  'Arabic': 'ara',
  'English': 'eng',
  'French': 'fra',
  'arabic (ar)': 'ara',
  'english (en)': 'eng',
  'french (fr)': 'fra',
  'ara': 'ara',
  'eng': 'eng',
  'fra': 'fra'
};

// Computed
interface TextPart {
  text: string
  isSuggestion?: boolean
  word?: string
  start?: number
  end?: number
  confidence: number
}

const textParts = computed<TextPart[]>(() => {
  if (!editorContent.value) {
    return [{ text: editorContent.value || '', confidence: 1 }];
  }

  const parts: TextPart[] = [];
  let lastIndex = 0;
  const text = editorContent.value;

  console.log('🔍 [Debug] Starting textParts computation:', {
      text,
      correctedWords: correctedWords.value,
      localSuggestions: localSuggestions.value
  });

  // First, handle corrected words
  correctedWords.value.forEach(correctedWord => {
      console.log('🔍 [Debug] Processing corrected word:', correctedWord);
      
      // Add text before the corrected word
      if (correctedWord.start > lastIndex) {
          parts.push({ 
              text: text.slice(lastIndex, correctedWord.start), 
              confidence: 1 
          });
      }

      // Add the corrected word
      parts.push({
          text: correctedWord.correctedWord,
          isSuggestion: true,
          word: correctedWord.originalWord,
          start: correctedWord.start,
          end: correctedWord.start + correctedWord.correctedWord.length,
          confidence: 1
      });

      lastIndex = correctedWord.start + correctedWord.correctedWord.length;
  });

  // Then handle remaining suggestions that haven't been corrected
  const sortedSuggestions = [...localSuggestions.value]
      .filter(suggestion => !correctedWords.value.some(cw => 
          cw.originalWord === suggestion.word && 
          cw.start === suggestion.start && 
          cw.end === suggestion.end
      ))
      .sort((a, b) => a.start - b.start);

  console.log('🔍 [Debug] Processing remaining suggestions:', sortedSuggestions);

  sortedSuggestions.forEach(suggestion => {
      // Skip if this suggestion's position overlaps with a corrected word
      if (correctedWords.value.some(cw => 
          (suggestion.start >= cw.start && suggestion.start < cw.start + cw.correctedWord.length) ||
          (suggestion.end > cw.start && suggestion.end <= cw.start + cw.correctedWord.length)
      )) {
          return;
      }

      // Add text before the suggestion if there's a gap
      if (suggestion.start > lastIndex) {
          parts.push({ 
              text: text.slice(lastIndex, suggestion.start), 
              confidence: 1 
          });
      }

      // Add the suggestion
      parts.push({
          text: text.slice(suggestion.start, suggestion.end),
          isSuggestion: true,
          word: suggestion.word,
          start: suggestion.start,
          end: suggestion.end,
          confidence: suggestion.confidence
      });

      lastIndex = suggestion.end;
  });

  // Add any remaining text after the last suggestion
  if (lastIndex < text.length) {
      parts.push({ 
          text: text.slice(lastIndex), 
          confidence: 1 
      });
  }

  console.log('🔍 [Debug] Final text parts:', parts);

  return parts;
});

const dropdownStyle = computed<CSSProperties>(() => {
  if (!dropdownPosition.value) return {};

  // Get the editor container dimensions
  const editorRect = editableTextRef.value?.getBoundingClientRect();
  if (!editorRect) return {};

  // Calculate available space
  const availableSpace = editorRect.width - dropdownPosition.value.left;
  const dropdownWidth = 200; // Approximate width of the dropdown

  // Adjust position if dropdown would overflow
  let left = dropdownPosition.value.left;
  if (availableSpace < dropdownWidth) {
    left = editorRect.width - dropdownWidth;
  }
  // Clamp left to 0 so it never overflows the left edge
  if (left < 0) {
    left = 0;
  }

  return {
    top: `${dropdownPosition.value.top}px`,
    left: `${left}px`,
    position: 'absolute',
    zIndex: 9999,
    background: 'white',
    boxShadow: '0 2px 8px rgba(0, 0, 0, 0.1)',
    padding: '0.25rem',
    borderRadius: '0.5rem',
    minWidth: '150px',
    maxWidth: '200px'
  };
});

// Add this helper function before the ocrResponse object
const findWordPosition = (text: string, word: string): { start: number; end: number } => {
  const index = text.indexOf(word);
  if (index === -1) {
    console.warn(`Word "${word}" not found in text`);
    return { start: 0, end: 0 }; // Return default position if word not found
  }
  return {
    start: index,
    end: index + word.length
  };
};


// Add this computed property after the other computed properties
const formattedProcessingTime = computed(() => {
  const time = parseFloat(processingTime.value);
  if (time >= 60) {
    const minutes = Math.floor(time / 60);
    const seconds = Math.round(time % 60);
    return `${minutes}m ${seconds}s`;
  }
  return `${time.toFixed(2)}s`;
});

const formattedElapsedTime = computed(() => {
  const minutes = Math.floor(elapsedTime.value / 60)
  const seconds = elapsedTime.value % 60
  return `${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`
})

const formattedSummaryElapsedTime = computed(() => {
  const minutes = Math.floor(summaryElapsedTime.value / 60)
  const seconds = summaryElapsedTime.value % 60
  return `${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`
})

// Methods

// Transform OCR service response to match frontend expectations
const transformOcrSuggestions = (suggestions: any[]): WordSuggestion[] => {
  if (!Array.isArray(suggestions)) {
    console.warn('🔍 [transformOcrSuggestions] Invalid suggestions format, returning empty array');
    return [];
  }

  return suggestions.map(suggestion => {
    // Handle different suggestion formats from OCR services
    let transformedSuggestions: Suggestion[] = [];

    if (Array.isArray(suggestion.suggestions)) {
      transformedSuggestions = suggestion.suggestions.map((s: any) => {
        if (typeof s === 'string') {
          return { text: s, confidence: suggestion.confidence || 0.5 };
        } else if (s && typeof s === 'object' && s.text) {
          return { text: s.text, confidence: s.confidence || suggestion.confidence || 0.5 };
        }
        return { text: String(s), confidence: suggestion.confidence || 0.5 };
      });
    } else if (typeof suggestion.suggestions === 'string') {
      transformedSuggestions = [{ text: suggestion.suggestions, confidence: suggestion.confidence || 0.5 }];
    }

    return {
      word: suggestion.word || '',
      suggestions: transformedSuggestions,
      confidence: suggestion.confidence || 0.5,
      start: suggestion.start || 0,
      end: suggestion.end || 0
    };
  }).filter(s => s.word && s.suggestions.length > 0);
};

// Add this helper function to detect RTL text
const isRTL = (text: string): boolean => {
  const rtlRegex = /[\u0591-\u07FF\u200F\u202B\u202E\uFB1D-\uFDFD\uFE70-\uFEFC]/;
  return rtlRegex.test(text);
};

const handleWordClick = (event: MouseEvent) => {
  const target = event.target as HTMLElement;
  if (target.tagName === 'SPAN' && (target.dataset.word || target.dataset.corrected)) {
    let word = target.dataset.word || '';
    const start = Number(target.dataset.start);
    const end = Number(target.dataset.end);
    const isRtlText = isRTL(word);

    // If this is a corrected word, find the original word
    const correction = correctedWords.value.find(
      cw => cw.correctedWord === word && cw.start === start && cw.end === end
    );
    if (correction) {
      word = correction.originalWord;
    }

    selectedWord.value = word;
    selectedSuggestion.value = { word, start, end };

    // Get the position relative to the viewport
    const rect = target.getBoundingClientRect();
    const editorRect = editableTextRef.value?.getBoundingClientRect();
    
    if (editorRect) {
      // Calculate position relative to the editor
      const top = rect.bottom - editorRect.top + 5; // Add 5px padding
      
      // Calculate left position with edge detection
      let left;
      const dropdownWidth = 200; // Approximate width of the dropdown
      const editorLeft = rect.left - editorRect.left;
      const editorRight = editorRect.right - rect.right;
      
      if (isRtlText) {
        // For RTL text, position from the right
        if (editorRight < dropdownWidth) {
          // If near right edge, align with right edge
          left = editorRect.width - dropdownWidth;
        } else {
          // Otherwise position from the right of the word
          left = rect.right - editorRect.left - dropdownWidth;
        }
      } else {
        // For LTR text, position from the left
        if (editorLeft < dropdownWidth) {
          // If near left edge, align with left edge
          left = 0;
        } else {
          // Otherwise position from the left of the word
          left = rect.left - editorRect.left;
        }
      }

      dropdownPosition.value = {
        top,
        left
      };
    }
  }
};

// Add click outside handler for dropdown
const handleClickOutside = (event: MouseEvent) => {
  if (!event.target) return;
  const target = event.target as HTMLElement;
  const dropdown = document.querySelector('.suggestion-dropdown');
  const wordSpan = document.querySelector('.corrected-word, .highlighted-word');
  
  // If click is outside both the dropdown and the word span
  if (!target.closest('.suggestion-dropdown') && !target.closest('.corrected-word, .highlighted-word')) {
    selectedWord.value = null;
    dropdownPosition.value = null;
  }
};

const handleWordLeave = () => {
  // Add a small delay before hiding the dropdown to allow moving to it
  setTimeout(() => {
    if (!isHoveringDropdown.value) {
      selectedWord.value = null;
      dropdownPosition.value = null;
    }
  }, 100);
};

const renderedContent = computed(() => {
  if (!editorContent.value) {
    return 'Le texte extrait par OCR sera affiché ici...';
  }

  console.log('🔍 [renderedContent] Starting with editor content:', editorContent.value);

  const isRtlText = isRTL(editorContent.value);
  const containerDir = isRtlText ? 'rtl' : 'ltr';

  // Merge corrections and suggestions into a single list
  const entries = [
    ...correctedWords.value.map(cw => ({
      type: 'correction',
      start: cw.start,
      end: cw.end,
      word: cw.originalWord,
      correctedWord: cw.correctedWord
    })),
    ...localSuggestions.value
      .filter(suggestion => !correctedWords.value.some(cw =>
        cw.start === suggestion.start && cw.end === suggestion.end
      ))
      .map(s => ({
        type: 'suggestion',
        start: s.start,
        end: s.end,
        word: s.word,
        confidence: s.confidence
      }))
  ];

  console.log('🔍 [renderedContent] Processed entries:', entries);

  // Sort by start index
  entries.sort((a, b) => a.start - b.start);

  let result = '';
  let currentPos = 0;

  for (const entry of entries) {
    // Skip if this entry overlaps with already processed text
    if (entry.start < currentPos) continue;
    // Add text before the entry
    if (entry.start > currentPos) {
      const textBefore = editorContent.value.substring(currentPos, entry.start);
      // Replace newlines with <br> tags
      result += textBefore.replace(/\n/g, '<br>');
    }
    if (entry.type === 'correction') {
      result += `<span class="corrected-word" data-word="${entry.word}" data-start="${entry.start}" data-end="${entry.end}" data-corrected="true">${(entry as any).correctedWord}</span>`;
    } else if (entry.type === 'suggestion') {
      result += `<span class="highlighted-word" data-word="${entry.word}" data-start="${entry.start}" data-end="${entry.end}">${editorContent.value.substring(entry.start, entry.end)}</span>`;
    }
    currentPos = entry.end;
  }
  // Add any remaining text after the last entry
  if (currentPos < editorContent.value.length) {
    const remainingText = editorContent.value.substring(currentPos);
    // Replace newlines with <br> tags
    result += remainingText.replace(/\n/g, '<br>');
  }

  // Wrap in a container with proper direction and preserve whitespace
  const finalResult = `<div dir="${containerDir}" style="text-align: ${isRtlText ? 'right' : 'left'}; white-space: pre-wrap;">${result}</div>`;
  console.log('🔍 [renderedContent] Final result:', finalResult);
  return finalResult;
});

const startOcr = async () => {
  if (isPdf.value) {
    showPageSelection.value = true
  } else {
    // For non-PDF documents, directly start OCR
    // First save the initial status if needed
    try {
      console.log('🔵 [OCR] Checking initial status before starting OCR...');
      const currentDoc = await documents.getById(props.selectedDocument.id);
      if (!currentDoc.data?.success || !currentDoc.data?.data) {
        throw new Error('Failed to fetch current document');
      }

      const doc = currentDoc.data.data;
      console.log('📦 [OCR] Current document status:', doc.status);
      
      // If recovery process is empty and status is 6, save it
      if ((!doc.recoveryProcess || doc.recoveryProcess === '[]') && doc.status === 6) {
        console.log('📦 [OCR] Saving initial Failed status (6) to recovery process');
        await documents.update(props.selectedDocument.id, {
          recoveryProcess: JSON.stringify([6])
        });
        console.log('✅ [OCR] Initial Failed status saved to recovery process');
      }
    } catch (error) {
      console.error('❌ [OCR] Failed to save initial status:', error);
    }

    // Then proceed with OCR
    handlePageSelection(0)
  }
  elapsedTime.value = 0
  if (timerInterval) clearInterval(timerInterval)
  timerInterval = window.setInterval(() => {
    elapsedTime.value++
  }, 1000)
}

const syncEditorContent = () => {
  if (editableTextRef.value) {
    // Store current cursor position and selection
    const selection = window.getSelection();
    const range = selection?.getRangeAt(0);
    let cursorOffset = 0;
    
    // Calculate cursor offset from the start of the content
    if (range) {
      const preCaretRange = range.cloneRange();
      preCaretRange.selectNodeContents(editableTextRef.value);
      preCaretRange.setEnd(range.endContainer, range.endOffset);
      cursorOffset = preCaretRange.toString().length;
    }

    // Get the content while preserving line breaks
    let html = editableTextRef.value.innerHTML;
    html = html.replace(/<br\s*\/?>/gi, '\n');
    // Remove any other HTML tags if needed
    const tempDiv = document.createElement('div');
    tempDiv.innerHTML = html;
    const newText = tempDiv.textContent || tempDiv.innerText || '';
    editorContent.value = newText;
    
    // Update suggestions positions after text changes
    localSuggestions.value = localSuggestions.value.map(suggestion => {
      const newPosition = findWordPosition(newText, suggestion.word);
      return {
        ...suggestion,
        start: newPosition.start,
        end: newPosition.end
      };
    }).filter(suggestion => suggestion.start !== 0 || suggestion.word === newText.slice(0, suggestion.word.length));

    // Emit the content update
    emit('update:content', newText);

    // Re-render with highlighting on next tick
    nextTick(() => {
      if (editableTextRef.value) {
        // Update the HTML with highlighted content while preserving line breaks
        editableTextRef.value.innerHTML = renderedContent.value;
        
        // Restore cursor position
        const selection = window.getSelection();
        const range = document.createRange();
        
        // Find the text node and offset where cursor should be placed
        let currentOffset = 0;
        let targetNode = null;
        let targetOffset = 0;
        
        const walk = document.createTreeWalker(
          editableTextRef.value,
          NodeFilter.SHOW_TEXT,
          null
        );
        
        let node;
        while ((node = walk.nextNode())) {
          const nodeLength = node.textContent?.length || 0;
          if (currentOffset + nodeLength >= cursorOffset) {
            targetNode = node;
            targetOffset = cursorOffset - currentOffset;
            break;
          }
          currentOffset += nodeLength;
        }
        
        if (targetNode) {
          range.setStart(targetNode, targetOffset);
          range.collapse(true);
          selection?.removeAllRanges();
          selection?.addRange(range);
        }
      }
    });
  }
};

const getSuggestionsForWord = (word: string) => {
  console.log('🔍 [getSuggestionsForWord] Starting with word:', word);
  console.log('🔍 [getSuggestionsForWord] selectedSuggestion:', selectedSuggestion.value);

  if (!selectedSuggestion.value) {
    console.log('❌ [getSuggestionsForWord] No selectedSuggestion found, returning empty array');
    return [];
  }

  const { start, end, word: clickedWord } = selectedSuggestion.value;
  console.log('🔍 [getSuggestionsForWord] Position:', { start, end, clickedWord });

  // Find all corrections for this position
  const correctionsForPosition = correctedWords.value.filter(cw => 
    cw.start === start || (cw.start <= start && cw.end >= end)
  );
  console.log('🔍 [getSuggestionsForWord] Corrections for position:', correctionsForPosition);

  // Get the original word (either from the first correction or the clicked word)
  const originalWord = correctionsForPosition[0]?.originalWord || clickedWord;
  console.log('🔍 [getSuggestionsForWord] Original word:', originalWord);

  // Find the suggestion entry with more flexible position matching
  let relevantSuggestionEntry = localSuggestions.value.find(s => 
    s.word === originalWord && 
    (s.start === start || (s.start <= start && s.end >= end))
  );
  console.log('🔍 [getSuggestionsForWord] Found in localSuggestions:', relevantSuggestionEntry);

  // If not found in localSuggestions, try originalSuggestions
  if (!relevantSuggestionEntry && originalSuggestions.value.length) {
    console.log('🔍 [getSuggestionsForWord] Checking originalSuggestions...');
    const originalEntry = originalSuggestions.value.find(s => 
      s.word === originalWord && 
      (s.start === start || (s.start <= start && s.end >= end))
    );
    console.log('🔍 [getSuggestionsForWord] Found in originalSuggestions:', originalEntry);
    
    if (originalEntry) {
      // Convert string suggestions to objects if needed
      relevantSuggestionEntry = {
        ...originalEntry,
        suggestions: originalEntry.suggestions.map(s => 
          typeof s === 'string' ? { text: s, confidence: originalEntry.confidence } : s
        )
      };
      console.log('🔍 [getSuggestionsForWord] Converted originalEntry:', relevantSuggestionEntry);
    }
  }

  // If still not found, try to find by position only with more flexible matching
  if (!relevantSuggestionEntry) {
    console.log('🔍 [getSuggestionsForWord] Checking by position only...');
    const positionEntry = localSuggestions.value.find(s => 
      s.start === start || (s.start <= start && s.end >= end)
    );
    console.log('🔍 [getSuggestionsForWord] Found by position:', positionEntry);
    
    if (positionEntry) {
      relevantSuggestionEntry = {
        ...positionEntry,
        suggestions: positionEntry.suggestions.map(s => 
          typeof s === 'string' ? { text: s, confidence: positionEntry.confidence } : s
        )
      };
      console.log('🔍 [getSuggestionsForWord] Converted positionEntry:', relevantSuggestionEntry);
    }
  }

  if (!relevantSuggestionEntry) {
    console.log('❌ [getSuggestionsForWord] No relevant suggestion entry found, returning empty array');
    return [];
  }

  // Get all suggestions from the entry
  let suggestionsList: Array<{ text: string; confidence: number }> = [];
  
  // Add suggestions from the entry, cleaning up numbered prefixes
  suggestionsList = relevantSuggestionEntry.suggestions.map(s => {
    const text = typeof s === 'string' ? s : s.text;
    // Remove numbered prefixes like "1.", "2.", etc.
    const cleanText = text.replace(/^\d+\.\s*/, '');
    return {
      text: cleanText,
      confidence: typeof s === 'string' ? relevantSuggestionEntry.confidence : s.confidence
    };
  });
  console.log('🔍 [getSuggestionsForWord] Initial suggestions list:', suggestionsList);

  // Always add the original word as plain text (no label)
  const cleanedOriginalWord = originalWord.replace(/^\d+\.\s*/, '');
  if (!suggestionsList.some(s => s.text === cleanedOriginalWord)) {
    suggestionsList.push({ text: cleanedOriginalWord, confidence: 1.0 });
    console.log('🔍 [getSuggestionsForWord] Added original word to suggestions');
  }

  // Add the current word if different from original
  if (clickedWord !== originalWord && !suggestionsList.some(s => s.text === clickedWord)) {
    suggestionsList.push({ text: clickedWord, confidence: 0.9 });
    console.log('🔍 [getSuggestionsForWord] Added current word to suggestions');
  }

  // Add all corrections for this position
  correctionsForPosition.forEach(cw => {
    if (!suggestionsList.some(s => s.text === cw.correctedWord)) {
      suggestionsList.push({ text: cw.correctedWord, confidence: 0.95 });
      console.log('🔍 [getSuggestionsForWord] Added correction to suggestions:', cw.correctedWord);
    }
  });

  // Sort by confidence
  const finalSuggestions = suggestionsList
    .filter(s => typeof s.text === 'string' && typeof s.confidence === 'number')
    .sort((a, b) => b.confidence - a.confidence);

  console.log('🔍 [getSuggestionsForWord] Final suggestions list before filtering duplicates:', finalSuggestions);

  // Remove duplicates, keeping the one with the highest confidence
  const uniqueSuggestionsMap = new Map<string, { text: string; confidence: number }>();
  for (const suggestion of finalSuggestions) {
    if (!uniqueSuggestionsMap.has(suggestion.text) || (uniqueSuggestionsMap.has(suggestion.text) && suggestion.confidence > (uniqueSuggestionsMap.get(suggestion.text)!).confidence)) {
      uniqueSuggestionsMap.set(suggestion.text, suggestion);
    }
  }

  const uniqueSuggestions = Array.from(uniqueSuggestionsMap.values());

  console.log('🔍 [getSuggestionsForWord] Final unique suggestions list:', uniqueSuggestions);

  return uniqueSuggestions;
};

// Add these interfaces before the applySuggestion function
interface SuggestionEntry {
  type: 'suggestion';
  word: string;
  suggestions: Array<{ text: string; confidence: number }>;
  confidence: number;
  start: number;
  end: number;
}

interface CorrectionEntry {
  type: 'correction';
  originalWord: string;
  correctedWord: string;
  start: number;
  end: number;
}

type TextEntry = SuggestionEntry | CorrectionEntry;

const applySuggestion = (newWord: string) => {
  if (!selectedSuggestion.value || !editableTextRef.value) return;

  // Use original word and original positions from selectedSuggestion.value
  const { start, end, word: clickedWordOriginal } = selectedSuggestion.value; // Renamed for clarity
  const content = editorContent.value; // This is the *current* text in the editor

  // Get the word currently displayed at the clicked position
  const wordCurrentlyDisplayed = content.substring(start, end);

  // If the new word is the same as the word currently displayed, do nothing
  if (newWord === wordCurrentlyDisplayed) {
    console.log('🔵 [applySuggestion] New word is same as current word displayed, returning early.');
    selectedWord.value = null;
    dropdownPosition.value = null;
    return;
  }
  console.log('🔵 [applySuggestion] Applying suggestion:', { newWord, wordCurrentlyDisplayed });

  // Remove (tes) prefix if present
  const cleanNewWord = newWord.startsWith('(tes) ') ? newWord.slice(6) : newWord;

  // Determine the actual original word at this position.
  // This could be the word from an existing correction's originalWord, or from the initial suggestions.
  let actualOriginalWord = clickedWordOriginal; // Start with the word associated with the click position

  // First, check if there's an existing correction that overlaps with the clicked position.
  // If so, the original word of that correction is the actual original word.
  const existingCorrectionForPos = correctedWords.value.find(cw =>
      (cw.start < end && cw.start + cw.correctedWord.length > start) // Check for overlap
  );

   if (existingCorrectionForPos) {
       actualOriginalWord = existingCorrectionForPos.originalWord;
   } else {
       // If no existing correction, try to find the original word from the initial originalSuggestions
       const originalEntry = originalSuggestions.value.find(s =>
           (s.start < end && s.end > start) // Check for overlap
       );
       if(originalEntry) {
           actualOriginalWord = originalEntry.word;
       }
       // If not found in originalSuggestions, it might be a word that was never suggested for correction initially,
       // in which case clickedWordOriginal is likely the actual original word.
   }


  // Find or create the suggestion entry associated with the actual original word and the initial position.
  // We prioritize finding an existing entry to add suggestions to.
  let suggestionEntry = localSuggestions.value.find(s =>
    s.word === actualOriginalWord &&
    s.start === start && // Look for exact initial position match
    s.end === end
  );

  // If no exact match, try finding by original word and checking for overlapping initial position
  if (!suggestionEntry) {
       suggestionEntry = localSuggestions.value.find(s =>
         s.word === actualOriginalWord &&
         ((s.start >= start && s.start < end) || (start >= s.start && start < s.end)) // Flexible position check against initial click range
       );
  }

  // If still no suggestion entry found, create a new one based on the actual original word and initial position.
  if (!suggestionEntry) {
      suggestionEntry = {
        word: actualOriginalWord,
        suggestions: [], // Initialize with empty array
        confidence: 1.0, // Default confidence for a new entry
        start: start, // Use initial click position
        end: end
      };
      localSuggestions.value.push(suggestionEntry);
  }


  // Create the new correction entry.
  const newCorrection = {
    originalWord: actualOriginalWord,
    correctedWord: cleanNewWord,
    start: start, // Store the correction with the original start position
    end: start + cleanNewWord.length // End position based on new word length
  };

  // Remove any existing correction for this original word at or around this initial position.
  // This prevents multiple corrections for the same original word at the same logical location.
  correctedWords.value = correctedWords.value.filter(cw =>
    !(cw.originalWord === actualOriginalWord &&
      ((cw.start >= start && cw.start < end) || (start >= cw.start && start < cw.end))) // Check against initial click range
  );


  // Add the new correction to the list of corrected words.
  correctedWords.value.push(newCorrection);

  // Update the associated suggestion entry with the relevant words.
  if (suggestionEntry) {
    // Always add the actual original word to suggestions with high confidence.
    if (!suggestionEntry.suggestions.some(s => s.text === actualOriginalWord)) {
      suggestionEntry.suggestions.push({
        text: actualOriginalWord,
        confidence: 1.0
      });
    }

    // Add the word that was currently displayed in the editor before applying the new word,
    // but only if it's different from the original and the new word, and not already in suggestions.
    // This ensures that previous corrections are also available in the dropdown.
     if (wordCurrentlyDisplayed !== actualOriginalWord && wordCurrentlyDisplayed !== cleanNewWord && !suggestionEntry.suggestions.some(s => s.text === wordCurrentlyDisplayed)) {
       suggestionEntry.suggestions.push({
         text: wordCurrentlyDisplayed,
         confidence: 0.95 // Confidence for previous correction/displayed word
       });
     }

    // Add the newly applied word to suggestions if it's not already there.
    if (!suggestionEntry.suggestions.some(s => s.text === cleanNewWord)) {
      suggestionEntry.suggestions.push({
        text: cleanNewWord,
        confidence: 0.95 // High confidence for the newly applied word
      });
    }
  }

  // Only rebuild text if we actually made a change (which the initial check now handles)
  // Rebuild the text from scratch based on all corrections
  editorContent.value = applyAllCorrections();


  // Update the editor content in the DOM
  nextTick(() => {
    if (editableTextRef.value) {
      editableTextRef.value.innerHTML = renderedContent.value;
    }
  });

  emit('update:content', editorContent.value);


  selectedWord.value = null;
  dropdownPosition.value = null;
};

const ignoreSuggestion = () => {
  if (selectedSuggestion.value) {
    const { start, end, word: originalWord } = selectedSuggestion.value;

    // Remove the correction if it exists for this original word and position
    correctedWords.value = correctedWords.value.filter(cw => 
      !(cw.originalWord === originalWord && cw.start === start && cw.end === end)
    );

    // Remove the suggestion entry from localSuggestions
    localSuggestions.value = localSuggestions.value.filter(s => 
      !(s.word === originalWord && s.start === start && s.end === end)
    );
    
    // Update the editor content to reflect the change
    nextTick(() => {
      if (editableTextRef.value) {
        // Force a re-render of the content without the ignored word's styling
        const content = editorContent.value;
        editorContent.value = '';
        nextTick(() => {
          editorContent.value = content;
          if (editableTextRef.value) {
            editableTextRef.value.innerHTML = renderedContent.value;
          }
        });
      }
    });
  }
  selectedWord.value = null;
  dropdownPosition.value = null;
}



const handleSave = async () => {
  try {
    isLoading.value = true;
    
    console.log('🔵 [handleSave] Saving OCR text, response, and summary for document:', props.selectedDocument.id);
    
    // First get the current document to preserve other fields
    const currentDoc = await documents.getById(props.selectedDocument.id);
    if (!currentDoc.data?.success || !currentDoc.data?.data) {
      throw new Error('Failed to fetch current document');
    }

    const doc = currentDoc.data.data;
    
    // Convert language string to enum value
    const language = backendLanguageMap[doc.language] ?? 0; // Default to English if unknown
    
    // Prepare the complete OCR response with all necessary data
    const ocrResponse = {
      text: editorContent.value,
      suggestions: localSuggestions.value,
      corrections: correctedWords.value,
      confidence: parseFloat(precision.value) / 100,
      processingTime: parseFloat(processingTime.value),
      language: doc.language,
      // Add metadata about the OCR process
      metadata: {
        engine: 'mistral',
        model: 'llama',
        timestamp: new Date().toISOString(),
        version: '1.0'
      }
    };
    
    // Log the complete OCR response before saving
    console.log('🔵 [handleSave] Complete OCR response:', ocrResponse);
    
    // Update with existing values plus the new OCR text, response, and summary
    const response = await documents.update(props.selectedDocument.id, {
      fileName: doc.fileName,
      pageCount: doc.pageCount,
      language: language,
      topic: doc.topic || '',
      description: doc.description || '',
      tags: doc.tags || [],
      isFavorite: doc.isFavorite || false,
      ocrText: editorContent.value,
      ocrResponse: JSON.stringify(ocrResponse),
      summary: summaryContent.value, // Always use the current summary value
      status: 4, // Set status to Complete (4) when saving OCR text
      lastModified: new Date().toISOString()
    });

    if (!response.data?.success) {
      throw new Error('Failed to save OCR text, response, and summary');
    }
    
    console.log('✅ [handleSave] OCR text, response, and summary saved successfully:', response);
    
    emit('save');
    emit('refresh'); // Move the refresh emit here, only after successful save
    
    // Show success toast
    showToast('Texte OCR, suggestions et résumé enregistrés avec succès.');
  } catch (error: any) {
    console.error('❌ [handleSave] Error saving OCR text, response, and summary:', error);
    
    // Show error toast
    showToast('Impossible d\'enregistrer le texte OCR, les suggestions et le résumé. Veuillez réessayer.', 'error');
  } finally {
    isLoading.value = false;
  }
};

// Watch for content updates
watch(() => props.initialContent, (newContent) => {
  if (newContent !== undefined) {
    editorContent.value = newContent
  }
})

// Add function to get PDF page count
const getPdfPageCount = async (file: File): Promise<number> => {
  const arrayBuffer = await file.arrayBuffer()
  const pdfDoc = await PDFDocument.load(arrayBuffer)
  return pdfDoc.getPageCount()
}

// Add this new function after the other methods
const initializeRecoveryProcess = async (documentId: string) => {
  try {
    console.log('🔵 [OCR] Initializing recovery process for document:', documentId);
    const currentDoc = await documents.getById(documentId);
    if (!currentDoc.data?.success || !currentDoc.data?.data) {
      throw new Error('Failed to fetch current document');
    }

    const doc = currentDoc.data.data;
    console.log('📦 [OCR] Current document status:', doc.status);
    
    // Initialize recovery process with current status if it's not already set
    if (!doc.recoveryProcess || doc.recoveryProcess === '[]') {
      const initialStatus = doc.status;
      console.log('📦 [OCR] Setting initial status in recovery process:', initialStatus);
      
      await documents.update(documentId, {
        recoveryProcess: JSON.stringify([initialStatus])
      });
      console.log('✅ [OCR] Initialized recovery process with status:', initialStatus);
    }
  } catch (error) {
    console.error('❌ [OCR] Failed to initialize recovery process:', error);
  }
};

// Update onMounted to initialize recovery process
onMounted(async () => {
  if (props.selectedDocument) {
    try {
      // Initialize recovery process first
      await initializeRecoveryProcess(props.selectedDocument.id);
      
      // Rest of the existing onMounted code...
      const docResponse = await documents.getById(props.selectedDocument.id)
      if (docResponse.data?.success && docResponse.data?.data) {
        const doc = docResponse.data.data;
        editorContent.value = doc.ocrText || '';
        originalContent.value = doc.ocrText || ''; // <-- Set only here!
        emit('update:content', editorContent.value)
        
        // Initialize markdown content for OCR text
        if (editorContent.value) {
          markdownContent.value = await marked(editorContent.value);
        }
        
        // Load the summary if it exists
        summaryContent.value = doc.summary || '';
        
        // Initialize markdown content for summary
        if (summaryContent.value) {
          summaryMarkdownContent.value = await marked(summaryContent.value);
        }
        
        // If we have OCR response, load suggestions and corrections
        if (doc.ocrResponse) {
          try {
            const savedResponse = JSON.parse(doc.ocrResponse)
            // Transform saved suggestions to ensure compatibility
            localSuggestions.value = transformOcrSuggestions(savedResponse.suggestions || [])
            correctedWords.value = savedResponse.corrections || []
            precision.value = (savedResponse.confidence * 100).toFixed(2)
            processingTime.value = savedResponse.processingTime.toFixed(2)
            hasStartedOcr.value = true // Set this to true to show metrics
          } catch (error) {
            console.error('Error parsing saved OCR response:', error)
          }
        }
      }

      // Load document preview and get page count for PDFs
      const fileResponse = await documents.getFile(props.selectedDocument.id)
      if (fileResponse.data) {
        const blob = new Blob([fileResponse.data], { type: props.selectedDocument.type })
        documentUrl.value = URL.createObjectURL(blob)
        imageLoading.value = false

        // Get page count for PDFs
        if (isPdf.value) {
          const file = new File([blob], props.selectedDocument.name, { type: props.selectedDocument.type })
          try {
            totalPages.value = await getPdfPageCount(file)
          } catch (error) {
            console.error('Error getting PDF page count:', error)
            totalPages.value = 1 // Fallback to 1 page
          }
        }
      } else {
        throw new Error('Failed to load document file')
      }
    } catch (error) {
      console.error('Error loading document:', error)
      showToast('Impossible de charger le document.', 'error')
    }
  }
})

onUnmounted(() => {
  if (documentUrl.value) {
    URL.revokeObjectURL(documentUrl.value)
  }
  if (timerInterval) {
    clearInterval(timerInterval)
    timerInterval = null
  }
  if (summaryTimerInterval) {
    clearInterval(summaryTimerInterval)
    summaryTimerInterval = null
  }
  if (toastTimeout.value) {
    clearTimeout(toastTimeout.value);
    toastTimeout.value = null;
  }
})

const exportToDoc = () => {
  if (!editorContent.value) return;
  
  // Create a Blob with the content
  const blob = new Blob([editorContent.value], { type: 'application/msword' });
  const url = URL.createObjectURL(blob);
  
  // Create a temporary link and trigger download
  const link = document.createElement('a');
  link.href = url;
  link.download = `${props.selectedDocument.name.split('.')[0]}.doc`;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
};

const exportToPdf = async () => {
  if (!editorContent.value) return;
  
  try {
    // First convert the content to markdown
    const markdown = await marked(editorContent.value);
    const isRtlText = isRTL(editorContent.value);
    
    // Create a temporary div with the content
    const element = document.createElement('div');
    element.style.padding = '20px';
    element.style.fontFamily = isRtlText ? 'Noto Naskh Arabic, Arial' : 'Arial';
    element.style.fontSize = '14pt';
    element.style.lineHeight = '1.6';
    element.style.direction = isRtlText ? 'rtl' : 'ltr';
    element.style.textAlign = isRtlText ? 'right' : 'left';
    element.style.whiteSpace = 'pre-wrap';
    element.style.width = '100%';
    element.style.maxWidth = '800px';
    element.style.margin = '0 auto';
    
    // Create content div with markdown styles
    const contentDiv = document.createElement('div');
    contentDiv.style.width = '100%';
    contentDiv.style.padding = '20px';
    contentDiv.className = 'prose dark:prose-invert max-w-none';
    contentDiv.innerHTML = markdown;
    
    element.appendChild(contentDiv);
    
    // Add the element to the document temporarily
    document.body.appendChild(element);
    
    // Configure html2pdf options
    const opt = {
      margin: 10,
      filename: `${props.selectedDocument.name.split('.')[0]}.pdf`,
      image: { type: 'jpeg', quality: 0.98 },
      html2canvas: { 
        scale: 2,
        useCORS: true,
        letterRendering: true,
        logging: true,
        allowTaint: true
      },
      jsPDF: { 
        unit: 'mm', 
        format: 'a4', 
        orientation: 'portrait'
      }
    };
    
    // Generate PDF
    await html2pdf().set(opt).from(element).save();
    
    // Remove the temporary element
    document.body.removeChild(element);
    
    showToast('PDF généré avec succès.');
  } catch (error) {
    console.error('Error generating PDF:', error);
    showToast('Impossible de générer le PDF. Veuillez réessayer.', 'error');
  }
};

const copyToClipboard = async () => {
  if (!editorContent.value) return;
  
  try {
    await navigator.clipboard.writeText(editorContent.value);
    showToast('Le texte a été copié dans le presse-papiers.');
  } catch (error) {
    showToast('Impossible de copier le texte.', 'error');
  }
};

const toggleActionMenu = () => {
  showActionMenu.value = !showActionMenu.value
}

// Add click outside handler to close menu
const closeActionMenu = (event: MouseEvent) => {
  const target = event.target as HTMLElement
  if (!target.closest('.floating-action-button')) {
    showActionMenu.value = false
  }
}

onMounted(() => {
  document.addEventListener('click', handleClickOutside);
  document.addEventListener('click', closeActionMenu);
  document.addEventListener('click', handleExportMenuClickOutside);
  document.addEventListener('click', handleSummaryExportMenuClickOutside);
})

onUnmounted(() => {
  // Clean up all event listeners to prevent memory leaks
  document.removeEventListener('click', handleClickOutside);
  document.removeEventListener('click', closeActionMenu);
  document.removeEventListener('click', handleExportMenuClickOutside);
  document.removeEventListener('click', handleSummaryExportMenuClickOutside);

  // Clean up any blob URLs
  if (documentUrl.value && documentUrl.value.startsWith('blob:')) {
    URL.revokeObjectURL(documentUrl.value);
  }

  // Reset reactive state
  selectedWord.value = null;
  selectedSuggestion.value = null;
  dropdownPosition.value = null;
  localSuggestions.value = [];
  originalSuggestions.value = [];
  correctedWords.value = [];
})

onUnmounted(() => {
  document.removeEventListener('click', handleClickOutside);
  document.removeEventListener('click', closeActionMenu);
  document.removeEventListener('click', handleExportMenuClickOutside);
  document.removeEventListener('click', handleSummaryExportMenuClickOutside);
})

const showToast = (message: string, type: 'success' | 'error' | 'warning' = 'success', persistent: boolean = false) => {
  // Clear any existing timeout
  if (toastTimeout.value) {
    clearTimeout(toastTimeout.value);
    toastTimeout.value = null;
  }

  toastMessage.value = message;
  toastType.value = type;
  isPersistentToast.value = persistent;
  
  // Only auto-hide if not persistent
  if (!persistent) {
    toastTimeout.value = setTimeout(() => {
      toastMessage.value = null;
      toastTimeout.value = null;
    }, type === 'success' ? 5000 : 3000); // 5 seconds for success, 3 for error
  }
};

const dismissToast = () => {
  toastMessage.value = null;
  isPersistentToast.value = false;
};

// Add new computed property for markdown rendering
const updateMarkdownContent = async () => {
  if (editorContent.value) {
    let content = editorContent.value;
    const mathExpressions: string[] = [];
    // Replace math expressions with placeholders
    content = content.replace(/\$([^$]+)\$/g, (match) => {
      mathExpressions.push(match);
      return `MATH_PLACEHOLDER_${mathExpressions.length - 1}`;
    });
    // Process markdown
    let markdown = await marked(content);
    // Restore math expressions and render them
    mathExpressions.forEach((expr, index) => {
      const placeholder = `MATH_PLACEHOLDER_${index}`;
      try {
        const renderedMath = katex.renderToString(expr.slice(1, -1), {
          throwOnError: false,
          displayMode: false,
          output: 'html'
        });
        markdown = markdown.replace(placeholder, renderedMath);
      } catch (e) {
        console.error('Error rendering math:', e);
        markdown = markdown.replace(placeholder, expr);
      }
    });
    markdownContent.value = markdown;
  }
};

// Add toggle function
const toggleMarkdownPreview = () => {
  showMarkdown.value = !showMarkdown.value
  if (showMarkdown.value) {
    updateMarkdownContent()
  }
}

// Watch for content changes to update markdown
watch(() => editorContent.value, () => {
  if (showMarkdown.value) {
    updateMarkdownContent()
  }
})

// Add new method to handle page selection
const handlePageSelection = async (pageNumber: number) => {
  try {
    console.log('🔵 [OCR] Starting OCR process for document:', {
      documentId: props.selectedDocument.id,
      documentName: props.selectedDocument.name,
      pageNumber,
      type: props.selectedDocument.type
    });

    isLoading.value = true;
    startTime.value = performance.now();
    hasStartedOcr.value = true;

    // Increment total attempts when starting OCR
    try {
      console.log('🔵 [OCR] Incrementing total attempts...');
      await ocrStatistics.incrementTotalAttempts();
      console.log('✅ [OCR] Total attempts incremented');
    } catch (error) {
      console.error('❌ [OCR] Failed to increment total attempts:', error);
    }

    // Update status to Processing (1) when OCR starts
    try {
      console.log('🔵 [OCR] Updating document status to Processing (1)...');
      const currentDoc = await documents.getById(props.selectedDocument.id);
      if (!currentDoc.data?.success || !currentDoc.data?.data) {
        throw new Error('Failed to fetch current document');
      }

      const doc = currentDoc.data.data;
      console.log('📦 [OCR] Current document status:', doc.status);
      
      // Only update recoveryProcess if initial state was Failed (6)
      let recoveryProcess = doc.recoveryProcess;
      if (doc.status === 6) {
        try {
          let recoveryArray: number[] = [];
          if (doc.recoveryProcess) {
            if (typeof doc.recoveryProcess === 'string') {
              recoveryArray = JSON.parse(doc.recoveryProcess);
            } else if (Array.isArray(doc.recoveryProcess)) {
              recoveryArray = doc.recoveryProcess;
            }
          }
          
          // Add Processing status (1) if not already present
          if (!recoveryArray.includes(1)) {
            recoveryArray.push(1);
          }
          
          recoveryProcess = JSON.stringify(recoveryArray);
        } catch (error) {
          console.warn('⚠️ [OCR] Error parsing recoveryProcess:', error);
          recoveryProcess = JSON.stringify([6, 1]); // Initialize with Failed and Processing
        }
      }
      
      // Update document with Processing status
      const updateDto: any = {
        fileName: doc.fileName,
        pageCount: doc.pageCount || 1,
        language: backendLanguageMap[doc.language] ?? 0,
        topic: doc.topic || '',
        description: doc.description || '',
        tags: doc.tags || [],
        isFavorite: doc.isFavorite || false,
        ocrText: doc.ocrText || '',
        ocrResponse: doc.ocrResponse || null,
        status: 1, // Processing
        lastModified: new Date().toISOString()
      };

      // Only include recoveryProcess in update if it was modified
      if (doc.status === 6) {
        updateDto.recoveryProcess = recoveryProcess;
      }

      await documents.update(props.selectedDocument.id, updateDto);
      console.log('✅ [OCR] Document status updated to Processing (1)');
    } catch (error) {
      console.error('❌ [OCR] Failed to update document status to Processing (1):', error);
    }

    // Get the file from the document service
    console.log('🔵 [OCR] Fetching document file...');
    const fileResponse = await documents.getFile(props.selectedDocument.id);
    
    if (!fileResponse.data) {
      throw new Error('Failed to retrieve document file');
    }
    console.log('✅ [OCR] Document file fetched successfully:', {
      fileSize: fileResponse.data.size,
      fileType: fileResponse.data.type
    });

    // Create a File object from the blob response
    const file = new File(
      [fileResponse.data], 
      props.selectedDocument.name, 
      { type: props.selectedDocument.type }
    );
    
    // Get the document details to get the language
    console.log('🔵 [OCR] Fetching document details...');
    const docResponse = await documents.getById(props.selectedDocument.id);
    if (!docResponse.data?.success || !docResponse.data?.data) {
      throw new Error('Failed to retrieve document details');
    }
    console.log('✅ [OCR] Document details fetched:', {
      language: docResponse.data.data.language,
      documentData: docResponse.data.data
    });
    
    // Map the language to the OCR service format
    const languageKey = String(docResponse.data.data.language);
    const language = ocrLanguageMap[languageKey] || 'eng';
    
    console.log('🔵 [OCR] Sending OCR request with parameters:', {
      language,
      pageNumber: pageNumber === -1 ? -1 : (pageNumber === 0 ? 0 : pageNumber - 1),
      model: 'llama',
      engine: 'mistral',
      fileName: file.name,
      fileType: file.type,
      fileSize: file.size
    });
    
    // Use the processFile method with the selected page
    const response = await ocr.processFile(
      file,
      language,
      pageNumber === -1 ? -1 : (pageNumber === 0 ? 0 : pageNumber - 1),
      'llama',
      'mistral',
      props.selectedDocument.id
    );
    
    if (response.data) {
      console.log('✅ [OCR] OCR response received:', {
        textLength: response.data.text.length,
        suggestionsCount: response.data.suggestions.length,
        confidence: response.data.confidence,
        processingTime: response.data.processingTime,
        containsPersonalData: response.data.contains_personal_data
      });

      // Make callback to backend
      try {
        console.log('🔵 [OCR] Making callback to backend...');
        const callbackResponse = await api.post('/api/Documents/ocr-callback', {
          documentId: props.selectedDocument.id,
          text: response.data.text,
          suggestions: response.data.suggestions,
          corrections: [],
          confidence: response.data.confidence,
          processingTime: response.data.processingTime
        });
        console.log('✅ [OCR] Callback successful:', callbackResponse.data);
      } catch (error) {
        console.error('❌ [OCR] Callback failed:', error);
        // Don't throw here, we still want to continue with the rest of the process
      }

      // Increment successful attempts since OCR completed successfully
      try {
        console.log('🔵 [OCR] Incrementing successful attempts...');
        await ocrStatistics.incrementSuccessfulAttempts();
        console.log('✅ [OCR] Successful attempts incremented');
      } catch (error) {
        console.error('❌ [OCR] Failed to increment successful attempts:', error);
      }

      // Update status to EnhancementInProgress (4) when OCR completes successfully
      try {
        console.log('🔵 [OCR] Updating document status to EnhancementInProgress (4)...');
        const currentDoc = await documents.getById(props.selectedDocument.id);
        if (!currentDoc.data?.success || !currentDoc.data?.data) {
          throw new Error('Failed to fetch current document');
        }

        const doc = currentDoc.data.data;
        let recoveryProcess = doc.recoveryProcess;
        
        try {
          let recoveryArray: number[] = [];
          if (doc.recoveryProcess) {
            if (typeof doc.recoveryProcess === 'string') {
              recoveryArray = JSON.parse(doc.recoveryProcess);
            } else if (Array.isArray(doc.recoveryProcess)) {
              recoveryArray = doc.recoveryProcess;
            }
          }
          
          // Add EnhancementInProgress status (4) if not already present
          if (!recoveryArray.includes(4)) {
            recoveryArray.push(4);
          }
          
          recoveryProcess = JSON.stringify(recoveryArray);
        } catch (error) {
          console.warn('⚠️ [OCR] Error parsing recoveryProcess:', error);
          recoveryProcess = JSON.stringify([6, 1, 4]); // Initialize with Failed, Processing, and EnhancementInProgress
        }

        // Only send the fields we want to update
        const updateDto: any = {
          fileName: doc.fileName,
          pageCount: doc.pageCount || 1,
          language: backendLanguageMap[doc.language] ?? 0,
          topic: doc.topic || '',
          description: doc.description || '',
          tags: doc.tags || [],
          isFavorite: doc.isFavorite || false,
          ocrText: response.data.text,
          ocrResponse: JSON.stringify({
            text: response.data.text,
            suggestions: response.data.suggestions,
            corrections: [],
            confidence: response.data.confidence,
            processingTime: response.data.processingTime,
            language: doc.language,
            metadata: {
              engine: 'mistral',
              model: 'llama',
              timestamp: new Date().toISOString(),
              version: '1.0'
            }
          }),
          status: 4,
          lastModified: new Date().toISOString(),
          recoveryProcess: recoveryProcess // Always include recoveryProcess in update
        };

        await documents.update(props.selectedDocument.id, updateDto);
        console.log('✅ [OCR] Document status updated to EnhancementInProgress (4)');
      } catch (error) {
        console.error('❌ [OCR] Failed to update document status to EnhancementInProgress (4):', error);
      }

      editorContent.value = response.data.text;
      originalContent.value = response.data.text; // <-- Set here when new OCR result

      // Transform OCR service response to match frontend expectations
      localSuggestions.value = transformOcrSuggestions(response.data.suggestions);
      
      precision.value = (response.data.confidence * 100).toFixed(2);
      processingTime.value = response.data.processingTime.toFixed(2);
      
      emit('update:content', editorContent.value);
      emit('launchOcr');
      
      // Show success toast first
      showToast('Traitement OCR réussi');
      
      // If document contains personal data, show warning toast after a short delay
      if (response.data.contains_personal_data) {
        setTimeout(() => {
          showToast('Ce document contient des informations personnelles.', 'warning', true);
        }, 3000); // Show warning 1 second after success toast
      }
    } else {
      throw new Error('Erreur lors du traitement OCR');
    }
  } catch (error: any) {
    console.error('❌ [OCR] OCR process failed:', {
      error: error.message,
      stack: error.stack,
      documentId: props.selectedDocument.id,
      documentName: props.selectedDocument.name
    });

    // Increment failed attempts since OCR failed
    try {
      console.log('🔵 [OCR] Incrementing failed attempts...');
      await ocrStatistics.incrementFailedAttempts();
      console.log('✅ [OCR] Failed attempts incremented');
    } catch (error) {
      console.error('❌ [OCR] Failed to increment failed attempts:', error);
    }

    // Update status to Failed (6) and include recoveryProcess
    try {
      console.log('🔵 [OCR] Updating document status to Failed (6)...');
      const currentDoc = await documents.getById(props.selectedDocument.id);
      if (!currentDoc.data?.success || !currentDoc.data?.data) {
        throw new Error('Failed to fetch current document');
      }

      const doc = currentDoc.data.data;
      let recoveryProcess = doc.recoveryProcess;
      
      // Only update recoveryProcess if initial state was Failed (6)
      if (doc.status === 6) {
        try {
          let recoveryArray: number[] = [];
          if (doc.recoveryProcess) {
            if (typeof doc.recoveryProcess === 'string') {
              recoveryArray = JSON.parse(doc.recoveryProcess);
            } else if (Array.isArray(doc.recoveryProcess)) {
              recoveryArray = doc.recoveryProcess;
            }
          }
          
          // Add Failed status (6) if not already present
          if (!recoveryArray.includes(6)) {
            recoveryArray.push(6);
          }
          
          recoveryProcess = JSON.stringify(recoveryArray);
        } catch (error) {
          console.warn('⚠️ [OCR] Error parsing recoveryProcess:', error);
          recoveryProcess = JSON.stringify([6]); // Initialize with Failed
        }
      }

      // Only send the minimal required fields for the update
      const updateDto: any = {
        fileName: doc.fileName,
        status: 6,
        lastModified: new Date().toISOString()
      };

      // Only include recoveryProcess in update if it was modified
      if (doc.status === 6) {
        updateDto.recoveryProcess = recoveryProcess;
      }

      await documents.update(props.selectedDocument.id, updateDto);
      console.log('✅ [OCR] Document status updated to Failed (6)');
    } catch (error) {
      console.error('❌ [OCR] Failed to update document status to Failed (6):', error);
    }

    editorContent.value = 'Erreur lors du traitement OCR.';
    localSuggestions.value = [];
    correctedWords.value = [];
    precision.value = '0.00';
    processingTime.value = '0.00';
    
    showToast('Le traitement OCR a échoué. Veuillez réessayer.', 'error');
  } finally {
    stopMessageSequence();
    isLoading.value = false;
    showPageSelection.value = false;
    if (timerInterval) {
      clearInterval(timerInterval)
      timerInterval = null
    }
    console.log('🔵 [OCR] OCR process completed');
  }
};

// Add computed property for PDF detection
const isPdf = computed(() => {
  return props.selectedDocument.type?.toLowerCase().includes('pdf')
})

// Add click outside handler for export menu
const handleExportMenuClickOutside = (event: MouseEvent) => {
  const target = event.target as HTMLElement
  if (!target.closest('.export-menu') && !target.closest('button')) {
    showExportMenu.value = false
  }
}

// Add this new function after the other methods
const ignoreAllCorrections = () => {
  // Remove all corrections and suggestions
  correctedWords.value = [];
  localSuggestions.value = [];
  
  // Update the editor content to reflect the change
  nextTick(() => {
    if (editableTextRef.value) {
      editableTextRef.value.innerHTML = renderedContent.value;
    }
  });
  
  showActionMenu.value = false;
}

// Add this function to show sequential messages
const startMessageSequence = () => {
  const messages = [
    { text: 'Préparation du fichier...', duration: 2000 },
    { text: 'Envoi du fichier à l\'OCR...', duration: 2000 },
    { text: 'Extraction du texte...', duration: 3000 },
    { text: 'Analyse du texte par l\'IA...', duration: 5000 },
    { text: 'Génération des suggestions...', duration: 3000 }
  ]
  
  let currentIndex = 0
  
  const showNextMessage = () => {
    if (currentIndex < messages.length) {
      loadingMessage.value = messages[currentIndex].text
      messageTimeout.value = window.setTimeout(() => {
        currentIndex++
        showNextMessage()
      }, messages[currentIndex].duration)
    }
  }
  
  showNextMessage()
}

// Add this function to stop the message sequence
const stopMessageSequence = () => {
  if (messageTimeout.value) {
    clearTimeout(messageTimeout.value)
    messageTimeout.value = null
  }
  loadingMessage.value = 'Traitement du document en cours...'
}

// Update the close handler
const handleClose = () => {
  emit('close')
}

// Add this new method after the existing methods
const toggleSummary = () => {
  showSummary.value = !showSummary.value
}

// Update the syncSummaryContent method
const syncSummaryContent = () => {
  if (summaryEditorRef.value) {
    // Store current cursor position and selection
    const selection = window.getSelection();
    const range = selection?.getRangeAt(0);
    let cursorOffset = 0;
    
    // Calculate cursor offset from the start of the content
    if (range) {
      const preCaretRange = range.cloneRange();
      preCaretRange.selectNodeContents(summaryEditorRef.value);
      preCaretRange.setEnd(range.endContainer, range.endOffset);
      cursorOffset = preCaretRange.toString().length;
    }

    // Get the content while preserving line breaks
    const newText = summaryEditorRef.value.innerText;
    summaryContent.value = newText;
    
    // Update markdown content if in markdown mode
    if (showSummaryMarkdown.value) {
      updateSummaryMarkdownContent();
    }
    
    // Update the summary in the database
    updateSummary();
    
    // Re-render with highlighting on next tick
    nextTick(() => {
      if (summaryEditorRef.value) {
        // Update the HTML with highlighted content while preserving line breaks
        if (showSummaryMarkdown.value) {
          summaryEditorRef.value.innerHTML = summaryMarkdownContent.value;
        } else {
          summaryEditorRef.value.innerHTML = summaryContent.value;
        }
        
        // Restore cursor position
        const selection = window.getSelection();
        const range = document.createRange();
        
        // Find the text node and offset where cursor should be placed
        let currentOffset = 0;
        let targetNode = null;
        let targetOffset = 0;
        
        const walk = document.createTreeWalker(
          summaryEditorRef.value,
          NodeFilter.SHOW_TEXT,
          null
        );
        
        let node;
        while ((node = walk.nextNode())) {
          const nodeLength = node.textContent?.length || 0;
          if (currentOffset + nodeLength >= cursorOffset) {
            targetNode = node;
            targetOffset = cursorOffset - currentOffset;
            break;
          }
          currentOffset += nodeLength;
        }
        
        if (targetNode) {
          range.setStart(targetNode, targetOffset);
          range.collapse(true);
          selection?.removeAllRanges();
          selection?.addRange(range);
        }
      }
    });
  }
};

// 🚀 ENHANCED: Updated generateSummary method with better error handling and logging
const generateSummary = async () => {
  // 🎯 ENHANCED: Remove any text length restrictions - always allow summary generation
  if (!editorContent.value || editorContent.value.trim().length === 0) {
    error.value = "Veuillez d'abord extraire le texte du document";
    return;
  }

  const textLength = editorContent.value.trim().length;
  const wordCount = editorContent.value.trim().split(/\s+/).length;

  console.log('🚀 [ENHANCED_SUMMARY] Starting summary generation');
  console.log(`📏 [ENHANCED_SUMMARY] Text: ${textLength} chars, ${wordCount} words`);
  console.log(`📝 [ENHANCED_SUMMARY] Content preview: "${editorContent.value.substring(0, 100)}..."`);

  try {
    isSummaryLoading.value = true;
    error.value = null;
    summaryElapsedTime.value = 0; // Reset summary timer
    if (summaryTimerInterval) clearInterval(summaryTimerInterval);
    summaryTimerInterval = window.setInterval(() => {
      summaryElapsedTime.value++;
    }, 1000);

    // Get language code for backend
    const languageKey = String(props.selectedDocument.language);
    const language = ocrLanguageMap[languageKey.toLowerCase()] || 'eng';

    // 🚀 ENHANCED: Detailed logging for debugging
    console.log('🔵 [ENHANCED_SUMMARY] Request details:', {
      text: editorContent.value,
      textLength,
      wordCount,
      language,
      model: 'llama',
      languageKey,
      originalLanguage: props.selectedDocument.language,
      timestamp: new Date().toISOString()
    });

    // Call the summary endpoint with enhanced error handling
    console.log('📡 [ENHANCED_SUMMARY] Calling OCR service...');
    const response = await ocr.summarize(editorContent.value, language, 'llama');

    console.log('📨 [ENHANCED_SUMMARY] Response received:', response);
    console.log('📊 [ENHANCED_SUMMARY] Response data:', response.data);

    // 🚀 ENHANCED: Better response validation and error handling
    if (response && response.data) {
      if (response.data.summary && response.data.summary.trim().length > 0) {
        console.log('✅ [ENHANCED_SUMMARY] Summary generated successfully');
        console.log(`📝 [ENHANCED_SUMMARY] Summary: "${response.data.summary}"`);
        console.log(`📏 [ENHANCED_SUMMARY] Summary length: ${response.data.summary.length} chars`);

        // Ensure proper RTL handling for Arabic text
        summaryContent.value = response.data.summary;

        // Log metadata if available
        if (response.data.metadata) {
          console.log('📊 [ENHANCED_SUMMARY] Metadata:', response.data.metadata);
        }

        // Sync the content to the editor with proper RTL settings
        nextTick(() => {
          if (summaryEditorRef.value) {
            summaryEditorRef.value.innerHTML = summaryContent.value;
            // Force RTL for Arabic text
            if (language === 'ara') {
              summaryEditorRef.value.style.direction = 'rtl';
              summaryEditorRef.value.style.textAlign = 'right';
              summaryEditorRef.value.style.unicodeBidi = 'bidi-override';
            }
            console.log('✅ [ENHANCED_SUMMARY] Summary content updated in editor');
          }
        });

        // Show success message
        showToast('Résumé généré avec succès!', 'success');

      } else if (response.data.error) {
        console.error('❌ [ENHANCED_SUMMARY] Server returned error:', response.data.error);
        error.value = `Erreur du serveur: ${response.data.error}`;
      } else {
        console.warn('⚠️ [ENHANCED_SUMMARY] Empty summary received');
        console.log('📊 [ENHANCED_SUMMARY] Full response data:', JSON.stringify(response.data, null, 2));
        error.value = 'Aucun résumé généré. Le texte pourrait être trop court ou contenir des erreurs.';
      }
    } else {
      console.error('❌ [ENHANCED_SUMMARY] Invalid response structure:', response);
      error.value = 'Réponse invalide du serveur de résumé.';
    }

  } catch (err: any) {
    console.error('❌ [ENHANCED_SUMMARY] Exception during summary generation:', err);
    console.error('❌ [ENHANCED_SUMMARY] Error details:', {
      message: err.message,
      status: err.response?.status,
      statusText: err.response?.statusText,
      data: err.response?.data,
      stack: err.stack
    });

    // 🚀 ENHANCED: More specific error messages
    if (err.response) {
      if (err.response.status === 404) {
        error.value = 'Service de résumé non disponible. Vérifiez que le service OCR est démarré.';
      } else if (err.response.status === 500) {
        error.value = 'Erreur interne du serveur de résumé. Veuillez réessayer.';
      } else if (err.response.status === 422) {
        error.value = 'Données invalides envoyées au service de résumé.';
      } else {
        error.value = `Erreur HTTP ${err.response.status}: ${err.response.statusText}`;
      }
    } else if (err.code === 'NETWORK_ERROR' || err.message.includes('Network Error')) {
      error.value = 'Erreur de connexion au service de résumé. Vérifiez votre connexion.';
    } else {
      error.value = `Erreur lors de la génération du résumé: ${err.message}`;
    }

  } finally {
    isSummaryLoading.value = false;
    if (summaryTimerInterval) {
      clearInterval(summaryTimerInterval);
      summaryTimerInterval = null;
    }
    console.log('🏁 [ENHANCED_SUMMARY] Summary generation completed');
  }
};

const exportSummaryToDoc = () => {
  if (!summaryContent.value) return;
  
  // Create a Blob with the content
  const blob = new Blob([summaryContent.value], { type: 'application/msword' });
  const url = URL.createObjectURL(blob);
  
  // Create a temporary link and trigger download
  const link = document.createElement('a');
  link.href = url;
  link.download = `${props.selectedDocument.name.split('.')[0]}_resume.doc`;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
};

const exportSummaryToPdf = async () => {
  if (!summaryContent.value) return;
  
  try {
    // First convert the content to markdown
    const markdown = await marked(summaryContent.value);
    const isRtlText = isRTL(summaryContent.value);
    
    // Create a temporary div with the content
    const element = document.createElement('div');
    element.style.padding = '20px';
    element.style.fontFamily = isRtlText ? 'Noto Naskh Arabic, Arial' : 'Arial';
    element.style.fontSize = '14pt';
    element.style.lineHeight = '1.6';
    element.style.direction = isRtlText ? 'rtl' : 'ltr';
    element.style.textAlign = isRtlText ? 'right' : 'left';
    element.style.whiteSpace = 'pre-wrap';
    element.style.width = '100%';
    element.style.maxWidth = '800px';
    element.style.margin = '0 auto';
    
    // Create content div with markdown styles
    const contentDiv = document.createElement('div');
    contentDiv.style.width = '100%';
    contentDiv.style.padding = '20px';
    contentDiv.className = 'prose dark:prose-invert max-w-none';
    contentDiv.innerHTML = markdown;
    
    element.appendChild(contentDiv);
    
    // Add the element to the document temporarily
    document.body.appendChild(element);
    
    // Configure html2pdf options
    const opt = {
      margin: 10,
      filename: `${props.selectedDocument.name.split('.')[0]}_resume.pdf`,
      image: { type: 'jpeg', quality: 0.98 },
      html2canvas: { 
        scale: 2,
        useCORS: true,
        letterRendering: true,
        logging: true,
        allowTaint: true
      },
      jsPDF: { 
        unit: 'mm', 
        format: 'a4', 
        orientation: 'portrait'
      }
    };
    
    // Generate PDF
    await html2pdf().set(opt).from(element).save();
    
    // Remove the temporary element
    document.body.removeChild(element);
    
    showToast('PDF du résumé généré avec succès.');
  } catch (error) {
    console.error('Error generating summary PDF:', error);
    showToast('Impossible de générer le PDF du résumé. Veuillez réessayer.', 'error');
  }
};

const copySummaryToClipboard = async () => {
  if (!summaryContent.value) return;
  
  try {
    await navigator.clipboard.writeText(summaryContent.value);
    showToast('Le résumé a été copié dans le presse-papiers.');
  } catch (error) {
    showToast('Impossible de copier le résumé.', 'error');
  }
};

// Update the toggleSummaryMarkdown method
const toggleSummaryMarkdown = async () => {
  showSummaryMarkdown.value = !showSummaryMarkdown.value;
  if (showSummaryMarkdown.value) {
    await updateSummaryMarkdownContent();
    if (summaryEditorRef.value) {
      summaryEditorRef.value.innerHTML = summaryMarkdownContent.value;
    }
  } else {
    if (summaryEditorRef.value) {
      summaryEditorRef.value.innerHTML = summaryContent.value;
    }
  }
};

// Update the updateSummaryMarkdownContent method
const updateSummaryMarkdownContent = async () => {
  if (summaryContent.value) {
    let content = summaryContent.value;
    const mathExpressions: string[] = [];
    content = content.replace(/\$([^$]+)\$/g, (match) => {
      mathExpressions.push(match);
      return `MATH_PLACEHOLDER_${mathExpressions.length - 1}`;
    });
    let markdown = await marked(content);
    mathExpressions.forEach((expr, index) => {
      const placeholder = `MATH_PLACEHOLDER_${index}`;
      try {
        const renderedMath = katex.renderToString(expr.slice(1, -1), {
          throwOnError: false,
          displayMode: false,
          output: 'html'
        });
        markdown = markdown.replace(placeholder, renderedMath);
      } catch (e) {
        console.error('Error rendering math:', e);
        markdown = markdown.replace(placeholder, expr);
      }
    });
    summaryMarkdownContent.value = markdown;
  }
};

// Add a watch for content changes
watch(() => summaryContent.value, async () => {
  if (showSummaryMarkdown.value) {
    await updateSummaryMarkdownContent();
    if (summaryEditorRef.value) {
      summaryEditorRef.value.innerHTML = summaryMarkdownContent.value;
    }
  }
});

// Add click outside handler for summary export menu
const handleSummaryExportMenuClickOutside = (event: MouseEvent) => {
  const target = event.target as HTMLElement
  if (!target.closest('.summary-export-menu') && !target.closest('button')) {
    showSummaryExportMenu.value = false
  }
};

// Update onMounted to add the new event listener
onMounted(() => {
  document.addEventListener('click', handleClickOutside);
  document.addEventListener('click', closeActionMenu);
  document.addEventListener('click', handleExportMenuClickOutside);
  document.addEventListener('click', handleSummaryExportMenuClickOutside);
});

// Update onUnmounted to remove the new event listener
onUnmounted(() => {
  document.removeEventListener('click', handleClickOutside);
  document.removeEventListener('click', closeActionMenu);
  document.removeEventListener('click', handleExportMenuClickOutside);
  document.removeEventListener('click', handleSummaryExportMenuClickOutside);
});

// Add this new method after the other methods
const updateSummary = async () => {
  try {
    isSummaryLoading.value = true;
    
    console.log('🔵 [updateSummary] Updating summary for document:', props.selectedDocument.id);
    
    // Update only the summary field
    const response = await documents.update(props.selectedDocument.id, {
      summary: summaryContent.value,
      lastModified: new Date().toISOString()
    });

    if (!response.data?.success) {
      throw new Error('Failed to update summary');
    }
    
    console.log('✅ [updateSummary] Summary updated successfully:', response);
    showToast('Résumé mis à jour avec succès.');
  } catch (error: any) {
    console.error('❌ [updateSummary] Error updating summary:', error);
    showToast('Impossible de mettre à jour le résumé. Veuillez réessayer.', 'error');
  } finally {
    isSummaryLoading.value = false;
  }
};



// Add these new functions to handle different types of suggestions
const getTesseractSuggestions = (word: string) => {
  const suggestion = localSuggestions.value.find(s => s.word === word);
  if (!suggestion) return [];
  return suggestion.suggestions.filter(s => s.text.startsWith('(tes)'));
};

const getLLMSuggestions = (word: string) => {
  const suggestion = localSuggestions.value.find(s => s.word === word);
  if (!suggestion) return [];
  return suggestion.suggestions.filter(s => !s.text.startsWith('(tes)'));
};

// Add this method to render text with math and handle bidirectional text
const renderTextWithMath = (text: string): string => {
  if (!text) return '';
  
  console.log('🔍 [renderTextWithMath] Starting with text:', text);
  
  let result = '';
  let lastIndex = 0;
  
  // First, handle display math ($$...$$)
  const displayMathRegex = /\$\$([\s\S]*?)\$\$/g;
  let displayMathMatch;
  while ((displayMathMatch = displayMathRegex.exec(text)) !== null) {
    console.log('📝 [renderTextWithMath] Found display math:', displayMathMatch[1]);
    // Add text before the math expression
    if (displayMathMatch.index > lastIndex) {
      result += renderBidirectionalText(text.substring(lastIndex, displayMathMatch.index));
    }
    // Add the display math expression with proper spacing
    try {
      const renderedMath = katex.renderToString(displayMathMatch[1], {
        throwOnError: false,
        displayMode: true,
        output: 'html'
      });
      result += `<div class="katex-display">${renderedMath}</div>`;
      console.log('✅ [renderTextWithMath] Successfully rendered display math');
    } catch (e) {
      console.error('❌ [renderTextWithMath] Error rendering display math:', e);
      result += displayMathMatch[0];
    }
    lastIndex = displayMathRegex.lastIndex;
  }

  // Then handle inline math ($...$)
  const inlineMathRegex = /\$([^$]+?)\$/g;
  let inlineMathMatch;
  while ((inlineMathMatch = inlineMathRegex.exec(text)) !== null) {
    console.log('📝 [renderTextWithMath] Found inline math:', inlineMathMatch[1]);
    
    // Add text before the math expression
    if (inlineMathMatch.index > lastIndex) {
      result += renderBidirectionalText(text.substring(lastIndex, inlineMathMatch.index));
    }
    
    // Process the entire math expression as one unit
    const mathContent = inlineMathMatch[1];
    console.log('🔍 [renderTextWithMath] Processing math content:', mathContent);
    
    try {
      const renderedMath = katex.renderToString(mathContent, {
        throwOnError: false,
        displayMode: false,
        output: 'html'
      });
      result += `<span class="katex-inline">${renderedMath}</span>`;
      console.log('✅ [renderTextWithMath] Successfully rendered math expression');
    } catch (e) {
      console.error('❌ [renderTextWithMath] Error rendering math expression:', e);
      result += inlineMathMatch[0];
    }
    
    lastIndex = inlineMathRegex.lastIndex;
  }

  // Add any remaining text after the last math expression
  if (lastIndex < text.length) {
    result += renderBidirectionalText(text.substring(lastIndex));
  }

  console.log('🔍 [renderTextWithMath] Final result:', result);
  return result;
};

// Add a helper function to handle bidirectional text for numbers
const renderBidirectionalText = (text: string): string => {
    if (!text) return '';

    let result = '';
    let lastIndex = 0;
    const numberRegex = /(\d+([\\.\\,]\\d+)?)/g; // Regex for numbers (integers or decimals)

    let numberMatch;
    while ((numberMatch = numberRegex.exec(text)) !== null) {
        // Add text before the number
        if (numberMatch.index > lastIndex) {
            result += text.substring(lastIndex, numberMatch.index);
        }

        // Wrap the number in a span with LTR direction
        result += `<span style="direction: ltr; unicode-bidi: embed;">${numberMatch[0]}</span>`;
        lastIndex = numberRegex.lastIndex;
    }

    // Add any remaining text after the last number
    if (lastIndex < text.length) {
        result += text.substring(lastIndex);
    }

    return result;
};

// Update the text update methods to handle math expressions
const updateExtractedText = async (newText: string) => {
  editorContent.value = newText;
  // Don't process with marked for math expressions
  markdownContent.value = newText;
  summaryContent.value = newText;
  summaryMarkdownContent.value = newText;
};

const updateEditedText = async (newText: string) => {
  editorContent.value = newText;
  // Don't process with marked for math expressions
  markdownContent.value = newText;
  summaryContent.value = newText;
  summaryMarkdownContent.value = newText;
};

// Add KaTeX CSS to document head if not already present
onMounted(() => {
  if (!document.querySelector('link[href*="katex.min.css"]')) {
    const link = document.createElement('link');
    link.rel = 'stylesheet';
    link.href = 'https://cdn.jsdelivr.net/npm/katex@0.16.9/dist/katex.min.css';
    document.head.appendChild(link);
  }
});

// Update the toastTimeout ref type
const toastTimeout = ref<ReturnType<typeof setTimeout> | null>(null);

// Add this at the top with other refs
const originalContent = ref(props.initialContent);

// In onMounted, set originalContent
onMounted(async () => {
  originalContent.value = props.initialContent;
  // ... existing code ...
});

// Replace the text rebuilding logic in applyAllCorrections with this:
const applyAllCorrections = () => {
  console.log('[applyAllCorrections] called'); // <-- Add this log at the very top
  let text = originalContent.value;
  let offset = 0;
  // Sort by start position in the original text
  const sorted = [...correctedWords.value].sort((a, b) => a.start - b.start);
  sorted.forEach((correction, idx) => {
    const start = correction.start + offset;
    const end = start + correction.originalWord.length;
    const wordAtPos = text.substring(start, end);
    console.log(`\n[applyAllCorrections] Correction #${idx + 1}`);
    console.log('  Current text:', text);
    console.log('  Correction:', correction);
    console.log('  Calculated start:', start, 'end:', end);
    console.log('  Word at position:', wordAtPos);
    if (wordAtPos === correction.originalWord) {
      console.log('  Replacement performed:', `${correction.originalWord} -> ${correction.correctedWord}`);
      text = text.substring(0, start) + correction.correctedWord + text.substring(end);
      offset += correction.correctedWord.length - correction.originalWord.length;
    } else {
      console.warn('  Replacement skipped: word at position does not match originalWord');
    }
  });
  console.log('[applyAllCorrections] Final text:', text);
  return text;
};

// In applySuggestion, after updating correctedWords, set:
editorContent.value = applyAllCorrections();
</script>

<style>
/* Update confidence level colors and add hover effect */
.highlighted-word {
  background-color: rgba(239, 68, 68, 0.5);
  padding: 2px;
  border-radius: 3px;
  cursor: pointer;
  transition: all 0.2s ease;
}

.highlighted-word:hover {
  background-color: rgba(239, 68, 68, 0.3);
}

.corrected-word {
  position: relative;
  cursor: pointer;
  transition: all 0.2s ease;
  padding: 2px;
  border-radius: 3px;
  display: inline-block;
}

.corrected-word::after {
  content: '';
  position: absolute;
  left: 0;
  right: 0;
  bottom: -2px;
  height: 2px;
  background-color: rgb(239, 68, 68);
  transform-origin: left;
  transform: scaleX(1);
  transition: transform 0.2s ease;
}

.corrected-word:hover {
  color: rgb(168, 85, 247);
  background-color: rgba(168, 85, 247, 0.1);
}

.corrected-word:hover::after {
  transform: scaleX(1.1);
  background-color: rgb(168, 85, 247);
}

/* Add styles for the editable content area */
[contenteditable="true"] {
  white-space: pre-wrap;
  word-wrap: break-word;
  overflow-wrap: break-word;
  -webkit-user-modify: read-write-plaintext-only;
  line-height: 1.5;
  unicode-bidi: plaintext;
}

/* Add RTL support for the editable content */
[contenteditable="true"][dir="rtl"] {
  text-align: right;
}

.suggestion-dropdown {
  @apply bg-white rounded-lg shadow-lg border border-gray-200;
  min-width: 300px;
  max-width: 400px;
}

.suggestion-item {
  @apply w-full px-3 py-2 text-left text-sm hover:bg-gray-50;
  display: flex;
  align-items: center;
  justify-content: space-between;
  position: relative;
  padding: 0.5rem 1rem;
  min-height: 2.5rem;
  box-sizing: border-box;
  width: 100%;
}

/* Add RTL support for suggestion items */
.suggestion-item[dir="rtl"] {
  text-align: right;
  flex-direction: row-reverse;
}

.suggestion-text {
  flex: 1 1 auto;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  margin-right: 1rem;
}

/* Update margin for RTL text */
.suggestion-item[dir="rtl"] .suggestion-text {
  margin-right: 0;
  margin-left: 1rem;
}

.confidence-badge {
  @apply px-2 py-0.5 text-xs rounded-full bg-gray-100 text-gray-600;
  width: 4rem;
  min-width: 4rem;
  text-align: center;
  flex-shrink: 0;
  margin-left: auto; /* Default for LTR */
  align-self: flex-start;
  white-space: nowrap;
  direction: ltr !important;
  unicode-bidi: embed !important;
}

/* Update margin for RTL text */
.suggestion-item[dir="rtl"] .confidence-badge {
  margin-left: auto; /* Push to the left in RTL layout */
  margin-right: 0;
}

/* Add styles for math expressions */
.katex {
  font-size: 1.1em;
  margin: 0 0.2em;
}

/* Ensure text areas can display math properly */
.text-area {
  white-space: pre-wrap;
  word-wrap: break-word;
  overflow-wrap: break-word;
}

/* Add styles for math display */
.katex-display {
  margin: 1em 0;
  text-align: center;
}

/* Ensure proper spacing around math expressions */
.text-area .katex {
  vertical-align: middle;
}

/* Update text area styles */
.text-area {
  white-space: pre-wrap;
  word-wrap: break-word;
  overflow-wrap: break-word;
  line-height: 1.5;
}

/* Add styles for math expressions */
.text-area .katex {
  vertical-align: middle;
  font-size: 1.1em;
}

.text-area .katex-display {
  margin: 1em 0;
  text-align: center;
}

/* Math display styles */
.katex-display {
  margin: 1em 0;
  padding: 1em;
  overflow-x: auto;
  overflow-y: hidden;
  text-align: center;
  background-color: rgba(0, 0, 0, 0.03);
  border-radius: 4px;
}

.katex-display > .katex {
  display: inline-block;
  text-align: initial;
  font-size: 1.21em;
}

.katex {
  font-size: 1.1em;
  line-height: 1.2;
  text-indent: 0;
  text-rendering: auto;
}

/* Ensure proper spacing around math expressions */
.katex-display + .katex-display {
  margin-top: 1em;
}

/* Add styles for math in contenteditable */
[contenteditable="true"] .katex-display {
  margin: 0.5em 0;
  padding: 0.5em;
}

[contenteditable="true"] .katex {
  pointer-events: none;
  user-select: none;
}

/* Update math expression styles */
.katex-display {
  margin: 1em 0;
  padding: 1em;
  overflow-x: auto;
  overflow-y: hidden;
  text-align: center;
  background-color: rgba(0, 0, 0, 0.03);
  border-radius: 4px;
  display: block;
}

.katex-inline {
  display: inline-block;
  vertical-align: middle;
  margin: 0 0.2em;
}

.katex {
  font-size: 1.1em;
  line-height: 1.2;
  text-indent: 0;
  text-rendering: auto;
}

/* Ensure proper spacing around math expressions */
.katex-display + .katex-display {
  margin-top: 1em;
}

/* Add styles for math in contenteditable */
[contenteditable="true"] .katex-display {
  margin: 0.5em 0;
  padding: 0.5em;
  pointer-events: none;
  user-select: none;
}

[contenteditable="true"] .katex-inline {
  pointer-events: none;
  user-select: none;
}

/* Ensure text areas can display math properly */
.text-area {
  white-space: pre-wrap;
  word-wrap: break-word;
  overflow-wrap: break-word;
}

/* Add styles for math display */
.katex-display > .katex {
  display: inline-block;
  text-align: initial;
  font-size: 1.21em;
}

/* Update the editable content area styles */
[contenteditable="true"] {
  white-space: pre-wrap;
  word-wrap: break-word;
  overflow-wrap: break-word;
  -webkit-user-modify: read-write-plaintext-only;
  line-height: 1.5;
  unicode-bidi: plaintext;
}

/* Add RTL support for the editable content */
[contenteditable="true"][dir="rtl"] {
  text-align: right;
}
</style>