<script setup>
import { Apple, BadgeCheck, Beef, Check, ChevronRight, Clock3, Fish, Leaf, LockKeyhole, MapPin, Milk, PackageCheck, ShieldCheck, ShoppingBasket, ShoppingCart, Snowflake, Truck } from '@lucide/vue'
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import ProductCard from '../components/ProductCard.vue'
import QuantityStepper from '../components/QuantityStepper.vue'
import { api } from '../services/api'
import { useShop } from '../state/shop'
import { useCustomerContext } from '../state/customer'

const props = defineProps({ id: { type: String, required: true } })
const route = useRoute()
const router = useRouter()
const { products, catalogLoaded, catalogLoading, catalogError, productById, leaderById, loadCatalog, addToCart, buyNowProduct, isLeaderFollowed } = useShop()
const { isAuthenticated, deliveryLocation } = useCustomerContext()
const product = computed(() => productById(props.id))
const leader = computed(() => leaderById(product.value?.leaderId))
const quantity = ref(1)
const added = ref(false)
const related = computed(() => products.filter((item) => item.id !== String(props.id)).slice(0, 4))
const defaultEvaluationDimensions = [
  { code: 'HIGH_QUALITY', name: '高品质', count: 0 },
  { code: 'FAST_SHIPPING', name: '发货快', count: 0 },
  { code: 'GOOD_PACKAGING', name: '包装完好', count: 0 },
  { code: 'COST_EFFECTIVE', name: '性价比高', count: 0 },
  { code: 'AFFORDABLE', name: '价格实惠', count: 0 },
  { code: 'RELIABLE_PROMOTER', name: '团长靠谱', count: 0 },
]
const evaluationSummary = ref({ totalCount: 0, dimensions: defaultEvaluationDimensions })
const evaluationLoading = ref(false)
let evaluationRequestId = 0
const authLink = computed(() => ({ name: 'auth', query: { redirect: route.fullPath } }))
const canViewPrice = computed(() => isAuthenticated.value && isLeaderFollowed(product.value?.leaderId))
const followLink = computed(() => canViewPrice.value ? null : `/leaders/${leader.value?.id ?? ''}`)
const intro = ref(null)
const introLoading = ref(false)
const introError = ref('')
let introRequestId = 0
async function checkIntro() {
  const requestId = ++introRequestId
  intro.value = null
  introLoading.value = false
  introError.value = ''
  const p = product.value
  const l = leader.value
  if (!p || !l || p.isFallback || !p.productId) return
  introLoading.value = true
  try {
    const result = await api.getPromoterProductIntro(l.id, p.productId)
    if (requestId !== introRequestId) return
    intro.value = result?.hasIntro ? result : null
  } catch (error) {
    if (requestId !== introRequestId) return
    if (error?.status !== 404) introError.value = '团长推荐信息暂时读取失败，请重试'
  } finally {
    if (requestId === introRequestId) introLoading.value = false
  }
}

// 与商品卡片一致的品类标识：不同图标 + 颜色 + 背景填充圆角
const categoryStyles = [
  { pattern: /果|fruit/i, icon: Apple, color: '#e2574c', bg: '#fdeceb' },
  { pattern: /菜|豆|vegetable/i, icon: Leaf, color: '#2e9e5b', bg: '#e9f7ef' },
  { pattern: /肉|禽|蛋|meat|egg/i, icon: Beef, color: '#c2571a', bg: '#fbeee6' },
  { pattern: /海|水产|fish|seafood/i, icon: Fish, color: '#2463a7', bg: '#e8f0fb' },
  { pattern: /乳|奶|烘焙|dairy|bakery/i, icon: Milk, color: '#b8860b', bg: '#fdf6e8' },
]
const defaultCategoryStyle = { icon: ShoppingBasket, color: '#6b7280', bg: '#f1f2f2' }
const categoryStyle = computed(() => {
  const name = String(product.value?.category ?? '')
  return categoryStyles.find((item) => item.pattern.test(name)) ?? defaultCategoryStyle
})

