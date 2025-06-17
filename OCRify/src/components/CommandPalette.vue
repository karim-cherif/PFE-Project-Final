<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import { Dialog, DialogContent } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import {
  Search,
  Home,
  FileText,
  User,
  HelpCircle,
  History,
  Settings,
  Command,
  Trash2,
  FileEdit,
  Moon,
  Sun,
  Shield
} from 'lucide-vue-next'
import { AuthService } from '@/services/auth-service'

const router = useRouter()
const props = defineProps<{
  open: boolean
}>()
const emit = defineEmits<{
  (e: 'update:open', value: boolean): void
}>()
const open = ref(props.open)
const searchQuery = ref('')
const selectedIndex = ref(0)

// Get current theme
const currentTheme = ref(localStorage.getItem('theme') || 'light')

// Define all available actions with their shortcuts
const actions = {
  dashboard: {
    id: 'dashboard',
    name: 'Tableau de bord',
    shortcut: ['⌘', 'B'],
    keywords: 'dashboard tableau de bord home accueil',
    icon: Home,
    perform: () => router.push('/dashboard'),
  },
  documents: {
    id: 'documents',
    name: 'Documents',
    shortcut: ['⌘', 'D'],
    keywords: 'documents files fichiers',
    icon: FileText,
    perform: () => router.push('/documents'),
  },
  editor: {
    id: 'editor',
    name: 'Éditeur de texte',
    shortcut: ['⌘', 'E'],
    keywords: 'editor éditeur text texte',
    icon: FileEdit,
    perform: () => router.push('/scanner'),
  },
  history: {
    id: 'history',
    name: 'Historique',
    shortcut: ['⌘', 'Y'],
    keywords: 'history historique recent derniers',
    icon: History,
    perform: () => router.push('/historique'),
  },
  trash: {
    id: 'trash',
    name: 'Corbeille',
    shortcut: ['⌘', '⌫'],
    keywords: 'trash corbeille deleted supprimés',
    icon: Trash2,
    perform: () => router.push('/corbeille'),
  },
  admin: {
    id: 'admin',
    name: 'Administration',
    shortcut: ['⌘', 'A'],
    keywords: 'admin administration users utilisateurs management gestion',
    icon: Shield,
    perform: () => router.push('/admin'),
  },
  settings: {
    id: 'settings',
    name: 'Paramètres',
    shortcut: ['⌘', ','],
    keywords: 'settings configuration paramètres',
    icon: Settings,
    perform: () => router.push('/parametres'),
  },
  help: {
    id: 'help',
    name: 'Aide',
    shortcut: ['⌘', '?'],
    keywords: 'help aide support documentation',
    icon: HelpCircle,
    perform: () => router.push('/aide'),
  },
  profile: {
    id: 'profile',
    name: 'Profil',
    shortcut: ['⌘', 'P'],
    keywords: 'profile account compte settings paramètres',
    icon: User,
    perform: () => {
      // Dispatch a custom event that NavUser will listen for
      window.dispatchEvent(new CustomEvent('toggleProfileDialog', {
        detail: { open: true }
      }))
    },
  },
  theme: {
    id: 'theme',
    name: currentTheme.value === 'light' ? 'Activer le mode sombre' : 'Activer le mode clair',
    shortcut: ['⌘', 'L'],
    keywords: 'theme thème dark sombre light clair mode',
    icon: currentTheme.value === 'light' ? Moon : Sun,
    perform: () => {
      const newTheme = currentTheme.value === 'light' ? 'dark' : 'light'
      currentTheme.value = newTheme
      localStorage.setItem('theme', newTheme)
      if (newTheme === 'dark') {
        document.documentElement.classList.add('dark')
      } else {
        document.documentElement.classList.remove('dark')
      }
      // Update the theme action to reflect the new state
      actions.theme.name = newTheme === 'light' ? 'Activer le mode sombre' : 'Activer le mode clair'
      actions.theme.icon = newTheme === 'light' ? Moon : Sun
    },
  },
}

// Check if user is admin
const isAdmin = computed(() => {
  const user = AuthService.getUser()
  console.log('🔍 [CommandPalette] Current user from AuthService:', user)
  console.log('🔍 [CommandPalette] User role:', user?.role)
  console.log('🔍 [CommandPalette] Is admin check:', user?.role === 'Admin')
  return user?.role === 'Admin'
})

