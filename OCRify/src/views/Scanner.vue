<script setup lang="ts">
import { ref, computed } from 'vue'
import { QuillEditor } from '@vueup/vue-quill'
import '@vueup/vue-quill/dist/vue-quill.snow.css'

import {
  FileText,
  Copy,
  FileDown,
  FileText as FileTextIcon,
  Scan,
} from 'lucide-vue-next'
import Button from '@/components/ui/button/Button.vue'
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

// Editor content
const editorContent = ref('')

// Editor formatting state
const editorFormatting = ref({
  bold: false,
  italic: false,
  underline: false,
  align: 'left',
  list: false,
  orderedList: false,
})

// Quill editor options
const editorOptions = {
  theme: 'snow',
  modules: {
    toolbar: 'full'
  },
  placeholder: 'Commencez à écrire votre texte ici...',
  readOnly: false
}

// Add ref for Quill instance
const quillInstance = ref<any>(null)

// Add method to handle editor ready
const onEditorReady = (quill: any) => {
  quillInstance.value = quill
  quill.enable()
}

// Copy text function
const copyText = async () => {
  try {
    const tempDiv = document.createElement('div')
    tempDiv.innerHTML = editorContent.value
    const plainText = tempDiv.textContent || tempDiv.innerText
    await navigator.clipboard.writeText(plainText)
  } catch (err) {
    console.error('Failed to copy text:', err)
  }
}

// Export as DOC
const exportAsDoc = () => {
  // Create a temporary div to hold the formatted content
  const tempDiv = document.createElement('div')
  tempDiv.innerHTML = editorContent.value

  // Convert images to base64
  const images = tempDiv.getElementsByTagName('img')
  for (let i = 0; i < images.length; i++) {
    const img = images[i]
    if (img.src.startsWith('data:image')) {
      continue // Skip if already base64
    }
    // Convert image to base64
    const canvas = document.createElement('canvas')
    const ctx = canvas.getContext('2d')
    const imgObj = new Image()
    imgObj.crossOrigin = 'Anonymous'
    imgObj.src = img.src
    imgObj.onload = () => {
      canvas.width = imgObj.width
      canvas.height = imgObj.height
      ctx?.drawImage(imgObj, 0, 0)
      img.src = canvas.toDataURL('image/png')
    }
  }

  // Create the document content with proper formatting
  const content = `
    <html>
      <head>
        <meta charset="UTF-8">
        <style>
          body { 
            font-family: Arial, sans-serif;
            line-height: 1.5;
            color: #000;
          }
          img { 
            max-width: 100%;
            height: auto;
          }
          p { margin: 0 0 1em; }
          h1, h2, h3, h4, h5, h6 { 
            margin: 1em 0 0.5em;
            font-weight: bold;
          }
          ul, ol { 
            margin: 1em 0;
            padding-left: 2em;
          }
          li { margin: 0.5em 0; }
          blockquote {
            margin: 1em 0;
            padding: 0.5em 1em;
            border-left: 4px solid #ccc;
          }
          pre {
            background: #f5f5f5;
            padding: 1em;
            border-radius: 4px;
            overflow-x: auto;
          }
          table {
            border-collapse: collapse;
            width: 100%;
            margin: 1em 0;
          }
          th, td {
            border: 1px solid #ddd;
            padding: 0.5em;
          }
          th {
            background: #f5f5f5;
          }
        </style>
      </head>
      <body>
        ${tempDiv.innerHTML}
      </body>
    </html>
  `

  const blob = new Blob([content], { type: 'application/msword' })
  const url = window.URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = 'document.doc'
  document.body.appendChild(a)
  a.click()
  window.URL.revokeObjectURL(url)
  document.body.removeChild(a)
}

// Export as PDF
const exportAsPdf = async () => {
  try {
    const { jsPDF } = await import('jspdf')
    const html2canvas = (await import('html2canvas')).default

    // Create a temporary div to hold the formatted content
    const tempDiv = document.createElement('div')
    tempDiv.innerHTML = editorContent.value
    tempDiv.style.width = '210mm' // A4 width
    tempDiv.style.padding = '20mm'
    tempDiv.style.fontFamily = 'Arial, sans-serif'
    tempDiv.style.fontSize = '12pt'
    tempDiv.style.lineHeight = '1.5'
    tempDiv.style.color = '#000'
    tempDiv.style.backgroundColor = '#fff'
    document.body.appendChild(tempDiv)

    // Convert the content to canvas
    const canvas = await html2canvas(tempDiv, {
      scale: 2,
      useCORS: true,
      logging: false,
      backgroundColor: '#ffffff'
    })

    // Create PDF
    const pdf = new jsPDF({
      orientation: 'portrait',
      unit: 'mm',
      format: 'a4'
    })

    // Add the canvas to the PDF
    const imgData = canvas.toDataURL('image/png')
    const pdfWidth = pdf.internal.pageSize.getWidth()
    const pdfHeight = (canvas.height * pdfWidth) / canvas.width
    pdf.addImage(imgData, 'PNG', 0, 0, pdfWidth, pdfHeight)

    // Save the PDF
    pdf.save('document.pdf')

    // Clean up
    document.body.removeChild(tempDiv)
  } catch (err) {
    console.error('Failed to export PDF:', err)
  }
}
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
                <BreadcrumbPage>Éditeur de texte</BreadcrumbPage>
              </BreadcrumbItem>
            </BreadcrumbList>
          </Breadcrumb>
        </div>
      </header>

      <div class="flex flex-1 flex-col gap-6 p-6">
        <div class="flex flex-col gap-2">
          <div class="flex items-center gap-2">
            <Scan class="h-6 w-6 text-primary" />
            <h1 class="text-2xl font-semibold">Éditeur de texte</h1>
          </div>
          <p class="text-muted-foreground">
            Créez et formatez vos documents avec notre éditeur de texte riche. Utilisez la barre d'outils pour modifier le style, ajouter des listes, des tableaux et plus encore.
          </p>
        </div>

        <div class="flex items-center justify-end">
                      <div class="flex items-center gap-2">
            <Button 
              variant="outline" 
              size="sm" 
              @click="copyText" 
              :disabled="!editorContent"
              class="hover:bg-blue-100 hover:text-blue-600 transition-colors"
            >
                      <Copy class="h-4 w-4 mr-2" />
                      Copier
                    </Button>
            <Button 
              variant="outline" 
              size="sm" 
              @click="exportAsDoc" 
              :disabled="!editorContent"
              class="hover:bg-green-100 hover:text-green-600 transition-colors"
            >
                      <FileTextIcon class="h-4 w-4 mr-2" />
                      DOC
                    </Button>
            <Button 
              variant="outline" 
              size="sm" 
              @click="exportAsPdf" 
              :disabled="!editorContent"
              class="hover:bg-red-100 hover:text-red-600 transition-colors"
            >
                      <FileDown class="h-4 w-4 mr-2" />
                      PDF
                    </Button>
                  </div>
                </div>

                <div class="relative">
                  <QuillEditor
                    v-model:content="editorContent"
                    contentType="html"
                    :options="editorOptions"
                    toolbar="full"
                    class="min-height-300"
                    @ready="onEditorReady"
                  />
        </div>
      </div>
    </SidebarInset>
  </SidebarProvider>
</template>

<style>
.min-height-300 {
  min-height: calc(100vh - 200px);
}

.ql-container {
  height: calc(100vh - 300px) !important;
}

.ql-editor {
  height: 100% !important;
}
</style> 