watch([() => props.id, catalogLoaded], () => {
  if (catalogLoaded.value && !product.value) router.replace('/search')
}, { immediate: true })

// 目录就绪或切换商品后读取团长推文，直接展示在商品亮点中。
watch([() => props.id, () => leader.value?.id, () => product.value?.productId, () => product.value?.isFallback],
  () => checkIntro(), { immediate: true, flush: 'sync' })
watch([() => props.id, () => leader.value?.id],
  () => loadEvaluationSummary(), { immediate: true, flush: 'sync' })
// 切换商品或离开页面后，旧请求不能覆盖新页面的推文内容。
onUnmounted(() => { introRequestId++; evaluationRequestId++ })

onMounted(() => loadCatalog().catch(() => { }))

function add() {
  if (!product.value || !leader.value) return
  addToCart(product.value.id, quantity.value)
  added.value = true
  window.setTimeout(() => { added.value = false }, 1400)
}

function buyNow() {
  if (!product.value || !leader.value) return
  buyNowProduct(product.value.id, quantity.value)
  router.push('/checkout')
}

async function loadEvaluationSummary() {
  const requestId = ++evaluationRequestId
  const p = product.value
  const l = leader.value
  evaluationSummary.value = { totalCount: 0, dimensions: defaultEvaluationDimensions }
  if (!p || !l?.id) return
  evaluationLoading.value = true
  try {
    const result = await api.getPromoterEvaluationSummary(l.id)
    if (requestId !== evaluationRequestId) return
    evaluationSummary.value = {
      totalCount: Number(result?.totalCount ?? 0),
      dimensions: Array.isArray(result?.dimensions) && result.dimensions.length
        ? result.dimensions
        : defaultEvaluationDimensions,
    }
  } catch {
    if (requestId === evaluationRequestId)
      evaluationSummary.value = { totalCount: 0, dimensions: defaultEvaluationDimensions }
  } finally {
    if (requestId === evaluationRequestId) evaluationLoading.value = false
  }
}
</script>