// Group actions into sections for the command palette
const sections = computed(() => {
  const navigationItems = [actions.dashboard, actions.documents, actions.editor, actions.history, actions.trash]

  // Add admin action if user is admin
  if (isAdmin.value) {
    navigationItems.splice(1, 0, actions.admin)
  }

  return [
    {
      id: 'navigation',
      name: 'Navigation',
      items: navigationItems,
    },
    {
      id: 'appearance',
      name: 'Apparence',
      items: [actions.theme],
    },
    {
      id: 'account',
      name: 'Compte',
      items: [actions.profile, actions.settings],
    },
    {
      id: 'help',
      name: 'Aide',
      items: [actions.help],
    },
  ]
})

// Calculate global index for an item based on its section and item indices
const getGlobalIndex = (sectionIndex: number, itemIndex: number): number => {
  let globalIndex = 0
  for (let i = 0; i < sectionIndex; i++) {
    globalIndex += filteredSections.value[i].items.length
  }
  return globalIndex + itemIndex
}

// Flatten all items for keyboard navigation
const allItems = computed(() => {
  return filteredSections.value.flatMap(section => section.items)
})

const filteredSections = computed(() => {
  const query = searchQuery.value.toLowerCase()
  if (!query) return sections.value

  return sections.value.map((section: any) => ({
    ...section,
    items: section.items.filter((item: any) =>
      item.name.toLowerCase().includes(query) ||
      item.keywords.toLowerCase().includes(query)
    ),
  })).filter((section: any) => section.items.length > 0)
})

const handleSelect = (action: any) => {
  action.perform()
  open.value = false
  searchQuery.value = ''
  selectedIndex.value = 0
}

const handleGlobalKeyDown = (e: KeyboardEvent) => {
  // Check for command palette toggle
  if (e.key === 'k' && (e.metaKey || e.ctrlKey)) {
    e.preventDefault()
    emit('update:open', !open.value)
    if (!open.value) {
      searchQuery.value = ''
      selectedIndex.value = 0
    }
    return
  }

  // Check for global shortcuts
  if (e.metaKey || e.ctrlKey) {
    switch (e.key.toLowerCase()) {
      case 'b':
        e.preventDefault()
        actions.dashboard.perform()
        break
      case 'd':
        e.preventDefault()
        actions.documents.perform()
        break
      case 'e':
        e.preventDefault()
        actions.editor.perform()
        break
      case 'y':
        e.preventDefault()
        actions.history.perform()
        break
      case 'backspace':
        e.preventDefault()
        actions.trash.perform()
        break
      case 'l':
        e.preventDefault()
        actions.theme.perform()
        break
      case ',':
        e.preventDefault()
        actions.settings.perform()
        break
      case '?':
        e.preventDefault()
        actions.help.perform()
        break
      case 'p':
        e.preventDefault()
        actions.profile.perform()
        break
      case 'a':
        if (isAdmin.value) {
          e.preventDefault()
          actions.admin.perform()
        }
        break
    }
  }
}

// Replace the existing handleKeyDown with this one
const handleKeyDown = (e: KeyboardEvent) => {
  if (open.value) {
    switch (e.key) {
      case 'ArrowUp':
        e.preventDefault()
        selectedIndex.value = selectedIndex.value > 0 ? selectedIndex.value - 1 : allItems.value.length - 1
        break
      case 'ArrowDown':
        e.preventDefault()
        selectedIndex.value = selectedIndex.value < allItems.value.length - 1 ? selectedIndex.value + 1 : 0
        break
      case 'Enter':
        e.preventDefault()
        if (allItems.value[selectedIndex.value]) {
          handleSelect(allItems.value[selectedIndex.value])
        }
        break
      case 'Escape':
        e.preventDefault()
        open.value = false
        searchQuery.value = ''
        selectedIndex.value = 0
        break
    }
  }
}

// Reset selected index when search query changes
watch(searchQuery, () => {
  selectedIndex.value = 0
})

watch(() => props.open, (newValue) => {
  open.value = newValue
})

watch(open, (newValue) => {
  emit('update:open', newValue)
})

onMounted(() => {
  window.addEventListener('keydown', handleGlobalKeyDown)
  window.addEventListener('keydown', handleKeyDown)
})

onUnmounted(() => {
  window.removeEventListener('keydown', handleGlobalKeyDown)
  window.removeEventListener('keydown', handleKeyDown)
})
</script>

