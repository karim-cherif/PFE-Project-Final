<script setup lang="ts">
import { ref, onMounted, computed, onBeforeUnmount, nextTick, watch } from 'vue'
import { useRouter } from 'vue-router'
import { auth, type UpdateUserDto, users } from '@/services/api'
import { AuthService } from '@/services/auth-service'
import { useUserStore } from '@/stores/userStore'
import {
  Avatar,
  AvatarFallback,
  AvatarImage,
} from '@/components/ui/avatar'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '../components/ui/dialog'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import {
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  useSidebar,
} from '@/components/ui/sidebar'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import Button from '@/components/ui/button/Button.vue'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Switch } from '@/components/ui/switch'
import {
  User,
  LogOut,
  Loader2,
  Save,
  ChevronsUpDown,
  Upload,
  Bell,
  Languages,
  Moon,
  Phone,
  MapPin,
  Building,
  Calendar,
  Trash2,
  Eye,
  EyeOff,
  X,
} from 'lucide-vue-next'
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover'
import { Separator } from '@/components/ui/separator'

const props = defineProps<{
  user?: {
    name: string
    email: string
    avatar: string
  }
}>()

const router = useRouter()
const { isMobile } = useSidebar()
const isLoggingOut = ref(false)
const showLogoutDialog = ref(false)
const showProfileDialog = ref(false)
const activeTab = ref('general')
const profileError = ref('')
const profileSuccess = ref(false)

const userStore = useUserStore()

// Use store's computed properties
const userName = computed(() => userStore.userName)
const userEmail = computed(() => userStore.userEmail)
const currentAvatar = computed(() => {
  const avatar = userStore.userAvatar
  return avatar === '' ? '' : avatar
})

// Add loading state for avatar
const isAvatarLoading = ref(false)

// Add computed property for avatar fallback with null checks
const avatarFallback = computed(() => {
  const firstName = userStore.userData?.firstName || ''
  const lastName = userStore.userData?.lastName || ''

  if (!firstName && !lastName) return 'U'

  const firstInitial = firstName.charAt(0).toUpperCase()
  const lastInitial = lastName.charAt(0).toUpperCase()

  const initials = `${firstInitial}${lastInitial}`
  return initials
})

// Handle avatar error - use consistent fallback
const handleAvatarError = (e: Event) => {
  const target = e.target as HTMLImageElement
  if (target && userStore.userData?.email) {
    // Use consistent avatar based on user email
    const seed = userStore.userData.email.toLowerCase()
    const fallbackUrl = `https://api.dicebear.com/9.x/croodles/svg?seed=${seed}`
    target.src = fallbackUrl
  }
}

// Update the avatar options to include more avatars
const avatarOptions = [
  { id: 1, url: 'https://api.dicebear.com/9.x/croodles/svg?seed=avatar1' },
  { id: 2, url: 'https://api.dicebear.com/9.x/croodles/svg?seed=avatar2' },
  { id: 3, url: 'https://api.dicebear.com/9.x/croodles/svg?seed=avatar3' },
  { id: 4, url: 'https://api.dicebear.com/9.x/croodles/svg?seed=avatar4' },
  { id: 5, url: 'https://api.dicebear.com/9.x/croodles/svg?seed=avatar5' },
  { id: 6, url: 'https://api.dicebear.com/9.x/croodles/svg?seed=avatar6' },
  { id: 7, url: 'https://api.dicebear.com/9.x/croodles/svg?seed=avatar7' },
  { id: 8, url: 'https://api.dicebear.com/9.x/croodles/svg?seed=avatar8' },
  { id: 9, url: 'https://api.dicebear.com/9.x/croodles/svg?seed=avatar9' },
  { id: 10, url: 'https://api.dicebear.com/9.x/croodles/svg?seed=avatar10' },
  { id: 11, url: 'https://api.dicebear.com/9.x/croodles/svg?seed=avatar11' },
  { id: 12, url: 'https://api.dicebear.com/9.x/croodles/svg?seed=avatar12' },
]

const languages = [
  { value: 'fr', label: 'Français' },
  { value: 'en', label: 'English' },
  { value: 'ar', label: 'العربية' },
]

const isSaving = ref(false)
const passwordError = ref('')
const passwordSuccess = ref(false)

// Add these refs for password visibility
const showCurrentPassword = ref(false)
const showNewPassword = ref(false)
const showConfirmPassword = ref(false)

