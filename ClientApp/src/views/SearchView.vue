<script setup>
import { SearchX, SlidersHorizontal } from '@lucide/vue'
import { computed, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import ProductCard from '../components/ProductCard.vue'
import StoreBreadcrumb from '../components/StoreBreadcrumb.vue'
import { useShop } from '../state/shop'

const route = useRoute()
const { categories, products } = useShop()
const storage = ref('all')
const sort = ref('default')

const activeCategory = computed(() => String(route.params.slug ?? ''))
const keyword = computed(() => String(route.query.q ?? '').trim())
const todayDeals = computed(() => route.query.deal === 'today')
const categoryName = computed(() => categories.find((item) => item.slug === activeCategory.value)?.name)
const pageTitle = computed(() => categoryName.value || (keyword.value ? `“${keyword.value}”的搜索结果` : todayDeals.value ? '今日特价' : '全部在团商品'))

const results = computed(() => {
  const list = products.filter((product) => {
    const text = `${product.name}${product.shortName}${product.spec}`.toLowerCase()
    return (!activeCategory.value || product.category === activeCategory.value)
      && (!keyword.value || text.includes(keyword.value.toLowerCase()))
      && (storage.value === 'all' || product.storage === storage.value)
  })
  if (sort.value === 'priceAsc') return [...list].sort((a, b) => a.price - b.price)
  if (sort.value === 'priceDesc') return [...list].sort((a, b) => b.price - a.price)
  if (sort.value === 'sold') return [...list].sort((a, b) => b.sold - a.sold)
  return list
})

watch(() => route.fullPath, () => {
  storage.value = 'all'
})
</script>

<template>
  <div class="store-container page-space">
    <StoreBreadcrumb :items="[{ label: todayDeals ? '今日特价' : categoryName || (keyword ? `搜索：${keyword}` : '全部商品') }]" />
    <div class="listing-header">
      <div><span class="title-icon"><SlidersHorizontal :size="22" /></span><div><h1>{{ pageTitle }}</h1></div></div>
      <span>共 {{ results.length }} 件商品</span>
    </div>

    <div class="listing-layout">
      <aside class="filter-panel">
        <div><strong>商品分类</strong><RouterLink to="/search">全部商品</RouterLink><RouterLink v-for="category in categories" :key="category.slug" :to="`/category/${category.slug}`">{{ category.name }}</RouterLink></div>
        <div><strong>温控方式</strong><label><input v-model="storage" type="radio" value="all" />全部</label><label><input v-model="storage" type="radio" value="冷藏" />冷藏</label></div>
      </aside>

      <section class="listing-results">
        <div class="sort-bar"><span>生鲜严选商品</span><label>排序<select v-model="sort"><option value="default">综合排序</option><option value="sold">参团人数</option><option value="priceAsc">价格从低到高</option><option value="priceDesc">价格从高到低</option></select></label></div>
        <div v-if="results.length" class="product-grid listing-product-grid"><ProductCard v-for="product in results" :key="product.id" :product="product" /></div>
        <div v-else class="store-empty"><SearchX :size="34" /><strong>没有找到符合条件的商品</strong><span>请清除筛选条件或尝试其他关键词</span><RouterLink class="btn btn-outline-secondary" to="/search">查看全部商品</RouterLink></div>
      </section>
    </div>
  </div>
</template>
