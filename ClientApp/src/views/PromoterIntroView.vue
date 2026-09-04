<script setup>
import { ArrowLeft, BadgeCheck, FileText, PenLine } from '@lucide/vue'
import { computed, onMounted, ref, watch } from 'vue'
import { api } from '../services/api'
import { useShop } from '../state/shop'

const props = defineProps({
  leaderId: { type: String, required: true },
  productId: { type: String, required: true },
})
const { products, leaderById, catalogLoading, catalogError, leadersLoading, loadLeaders, loadCatalog } = useShop()
const leader = computed(() => leaderById(props.leaderId))
const product = computed(() => products.find((item) =>
  item.productId === String(props.productId) && item.leaderId === String(props.leaderId)))
const intro = ref(null)
const introLoaded = ref(false)
const introError = ref('')
const notFound = ref(false)

onMounted(async () => {
  await loadBase()
  await fetchIntro()
})

watch(() => [props.leaderId, props.productId], async () => {
  await loadBase()
  await fetchIntro()
})

async function loadBase() {
  try {
    await Promise.all([loadLeaders(), loadCatalog()])
  } catch {
    // 目录读取失败时保留页面加载/失败状态，允许消费者重试
  }
}

async function fetchIntro() {
  introLoaded.value = false
  introError.value = ''
  notFound.value = false
  if (!leader.value || !product.value) {
    introLoaded.value = true
    return
  }
  try {
    const result = await api.getPromoterProductIntro(leader.value.id, product.value.productId)
    intro.value = result?.hasIntro ? result : null
  } catch (error) {
    if (error?.status === 404) notFound.value = true
    else introError.value = error?.message || '推文读取失败'
    intro.value = null
  } finally {
    introLoaded.value = true
  }
}
</script>

<template>
  <div class="store-container page-space promo-intro-page">
    <nav class="pi-crumbs">
      <RouterLink :to="product ? `/products/${product.id}` : '/search'"><ArrowLeft :size="14" /> 返回商品详情</RouterLink>
      <span class="pi-crumb-sep">/</span>
      <RouterLink :to="leader ? `/leaders/${leader.id}` : '/leaders'">{{ leader?.name ?? '团长' }}的主页</RouterLink>
      <span class="pi-crumb-sep">/</span><em>团长推文</em>
    </nav>

    <template v-if="product && leader">
      <header class="pi-head">
        <div class="pi-head-product">
          <img :src="product.image" :alt="product.name" @error="$event.target.style.opacity = 0" />
          <div class="pi-head-product-copy">
            <span class="pi-eyebrow">团长正在带货 · {{ product.category }}</span>
            <h1>{{ product.name }}</h1>
            <p>{{ product.spec }} · {{ leader.name }}团长为你推荐</p>
          </div>
        </div>
        <div class="pi-head-leader">
          <img :src="leader.avatar" :alt="`${leader.name}团长头像`" />
          <div>
            <span class="pi-verified"><BadgeCheck :size="14" /> 平台认证</span>
            <strong>{{ leader.name }}团长</strong>
            <small>看看他怎么说这款商品</small>
          </div>
        </div>
      </header>

      <main class="pi-main">
        <div v-if="!introLoaded" class="pi-state" role="status">
          <span class="pi-spinner"></span>正在读取团长推文…
        </div>

        <template v-else-if="intro">
          <article class="pi-article">
            <div class="pi-article-label"><PenLine :size="15" /> 团长推文</div>
            <h2 v-if="intro.title" class="pi-title">{{ intro.title }}</h2>
            <template v-for="(section, idx) in intro.sections" :key="idx">
              <p v-if="section.text" class="pi-text">{{ section.text }}</p>
              <div v-if="section.images && section.images.length" class="pi-images">
                <img v-for="(url, i) in section.images" :key="i" :src="url" alt="推文配图" loading="lazy"
                  @error="$event.target.style.display = 'none'" />
              </div>
            </template>
          </article>
          <p class="pi-tail">—— {{ leader.name }}团长 · 新鲜冷链直达 ——</p>
        </template>

        <div v-else-if="introError" class="pi-state" role="alert">
          <FileText :size="34" />
          <strong>推文读取失败</strong>
          <span>{{ introError }}</span>
          <button class="btn btn-outline-secondary" type="button" @click="fetchIntro">重新加载</button>
        </div>

        <div v-else-if="notFound" class="pi-state">
          <FileText :size="34" />
          <strong>暂无法查看该推文</strong>
          <span>该商品已不在 {{ leader.name }}团长的在售列表中，可能已被下架或移除。</span>
          <RouterLink class="btn btn-buy" :to="`/leaders/${leader.id}`">逛逛团长在售商品</RouterLink>
        </div>

        <div v-else class="pi-state">
          <FileText :size="34" />
          <strong>团长暂未撰写该商品的推文</strong>
          <span>商品详情页当前展示的是供应商提供的商品介绍，也可关注团长获取更多推荐。</span>
          <RouterLink class="btn btn-buy" :to="`/products/${product.id}`">返回商品详情</RouterLink>
        </div>
      </main>
    </template>

    <div v-else-if="catalogLoading || leadersLoading" class="pi-state pi-page-state" role="status">
      <span class="pi-spinner"></span>正在加载…
    </div>

    <div v-else class="pi-state pi-page-state">
      <FileText :size="34" />
      <strong>未找到对应的团长推文</strong>
      <span>{{ catalogError || '团长或商品信息不存在，可能已下架或已删除。' }}</span>
      <RouterLink class="btn btn-outline-secondary" to="/search">返回商城</RouterLink>
    </div>
  </div>
