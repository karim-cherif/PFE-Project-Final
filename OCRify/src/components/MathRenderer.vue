<template>
  <span v-html="renderedMath"></span>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import katex from 'katex'
import 'katex/dist/katex.min.css'

const props = defineProps<{
  text: string
}>()

const renderedMath = computed(() => {
  if (!props.text) return ''
  
  // Replace LaTeX math expressions with KaTeX rendered HTML
  return props.text.replace(/\$([^$]+)\$/g, (match, math) => {
    try {
      return katex.renderToString(math, {
        throwOnError: false,
        displayMode: false
      })
    } catch (e) {
      console.error('Error rendering math:', e)
      return match // Return original if rendering fails
    }
  })
})
</script>

<style>
.katex {
  font-size: 1.1em;
  margin: 0 0.2em;
}

.katex-display {
  margin: 1em 0;
  text-align: center;
}
</style> 