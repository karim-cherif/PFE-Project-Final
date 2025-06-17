<script setup lang="ts">
import { ref, computed } from 'vue'
import {
  HelpCircle,
  Book,
  MessageCircle,
  Mail,
  FileText,
  ChevronRight,
  Search,
  MessageSquare,
  Languages,
  Folder,
} from 'lucide-vue-next'
import { Input } from '@/components/ui/input'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
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
import { Button } from '@/components/ui/button'

const searchQuery = ref('')

const faqs = [
  {
    question: "Comment importer un document ?",
    answer: "Vous pouvez importer un document en cliquant sur le bouton 'Nouveau document' ou en utilisant la fonction glisser-déposer. L'application prend en charge les formats PDF et images (PNG, JPG, JPEG).",
    category: "Démarrage"
  },
  {
    question: "Quelles langues sont supportées pour l'OCR ?",
    answer: "L'application prend en charge trois langues principales : Français, Anglais et Arabe. La détection de la langue est automatique, mais vous pouvez également la spécifier manuellement lors de l'importation.",
    category: "OCR"
  },
  {
    question: "Comment fonctionne l'OCR pour l'arabe ?",
    answer: "L'OCR pour l'arabe utilise un traitement spécial pour gérer le texte de droite à gauche (RTL). Le texte extrait conserve sa direction RTL et peut être copié-collé tout en maintenant sa direction correcte.",
    category: "OCR"
  },
  {
    question: "Comment améliorer la précision de l'OCR ?",
    answer: "Pour de meilleurs résultats : utilisez des documents de bonne qualité, évitez les images floues, assurez-vous que le texte est bien contrasté, et spécifiez la bonne langue du document.",
    category: "OCR"
  },
  {
    question: "Comment utiliser les suggestions OCR ?",
    answer: "Cliquez sur les mots soulignés en rouge pour voir les suggestions d'amélioration. Sélectionnez la suggestion appropriée ou ignorez-la si le mot original est correct.",
    category: "OCR"
  },
  {
    question: "Comment partager un document ?",
    answer: "Cliquez sur le bouton 'Partager' dans la liste des documents. Vous pouvez partager le document original, le texte OCR ou la version améliorée par IA.",
    category: "Partage"
  },
  {
    question: "Comment gérer mes documents ?",
    answer: "Vous pouvez organiser vos documents en utilisant les tags, marquer des documents comme favoris, filtrer par langue ou type de fichier, et effectuer des recherches dans le texte extrait.",
    category: "Gestion"
  },
  {
    question: "Comment exporter le texte extrait ?",
    answer: "Vous pouvez copier le texte directement depuis le panneau OCR, l'exporter en PDF, Word ou texte brut. Le texte conserve sa mise en forme et sa direction (RTL pour l'arabe).",
    category: "Export"
  },
  {
    question: "Comment accéder au tableau de bord administrateur ?",
    answer: "Si vous êtes administrateur, vous verrez 'Administration' dans la barre latérale ou vous pouvez utiliser Ctrl+K puis taper 'admin' pour y accéder rapidement.",
    category: "Administration"
  },
  {
    question: "Quels formats de fichiers sont supportés ?",
    answer: "L'application supporte les PDF, PNG, JPG, JPEG et WEBP. La taille maximale est de 10MB par fichier.",
    category: "Formats"
  }
]

const stepByStepGuides = [
  {
    title: "Guide complet : Traitement d'un document",
    steps: [
      {
        title: "1. Importer votre document",
        description: "Cliquez sur 'Nouveau document' ou glissez-déposez votre fichier",
        details: "Formats supportés : PDF, PNG, JPG, JPEG, WEBP (max 10MB)"
      },
      {
        title: "2. Sélectionner la langue",
        description: "Choisissez la langue du document ou laissez la détection automatique",
        details: "Langues disponibles : Français, Anglais, Arabe"
      },
      {
        title: "3. Lancer le traitement OCR",
        description: "Cliquez sur 'Traiter' pour extraire le texte",
        details: "Le traitement peut prendre quelques secondes selon la taille du document"
      },
      {
        title: "4. Réviser les suggestions",
        description: "Examinez les mots soulignés et appliquez les corrections",
        details: "Cliquez sur les mots en rouge pour voir les suggestions d'amélioration"
      },
      {
        title: "5. Améliorer avec l'IA",
        description: "Utilisez l'amélioration IA pour optimiser le texte",
        details: "L'IA corrige la grammaire et améliore la lisibilité"
      },
      {
        title: "6. Exporter ou partager",
        description: "Sauvegardez votre travail ou partagez-le",
        details: "Options : PDF, Word, texte brut, ou partage par email"
      }
    ]
  },
  {
    title: "Guide : Optimiser l'OCR pour l'arabe",
    steps: [
      {
        title: "1. Préparer le document",
        description: "Assurez-vous que le texte arabe est clair et bien contrasté",
        details: "Évitez les polices décoratives et les textes manuscrits"
      },
      {
        title: "2. Sélectionner 'Arabe' comme langue",
        description: "Spécifiez explicitement l'arabe pour de meilleurs résultats",
        details: "La détection automatique peut parfois confondre avec d'autres langues"
      },
      {
        title: "3. Vérifier l'orientation RTL",
        description: "Le texte extrait doit s'afficher de droite à gauche",
        details: "Si l'orientation est incorrecte, contactez le support"
      },
      {
        title: "4. Utiliser les suggestions spécialisées",
        description: "Les suggestions pour l'arabe utilisent des modèles spécialisés",
        details: "Le modèle Qwen3 est optimisé pour l'arabe moderne"
      }
    ]
  }
]

