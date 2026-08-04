<script setup>
import { ChevronRight } from '@lucide/vue'
defineProps({ items: { type: Array, default: () => [] } })
</script>

<template>
  <nav class="store-breadcrumb" aria-label="面包屑导航">
    <RouterLink to="/">首页</RouterLink>
    <template v-for="item in items" :key="item.label">
      <ChevronRight :size="14" />
      <RouterLink v-if="item.to" :to="item.to">{{ item.label }}</RouterLink>
      <span v-else>{{ item.label }}</span>
    </template>
  </nav>
</template>
