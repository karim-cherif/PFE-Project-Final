<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useToast } from '@/components/ui/toast/use-toast'
import { Moon, Sun, Monitor, Settings as SettingsIcon, Clock } from 'lucide-vue-next'
import Button from '@/components/ui/button/Button.vue'
import Switch from '@/components/ui/switch/Switch.vue'
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card'
import { Label } from '@/components/ui/label'
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
import { Badge } from '@/components/ui/badge'

const { toast } = useToast()

// Simplified settings - only dark mode for now
const settings = ref({
  theme: 'system'
})

const saving = ref(false)

// Load settings from localStorage for now (simplified)
const loadSettings = () => {
  try {
    const savedTheme = localStorage.getItem('theme') || 'system'
    settings.value.theme = savedTheme
    applyTheme(savedTheme)
    console.log('✅ [Settings] Settings loaded from localStorage:', settings.value)
  } catch (error) {
    console.error('❌ [Settings] Error loading settings:', error)
    settings.value.theme = 'system'
    applyTheme('system')
  }
}

// Save settings to localStorage (simplified)
const saveSettings = () => {
  saving.value = true
  try {
    localStorage.setItem('theme', settings.value.theme)
    applyTheme(settings.value.theme)
    console.log('✅ [Settings] Settings saved to localStorage:', settings.value)

    toast({
      title: 'Succès',
      description: 'Thème sauvegardé avec succès',
      variant: 'default'
    })
  } catch (error: any) {
    console.error('❌ [Settings] Error saving settings:', error)
    toast({
      title: 'Erreur',
      description: 'Erreur lors de la sauvegarde du thème',
      variant: 'destructive'
    })
  } finally {
    saving.value = false
  }
}

// Apply theme to document
const applyTheme = (theme: string) => {
  const root = document.documentElement

  if (theme === 'dark') {
    root.classList.add('dark')
  } else if (theme === 'light') {
    root.classList.remove('dark')
  } else {
    // System theme
    const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches
    if (prefersDark) {
      root.classList.add('dark')
    } else {
      root.classList.remove('dark')
    }
  }
}

// Theme options
const themeOptions = [
  { value: 'light', label: 'Clair', icon: Sun },
  { value: 'dark', label: 'Sombre', icon: Moon },
  { value: 'system', label: 'Système', icon: Monitor }
]

// Get current theme icon
const currentThemeIcon = computed(() => {
  const option = themeOptions.find(opt => opt.value === settings.value.theme)
  return option?.icon || Monitor
})

onMounted(() => {
  loadSettings()

  // Listen for system theme changes
  const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)')
  mediaQuery.addEventListener('change', () => {
    if (settings.value.theme === 'system') {
      applyTheme('system')
    }
  })
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
                <BreadcrumbPage>Paramètres</BreadcrumbPage>
              </BreadcrumbItem>
            </BreadcrumbList>
          </Breadcrumb>
        </div>
      </header>

      <div class="flex flex-1 flex-col gap-6 p-6">
        <div class="flex items-center justify-between">
          <div>
            <h1 class="text-2xl font-semibold">Paramètres</h1>
            <p class="text-muted-foreground mt-1">Configuration OCR</p>
          </div>
          <Button @click="saveSettings" :disabled="saving" class="flex items-center gap-2">
            <component :is="currentThemeIcon" class="h-4 w-4" />
            {{ saving ? 'Application...' : 'Appliquer le thème' }}
          </Button>
        </div>

        <!-- Theme Settings -->
        <Card>
          <CardHeader>
            <CardTitle class="flex items-center gap-2">
              <component :is="currentThemeIcon" class="h-5 w-5" />
              Thème de l'interface
            </CardTitle>
            <CardDescription>
              Personnalisez l'apparence de l'application selon vos préférences
            </CardDescription>
          </CardHeader>
          <CardContent class="space-y-6">
            <div class="grid gap-3">
              <Label>Sélectionner le thème</Label>
              <div class="grid grid-cols-3 gap-3">
                <div
                  v-for="option in themeOptions"
                  :key="option.value"
                  @click="settings.theme = option.value"
                  class="flex flex-col items-center gap-2 p-4 border rounded-lg cursor-pointer transition-colors hover:bg-muted/50"
                  :class="{
                    'border-primary bg-primary/10': settings.theme === option.value,
                    'border-border': settings.theme !== option.value
                  }"
                >
                  <component :is="option.icon" class="h-6 w-6" />
                  <span class="text-sm font-medium">{{ option.label }}</span>
                </div>
              </div>
            </div>
          </CardContent>
        </Card>

        <!-- Coming Soon Section -->
        <Card>
          <CardHeader>
            <CardTitle class="flex items-center gap-2">
              <Clock class="h-5 w-5" />
              Fonctionnalités à venir
              <Badge variant="secondary">Bientôt</Badge>
            </CardTitle>
            <CardDescription>
              Ces paramètres seront disponibles dans les prochaines versions
            </CardDescription>
          </CardHeader>
          <CardContent class="space-y-4">
            <div class="space-y-3 opacity-60">
              <div class="flex items-center justify-between">
                <div>
                  <div class="font-medium">Paramètres OCR avancés</div>
                  <div class="text-sm text-muted-foreground">Configuration des langues et seuils de confiance</div>
                </div>
                <Switch :modelValue="false" disabled />
              </div>
              <Separator />
              <div class="flex items-center justify-between">
                <div>
                  <div class="font-medium">Notifications</div>
                  <div class="text-sm text-muted-foreground">Alertes et notifications push</div>
                </div>
                <Switch :modelValue="false" disabled />
              </div>
              <Separator />
              <div class="flex items-center justify-between">
                <div>
                  <div class="font-medium">Sauvegarde automatique</div>
                  <div class="text-sm text-muted-foreground">Synchronisation cloud et sauvegarde</div>
                </div>
                <Switch :modelValue="false" disabled />
              </div>
              <Separator />
              <div class="flex items-center justify-between">
                <div>
                  <div class="font-medium">Préférences de langue</div>
                  <div class="text-sm text-muted-foreground">Interface multilingue</div>
                </div>
                <Switch :modelValue="false" disabled />
              </div>
            </div>
          </CardContent>
        </Card>
      </div>
    </SidebarInset>
  </SidebarProvider>
</template>