<template>
  <div v-if="product && leader" class="store-container page-space product-detail-page">
    <section class="product-detail-main">
      <div class="product-gallery">
        <div class="product-main-image"><img :src="product.image" :alt="product.name"
            :class="{ 'fallback-photo-tint': product.image === product.fallbackImage }"
            @error="$event.target.classList.add('fallback-photo-tint'); $event.target.src = product.fallbackImage" /><span
            :class="`storage-badge storage-badge-${product.storageType.toLowerCase()}`">
            {{ product.storage }}
          </span></div>
        <div class="product-thumb active"><img :src="product.image" alt="商品主图缩略图"
            :class="{ 'fallback-photo-tint': product.image === product.fallbackImage }"
            @error="$event.target.classList.add('fallback-photo-tint'); $event.target.src = product.fallbackImage" /></div>
      </div>

      <div class="product-info-column">
        <span class="detail-category-badge" :style="{ color: categoryStyle.color, background: categoryStyle.bg }">
          <component :is="categoryStyle.icon" :size="14" />
          {{ product.category }}
        </span>
        <h1>{{ product.name }}</h1>
        <p class="detail-summary">{{ product.summary }}</p>
        <div class="detail-leader-panel">
          <img :src="leader.avatar" :alt="`${leader.name}头像`" />
          <div><span><strong>{{ leader.name }}</strong>
              <BadgeCheck :size="16" />
            </span><small>{{ leader.title }} · {{ leader.area }}</small></div>
          <RouterLink :to="`/leaders/${leader.id}`">查看详情
            <ChevronRight :size="15" />
          </RouterLink>
        </div>
        <dl class="product-facts">
          <div>
            <dt>规格</dt>
            <dd>{{ product.spec }}</dd>
          </div>
          <div>
            <dt>温控</dt>
            <dd :class="`storage-text storage-text-${product.storageType.toLowerCase()}`">{{ product.storage }}</dd>
          </div>
          <div v-if="canViewPrice">
            <dt>库存</dt>
            <dd>现货 {{ product.stock }} 件</dd>
          </div>
        </dl>
      </div>

      <aside class="buy-box">
        <template v-if="canViewPrice">
          <div class="buy-price"><span>¥</span><strong>{{ product.price.toFixed(2) }}</strong></div>
        </template>
        <div v-else class="buy-gated-message">
          <LockKeyhole :size="20" /><strong>关注团长后查看专属价格</strong><span>{{ isAuthenticated ? '关注该团长后，即可立即查看价格和购买商品' :
            '登录后关注该团长，即可查看价格和购买商品' }}</span>
        </div>
        <div class="delivery-promise">
          <Truck :size="19" />
          <div><strong>{{ product.delivery }}</strong><span>配送至 {{ deliveryLocation }}</span></div>
        </div>
        <template v-if="canViewPrice">
          <div class="stock-status">
            <Check :size="17" />有货
          </div>
          <label class="buy-quantity">数量
            <QuantityStepper v-model="quantity" :max="product.stock" />
          </label>
          <button class="btn btn-cart w-100" type="button" :disabled="product.isFallback" @click="add">
            <Check v-if="added" :size="18" />
            <ShoppingCart v-else :size="18" />{{ product.isFallback ? '真实目录恢复后可购买' : added ? '已加入购物车' : '加入购物车' }}
          </button>
          <button class="btn btn-buy w-100" type="button" :disabled="product.isFallback" @click="buyNow">{{
            product.isFallback ? '暂不可下单' : '立即购买' }}</button>
        </template>
        <RouterLink v-else class="btn btn-buy w-100 gated-login-button" :to="isAuthenticated ? followLink : authLink">{{
          isAuthenticated ? '前往关注团长' : '登录并关注团长' }}</RouterLink>
        <small class="buy-box-guarantee">
          <ShieldCheck :size="15" />平台交易保障 · 认证团长
        </small>
      </aside>
    </section>

    <section class="detail-info-band">
      <div>
        <MapPin :size="24" /><span><strong>精选货源</strong></span>
      </div>
      <div>
        <PackageCheck :size="24" /><span><strong>便捷下单</strong></span>
      </div>
      <div>
        <Clock3 :size="24" /><span><strong>准时发货</strong></span>
      </div>
      <div>
        <Snowflake :size="24" /><span><strong>冷链到家</strong></span>
      </div>
    </section>

    <section class="product-community-module product-description-section">
      <header>
        <h2>商品详情</h2>
      </header>
      <div class="description-grid">
        <div class="product-highlights">
          <div v-if="intro" class="inline-promoter-intro">
            <h4 v-if="intro.title">{{ intro.title }}</h4>
            <template v-for="(section, sectionIndex) in intro.sections" :key="sectionIndex">
              <p v-if="section.text">{{ section.text }}</p>
              <div v-if="section.images?.length" class="inline-promoter-intro-images">
                <img v-for="(url, imageIndex) in section.images" :key="`${sectionIndex}-${imageIndex}`" :src="url"
                  alt="团长推文配图" loading="lazy" @error="$event.target.style.display = 'none'" />
              </div>
            </template>
          </div>
          <p v-else-if="introLoading" class="inline-intro-state">正在读取团长推文…</p>
          <div v-else-if="introError" class="inline-intro-state" role="alert">
            <span>{{ introError }}</span>
            <button class="btn btn-sm btn-outline-secondary" type="button" @click="checkIntro">重新加载</button>
          </div>
          <p v-else>{{ product.summary }}</p>
        </div>
      </div>
    </section>

    <section class="product-community-module product-showcase-module" aria-labelledby="showcase-title">
      <header>
        <h2 id="showcase-title">该团购所属团长主页评价</h2>
        <span>{{ evaluationLoading ? '读取中…' : `全部商品共 ${evaluationSummary.totalCount} 条评价` }}</span>
      </header>
      <div class="showcase-tag-list" aria-label="商品评价标签">
        <span v-for="dimension in evaluationSummary.dimensions" :key="dimension.code">{{ dimension.name }} ({{ dimension.count }})</span>
      </div>
    </section>

    <section class="product-community-module product-group-records-module" aria-labelledby="group-records-title">
      <header>
        <h2 id="group-records-title">跟团记录</h2>
      </header>
      <div class="group-record-list-empty">暂无跟团记录</div>
    </section>

    <section class="home-section px-0">
      <div class="section-title-row">
        <div>
          <h2>你可能还喜欢</h2>
        </div>
      </div>
      <div class="product-grid">
        <ProductCard v-for="item in related" :key="item.id" :product="item" />
      </div>
    </section>
  </div>
  <div v-else-if="catalogLoading" class="store-container page-space store-loading" role="status">正在读取商品信息…</div>
  <div v-else-if="catalogError" class="store-container page-space store-empty" role="alert">
    <strong>商品信息读取失败</strong><span>{{ catalogError }}</span><button class="btn btn-outline-secondary" type="button"
      @click="loadCatalog(true)">重新加载</button>
  </div>