// Password validation functions
const hasMinLength = (password: string): boolean => password.length >= 8
const hasUpperCase = (password: string): boolean => /[A-Z]/.test(password)
const hasLowerCase = (password: string): boolean => /[a-z]/.test(password)
const hasNumber = (password: string): boolean => /\d/.test(password)
const hasSpecialChar = (password: string): boolean => /[@$!%*?&]/.test(password)

// Password validation function
const validatePassword = (password: string): boolean => {
  return hasMinLength(password) &&
         hasUpperCase(password) &&
         hasLowerCase(password) &&
         hasNumber(password) &&
         hasSpecialChar(password)
}

// Update the profile form to remove unnecessary fields
const profileForm = ref({
  firstName: '',
  lastName: '',
  email: '',
  selectedAvatar: avatarOptions[0].url, // Default to first avatar
  currentPassword: '',
  newPassword: '',
  confirmPassword: '',
})

// Update the handleProfileDialogOpen function
const handleProfileDialogOpen = async (open: boolean) => {
  console.log('🔵 [handleProfileDialogOpen] Dialog state:', open)

  if (open) {
    try {
      console.log('🔵 [handleProfileDialogOpen] Starting to fetch user profile...')
      console.log('🔵 [handleProfileDialogOpen] Current user store state:', userStore.userData)

      const response = await users.getProfile()
      console.log('✅ [handleProfileDialogOpen] Raw API response:', response)

      // Access the nested data correctly
      const profile = response.data.data
      console.log('📦 [handleProfileDialogOpen] Parsed profile data:', profile)

      if (!profile) {
        console.error('❌ [handleProfileDialogOpen] No profile data received')
        throw new Error('No profile data received')
      }

      // Update the form with the profile data
      profileForm.value = {
        firstName: profile.firstName || '',
        lastName: profile.lastName || '',
        email: profile.email || '',
        selectedAvatar: profile.profilePictureUrl || '',
        currentPassword: '',
        newPassword: '',
        confirmPassword: '',
      }
      console.log('📝 [handleProfileDialogOpen] Updated form data:', profileForm.value)

      // Update the user store
      if (userStore.userData) {
        console.log('🔄 [handleProfileDialogOpen] Updating user store...')
        console.log('🔄 [handleProfileDialogOpen] Previous store state:', userStore.userData)

        userStore.userData = {
          ...userStore.userData,
          firstName: profile.firstName,
          lastName: profile.lastName,
          email: profile.email,
          profilePictureUrl: profile.profilePictureUrl
        }

        console.log('✅ [handleProfileDialogOpen] New store state:', userStore.userData)
      } else {
        console.warn('⚠️ [handleProfileDialogOpen] No existing user store data to update')
      }

      console.log('✅ [handleProfileDialogOpen] Profile loading completed successfully')
    } catch (error) {
      console.error('❌ [handleProfileDialogOpen] Error loading profile:', error)
      profileError.value = 'Erreur lors du chargement du profil'
    }
  } else {
    console.log('🔵 [handleProfileDialogOpen] Closing dialog, resetting form...')
    // Reset form and error states when closing
    profileForm.value = {
      firstName: '',
      lastName: '',
      email: '',
      selectedAvatar: '',
      currentPassword: '',
      newPassword: '',
      confirmPassword: '',
    }
    profileError.value = ''
    profileSuccess.value = false
    console.log('✅ [handleProfileDialogOpen] Form reset completed')
  }
}

// Force refresh user data when component is mounted
onMounted(async () => {
  await userStore.fetchUserProfile(true)
})

// Watch for changes in user data
watch(() => userStore.userData, async (newUserData) => {
  if (newUserData) {
    await userStore.fetchUserProfile(true)
  }
}, { immediate: true })

// Add new refs for avatar selection feedback
const avatarSuccess = ref(false)
const avatarSuccessMessage = ref('')
const showAvatarPreview = ref(false)
const tempSelectedAvatar = ref('')

// Add a new ref to track the original avatar
const originalAvatar = ref('')

// Add a ref to track if the avatar panel is open
const isAvatarPanelOpen = ref(false)

// Add this near the other refs at the top
const avatarKey = ref(0)