</template>

<style scoped>
.promo-intro-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.pi-crumbs {
  display: flex;
  align-items: center;
  gap: 7px;
  color: var(--muted);
  font-size: 11px;
}

.pi-crumbs a {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  color: var(--brand);
  text-decoration: none;
}

.pi-crumbs a:hover {
  text-decoration: underline;
}

.pi-crumbs em {
  color: var(--ink);
  font-style: normal;
  font-weight: 700;
}

.pi-crumb-sep {
  color: #c3ccc7;
}

.pi-head {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  gap: 20px;
  align-items: stretch;
  border: 1px solid var(--line);
  border-radius: 14px;
  background: #fff;
  overflow: hidden;
}

.pi-head-product {
  display: flex;
  gap: 18px;
  align-items: center;
  padding: 20px 24px;
}

.pi-head-product>img {
  width: 92px;
  height: 92px;
  flex: 0 0 auto;
  border-radius: 12px;
  object-fit: cover;
  background: #eef1ef;
}

.pi-head-product-copy {
  min-width: 0;
}

.pi-eyebrow {
  display: inline-block;
  margin-bottom: 6px;
  padding: 3px 9px;
  border-radius: 999px;
  background: #eaf5ef;
  color: var(--brand);
  font-size: 10px;
  font-weight: 750;
}

.pi-head-product-copy h1 {
  margin: 0 0 7px;
  font-size: 21px;
  font-weight: 800;
  line-height: 1.35;
}

.pi-head-product-copy p {
  margin: 0;
  color: var(--muted);
  font-size: 11px;
}

.pi-head-leader {
  display: flex;
  gap: 12px;
  align-items: center;
  min-width: 240px;
  padding: 20px 26px;
  border-left: 1px solid var(--line);
  background: #f6f9f7;
}

.pi-head-leader>img {
  width: 56px;
  height: 56px;
  border-radius: 50%;
  object-fit: cover;
}

.pi-head-leader>div {
  display: flex;
  flex-direction: column;
}

.pi-verified {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  margin-bottom: 4px;
  color: var(--brand);
  font-size: 10px;
  font-weight: 750;
}

.pi-head-leader strong {
  font-size: 14px;
}

.pi-head-leader small {
  margin-top: 3px;
  color: var(--muted);
  font-size: 10px;
}

.pi-main {
  min-height: 220px;
}

.pi-article {
  padding: 34px 38px;
  border: 1px solid var(--line);
  border-radius: 14px;
  background: #fff;
}

.pi-article-label {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  margin-bottom: 18px;
  padding: 4px 12px;
  border-radius: 999px;
  background: #fdeed3;
  color: #b06a12;
  font-size: 11px;
  font-weight: 750;
}

.pi-title {
  margin: 0 0 20px;
  color: #223b46;
  font-size: 24px;
  font-weight: 800;
  line-height: 1.4;
}

.pi-text {
  margin: 0 0 16px;
  color: #4a5568;
  font-size: 15px;
  line-height: 2;
  overflow-wrap: anywhere;
  white-space: pre-wrap;
  word-break: break-word;
}

.pi-text:last-child {
  margin-bottom: 0;
}

.pi-images {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  margin: 0 0 16px;
}

.pi-images:last-child {
  margin-bottom: 0;
}

.pi-images img {
  max-width: 100%;
  max-height: 420px;
  border: 1px solid #edf2f7;
  border-radius: 10px;
  object-fit: contain;
  background: #f8fafc;
}

.pi-tail {
  margin: 18px 0 0;
  color: #a0aec0;
  font-size: 12px;
  text-align: center;
}

.pi-state {
  display: flex;
  min-height: 240px;
  flex-direction: column;
  gap: 8px;
  align-items: center;
  justify-content: center;
  padding: 30px;
  border: 1px dashed #d5ddd8;
  border-radius: 14px;
  background: #fff;
  color: var(--muted);
  text-align: center;
}

.pi-state svg {
  color: #b9c6bf;
}

.pi-state strong {
  color: var(--ink);
  font-size: 15px;
}

.pi-state span {
  font-size: 12px;
  line-height: 1.7;
}

.pi-state .btn {
  margin-top: 6px;
}

.pi-page-state {
  min-height: 300px;
}

.pi-spinner {
  width: 20px;
  height: 20px;
  border: 2px solid #dfe9e3;
  border-top-color: var(--brand);
  border-radius: 50%;
  animation: pi-spin 0.8s linear infinite;
}

@keyframes pi-spin {
  to {
    transform: rotate(360deg);
  }
}

@media (max-width: 767.98px) {
  .pi-head {
    grid-template-columns: 1fr;
  }

  .pi-head-leader {
    border-top: 1px solid var(--line);
    border-left: 0;
  }

  .pi-article {
    padding: 22px 18px;
  }
}
</style>