</template>

<style scoped>
.product-detail-main {
  display: grid;
  grid-template-columns: minmax(300px, .95fr) minmax(330px, 1fr) 300px;
  gap: 28px;
  padding: 24px;
  border: 1px solid var(--line);
  background: #fff;
}

.product-gallery {
  display: grid;
  grid-template-columns: 62px minmax(0, 1fr);
  grid-template-rows: auto;
  gap: 10px;
  align-items: start;
}

.product-main-image {
  position: relative;
  grid-column: 2;
  aspect-ratio: 1 / 1;
  overflow: hidden;
  background: #eef1ef;
}

.product-main-image img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.product-main-image>span {
  position: absolute;
  left: 10px;
  bottom: 10px;
  display: inline-flex;
  align-items: center;
  gap: 5px;
  padding: 5px 8px;
  border-radius: 4px;
  background: rgba(255, 255, 255, .94);
  color: #2463a7;
  font-size: 10px;
  font-weight: 700;
}

.product-thumb {
  grid-column: 1;
  grid-row: 1;
  aspect-ratio: 1 / 1;
  padding: 2px;
  border: 2px solid var(--brand);
}

.product-thumb img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.product-info-column {
  min-width: 0;
}

.detail-category-badge {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  margin-bottom: 9px;
  padding: 5px 10px;
  border-radius: 999px;
  font-size: 11px;
  font-weight: 750;
  line-height: 1;
}

.product-info-column h1 {
  margin: 0;
  font-size: 25px;
  font-weight: 800;
  line-height: 1.35;
}

.detail-summary {
  margin: 9px 0 18px;
  color: var(--muted);
  line-height: 1.65;
  overflow-wrap: anywhere;
  word-break: break-word;
}

.detail-leader-panel {
  display: grid;
  grid-template-columns: 48px minmax(0, 1fr) auto;
  gap: 10px;
  align-items: center;
  padding: 12px;
  border: 1px solid #cfe1d8;
  background: #f2f8f5;
}

.detail-leader-panel>img {
  width: 48px;
  height: 48px;
  border-radius: 50%;
  object-fit: cover;
}

.detail-leader-panel>div {
  min-width: 0;
}

.detail-leader-panel>div>span {
  display: flex;
  align-items: center;
  gap: 4px;
}

.detail-leader-panel svg {
  color: var(--brand);
}