// Update the selectAvatar function
const selectAvatar = (avatarUrl: string) => {
  // Store the original avatar if not already stored
  if (!originalAvatar.value) {
    originalAvatar.value = userStore.userAvatar
  }
  tempSelectedAvatar.value = avatarUrl
  showAvatarPreview.value = true
}

// Update the handleAvatarUpload function
const handleAvatarUpload = (event: Event) => {
  const input = event.target as HTMLInputElement
  if (input.files?.length) {
    const file = input.files[0]

    // Validate file type
    if (!file.type.startsWith('image/')) {
      profileError.value = 'Veuillez sélectionner une image valide'
      return
    }

    // Validate file size (max 2MB)
    if (file.size > 2 * 1024 * 1024) {
      profileError.value = 'L\'image ne doit pas dépasser 2MB'
      return
    }

    const reader = new FileReader()
    reader.onload = (e) => {
      if (e.target?.result) {
        const base64String = e.target.result as string
        // Validate base64 string
        if (base64String.startsWith('data:image/')) {
          tempSelectedAvatar.value = base64String
          showAvatarPreview.value = true
          avatarSuccess.value = true
          avatarSuccessMessage.value = 'Image importée avec succès'
          profileError.value = ''

          // Reset success message after 3 seconds
          setTimeout(() => {
            avatarSuccess.value = false
          }, 3000)
        } else {
          profileError.value = 'Format d\'image invalide'
        }
      }
    }
    reader.onerror = () => {
      profileError.value = 'Erreur lors de la lecture du fichier'
    }
    reader.readAsDataURL(file)
  }
}

// Update the applyAvatarChanges function
const applyAvatarChanges = async () => {
  if (tempSelectedAvatar.value) {
    try {
      // Update the form's selected avatar
      profileForm.value.selectedAvatar = tempSelectedAvatar.value

      // Update the user store's avatar
      if (userStore.userData) {
        userStore.userData = {
          ...userStore.userData,
          profilePictureUrl: tempSelectedAvatar.value
        }
      }

      // Save the changes immediately
      const updateData: UpdateUserDto = {
        firstName: profileForm.value.firstName.trim(),
        lastName: profileForm.value.lastName.trim(),
        email: profileForm.value.email.trim(),
        profilePictureUrl: profileForm.value.selectedAvatar || undefined // Use undefined instead of null
      }

      await userStore.updateProfile(updateData)

      // Reset the preview state but keep the panel open
      showAvatarPreview.value = false
      originalAvatar.value = tempSelectedAvatar.value // Store the new avatar as the original
      tempSelectedAvatar.value = ''

      // Show success message
      avatarSuccess.value = true
      avatarSuccessMessage.value = 'Avatar mis à jour avec succès'

      // Reset success message after 3 seconds
      setTimeout(() => {
        avatarSuccess.value = false
      }, 3000)
    } catch (error) {
      console.error('❌ [applyAvatarChanges] Error updating avatar:', error)
      avatarSuccessMessage.value = 'Erreur lors de la mise à jour de l\'avatar'
      avatarSuccess.value = false
    }
  }
}

// Update the cancelAvatarChanges function
const cancelAvatarChanges = () => {
  // Reset everything to the original state
  profileForm.value.selectedAvatar = originalAvatar.value
  tempSelectedAvatar.value = ''
  showAvatarPreview.value = false
  originalAvatar.value = ''
}

// Update the removeAvatar function
const removeAvatar = async () => {
  // Clear the avatar from localStorage
  localStorage.removeItem('userAvatar')

  // Update the store's avatar first
  userStore.setUserAvatar('')

  // Force a store update
  if (userStore.userData) {
    userStore.userData = {
      ...userStore.userData,
      profilePictureUrl: ''
    }
  }

  // Update the form's selected avatar
  profileForm.value.selectedAvatar = ''

  // Update the preview state
  tempSelectedAvatar.value = ''
  showAvatarPreview.value = false

  // Force a re-render by incrementing the key
  avatarKey.value++

  // Show success message
  avatarSuccess.value = true
  avatarSuccessMessage.value = 'Avatar supprimé avec succès'

  // Reset success message after 3 seconds
  setTimeout(() => {
    avatarSuccess.value = false
  }, 3000)
}

// Update the handlePopoverClose function
const handlePopoverClose = (open: boolean) => {
  if (!open) {
    // Reset everything to the original state when popover is closed
    profileForm.value.selectedAvatar = originalAvatar.value
    tempSelectedAvatar.value = ''
    showAvatarPreview.value = false
    originalAvatar.value = ''
  }
}

