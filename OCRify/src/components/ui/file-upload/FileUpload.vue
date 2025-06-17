<!-- FileUpload.vue -->
<script setup lang="ts">
import { ref, computed } from 'vue'
import { Upload, X, FileText, AlertCircle, CheckCircle2, Loader2, Plus } from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { Progress } from '@/components/ui/progress'
import { useToast } from '@/components/ui/toast/use-toast'
import { documents } from '@/services/api'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import {
  Stepper,
  StepperItem,
  StepperIndicator,
  StepperTitle,
  StepperDescription,
  StepperTrigger,
  StepperSeparator
} from '@/components/ui/stepper'

const props = defineProps<{
  multiple?: boolean
  maxSize?: number // in MB
  accept?: string
  showPreview?: boolean
}>()

const emit = defineEmits<{
  (e: 'upload-success', files: File[]): void
  (e: 'upload-error', error: any): void
  (e: 'file-select', files: File[]): void
}>()

const { toast } = useToast()
const dragOver = ref(false)
const isUploading = ref(false)
const uploadProgress = ref(0)
const selectedFiles = ref<File[]>([])
const previewUrls = ref<string[]>([])

// Per-file metadata array
const fileMetadata = ref<any[]>([])
const newTag = ref('')
const tagInputIndex = ref(-1)

const singleFileMetadata = ref({
  language: 'French',
  topic: '',
  description: '',
  tags: [] as string[]
})

const maxSizeBytes = computed(() => (props.maxSize || 10) * 1024 * 1024)

const handleFileSelect = (files: FileList | null) => {
  if (!files) return
  selectedFiles.value = []
  previewUrls.value = []
  fileMetadata.value = []
  singleFileMetadata.value = {
    language: 'French',
    topic: '',
    description: '',
    tags: []
  }
  Array.from(files).forEach(file => {
    if (file.size > maxSizeBytes.value) {
      toast({
        title: 'Fichier trop volumineux',
        description: `${file.name} dépasse la taille maximale de ${props.maxSize}MB`,
        variant: 'destructive'
      })
      return
    }
    selectedFiles.value.push(file)
    fileMetadata.value.push({
      language: 'French',
      topic: '',
      description: '',
      tags: []
    })
    if (props.showPreview && file.type.startsWith('image/')) {
      const reader = new FileReader()
      reader.onload = (e) => {
        if (e.target?.result) {
          previewUrls.value.push(e.target.result as string)
        }
      }
      reader.readAsDataURL(file)
    }
  })
  emit('file-select', selectedFiles.value)
}

const simulateProgress = () => {
  uploadProgress.value = 0
  const interval = setInterval(() => {
    if (uploadProgress.value < 90) {
      uploadProgress.value += Math.random() * 10
    }
  }, 200)
  return interval
}

const handleUpload = async () => {
  if (!selectedFiles.value.length) return
  try {
    isUploading.value = true
    const progressInterval = simulateProgress()
    const files = selectedFiles.value
    if (files.length === 1) {
      // Single file upload
      const meta = singleFileMetadata.value
      const payload = {
        language: meta.language === 'French' ? 1 : meta.language === 'English' ? 0 : 2,
        topic: meta.topic || '',
        description: meta.description || '',
        tags: meta.tags.map((tag: string) => tag.trim()).filter((tag: string) => tag),
        isFavorite: false
      }
      await documents.upload(files[0], payload)
    } else {
      // Bulk upload
      const metadataArr = fileMetadata.value.map(meta => ({
        language: String(meta.language === 'French' ? 1 : meta.language === 'English' ? 0 : 2),
        topic: meta.topic || '',
        description: meta.description || '',
        tags: meta.tags.map((tag: string) => tag.trim()).filter((tag: string) => tag),
        isFavorite: false
      }))
      await documents.bulkUpload(files, metadataArr)
    }
    toast({
      title: 'Téléversement réussi',
      description: `${files.length} document(s) ont été téléversés.`,
    })
    emit('upload-success', files)
    selectedFiles.value = []
    previewUrls.value = []
    fileMetadata.value = []
    singleFileMetadata.value = {
      language: 'French',
      topic: '',
      description: '',
      tags: []
    }
    newTag.value = ''
    tagInputIndex.value = -1
  } catch (error: any) {
    console.error('Upload error:', error)
    toast({
      title: 'Erreur de téléchargement',
      description: error.response?.data?.message || error.message,
      variant: 'destructive'
    })
    emit('upload-error', error)
  } finally {
    setTimeout(() => {
      isUploading.value = false
      uploadProgress.value = 0
      selectedFiles.value = []
      previewUrls.value = []
      fileMetadata.value = []
      singleFileMetadata.value = {
        language: 'French',
        topic: '',
        description: '',
        tags: []
      }
      tagInputIndex.value = -1
    }, 1000)
  }
}

