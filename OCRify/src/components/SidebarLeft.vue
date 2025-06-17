<script setup lang="ts">
import { computed, onMounted, onBeforeUnmount, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import NavUser from '@/components/NavUser.vue'
import ThemeToggle from '@/components/ThemeToggle.vue'
import { AuthService } from '@/services/auth-service'
import { user } from '@/services/api'
import {
  Sidebar,
  SidebarContent,
  SidebarHeader,
  SidebarRail,
  SidebarFooter,
} from '@/components/ui/sidebar'
import {
  FileText,
  Home,
  Settings,
  FolderOpen,
  HelpCircle,
  Trash2,
  FileEdit,
  History,
  Shield
} from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { useUserStore } from '@/stores/userStore'
import { jwtDecode } from 'jwt-decode'

const route = useRoute()
const router = useRouter()
const userStore = useUserStore()

// App info
const appInfo = {
  name: 'OCRify',
  logo: FileText,
}

// Main navigation items
const navItems = computed(() => [
  {
    title: 'Accueil',
    url: '/dashboard',
    icon: Home,
    isActive: route.path === '/dashboard',
  },
  {
    title: 'Documents',
    url: '/documents',
    icon: FolderOpen,
    isActive: route.path === '/documents',
  },
  {
    title: 'Corbeille',
    url: '/corbeille',
    icon: Trash2,
    isActive: route.path === '/corbeille',
  },
])

// Secondary navigation
const secondaryItems = [
  {
    title: 'Settings',
    url: '/settings',
    icon: Settings,
  },
  {
    title: 'Help',
    url: '/help',
    icon: HelpCircle,
  },
]

// Replace user data management with store
const userData = computed(() => ({
  name: userStore.userName,
  email: userStore.userEmail,
  avatar: userStore.userAvatar
}))

// Navigation guard is now handled globally in router/index.ts

// Only fetch profile once on mount if authenticated
onMounted(async () => {
  if (AuthService.isAuthenticated()) {
    await userStore.fetchUserProfile()
  }
})

// Check if user is admin
const isAdmin = computed(() => {
  const user = AuthService.getUser()
  console.log('🔍 [SidebarLeft] Current user from AuthService:', user)
  console.log('🔍 [SidebarLeft] User role:', user?.role)
  console.log('🔍 [SidebarLeft] Is admin check:', user?.role === 'Admin')

  // Also check the token for role information
  const token = AuthService.getToken()
  if (token) {
    try {
      const decoded = jwtDecode(token)
      console.log('🔍 [SidebarLeft] Decoded token:', decoded)
    } catch (e) {
      console.error('🔍 [SidebarLeft] Error decoding token:', e)
    }
  }

  return user?.role === 'Admin'
})

const navigation = computed(() => {
  const baseNavigation = [
    {
      title: 'Tableau de bord',
      icon: Home,
      to: '/dashboard'
    },
    {
      title: 'Documents',
      icon: FileText,
      to: '/documents'
    },
    {
      title: 'Éditeur de texte',
      icon: FileEdit,
      to: '/scanner'
    },
    {
      title: 'Historique',
      icon: History,
      to: '/historique'
    },
    {
      title: 'Corbeille',
      icon: Trash2,
      to: '/corbeille'
    }
  ]

  // Add admin dashboard if user is admin
  if (isAdmin.value) {
    baseNavigation.splice(1, 0, {
      title: 'Administration',
      icon: Shield,
      to: '/admin'
    })
  }

  return baseNavigation
})

const bottomNavigation = [
  {
    title: 'Paramètres',
    icon: Settings,
    to: '/parametres'
  },
  {
    title: 'Aide',
    icon: HelpCircle,
    to: '/aide'
  }
]

const isActive = (path: string) => route.path === path
</script>

<template>
  <Sidebar class="border-r">
    <SidebarHeader>
      <div class="flex items-center gap-2 p-4">
        <component :is="appInfo.logo" class="h-6 w-6 text-primary" />
        <span class="font-semibold">{{ appInfo.name }}</span>
      </div>
      <nav class="grid gap-1 px-2">
        <Button
          v-for="item in navigation"
          :key="item.to"
          variant="ghost"
          class="justify-start"
          :class="{ 'bg-muted': isActive(item.to) }"
          @click="router.push(item.to)"
        >
          <component :is="item.icon" class="mr-2 h-4 w-4" />
          {{ item.title }}
        </Button>
      </nav>
    </SidebarHeader>
    <SidebarContent>
      <div class="mt-auto px-2">
        <nav class="grid gap-1">
          <Button
            v-for="item in bottomNavigation"
            :key="item.to"
            variant="ghost"
            class="justify-start"
            :class="{ 'bg-muted': isActive(item.to) }"
            @click="router.push(item.to)"
          >
            <component :is="item.icon" class="mr-2 h-4 w-4" />
            {{ item.title }}
          </Button>
        </nav>
      </div>
    </SidebarContent>
    <SidebarFooter class="border-t">
      <div class="flex items-center justify-between p-2">
        <div class="flex-1">
          <NavUser v-if="userData" :user="userData" />
        </div>
        <div class="flex items-center justify-center px-2">
          <ThemeToggle />
        </div>
      </div>
    </SidebarFooter>
    <SidebarRail />
  </Sidebar>
</template>
