<!-- src/views/SignUp.vue -->
<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import ThemeToggle from '@/components/theme-toggle.vue'
import { auth } from '@/services/api'
import { AuthService } from '@/services/auth-service'
import { googleOAuth } from '@/services/google-oauth'
import { Eye, EyeOff } from 'lucide-vue-next'

// Disable keyboard shortcuts
const disableKeyboardShortcuts = (e: KeyboardEvent) => {
  // Prevent Command+K specifically
  if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
    e.preventDefault()
    e.stopPropagation()
  }
}



onUnmounted(() => {
  document.removeEventListener('keydown', disableKeyboardShortcuts, true)
})

const router = useRouter()

const formData = ref({
  firstName: '',
  lastName: '',
  email: '',
  password: '',
  confirmPassword: ''
})

const showPassword = ref(false)
const showConfirmPassword = ref(false)
const error = ref('')
const success = ref('')
const loading = ref(false)

// Add password requirements check
const passwordRequirements = {
  hasUpperCase: (password: string) => /[A-Z]/.test(password),
  hasLowerCase: (password: string) => /[a-z]/.test(password),
  hasNumber: (password: string) => /[0-9]/.test(password),
  hasSpecialChar: (password: string) => /[!@#$%^&*(),.?":{}|<>]/.test(password),
  hasMinLength: (password: string) => password.length >= 8
}

const getPasswordRequirements = computed(() => {
  const password = formData.value.password
  return {
    hasUpperCase: passwordRequirements.hasUpperCase(password),
    hasLowerCase: passwordRequirements.hasLowerCase(password),
    hasNumber: passwordRequirements.hasNumber(password),
    hasSpecialChar: passwordRequirements.hasSpecialChar(password),
    hasMinLength: passwordRequirements.hasMinLength(password)
  }
})

const handleSubmit = async (e: Event) => {
  e.preventDefault()
  error.value = ''
  success.value = ''

  // Check password requirements before submitting
  const requirements = getPasswordRequirements.value
  if (!Object.values(requirements).every(Boolean)) {
    error.value = 'Le mot de passe ne respecte pas les critères de sécurité'
    return
  }

  if (formData.value.password !== formData.value.confirmPassword) {
    error.value = 'Les mots de passe ne correspondent pas'
    return
  }

  try {
    loading.value = true
    console.log('Attempting registration with:', {
      firstName: formData.value.firstName,
      lastName: formData.value.lastName,
      email: formData.value.email,
      password: formData.value.password
    })

    const response = await auth.register({
      firstName: formData.value.firstName,
      lastName: formData.value.lastName,
      email: formData.value.email,
      password: formData.value.password,
      confirmPassword: formData.value.confirmPassword
    })

    console.log('Registration response:', response.data)

    // Store the token if returned
    if (response.data?.data?.token) {
      localStorage.setItem('token', response.data.data.token)
    }

    success.value = 'Inscription réussie! Redirection vers la page de connexion...'

    // Wait 2 seconds before redirecting to show the success message
    setTimeout(() => {
      router.push('/sign-in')
    }, 2000)
  } catch (err: any) {
    console.error('Registration error details:', {
      message: err.message,
      response: err.response?.data,
      status: err.response?.status
    })

    // Display the exact error message from the backend
    if (err.response?.status === 400) {
      // If we get a 400 error and there's an email in the form, it's likely a duplicate email
      if (formData.value.email) {
        error.value = 'Cette adresse email est déjà utilisée'
      } else {
        error.value = err.response?.data?.message || 'Une erreur est survenue lors de l\'inscription. Veuillez réessayer.'
      }
    } else {
      error.value = 'Une erreur est survenue lors de l\'inscription. Veuillez réessayer.'
    }
  } finally {
    loading.value = false
  }
}

// Enhanced Google OAuth2 Implementation using shared service
const signInWithGoogle = async () => {
  try {
    await googleOAuth.signIn(
      router,
      (message: string) => { error.value = message },
      (isLoading: boolean) => { loading.value = isLoading }
    )
  } catch (err: any) {
    console.error('Google Sign-In error:', err)
    error.value = err.message || 'Erreur lors de l\'authentification Google'
  }
}

// Update the onMounted to also initialize Google OAuth
onMounted(async () => {
  document.addEventListener('keydown', disableKeyboardShortcuts, true)

  try {
    // Small delay to ensure DOM is ready
    await new Promise(resolve => setTimeout(resolve, 100))
    await googleOAuth.initialize()
  } catch (err) {
    console.warn('Initial Google OAuth initialization failed, will retry on user interaction:', err)
    // Don't show error immediately, let user try clicking the button
  }
})
</script>

<template>
  <div class="min-h-screen w-full flex items-center justify-center bg-white dark:bg-gray-950 p-4">
    <!-- Grid Background -->
    <div
      class="absolute inset-0 z-0 bg-white dark:bg-gray-950"
      style="
        background-image:
          linear-gradient(to right, rgb(55 65 81 / 0.2) 1px, transparent 1px),
          linear-gradient(to bottom, rgb(55 65 81 / 0.2) 1px, transparent 1px);
        background-size: 24px 24px;
        mask-image: radial-gradient(ellipse 80% 50% at 50% -20%, black 90%, transparent 100%);
      "
    ></div>

    <Card class="w-full max-w-md relative z-10 bg-white/80 dark:bg-gray-950/80 backdrop-blur-sm border dark:border-gray-800">
      <div class="absolute right-4 top-4">
        <ThemeToggle />
      </div>
      <CardHeader class="space-y-2 pt-8 pb-4">
        <CardTitle class="text-3xl font-bold text-center">
          Créer un compte
        </CardTitle>
        <CardDescription class="text-center text-base whitespace-nowrap">
          Inscrivez-vous&nbsp;pour&nbsp;commencer&nbsp;à&nbsp;gérer&nbsp;vos&nbsp;documents
        </CardDescription>
      </CardHeader>
      <CardContent>
        <form @submit="handleSubmit" class="grid gap-6">
          <div v-if="error" class="bg-red-100 dark:bg-red-900/30 text-red-600 dark:text-red-400 p-3 rounded-md text-sm">
            {{ error }}
          </div>

          <div v-if="success" class="bg-green-100 dark:bg-green-900/30 text-green-600 dark:text-green-400 p-3 rounded-md text-sm">
            {{ success }}
          </div>

          <div class="grid gap-3">
            <div class="grid gap-3 md:grid-cols-2">
              <div class="space-y-2">
                <Label for="firstName">Prénom</Label>
                <Input
                  id="firstName"
                  v-model="formData.firstName"
                  type="text"
                  placeholder="Jean"
                  class="w-full"
                  required
                />
              </div>
              <div class="space-y-2">
                <Label for="lastName">Nom</Label>
                <Input
                  id="lastName"
                  v-model="formData.lastName"
                  type="text"
                  placeholder="Dupont"
                  class="w-full"
                  required
                />
              </div>
            </div>
            <div class="space-y-2">
              <Label for="email">Email</Label>
              <Input
                id="email"
                v-model="formData.email"
                type="email"
                placeholder="exemple@email.com"
                class="w-full"
                required
              />
            </div>
            <div class="space-y-2">
              <Label for="password">Mot de passe</Label>
              <div class="relative">
                <Input
                  id="password"
                  v-model="formData.password"
                  :type="showPassword ? 'text' : 'password'"
                  class="w-full pr-10"
                  required
                />
                <button
                  type="button"
                  class="absolute right-3 top-1/2 -translate-y-1/2 text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200"
                  @click="showPassword = !showPassword"
                >
                  <Eye v-if="!showPassword" class="h-4 w-4" />
                  <EyeOff v-else class="h-4 w-4" />
                </button>
              </div>
              <!-- Add password requirements display -->
              <div class="text-sm space-y-1 mt-1">
                <p class="text-muted-foreground">Le mot de passe doit contenir :</p>
                <ul class="space-y-1">
                  <li class="flex items-center gap-2">
                    <span :class="getPasswordRequirements.hasUpperCase ? 'text-green-500' : 'text-red-500'">•</span>
                    <span :class="getPasswordRequirements.hasUpperCase ? 'text-green-500' : 'text-muted-foreground'">
                      Au moins une majuscule
                    </span>
                  </li>
                  <li class="flex items-center gap-2">
                    <span :class="getPasswordRequirements.hasLowerCase ? 'text-green-500' : 'text-red-500'">•</span>
                    <span :class="getPasswordRequirements.hasLowerCase ? 'text-green-500' : 'text-muted-foreground'">
                      Au moins une minuscule
                    </span>
                  </li>
                  <li class="flex items-center gap-2">
                    <span :class="getPasswordRequirements.hasNumber ? 'text-green-500' : 'text-red-500'">•</span>
                    <span :class="getPasswordRequirements.hasNumber ? 'text-green-500' : 'text-muted-foreground'">
                      Au moins un chiffre
                    </span>
                  </li>
                  <li class="flex items-center gap-2">
                    <span :class="getPasswordRequirements.hasSpecialChar ? 'text-green-500' : 'text-red-500'">•</span>
                    <span :class="getPasswordRequirements.hasSpecialChar ? 'text-green-500' : 'text-muted-foreground'">
                      Au moins un caractère spécial (!@#$%^&*(),.?":{}|<>)
                    </span>
                  </li>
                  <li class="flex items-center gap-2">
                    <span :class="getPasswordRequirements.hasMinLength ? 'text-green-500' : 'text-red-500'">•</span>
                    <span :class="getPasswordRequirements.hasMinLength ? 'text-green-500' : 'text-muted-foreground'">
                      Au moins 8 caractères
                    </span>
                  </li>
                </ul>
              </div>
            </div>
            <div class="space-y-2">
              <Label for="confirmPassword">Confirmer le mot de passe</Label>
              <div class="relative">
                <Input
                  id="confirmPassword"
                  v-model="formData.confirmPassword"
                  :type="showConfirmPassword ? 'text' : 'password'"
                  class="w-full pr-10"
                  required
                />
                <button
                  type="button"
                  class="absolute right-3 top-1/2 -translate-y-1/2 text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200"
                  @click="showConfirmPassword = !showConfirmPassword"
                >
                  <Eye v-if="!showConfirmPassword" class="h-4 w-4" />
                  <EyeOff v-else class="h-4 w-4" />
                </button>
              </div>
            </div>
          </div>

          <div class="space-y-4">
            <Button
              type="submit"
              class="w-full text-base py-5"
              :disabled="loading"
            >
              {{ loading ? 'Inscription en cours...' : 'S\'inscrire' }}
            </Button>

            <div class="relative">
              <div class="absolute inset-0 flex items-center">
                <span class="w-full border-t" />
              </div>
              <div class="relative flex justify-center text-xs uppercase">
                <span class="bg-white dark:bg-gray-950 px-2 text-muted-foreground">Ou s'inscrire avec</span>
              </div>
            </div>

            <!-- Google Sign-In Button -->
            <Button
              type="button"
              variant="outline"
              class="w-full text-base py-5 flex items-center justify-center gap-3"
              :disabled="loading"
              @click="signInWithGoogle"
            >
              <svg class="w-5 h-5" viewBox="0 0 24 24">
                <path fill="#4285F4" d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z"/>
                <path fill="#34A853" d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z"/>
                <path fill="#FBBC05" d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z"/>
                <path fill="#EA4335" d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z"/>
              </svg>
              S'inscrire avec Google
            </Button>
          </div>
        </form>

        <div class="mt-6 text-center text-sm">
          Vous avez déjà un compte?
          <router-link to="/sign-in" class="text-primary hover:underline font-medium ml-1">
            Se connecter
          </router-link>
        </div>

        <div class="mt-4 text-center">
          <router-link to="/" class="text-sm text-muted-foreground hover:text-primary transition-colors">
            ← Retour à l'accueil
          </router-link>
        </div>
      </CardContent>
    </Card>
  </div>
</template>

<style scoped>
.card-shadow {
  box-shadow: 0 0 0 1px rgba(0, 0, 0, 0.05), 0 1px 3px rgba(0, 0, 0, 0.1);
}
</style>