<script setup lang="ts">
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'

defineProps<{
  title?: string
  data: {
    name: string
    color: string
    value: any
  }[]
}>()
</script>

<template>
  <div class="bg-white min-w-[180px] p-2 flex flex-col gap-1 rounded-lg">
    <div v-if="title" class="text-base font-semibold mb-1">{{ title }}</div>
    <div v-for="(item, key) in data" :key="key" class="flex items-center gap-2 py-1">
      <span class="inline-block w-1 h-5 rounded mr-2" :style="{ backgroundColor: item.color }"></span>
      <span class="text-sm">{{ item.name }}</span>
      <span class="ml-auto font-bold text-sm">{{ item.value }}</span>
    </div>
  </div>
</template>