const removeFile = (index: number) => {
  selectedFiles.value.splice(index, 1)
  previewUrls.value.splice(index, 1)
  fileMetadata.value.splice(index, 1)
}

const handleDrop = (event: DragEvent) => {
  event.preventDefault()
  dragOver.value = false
  handleFileSelect(event.dataTransfer?.files || null)
}
</script>

<template>
  <div class="space-y-4">
    <!-- Upload Area -->
    <div
      class="border-2 border-dashed rounded-lg p-6 transition-all duration-200 relative"
      :class="{
        'border-primary bg-primary/5': dragOver,
        'hover:bg-muted/50': !isUploading,
        'border-muted cursor-not-allowed': isUploading
      }"
      @dragover.prevent="() => dragOver = true"
      @dragleave.prevent="() => dragOver = false"
      @drop.prevent="handleDrop"
    >
      <input
        type="file"
        :multiple="props.multiple"
        :accept="props.accept"
        class="absolute inset-0 w-full h-full opacity-0 cursor-pointer"
        :class="{ 'pointer-events-none': isUploading }"
        @change="(e: Event) => {
          const target = e.target as HTMLInputElement
          handleFileSelect(target.files)
        }"
      />
      
      <div class="flex flex-col items-center justify-center gap-2">
        <div class="rounded-full p-3 bg-primary/10">
          <Upload v-if="!isUploading" class="h-6 w-6 text-primary animate-pulse" />
          <Loader2 v-else class="h-6 w-6 text-primary animate-spin" />
        </div>
        
        <div class="text-center">
          <p class="text-sm font-medium">
            {{ isUploading ? 'Téléchargement en cours...' : 'Glissez vos documents ici' }}
          </p>
          <p class="text-xs text-muted-foreground mt-1">
            {{ isUploading ? 'Veuillez patienter' : 'ou cliquez pour sélectionner' }}
          </p>
          <p v-if="!isUploading" class="text-xs text-muted-foreground mt-1">
            PDF, JPG, PNG (max {{ props.maxSize || 10 }}MB)
          </p>
        </div>
      </div>
      
      <!-- Upload Progress -->
      <div v-if="isUploading" class="mt-4">
        <Progress :value="uploadProgress" class="h-1">
          <div
            class="h-full bg-primary transition-all duration-500"
            :style="{ width: `${uploadProgress}%` }"
          ></div>
        </Progress>
        <p class="text-xs text-center text-muted-foreground mt-2">
          {{ Math.round(uploadProgress) }}%
        </p>
      </div>
    </div>
    
    <!-- Single file metadata form -->
    <div v-if="selectedFiles.length === 1 && !isUploading" class="space-y-4 p-4 bg-muted/30 rounded-lg">
      <div class="flex items-center gap-3 mb-4">
        <FileText class="h-5 w-5 text-primary" />
        <span class="font-medium truncate" style="max-width: 200px; overflow: hidden; white-space: nowrap; text-overflow: ellipsis;" :title="selectedFiles[0].name">
          {{ selectedFiles[0].name }}
        </span>
        <span class="text-xs text-muted-foreground">{{ (selectedFiles[0].size / 1024 / 1024).toFixed(2) }} MB</span>
      </div>
      <div class="grid gap-4">
        <div class="grid grid-cols-4 items-center gap-4">
          <Label for="language" class="text-right">Langue</Label>
          <Select v-model="singleFileMetadata.language" class="col-span-3">
            <SelectTrigger>
              <SelectValue placeholder="Sélectionner une langue" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="French">Français</SelectItem>
              <SelectItem value="English">English</SelectItem>
              <SelectItem value="Arabic">العربية</SelectItem>
            </SelectContent>
          </Select>
        </div>
        <div class="grid grid-cols-4 items-center gap-4">
          <Label for="topic" class="text-right">Sujet</Label>
          <Input id="topic" v-model="singleFileMetadata.topic" class="col-span-3" placeholder="Sujet du document" />
        </div>
        <div class="grid grid-cols-4 items-center gap-4">
          <Label for="description" class="text-right">Description</Label>
          <Textarea id="description" v-model="singleFileMetadata.description" class="col-span-3" placeholder="Description du document" />
        </div>
        <div class="grid grid-cols-4 items-center gap-4">
          <Label for="tags" class="text-right">Tags</Label>
          <div class="col-span-3 space-y-2">
            <div class="flex flex-wrap gap-2">
              <div
                v-for="(tag, index) in singleFileMetadata.tags"
                :key="index"
                class="flex items-center gap-1 px-2 py-1 bg-primary/10 rounded-full text-sm"
              >
                <span>{{ tag }}</span>
                <Button
                  variant="ghost"
                  size="icon"
                  class="h-4 w-4 hover:bg-primary/20"
                  @click="singleFileMetadata.tags.splice(index, 1)"
                >
                  <X class="h-3 w-3" />
                </Button>
              </div>
            </div>
            <div class="flex items-center gap-2">
              <Input
                v-model="newTag"
                class="w-full"
                placeholder="Ajouter un tag"
                @keydown.enter="() => {
                  if (newTag.trim()) {
                    singleFileMetadata.tags.push(newTag.trim())
                    newTag = ''
                  }
                }"
              />
              <Button
                variant="outline"
                size="sm"
                @click="() => {
                  if (newTag.trim()) {
                    singleFileMetadata.tags.push(newTag.trim())
                    newTag = ''
                  }
                }"
              >
                <Plus class="h-4 w-4" />
              </Button>
            </div>
          </div>
        </div>
      </div>
      <div class="flex justify-end">
        <Button :disabled="isUploading" @click="handleUpload">
          <Upload v-if="!isUploading" class="h-4 w-4 mr-2" />
          <Loader2 v-else class="h-4 w-4 mr-2 animate-spin" />
          {{ isUploading ? 'Téléchargement...' : 'Téléverser' }}
        </Button>
      </div>
    </div>
    
    <!-- Bulk Edit Table/List -->
    <div v-if="selectedFiles.length > 1 && !isUploading" class="overflow-x-auto">
      <table class="min-w-full divide-y divide-gray-200">
        <thead>
          <tr>
            <th class="px-2 py-2 text-left text-xs font-medium text-gray-500 uppercase">Fichier</th>
            <th class="px-2 py-2 text-left text-xs font-medium text-gray-500 uppercase">Taille</th>
            <th class="px-2 py-2 text-left text-xs font-medium text-gray-500 uppercase">Langue</th>
            <th class="px-2 py-2 text-left text-xs font-medium text-gray-500 uppercase">Sujet</th>
            <th class="px-2 py-2 text-left text-xs font-medium text-gray-500 uppercase">Description</th>
            <th class="px-2 py-2 text-left text-xs font-medium text-gray-500 uppercase">Tags</th>
            <th class="px-2 py-2"></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="(file, idx) in selectedFiles" :key="file.name + idx" class="bg-white even:bg-gray-50">
            <td class="px-2 py-2 max-w-[200px] truncate" :title="file.name">
              <div class="flex items-center gap-2">
                <FileText class="h-5 w-5 text-primary" />
                <span class="truncate">{{ file.name }}</span>
              </div>
            </td>
            <td class="px-2 py-2 text-xs text-muted-foreground">{{ (file.size / 1024 / 1024).toFixed(2) }} MB</td>
            <td class="px-2 py-2">
              <Select v-model="fileMetadata[idx].language">
                <SelectTrigger>
                  <SelectValue placeholder="Sélectionner une langue" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="French">Français</SelectItem>
                  <SelectItem value="English">English</SelectItem>
                  <SelectItem value="Arabic">العربية</SelectItem>
                </SelectContent>
              </Select>
            </td>
            <td class="px-2 py-2">
              <Input v-model="fileMetadata[idx].topic" placeholder="Sujet" />
            </td>
            <td class="px-2 py-2">
              <Textarea v-model="fileMetadata[idx].description" placeholder="Description" rows="1" />
            </td>
            <td class="px-2 py-2">
              <div class="flex flex-wrap gap-1">
                <span v-for="(tag, tagIdx) in fileMetadata[idx].tags" :key="tagIdx" class="px-2 py-0.5 bg-primary/10 rounded-full text-xs flex items-center gap-1">
                  {{ tag }}
                  <button type="button" @click="fileMetadata[idx].tags.splice(tagIdx, 1)" class="ml-1 text-gray-400 hover:text-red-500">
                    <X class="h-3 w-3" />
                  </button>
                </span>
                <Input
                  v-if="tagInputIndex === idx"
                  v-model="newTag"
                  class="w-20 text-xs"
                  placeholder="+ tag"
                  @keydown.enter.prevent="() => { if (newTag.trim()) { fileMetadata[idx].tags.push(newTag.trim()); newTag = ''; tagInputIndex = -1; } }"
                  @blur="() => { tagInputIndex = -1; newTag = '' }"
                  autofocus
                />
                <Button v-else variant="outline" size="icon" class="h-5 w-5 p-0" @click="() => { tagInputIndex = idx; newTag = '' }">
                  <Plus class="h-3 w-3" />
                </Button>
              </div>
            </td>
            <td class="px-2 py-2">
              <Button variant="ghost" size="icon" @click="removeFile(idx)">
                <X class="h-4 w-4" />
              </Button>
            </td>
          </tr>
        </tbody>
      </table>
      <div class="flex justify-end mt-4">
        <Button :disabled="isUploading" @click="handleUpload">
          <Upload v-if="!isUploading" class="h-4 w-4 mr-2" />
          <Loader2 v-else class="h-4 w-4 mr-2 animate-spin" />
          {{ isUploading ? 'Téléchargement...' : 'Tout téléverser' }}
        </Button>
      </div>
    </div>
  </div>
</template> 