const troubleshooting = [
  {
    problem: "L'OCR ne détecte pas le texte",
    solutions: [
      "Vérifiez que l'image est de bonne qualité",
      "Assurez-vous que le contraste est suffisant",
      "Essayez de spécifier manuellement la langue",
      "Vérifiez que le fichier n'est pas corrompu"
    ]
  },
  {
    problem: "Le texte arabe s'affiche mal",
    solutions: [
      "Vérifiez que votre navigateur supporte l'arabe",
      "Actualisez la page",
      "Essayez un autre navigateur",
      "Contactez le support si le problème persiste"
    ]
  },
  {
    problem: "Les suggestions ne s'affichent pas",
    solutions: [
      "Vérifiez votre connexion internet",
      "Attendez quelques secondes pour le chargement",
      "Actualisez la page",
      "Vérifiez les paramètres de seuil de confiance"
    ]
  },
  {
    problem: "Impossible de partager un document",
    solutions: [
      "Vérifiez que vous avez les droits de partage",
      "Assurez-vous que l'email est valide",
      "Vérifiez votre connexion internet",
      "Contactez l'administrateur si nécessaire"
    ]
  }
]

const guides = [
  {
    title: "Guide de démarrage",
    description: "Apprenez à utiliser les fonctionnalités de base",
    icon: Book
  },
  {
    title: "Traitement OCR",
    description: "Optimisez la reconnaissance de texte",
    icon: FileText
  },
  {
    title: "Support multilingue",
    description: "Utilisez l'OCR avec différentes langues",
    icon: Languages
  },
  {
    title: "Gestion des documents",
    description: "Organisez et gérez vos documents",
    icon: Folder
  }
]

// Add new section for language-specific tips
const languageTips = [
  {
    language: "Français",
    tips: [
      "Utilisez une police claire et lisible",
      "Évitez les caractères spéciaux",
      "Maintenez un bon contraste"
    ]
  },
  {
    language: "English",
    tips: [
      "Ensure good image quality",
      "Avoid handwritten text",
      "Use standard fonts"
    ]
  },
  {
    language: "العربية",
    tips: [
      "تأكد من جودة الصورة",
      "استخدم خط واضح",
      "حافظ على التباعد المناسب"
    ]
  }
]