const handleLogout = async () => {
  isLoggingOut.value = true
  try {
    // Close the dialog first
    showLogoutDialog.value = false

    // Call the logout service
    await AuthService.logout()

    // The navigation will be handled by AuthService
    // No need to manually redirect
  } catch (error) {
    // If there's an error, we still want to clear the UI state
    showLogoutDialog.value = false
  } finally {
    isLoggingOut.value = false
  }
}

// Cleanup on unmount
onBeforeUnmount(() => {
  // Remove event listener
  window.removeEventListener('toggleProfileDialog', ((event: CustomEvent) => {
    showProfileDialog.value = event.detail?.open ?? !showProfileDialog.value
  }) as EventListener)
  // Reset all refs
  isLoggingOut.value = false
  showLogoutDialog.value = false
  showProfileDialog.value = false
  profileError.value = ''
  profileSuccess.value = false
  passwordError.value = ''
  passwordSuccess.value = false
})

const userFirstName = computed(() => {
  return userStore.userData?.firstName || ''
})

// Form validation
const validateProfile = (): string | null => {
  if (!profileForm.value.firstName.trim()) {
    return 'Le prénom est requis'
  }
  if (!profileForm.value.lastName.trim()) {
    return 'Le nom est requis'
  }
  if (!profileForm.value.email.trim()) {
    return 'L\'email est requis'
  }
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(profileForm.value.email)) {
    return 'L\'email n\'est pas valide'
  }
  return null
}

// Update the handleSaveProfile function
const handleSaveProfile = async () => {
  console.log('🔵 [handleSaveProfile] Starting profile save...')
  console.log('📝 [handleSaveProfile] Current form data:', profileForm.value)

  isSaving.value = true
  profileError.value = ''
  profileSuccess.value = false

  try {
    if (activeTab.value === 'general') {
      console.log('🔵 [handleSaveProfile] Saving general profile information...')

      // Validate profile data
      const validationError = validateProfile()
      if (validationError) {
        console.error('❌ [handleSaveProfile] Validation error:', validationError)
        throw new Error(validationError)
      }
      console.log('✅ [handleSaveProfile] Form validation passed')

      // Prepare update data
      const updateData: UpdateUserDto = {
        firstName: profileForm.value.firstName.trim(),
        lastName: profileForm.value.lastName.trim(),
        email: profileForm.value.email.trim(),
        profilePictureUrl: profileForm.value.selectedAvatar || undefined // Use undefined instead of null
      }
      console.log('📦 [handleSaveProfile] Prepared update data:', updateData)

      // Update profile using store
      const result = await userStore.updateProfile(updateData)
      console.log('✅ [handleSaveProfile] Profile update response:', result)

      // Update the user store's avatar
      if (userStore.userData) {
        userStore.userData = {
          ...userStore.userData,
          profilePictureUrl: profileForm.value.selectedAvatar
        }
      }

      // Show success message
      profileSuccess.value = true
      console.log('✅ [handleSaveProfile] Profile update completed successfully')

      // Force a silent refresh of user data
      await userStore.fetchUserProfile(true)

      // Close the dialog after a short delay
      setTimeout(() => {
        showProfileDialog.value = false
      }, 1500)
    } else if (activeTab.value === 'security' && profileForm.value.newPassword) {
      console.log('🔵 [handleSaveProfile] Processing password change...')

      // Validate password requirements
      if (!validatePassword(profileForm.value.newPassword)) {
        console.error('❌ [handleSaveProfile] Password validation failed')
        throw new Error('Le mot de passe doit contenir au moins 8 caractères, une majuscule, une minuscule, un chiffre et un caractère spécial')
      }
      console.log('✅ [handleSaveProfile] Password validation passed')

      // Validate passwords match
      if (profileForm.value.newPassword !== profileForm.value.confirmPassword) {
        console.error('❌ [handleSaveProfile] Passwords do not match')
        throw new Error('Les mots de passe ne correspondent pas')
      }
      console.log('✅ [handleSaveProfile] Password confirmation passed')

      // Call the change password API
      await auth.changePassword({
        currentPassword: profileForm.value.currentPassword,
        newPassword: profileForm.value.newPassword
      })
      console.log('✅ [handleSaveProfile] Password change API call successful')

      // Clear password fields after successful change
      profileForm.value.newPassword = ''
      profileForm.value.confirmPassword = ''
      profileForm.value.currentPassword = ''

      // Show success message
      passwordSuccess.value = true
      console.log('✅ [handleSaveProfile] Password change completed successfully')

      // Reset success message after 3 seconds
      setTimeout(() => {
        passwordSuccess.value = false
      }, 3000)
    }
  } catch (error: any) {
    console.error('❌ [handleSaveProfile] Error:', error)
    profileError.value = error.message || 'Une erreur est survenue lors de la mise à jour du profil'
  } finally {
    isSaving.value = false
  }
}
</script>

