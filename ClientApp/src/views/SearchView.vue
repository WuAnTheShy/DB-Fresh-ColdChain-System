<script setup>
import { RefreshCw, SearchX, SlidersHorizontal } from '@lucide/vue'
import { computed, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import ProductCard from '../components/ProductCard.vue'
import { useShop } from '../state/shop'
import { useCustomerContext } from '../state/customer'

const route = useRoute()
const { categories, products, catalogLoading, catalogError, catalogUsingFallback, loadCatalog } = useShop()
const { isAuthenticated } = useCustomerContext()
const storage = ref('all')
const sort = ref('default')
const storageOptions = computed(() => [...new Set(products.map((product) => product.storage).filter(Boolean))])

const activeCategory = computed(() => String(route.params.slug ?? ''))
const keyword = computed(() => String(route.query.q ?? '').trim())
const categoryName = computed(() => categories.find((item) => item.slug === activeCategory.value)?.name)
const pageTitle = computed(() => categoryName.value || (keyword.value ? `“${keyword.value}”的搜索结果` : '全部在团商品'))

const results = computed(() => {
  const list = products.filter((product) => {
    const text = `${product.name}${product.shortName}${product.spec}`.toLowerCase()
    return (!activeCategory.value || product.category === activeCategory.value)
      && (!keyword.value || text.includes(keyword.value.toLowerCase()))
      && (storage.value === 'all' || product.storage === storage.value)
  })
  if (sort.value === 'priceAsc') return [...list].sort((a, b) => a.price - b.price)
  if (sort.value === 'priceDesc') return [...list].sort((a, b) => b.price - a.price)
  return list
})

watch(() => route.fullPath, () => {
  storage.value = 'all'
})
</script>

<template>
  <div class="store-container page-space">
    <div class="listing-header">
      <div><span class="title-icon"><SlidersHorizontal :size="22" /></span><div><h1>{{ pageTitle }}</h1></div></div>
      <span>共 {{ results.length }} 件商品</span>
    </div>

    <div class="listing-layout">
      <aside class="filter-panel">
        <div><strong>商品分类</strong><RouterLink to="/search">全部商品</RouterLink><RouterLink v-for="category in categories" :key="category.slug" :to="`/category/${category.slug}`">{{ category.name }}</RouterLink></div>
        <div><strong>温控方式</strong><label><input v-model="storage" type="radio" value="all" />全部</label><label v-for="option in storageOptions" :key="option"><input v-model="storage" type="radio" :value="option" />{{ option }}</label></div>
      </aside>

      <section class="listing-results">
        <div class="sort-bar"><span>当前在团商品</span><label>排序<select v-model="sort"><option value="default">综合排序</option><option v-if="isAuthenticated" value="priceAsc">价格从低到高</option><option v-if="isAuthenticated" value="priceDesc">价格从高到低</option></select></label></div>
        <div v-if="catalogUsingFallback" class="alert alert-warning" role="status">真实目录暂时不可用，当前为只读兜底展示，暂不可下单。</div>
        <div v-if="catalogLoading" class="store-loading" role="status">正在读取商品目录…</div>
        <div v-else-if="catalogError && !catalogUsingFallback" class="store-empty" role="alert"><strong>商品目录读取失败</strong><span>{{ catalogError }}</span><button class="btn btn-outline-secondary" type="button" @click="loadCatalog(true)"><RefreshCw :size="15" />重新加载</button></div>
        <template v-else-if="results.length">
          <div class="product-grid listing-product-grid"><ProductCard v-for="product in results" :key="product.id" :product="product" /></div>
          <p class="list-end-tip">到底了~</p>
        </template>
        <div v-else class="store-empty"><SearchX :size="34" /><strong>没有找到符合条件的商品</strong><span>请清除筛选条件或尝试其他关键词</span><RouterLink class="btn btn-outline-secondary" to="/search">查看全部商品</RouterLink></div>
      </section>
    </div>
  </div>
</template>

<style scoped>
.listing-layout { display: grid; grid-template-columns: 210px minmax(0, 1fr); gap: 16px; align-items: start; }
.filter-panel { border: 1px solid var(--line); background: #fff; }
.filter-panel > div { display: flex; flex-direction: column; gap: 9px; padding: 15px; border-bottom: 1px solid var(--line); }
.filter-panel > div:last-child { border-bottom: 0; }
.filter-panel strong { margin-bottom: 2px; font-size: 12px; }
.filter-panel a, .filter-panel label { color: #53605a; font-size: 11px; text-decoration: none; }
.filter-panel a:hover, .filter-panel a.router-link-active { color: var(--brand); font-weight: 700; }
.filter-panel label { display: flex; align-items: center; gap: 7px; }
.filter-panel input { accent-color: var(--brand); }
.sort-bar { display: flex; min-height: 48px; align-items: center; justify-content: space-between; gap: 12px; margin-bottom: 13px; padding: 0 14px; border: 1px solid var(--line); background: #fff; }
.sort-bar > span { font-weight: 700; }
.sort-bar label { display: flex; align-items: center; gap: 7px; color: var(--muted); font-size: 10px; }
.sort-bar select { height: 32px; border: 1px solid #cbd3cf; border-radius: 4px; background: #fff; }
.listing-results { min-width: 0; }

@media (max-width: 991.98px) {
  .listing-product-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
}

@media (max-width: 767.98px) {
  .listing-layout { grid-template-columns: 1fr; }
  .filter-panel { display: grid; grid-template-columns: repeat(3, 1fr); }
  .filter-panel > div { min-width: 0; border-right: 1px solid var(--line); border-bottom: 0; }
  .filter-panel label { font-size: 9px; }
  .filter-panel a:not(.router-link-active), .filter-panel > div:nth-child(2) label:nth-of-type(n+3) { display: none; }
  .sort-bar { padding: 0 10px; }

  /* 移动端分类 / 搜索结果商品列表单列 */
  .product-grid.listing-product-grid { grid-template-columns: minmax(0, 1fr); gap: 12px; }
}
</style>