const filteredFaqs = computed(() => {
  if (!searchQuery.value) return faqs
  const query = searchQuery.value.toLowerCase()
  return faqs.filter(faq =>
    faq.question.toLowerCase().includes(query) ||
    faq.answer.toLowerCase().includes(query)
  )
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
                <BreadcrumbPage>Aide</BreadcrumbPage>
              </BreadcrumbItem>
            </BreadcrumbList>
          </Breadcrumb>
        </div>
      </header>

      <div class="flex flex-1 flex-col gap-6 p-6">
        <div class="flex flex-col gap-2">
          <div class="flex items-center gap-2">
            <HelpCircle class="h-6 w-6 text-primary" />
            <h1 class="text-2xl font-semibold">Centre d'aide</h1>
          </div>
          <p class="text-muted-foreground">
            Trouvez des réponses à vos questions et apprenez à utiliser toutes les fonctionnalités de notre application.
          </p>
        </div>

        <!-- Main Content -->
        <div class="grid gap-6">
          <!-- Search Section -->
          <Card>
            <CardContent class="pt-6">
              <div class="flex items-center gap-2">
                <Search class="h-4 w-4 text-muted-foreground" />
                <input
                  v-model="searchQuery"
                  type="text"
                  placeholder="Rechercher une question..."
                  class="flex-1 bg-transparent border-none focus:outline-none"
                />
              </div>
            </CardContent>
          </Card>

          <!-- Quick Guides -->
          <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            <Card v-for="guide in guides" :key="guide.title" class="hover:bg-muted/50 transition-colors">
              <CardContent class="pt-6">
                <div class="flex items-center gap-4">
                  <component :is="guide.icon" class="h-8 w-8 text-primary" />
                  <div>
                    <h3 class="font-medium">{{ guide.title }}</h3>
                    <p class="text-sm text-muted-foreground">{{ guide.description }}</p>
                  </div>
                </div>
              </CardContent>
            </Card>
          </div>

          <!-- Language Tips -->
          <Card>
            <CardHeader>
              <CardTitle>Conseils par langue</CardTitle>
              <CardDescription>Optimisez la reconnaissance pour chaque langue</CardDescription>
            </CardHeader>
            <CardContent>
              <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
                <div v-for="lang in languageTips" :key="lang.language" class="space-y-4">
                  <h3 class="font-medium" :class="{ 'text-right': lang.language === 'العربية' }">{{ lang.language }}</h3>
                  <ul class="space-y-2">
                    <li v-for="tip in lang.tips" :key="tip"
                        class="text-sm text-muted-foreground"
                        :class="{ 'text-right': lang.language === 'العربية' }">
                      {{ tip }}
                    </li>
                  </ul>
                </div>
              </div>
            </CardContent>
          </Card>

          <!-- Step-by-Step Guides -->
          <div class="space-y-6">
            <h2 class="text-xl font-semibold">Guides détaillés</h2>
            <div v-for="guide in stepByStepGuides" :key="guide.title" class="space-y-4">
              <Card>
                <CardHeader>
                  <CardTitle>{{ guide.title }}</CardTitle>
                </CardHeader>
                <CardContent>
                  <div class="space-y-4">
                    <div v-for="step in guide.steps" :key="step.title" class="border-l-2 border-primary/20 pl-4 space-y-2">
                      <h4 class="font-medium text-primary">{{ step.title }}</h4>
                      <p class="text-sm">{{ step.description }}</p>
                      <p class="text-xs text-muted-foreground">{{ step.details }}</p>
                    </div>
                  </div>
                </CardContent>
              </Card>
            </div>
          </div>

          <!-- Troubleshooting Section -->
          <Card>
            <CardHeader>
              <CardTitle>Résolution de problèmes</CardTitle>
              <CardDescription>Solutions aux problèmes courants</CardDescription>
            </CardHeader>
            <CardContent>
              <div class="space-y-6">
                <div v-for="issue in troubleshooting" :key="issue.problem" class="space-y-3">
                  <h3 class="font-medium text-destructive">{{ issue.problem }}</h3>
                  <ul class="space-y-1 ml-4">
                    <li v-for="solution in issue.solutions" :key="solution" class="text-sm text-muted-foreground list-disc">
                      {{ solution }}
                    </li>
                  </ul>
                </div>
              </div>
            </CardContent>
          </Card>

          <!-- FAQ Section -->
          <Card>
            <CardHeader>
              <CardTitle>Questions fréquentes</CardTitle>
              <CardDescription>Les questions les plus posées par nos utilisateurs</CardDescription>
            </CardHeader>
            <CardContent>
              <div class="space-y-6">
                <div v-for="faq in filteredFaqs" :key="faq.question" class="space-y-2">
                  <div class="flex items-center gap-2">
                    <h3 class="font-medium">{{ faq.question }}</h3>
                    <span class="text-xs bg-primary/10 text-primary px-2 py-1 rounded">{{ faq.category }}</span>
                  </div>
                  <p class="text-sm text-muted-foreground">{{ faq.answer }}</p>
                </div>
              </div>
            </CardContent>
          </Card>

          <!-- Contact Section -->
          <Card>
            <CardHeader>
              <CardTitle>Besoin d'aide supplémentaire ?</CardTitle>
              <CardDescription>Notre équipe est là pour vous aider</CardDescription>
            </CardHeader>
            <CardContent>
              <div class="grid gap-4">
                <Button variant="outline" class="w-full justify-start">
                  <Mail class="h-4 w-4 mr-2" />
                  Nous contacter par email
                </Button>
                <Button variant="outline" class="w-full justify-start">
                  <MessageSquare class="h-4 w-4 mr-2" />
                  Chat en direct
                </Button>
              </div>
            </CardContent>
          </Card>
        </div>
      </div>
    </SidebarInset>
  </SidebarProvider>
</template>

<style>
/* Add RTL support for Arabic content */
[lang="ar"] {
  direction: rtl;
  text-align: right;
}

/* Add hover effects for cards */
.hover\:bg-muted\/50 {
  transition: background-color 0.2s ease-in-out;
}

/* Improve spacing for language tips */
.language-tips {
  display: grid;
  gap: 1.5rem;
}

@media (min-width: 768px) {
  .language-tips {
    grid-template-columns: repeat(3, 1fr);
  }
}
</style>