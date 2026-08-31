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

<style scoped>
.store-breadcrumb { display: flex; min-height: 26px; align-items: center; gap: 3px; margin-bottom: 13px; overflow: hidden; color: #65706b; font-size: 11px; white-space: nowrap; }
.store-breadcrumb a { color: #59655f; text-decoration: none; }
.store-breadcrumb span { overflow: hidden; text-overflow: ellipsis; }
</style>