.detail-leader-panel small {
  display: block;
  margin-top: 3px;
  overflow: hidden;
  color: var(--muted);
  font-size: 9px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.detail-leader-panel>a {
  display: inline-flex;
  align-items: center;
  color: var(--brand);
  font-size: 10px;
  font-weight: 700;
  text-decoration: none;
}

.product-facts {
  margin: 15px 0 0;
}

.product-facts>div {
  display: grid;
  grid-template-columns: 62px 1fr;
  padding: 10px 0;
  border-bottom: 1px solid var(--line);
}

.product-facts dt {
  color: var(--muted);
  font-size: 10px;
}

.product-facts dd {
  margin: 0;
  font-size: 11px;
}

/* 温控颜色区分：冷藏=蓝 / 冷冻=冰蓝 / 常温=暖橙 */
.storage-text {
  font-style: normal;
  font-weight: 700;
}

.storage-text-chilled {
  color: #2463a7;
}

.storage-text-frozen {
  color: #0e7490;
}

.storage-text-ambient {
  color: #c2571a;
}

.storage-badge {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  padding: 5px 8px;
  border-radius: 4px;
  background: rgba(255, 255, 255, .94);
  font-size: 10px;
  font-weight: 700;
}

.product-main-image>span.storage-badge-chilled {
  color: #2463a7;
}

.product-main-image>span.storage-badge-frozen {
  color: #0e7490;
}

.product-main-image>span.storage-badge-ambient {
  color: #c2571a;
}

.detail-info-band small .storage-text {
  font-size: inherit;
}

.buy-box {
  align-self: start;
  padding: 18px;
  border: 1px solid #cfd7d3;
  border-radius: 6px;
  box-shadow: 0 3px 10px rgba(23, 33, 29, .08);
}

.buy-price {
  display: flex;
  align-items: baseline;
  color: var(--danger);
}

.buy-price>span {
  font-size: 16px;
}

.buy-price strong {
  font-size: 30px;
}

.buy-price {
  margin-bottom: 15px;
}

.buy-gated-message {
  display: flex;
  flex-direction: column;
  gap: 5px;
  margin-bottom: 15px;
  padding: 13px;
  border: 1px solid #cfe1d8;
  background: #f2f8f5;
  color: var(--brand);
}

.buy-gated-message strong {
  color: var(--ink);
  font-size: 13px;
}

.buy-gated-message span {
  color: var(--muted);
  font-size: 10px;
  line-height: 1.5;
}

.gated-login-button {
  margin-top: 8px;
}

.delivery-promise {
  display: flex;
  gap: 8px;
  margin-bottom: 12px;
  color: var(--brand);
}

.delivery-promise div {
  display: flex;
  min-width: 0;
  flex-direction: column;
}

.delivery-promise strong {
  color: var(--ink);
  font-size: 11px;
}

.delivery-promise span {
  margin-top: 2px;
  color: var(--muted);
  font-size: 9px;
}

.stock-status {
  display: flex;
  align-items: center;
  gap: 5px;
  margin-bottom: 14px;
  color: var(--brand);
  font-size: 11px;
  font-weight: 700;
}

.buy-quantity {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin: 15px 0;
  color: var(--muted);
  font-size: 10px;
}

.buy-box>.btn {
  margin-top: 8px;
}

.buy-box-guarantee {
  display: flex;
  align-items: center;
  gap: 4px;
  margin-top: 13px;
  color: var(--muted);
  font-size: 9px;
}

.detail-info-band {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  margin-top: 16px;
  border: 1px solid var(--line);
  background: #fff;
}

.detail-info-band>div {
  display: flex;
  min-height: 78px;
  align-items: center;
  justify-content: center;
  gap: 9px;
  border-right: 1px solid var(--line);
  color: var(--brand);
}

.detail-info-band>div:last-child {
  border-right: 0;
}

.detail-info-band span {
  display: flex;
  flex-direction: column;
}

.detail-info-band strong {
  display: inline-flex;
  height: 30px;
  align-items: center;
  color: var(--ink);
  font-size: 14px;
  line-height: 30px;
}

.detail-info-band small {
  color: var(--muted);
  font-size: 9px;
}

.product-description-section {
  overflow: hidden;
}

.description-grid {
  display: grid;
  grid-template-columns: 1fr;
  gap: 20px;
  min-height: 96px;
  padding: 30px 26px;
}

.description-grid h3 {
  margin: 0 0 7px;
  font-size: 12px;
}

.description-grid p {
  margin: 0;
  color: var(--muted);
  font-size: 10px;
  line-height: 1.7;
}

.inline-promoter-intro h4 {
  margin: 0 0 10px;
  color: var(--ink);
  font-size: 15px;
}

.inline-promoter-intro p + p,
.inline-promoter-intro-images + p {
  margin-top: 10px;
}

.inline-promoter-intro-images {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 12px;
  margin: 12px 0;
}

.inline-promoter-intro-images img {
  width: 100%;
  max-height: 420px;
  border-radius: 8px;
  object-fit: cover;
}

.inline-intro-state {
  display: flex;
  align-items: center;
  gap: 10px;
}

.product-community-module {
  margin-top: 16px;
  background: #fff;
}

.product-community-module>header {
  display: flex;
  min-height: 72px;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 0 26px;
  border-bottom: 1px solid #edf0ee;
}

.product-community-module h2 {
  margin: 0;
  color: var(--ink);
  font-size: 18px;
  font-weight: 700;
}

.product-community-module>header>span {
  display: inline-flex;
  align-items: center;
  color: #9a9f9c;
  font-size: 15px;
}

.showcase-tag-list {
  display: flex;
  flex-wrap: wrap;
  gap: 11px 13px;
  padding: 20px 26px 28px;
}

.showcase-tag-list span {
  padding: 7px 13px;
  border-radius: 5px;
  background: #e8f8f1;
  color: #13b86c;
  font-size: 15px;
  line-height: 1;
}

.group-record-list-empty {
  min-height: 96px;
  padding: 30px 26px;
  color: #9a9f9c;
  font-size: 14px;
}

@media (max-width: 1199.98px) {
  .product-detail-main {
    grid-template-columns: minmax(270px, .9fr) minmax(300px, 1fr);
  }

  .buy-box {
    grid-column: 2;
  }

  .product-gallery {
    grid-row: span 2;
  }
}

@media (max-width: 991.98px) {
  .product-detail-main {
    grid-template-columns: minmax(280px, .9fr) minmax(300px, 1fr);
    gap: 20px;
  }

  .buy-box {
    grid-column: 1 / -1;
  }

  .product-gallery {
    grid-row: auto;
  }

  .detail-info-band {
    grid-template-columns: repeat(2, 1fr);
  }

  .detail-info-band>div:nth-child(2) {
    border-right: 0;
  }

  .detail-info-band>div:nth-child(-n+2) {
    border-bottom: 1px solid var(--line);
  }

  .description-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 767.98px) {
  .product-detail-main {
    grid-template-columns: 1fr;
    gap: 18px;
    padding: 14px;
  }

  .product-gallery {
    grid-template-columns: 48px minmax(0, 1fr);
  }

  .product-info-column h1 {
    font-size: 21px;
  }

  .buy-box {
    grid-column: auto;
  }

  .detail-info-band {
    grid-template-columns: 1fr 1fr;
  }

  .detail-info-band>div {
    min-height: 68px;
    padding: 7px;
  }

  .product-community-module>header {
    min-height: 64px;
    padding: 0 16px;
  }

  .product-community-module h2 {
    font-size: 16px;
  }

  .product-community-module>header>span {
    font-size: 13px;
  }

  .showcase-tag-list {
    gap: 9px;
    padding: 16px 16px 22px;
  }

  .description-grid {
    padding: 26px 16px;
  }

  .showcase-tag-list span {
    padding: 7px 10px;
    font-size: 13px;
  }

  .group-record-list-empty {
    padding: 26px 16px;
  }
}

</style>
