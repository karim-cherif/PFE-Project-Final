<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { documents } from '@/services/api'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Badge } from '@/components/ui/badge'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { useToast } from '@/components/ui/toast/use-toast'
import { Share2, Mail, Link, Trash2, Calendar, User } from 'lucide-vue-next'

interface ShareToken {
  id: string
  documentId: string
  token: string
  expiryDate: string
  type: string
  createdAt: string
  createdBy: string
  documentName: string
  recipientEmail: string
  isExpired: boolean
}

interface Props {
  open: boolean
  documentId: string
  documentName: string
}

interface Emits {
  (e: 'update:open', value: boolean): void
  (e: 'shared'): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

const { toast } = useToast()

// State
const loading = ref(false)
const shares = ref<ShareToken[]>([])
const shareForm = ref({
  email: '',
  type: 'Original'
})

// Computed
const isOpen = computed({
  get: () => props.open,
  set: (value) => emit('update:open', value)
})

const shareTypes = [
  { value: 'Original', label: 'Document original', description: 'Partager le fichier original (PDF, image, etc.)' },
  { value: 'ExtractedText', label: 'Texte extrait', description: 'Partager le texte extrait par OCR (fichier .txt)' }
]

// Methods
const fetchShares = async () => {
  if (!props.documentId) return

  try {
    loading.value = true
    const response = await documents.getDocumentShares(props.documentId)
    if (response.success) {
      shares.value = response.data || []
    }
  } catch (error: any) {
    console.error('Error fetching shares:', error)
    // Don't show error toast for this as it might be expected
  } finally {
    loading.value = false
  }
}

const shareDocument = async () => {
  if (!shareForm.value.email || !shareForm.value.type) {
    toast({
      title: 'Erreur',
      description: 'Veuillez remplir tous les champs',
      variant: 'destructive'
    })
    return
  }

  try {
    loading.value = true
    await documents.shareDocument(props.documentId, {
      email: shareForm.value.email,
      type: shareForm.value.type
    })

    toast({
      title: 'Succès',
      description: 'Document partagé avec succès'
    })

    // Reset form
    shareForm.value = {
      email: '',
      type: 'Original'
    }

    // Refresh shares list
    await fetchShares()
    emit('shared')
  } catch (error: any) {
    toast({
      title: 'Erreur',
      description: error.response?.data?.message || 'Impossible de partager le document',
      variant: 'destructive'
    })
  } finally {
    loading.value = false
  }
}

const revokeShare = async (shareId: string) => {
  try {
    await documents.revokeDocumentShare(shareId)
    toast({
      title: 'Succès',
      description: 'Partage révoqué avec succès'
    })
    await fetchShares()
  } catch (error: any) {
    toast({
      title: 'Erreur',
      description: 'Impossible de révoquer le partage',
      variant: 'destructive'
    })
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

const getShareTypeBadge = (type: string) => {
  switch (type) {
    case 'Original':
      return { variant: 'default' as const, label: 'Document original' }
    case 'ExtractedText':
      return { variant: 'secondary' as const, label: 'Texte extrait' }
    case 'Ocr':  // Legacy support
      return { variant: 'secondary' as const, label: 'Texte OCR' }
    case 'Enhanced':  // Legacy support
      return { variant: 'outline' as const, label: 'Texte amélioré' }
    default:
      return { variant: 'default' as const, label: type }
  }
}

const copyShareLink = async (token: string) => {
  // Use the backend API URL for the share link
  const apiUrl = import.meta.env.VITE_API_URL || 'http://localhost:5000'
  const shareLink = `${apiUrl}/api/Documents/download?token=${token}`
  try {
    await navigator.clipboard.writeText(shareLink)
    toast({
      title: 'Succès',
      description: 'Lien copié dans le presse-papiers'
    })
  } catch (error) {
    toast({
      title: 'Erreur',
      description: 'Impossible de copier le lien',
      variant: 'destructive'
    })
  }
}

// Watch for dialog open/close
onMounted(() => {
  if (props.open) {
    fetchShares()
  }
})

// Fetch shares when dialog opens
const handleOpenChange = (open: boolean) => {
  isOpen.value = open
  if (open) {
    fetchShares()
  }
}
</script>

<template>
  <Dialog :open="isOpen" @update:open="handleOpenChange">
    <DialogContent class="sm:max-w-[600px]">
      <DialogHeader>
        <DialogTitle class="flex items-center gap-2">
          <Share2 class="h-5 w-5" />
          Partager le document
        </DialogTitle>
        <DialogDescription>
          Partagez "{{ documentName }}" avec d'autres utilisateurs
        </DialogDescription>
      </DialogHeader>

      <Tabs default-value="share" class="w-full">
        <TabsList class="grid w-full grid-cols-2">
          <TabsTrigger value="share">Nouveau partage</TabsTrigger>
          <TabsTrigger value="manage">Gérer les partages</TabsTrigger>
        </TabsList>

        <TabsContent value="share" class="space-y-4">
          <div class="grid gap-4">
            <div class="grid gap-2">
              <label for="email" class="text-sm font-medium">Email du destinataire</label>
              <Input
                id="email"
                v-model="shareForm.email"
                type="email"
                placeholder="exemple@email.com"
                :disabled="loading"
              />
            </div>

            <div class="grid gap-2">
              <label for="type" class="text-sm font-medium">Type de partage</label>
              <Select v-model="shareForm.type" :disabled="loading">
                <SelectTrigger>
                  <SelectValue placeholder="Sélectionner le type" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem
                    v-for="type in shareTypes"
                    :key="type.value"
                    :value="type.value"
                  >
                    <div class="flex flex-col">
                      <span class="font-medium">{{ type.label }}</span>
                      <span class="text-xs text-muted-foreground">{{ type.description }}</span>
                    </div>
                  </SelectItem>
                </SelectContent>
              </Select>
            </div>

            <Button @click="shareDocument" :disabled="loading" class="w-full">
              <Mail class="h-4 w-4 mr-2" />
              {{ loading ? 'Partage en cours...' : 'Partager par email' }}
            </Button>
          </div>
        </TabsContent>

        <TabsContent value="manage" class="space-y-4">
          <div class="text-sm text-muted-foreground">
            Gérez les partages existants pour ce document
          </div>

          <div v-if="loading" class="text-center py-4">
            Chargement des partages...
          </div>

          <div v-else-if="shares.length === 0" class="text-center py-8 text-muted-foreground">
            <Share2 class="h-12 w-12 mx-auto mb-4 opacity-50" />
            <p>Aucun partage actif pour ce document</p>
          </div>

          <div v-else class="border rounded-md">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Type</TableHead>
                  <TableHead>Créé le</TableHead>
                  <TableHead>Expire le</TableHead>
                  <TableHead>Statut</TableHead>
                  <TableHead class="w-24">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                <TableRow v-for="share in shares" :key="share.id">
                  <TableCell>
                    <Badge :variant="getShareTypeBadge(share.type).variant">
                      {{ getShareTypeBadge(share.type).label }}
                    </Badge>
                  </TableCell>
                  <TableCell class="text-sm">
                    <div class="flex items-center gap-1">
                      <Calendar class="h-3 w-3" />
                      {{ formatDate(share.createdAt) }}
                    </div>
                  </TableCell>
                  <TableCell class="text-sm">
                    <div class="flex items-center gap-1">
                      <Calendar class="h-3 w-3" />
                      {{ formatDate(share.expiryDate) }}
                    </div>
                  </TableCell>
                  <TableCell>
                    <Badge :variant="share.isExpired ? 'destructive' : 'default'">
                      {{ share.isExpired ? 'Expiré' : 'Actif' }}
                    </Badge>
                  </TableCell>
                  <TableCell>
                    <div class="flex items-center gap-1">
                      <Button
                        size="sm"
                        variant="ghost"
                        @click="copyShareLink(share.token)"
                        :disabled="share.isExpired"
                      >
                        <Link class="h-4 w-4" />
                      </Button>
                      <Button
                        size="sm"
                        variant="ghost"
                        @click="revokeShare(share.id)"
                        :disabled="share.isExpired"
                      >
                        <Trash2 class="h-4 w-4" />
                      </Button>
                    </div>
                  </TableCell>
                </TableRow>
              </TableBody>
            </Table>
          </div>
        </TabsContent>
      </Tabs>

      <DialogFooter>
        <Button variant="outline" @click="isOpen = false">
          Fermer
        </Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
