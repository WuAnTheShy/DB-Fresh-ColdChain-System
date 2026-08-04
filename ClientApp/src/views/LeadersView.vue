<script setup>
import { Search, SlidersHorizontal, Store } from '@lucide/vue'
import { computed, ref } from 'vue'
import LeaderCard from '../components/LeaderCard.vue'
import StoreBreadcrumb from '../components/StoreBreadcrumb.vue'
import { useShop } from '../state/shop'

const { leaders } = useShop()
const keyword = ref('')
const area = ref('all')
const results = computed(() => leaders.filter((leader) => {
  const matchesKeyword = !keyword.value || `${leader.name}${leader.title}${leader.description}`.includes(keyword.value)
  const matchesArea = area.value === 'all' || leader.area.includes(area.value)
  return matchesKeyword && matchesArea
}))
</script>

<template>
  <div class="store-container page-space">
    <StoreBreadcrumb :items="[{ label: '团长广场' }]" />
    <div class="listing-header">
      <div><span class="title-icon"><Store :size="23" /></span><div><h1>团长广场</h1><p>查看平台认证团长以及他们正在带货的商品</p></div></div>
      <span>{{ results.length }} 位团长正在开团</span>
    </div>

    <section class="leader-toolbar">
      <label class="toolbar-search"><Search :size="18" /><input v-model.trim="keyword" type="search" placeholder="搜索团长姓名或擅长品类" /></label>
      <label class="toolbar-select"><SlidersHorizontal :size="17" /><select v-model="area"><option value="all">全部服务区域</option><option value="浦东新区">浦东新区</option><option value="徐汇区">徐汇区</option><option value="闵行区">闵行区</option></select></label>
    </section>

    <div v-if="results.length" class="leader-grid leader-directory-grid">
      <LeaderCard v-for="leader in results" :key="leader.id" :leader="leader" />
    </div>
    <div v-else class="store-empty"><Store :size="32" /><strong>没有找到匹配的团长</strong><span>请尝试更换姓名或服务区域</span></div>
  </div>
</template>
