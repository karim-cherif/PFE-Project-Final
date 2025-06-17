<script setup lang="ts">
import { AvatarFallback, type AvatarFallbackProps } from 'reka-ui'

const props = defineProps<AvatarFallbackProps>()
</script>

<template>
  <AvatarFallback v-bind="props" class="h-full w-full flex items-center justify-center bg-muted text-muted-foreground">
    <slot />
  </AvatarFallback>
</template>