<template>
  <!-- Logout Confirmation Dialog -->
  <Dialog v-model:open="showLogoutDialog">
    <DialogContent>
      <DialogHeader>
        <DialogTitle>Déconnexion</DialogTitle>
        <DialogDescription>
          Êtes-vous sûr de vouloir vous déconnecter ?
        </DialogDescription>
      </DialogHeader>
      <DialogFooter>
        <Button
          variant="outline"
          @click="showLogoutDialog = false"
          :disabled="isLoggingOut"
        >
          Annuler
        </Button>
        <Button
          variant="destructive"
          @click="handleLogout"
          :disabled="isLoggingOut"
        >
          <Loader2 v-if="isLoggingOut" class="mr-2 h-4 w-4 animate-spin" />
          {{ isLoggingOut ? 'Déconnexion...' : 'Se déconnecter' }}
        </Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>

  <!-- Profile Dialog -->
  <Dialog v-model:open="showProfileDialog" @update:open="handleProfileDialogOpen">
    <DialogContent class="sm:max-w-[600px] max-h-[90vh] overflow-y-auto">
      <DialogHeader>
        <DialogTitle>Profil</DialogTitle>
        <DialogDescription>
          Gérez vos informations personnelles et vos préférences
        </DialogDescription>
      </DialogHeader>

      <Tabs v-model="activeTab" class="w-full">
        <TabsList class="grid w-full grid-cols-2 sticky top-0 bg-background z-10">
          <TabsTrigger value="general">Général</TabsTrigger>
          <TabsTrigger value="security">Sécurité</TabsTrigger>
        </TabsList>

        <form @submit.prevent="handleSaveProfile" class="space-y-6">
          <!-- General Tab -->
          <TabsContent value="general" class="space-y-4 mt-4">
            <!-- Avatar Upload -->
            <div class="space-y-4">
              <Label>Photo de profil</Label>
              <div class="flex items-center gap-4">
                <Avatar class="h-20 w-20 rounded-full overflow-hidden" :key="avatarKey">
                  <AvatarImage
                    v-if="profileForm.selectedAvatar !== ''"
                    :src="profileForm.selectedAvatar"
                    :alt="profileForm.firstName + ' ' + profileForm.lastName"
                    @error="handleAvatarError"
                    :class="{ 'opacity-0': isAvatarLoading }"
                    @load="isAvatarLoading = false"
                  />
                  <AvatarFallback
                    v-else
                    class="rounded-full bg-muted text-muted-foreground h-full w-full flex items-center justify-center text-2xl font-semibold"
                  >
                    {{ avatarFallback }}
                  </AvatarFallback>
                </Avatar>
                <div class="space-y-2">
                  <div class="flex items-center gap-2">
                    <Popover @update:open="handlePopoverClose">
                      <PopoverTrigger as-child>
                        <Button type="button" variant="outline" size="sm">
                          <Upload class="h-4 w-4 mr-2" />
                          Changer
                        </Button>
                      </PopoverTrigger>
                      <PopoverContent class="w-80 p-3">
                        <div class="space-y-4">
                          <div class="space-y-2">
                            <h4 class="font-medium text-sm">Choisir un avatar</h4>
                            <div class="grid grid-cols-4 gap-2">
                              <Button
                                v-for="avatar in avatarOptions"
                                :key="avatar.id"
                                type="button"
                                variant="ghost"
                                size="sm"
                                class="p-0 h-12 w-12 hover:bg-transparent"
                                :class="{ 'ring-2 ring-primary ring-offset-2': tempSelectedAvatar === avatar.url }"
                                @click="selectAvatar(avatar.url)"
                              >
                                <Avatar class="h-full w-full rounded-full overflow-hidden">
                                  <AvatarImage :src="avatar.url" :alt="'Avatar ' + avatar.id" />
                                  <AvatarFallback class="rounded-full">{{ avatar.id }}</AvatarFallback>
                                </Avatar>
                              </Button>
                            </div>

                            <!-- Avatar preview actions -->
                            <div v-if="showAvatarPreview" class="flex items-center gap-2 mt-2">
                              <Button type="button" variant="outline" size="sm" @click="applyAvatarChanges" class="flex-1">
                                <Save class="h-4 w-4 mr-2" />
                                Appliquer
                              </Button>
                              <Button type="button" variant="outline" size="sm" @click="cancelAvatarChanges" class="flex-1">
                                <X class="h-4 w-4 mr-2" />
                                Annuler
                              </Button>
                            </div>

                            <!-- Avatar success message -->
                            <div v-if="avatarSuccess" class="flex items-center gap-2 text-sm text-green-600 animate-fade-in mt-2">
                              <svg class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
                              </svg>
                              {{ avatarSuccessMessage }}
                            </div>
                          </div>
                        </div>
                      </PopoverContent>
                    </Popover>
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      @click="removeAvatar"
                    >
                      <Trash2 class="h-4 w-4" />
                      Supprimer
                    </Button>
                  </div>
                </div>
              </div>
            </div>

            <!-- Personal Information -->
            <div class="grid gap-4">
              <div class="grid gap-2">
                <Label for="firstName">Prénom</Label>
                <Input
                  id="firstName"
                  v-model="profileForm.firstName"
                  placeholder="Votre prénom"
                  :disabled="isSaving"
                />
              </div>

              <div class="grid gap-2">
                <Label for="lastName">Nom</Label>
                <Input
                  id="lastName"
                  v-model="profileForm.lastName"
                  placeholder="Votre nom"
                  :disabled="isSaving"
                />
              </div>

              <div class="grid gap-2">
                <Label for="email">Email</Label>
                <Input
                  id="email"
                  type="email"
                  v-model="profileForm.email"
                  placeholder="votre@email.com"
                  :disabled="isSaving"
                />
              </div>
            </div>

            <div v-if="profileError" class="text-sm text-destructive">
              {{ profileError }}
            </div>

            <div v-if="profileSuccess" class="flex items-center gap-2 text-sm text-green-600 animate-fade-in">
              <svg class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
              </svg>
              Profil mis à jour avec succès !
            </div>
          </TabsContent>

          <!-- Security Tab -->
          <TabsContent value="security" class="space-y-4 mt-4">
            <div class="grid gap-4">
              <div class="grid gap-2">
                <Label for="current-password">Mot de passe actuel</Label>
                <div class="relative">
                  <Input
                    id="current-password"
                    :type="showCurrentPassword ? 'text' : 'password'"
                    v-model="profileForm.currentPassword"
                    placeholder="••••••••"
                    :disabled="isSaving"
                  />
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    class="absolute right-0 top-0 h-full px-3 py-2 hover:bg-transparent"
                    @click="showCurrentPassword = !showCurrentPassword"
                  >
                    <Eye v-if="!showCurrentPassword" class="h-4 w-4 text-muted-foreground" />
                    <EyeOff v-else class="h-4 w-4 text-muted-foreground" />
                  </Button>
                </div>
              </div>

              <div class="grid gap-2">
                <Label for="new-password">Nouveau mot de passe</Label>
                <div class="relative">
                  <Input
                    id="new-password"
                    :type="showNewPassword ? 'text' : 'password'"
                    v-model="profileForm.newPassword"
                    placeholder="••••••••"
                    :disabled="isSaving"
                  />
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    class="absolute right-0 top-0 h-full px-3 py-2 hover:bg-transparent"
                    @click="showNewPassword = !showNewPassword"
                  >
                    <Eye v-if="!showNewPassword" class="h-4 w-4 text-muted-foreground" />
                    <EyeOff v-else class="h-4 w-4 text-muted-foreground" />
                  </Button>
                </div>
                <p class="text-sm text-muted-foreground">
                  Le mot de passe doit contenir :
                </p>
                <ul class="space-y-1 text-sm text-muted-foreground">
                  <li class="flex items-center gap-2">
                    <svg
                      class="h-4 w-4"
                      :class="hasMinLength(profileForm.newPassword) ? 'text-green-500' : 'text-muted-foreground'"
                      fill="none"
                      viewBox="0 0 24 24"
                      stroke="currentColor"
                    >
                      <path
                        v-if="hasMinLength(profileForm.newPassword)"
                        stroke-linecap="round"
                        stroke-linejoin="round"
                        stroke-width="2"
                        d="M5 13l4 4L19 7"
                      />
                      <path
                        v-else
                        stroke-linecap="round"
                        stroke-linejoin="round"
                        stroke-width="2"
                        d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z"
                      />
                    </svg>
                    <span :class="hasMinLength(profileForm.newPassword) ? 'text-green-500' : 'text-muted-foreground'">
                      Au moins 8 caractères
                    </span>
                  </li>
                  <li class="flex items-center gap-2">
                    <svg
                      class="h-4 w-4"
                      :class="hasUpperCase(profileForm.newPassword) ? 'text-green-500' : 'text-muted-foreground'"
                      fill="none"
                      viewBox="0 0 24 24"
                      stroke="currentColor"
                    >
                      <path
                        v-if="hasUpperCase(profileForm.newPassword)"
                        stroke-linecap="round"
                        stroke-linejoin="round"
                        stroke-width="2"
                        d="M5 13l4 4L19 7"
                      />
                      <path
                        v-else
                        stroke-linecap="round"
                        stroke-linejoin="round"
                        stroke-width="2"
                        d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z"
                      />
                    </svg>
                    <span :class="hasUpperCase(profileForm.newPassword) ? 'text-green-500' : 'text-muted-foreground'">
                      Au moins une majuscule
                    </span>
                  </li>
                  <li class="flex items-center gap-2">
                    <svg
                      class="h-4 w-4"
                      :class="hasLowerCase(profileForm.newPassword) ? 'text-green-500' : 'text-muted-foreground'"
                      fill="none"
                      viewBox="0 0 24 24"
                      stroke="currentColor"
                    >
                      <path
                        v-if="hasLowerCase(profileForm.newPassword)"
                        stroke-linecap="round"
                        stroke-linejoin="round"
                        stroke-width="2"
                        d="M5 13l4 4L19 7"
                      />
                      <path
                        v-else
                        stroke-linecap="round"
                        stroke-linejoin="round"
                        stroke-width="2"
                        d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z"
                      />
                    </svg>
                    <span :class="hasLowerCase(profileForm.newPassword) ? 'text-green-500' : 'text-muted-foreground'">
                      Au moins une minuscule
                    </span>
                  </li>
                  <li class="flex items-center gap-2">
                    <svg
                      class="h-4 w-4"
                      :class="hasNumber(profileForm.newPassword) ? 'text-green-500' : 'text-muted-foreground'"
                      fill="none"
                      viewBox="0 0 24 24"
                      stroke="currentColor"
                    >
                      <path
                        v-if="hasNumber(profileForm.newPassword)"
                        stroke-linecap="round"
                        stroke-linejoin="round"
                        stroke-width="2"
                        d="M5 13l4 4L19 7"
                      />
                      <path
                        v-else
                        stroke-linecap="round"
                        stroke-linejoin="round"
                        stroke-width="2"
                        d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z"
                      />
                    </svg>
                    <span :class="hasNumber(profileForm.newPassword) ? 'text-green-500' : 'text-muted-foreground'">
                      Au moins un chiffre
                    </span>
                  </li>
                  <li class="flex items-center gap-2">
                    <svg
                      class="h-4 w-4"
                      :class="hasSpecialChar(profileForm.newPassword) ? 'text-green-500' : 'text-muted-foreground'"
                      fill="none"
                      viewBox="0 0 24 24"
                      stroke="currentColor"
                    >
                      <path
                        v-if="hasSpecialChar(profileForm.newPassword)"
                        stroke-linecap="round"
                        stroke-linejoin="round"
                        stroke-width="2"
                        d="M5 13l4 4L19 7"
                      />
                      <path
                        v-else
                        stroke-linecap="round"
                        stroke-linejoin="round"
                        stroke-width="2"
                        d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z"
                      />
                    </svg>
                    <span :class="hasSpecialChar(profileForm.newPassword) ? 'text-green-500' : 'text-muted-foreground'">
                      Au moins un caractère spécial (@$!%*?&)
                    </span>
                  </li>
                </ul>
              </div>

              <div class="grid gap-2">
                <Label for="confirm-password">Confirmer le mot de passe</Label>
                <div class="relative">
                  <Input
                    id="confirm-password"
                    :type="showConfirmPassword ? 'text' : 'password'"
                    v-model="profileForm.confirmPassword"
                    placeholder="••••••••"
                    :disabled="isSaving"
                  />
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    class="absolute right-0 top-0 h-full px-3 py-2 hover:bg-transparent"
                    @click="showConfirmPassword = !showConfirmPassword"
                  >
                    <Eye v-if="!showConfirmPassword" class="h-4 w-4 text-muted-foreground" />
                    <EyeOff v-else class="h-4 w-4 text-muted-foreground" />
                  </Button>
                </div>
              </div>

              <div v-if="passwordError" class="text-sm text-destructive">
                {{ passwordError }}
              </div>

              <div v-if="passwordSuccess" class="flex items-center gap-2 text-sm text-green-600 animate-fade-in">
                <svg class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7" />
                </svg>
                Mot de passe modifié avec succès !
              </div>
            </div>
          </TabsContent>
        </form>
      </Tabs>

      <DialogFooter class="sticky bottom-0 bg-background pt-4 mt-6">
        <Button type="submit" :disabled="isSaving" @click="handleSaveProfile">
          <Save v-if="!isSaving" class="mr-2 h-4 w-4" />
          <Loader2 v-else class="mr-2 h-4 w-4 animate-spin" />
          {{ isSaving ? 'Enregistrement...' : 'Enregistrer' }}
        </Button>
      </DialogFooter>
    </DialogContent>
  </Dialog>

  <SidebarMenu>
    <SidebarMenuItem>
      <DropdownMenu>
        <DropdownMenuTrigger as-child>
          <SidebarMenuButton
            size="lg"
            class="data-[state=open]:bg-sidebar-accent data-[state=open]:text-sidebar-accent-foreground"
          >
            <Avatar class="h-8 w-8 rounded-full overflow-hidden" :key="avatarKey">
              <AvatarImage
                v-if="currentAvatar !== ''"
                :src="currentAvatar"
                :alt="userName"
                @error="handleAvatarError"
                :class="{ 'opacity-0': isAvatarLoading }"
                @load="isAvatarLoading = false"
              />
              <AvatarFallback class="rounded-full">
                {{ avatarFallback }}
              </AvatarFallback>
            </Avatar>
            <div class="grid flex-1 text-left text-sm leading-tight">
              <span class="truncate font-semibold">{{ userFirstName }}</span>
            </div>
            <ChevronsUpDown class="ml-auto size-4" />
          </SidebarMenuButton>
        </DropdownMenuTrigger>
        <DropdownMenuContent
          class="w-[--reka-dropdown-menu-trigger-width] min-w-56 rounded-lg"
          :side="isMobile ? 'bottom' : 'right'"
          align="start"
          :side-offset="4"
        >
          <DropdownMenuLabel class="p-0 font-normal">
            <div class="flex items-center gap-2 px-1 py-1.5 text-left text-sm">
              <Avatar class="h-8 w-8 rounded-full overflow-hidden" :key="avatarKey">
                <AvatarImage
                  v-if="currentAvatar !== ''"
                  :src="currentAvatar"
                  :alt="userName"
                  @error="handleAvatarError"
                  :class="{ 'opacity-0': isAvatarLoading }"
                  @load="isAvatarLoading = false"
                />
                <AvatarFallback class="rounded-full">
                  {{ avatarFallback }}
                </AvatarFallback>
              </Avatar>
              <div class="grid flex-1 text-left text-sm leading-tight">
                <span class="truncate font-semibold">{{ userName }}</span>
                <span class="truncate text-xs">{{ userEmail }}</span>
              </div>
            </div>
          </DropdownMenuLabel>
          <DropdownMenuSeparator />
          <DropdownMenuGroup>
            <DropdownMenuItem @click="() => {
              showProfileDialog = true;
              handleProfileDialogOpen(true);
            }">
              <User class="mr-2 h-4 w-4" />
              <span>Compte</span>
            </DropdownMenuItem>
          </DropdownMenuGroup>
          <DropdownMenuSeparator />
          <DropdownMenuItem @click="showLogoutDialog = true">
            <LogOut class="mr-2 h-4 w-4" />
            <span>Déconnexion</span>
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    </SidebarMenuItem>
  </SidebarMenu>
</template>