<template>
  <Dialog :open="open" @update:open="open = $event">
    <DialogContent class="fixed left-[50%] top-[50%] z-50 w-full max-w-[450px] translate-x-[-50%] translate-y-[-50%] gap-0 p-0 border-none bg-transparent shadow-lg">
      <div class="flex flex-col w-full rounded-lg border bg-popover text-popover-foreground shadow-lg">
        <!-- Search Header -->
        <div class="flex items-center border-b px-3 py-3 pr-12">
          <Search class="mr-2 h-4 w-4 shrink-0 text-muted-foreground" />
          <Input
            v-model="searchQuery"
            placeholder="Tapez une commande ou recherchez..."
            class="w-full bg-transparent border-0 p-0 placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-0"
          />
          <div class="flex items-center gap-2 ml-2">
            <kbd class="pointer-events-none h-5 select-none items-center gap-1 rounded border bg-muted px-1.5 font-mono text-[10px] font-medium opacity-100">
              <span class="text-xs">⌘</span>K
            </kbd>
          </div>
        </div>

        <!-- Results -->
        <div class="max-h-[300px] overflow-y-auto overflow-x-hidden">
          <div v-for="(section, sectionIndex) in filteredSections" :key="section.id" class="px-2 py-2">
            <div class="text-xs font-semibold text-muted-foreground px-2 py-1.5">
              {{ section.name }}
            </div>
            <div class="space-y-1">
              <button
                v-for="(action, actionIndex) in section.items"
                :key="action.id"
                :class="[
                  'w-full rounded-sm px-2 py-1.5 text-sm hover:bg-accent hover:text-accent-foreground flex items-center justify-between group',
                  {
                    'bg-accent text-accent-foreground': selectedIndex === getGlobalIndex(sectionIndex, actionIndex)
                  }
                ]"
                @click="handleSelect(action)"
                @mousemove="selectedIndex = getGlobalIndex(sectionIndex, actionIndex)"
              >
                <div class="flex items-center gap-2">
                  <component
                    :is="action.icon"
                    :class="[
                      'h-4 w-4',
                      selectedIndex === getGlobalIndex(sectionIndex, actionIndex)
                        ? 'text-accent-foreground'
                        : 'text-muted-foreground group-hover:text-accent-foreground'
                    ]"
                  />
                  <span>{{ action.name }}</span>
                </div>
                <div v-if="action.shortcut" class="flex items-center gap-1">
                  <kbd
                    v-for="key in action.shortcut"
                    :key="key"
                    class="pointer-events-none h-5 select-none items-center gap-1 rounded border bg-muted px-1.5 font-mono text-[10px] font-medium opacity-100"
                  >
                    {{ key }}
                  </kbd>
                </div>
              </button>
            </div>
          </div>

          <!-- Empty State -->
          <div
            v-if="filteredSections.length === 0"
            class="py-6 text-center text-sm text-muted-foreground"
          >
            Aucun résultat trouvé.
          </div>
        </div>

        <!-- Footer -->
        <div class="border-t px-2 py-2 text-xs text-muted-foreground flex items-center justify-between">
          <div class="flex items-center gap-2">
            <Command class="h-4 w-4" />
            <span>Palette de commandes OCRify</span>
          </div>
          <div class="flex items-center gap-1">
            <kbd class="pointer-events-none h-5 select-none items-center gap-1 rounded border bg-muted px-1.5 font-mono text-[10px] font-medium opacity-100">
              ↑↓
            </kbd>
            <span>pour naviguer</span>
            <kbd class="pointer-events-none h-5 select-none items-center gap-1 rounded border bg-muted px-1.5 font-mono text-[10px] font-medium opacity-100 ml-1">
              ↵
            </kbd>
            <span>pour sélectionner</span>
          </div>
        </div>
      </div>
    </DialogContent>
  </Dialog>
</template>

<style scoped>
.dark {
  color-scheme: dark;
}

/* Ensure proper contrast in dark mode */
:deep(.dark) .bg-muted {
  background-color: hsl(var(--muted));
}

:deep(.dark) .text-muted-foreground {
  color: hsl(var(--muted-foreground));
}

:deep(.dark) .bg-accent {
  background-color: hsl(var(--accent));
}

:deep(.dark) .text-accent-foreground {
  color: hsl(var(--accent-foreground));
}

:deep(.dark) .bg-popover {
  background-color: hsl(var(--popover));
}

:deep(.dark) .text-popover-foreground {
  color: hsl(var(--popover-foreground));
}

:deep(.dark) .border {
  border-color: hsl(var(--border));
}
</style>