<script setup>
import { BadgeCheck, Check, ChevronRight, Clock3, LockKeyhole, MapPin, PackageCheck, ShieldCheck, ShoppingCart, Snowflake, Truck } from '@lucide/vue'
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import ProductCard from '../components/ProductCard.vue'
import QuantityStepper from '../components/QuantityStepper.vue'
import StoreBreadcrumb from '../components/StoreBreadcrumb.vue'
import { useShop } from '../state/shop'
import { useCustomerContext } from '../state/customer'

const props = defineProps({ id: { type: String, required: true } })
const route = useRoute()
const router = useRouter()
const { products, productById, leaderById, recordProductEntry, addToCart } = useShop()
const { isAuthenticated } = useCustomerContext()
const product = computed(() => productById(props.id))
const leader = computed(() => leaderById(product.value?.leaderId))
const quantity = ref(1)
const added = ref(false)
const related = computed(() => products.filter((item) => item.id !== String(props.id)))
const authLink = computed(() => ({ name: 'auth', query: { redirect: route.fullPath } }))

watch(() => props.id, (productId) => {
  if (productById(productId)) recordProductEntry(productId)
}, { immediate: true })

if (!product.value) router.replace('/search')

function add() {
  if (!product.value || !leader.value) return
  addToCart(product.value.id, quantity.value)
  added.value = true
  window.setTimeout(() => { added.value = false }, 1400)
}

function buyNow() {
  add()
  router.push('/cart')
}
</script>

<template>
  <div v-if="product && leader" class="store-container page-space product-detail-page">
    <StoreBreadcrumb :items="[{ label: product.shortName, to: `/category/${product.category}` }, { label: product.name }]" />

    <section class="product-detail-main">
      <div class="product-gallery">
        <div class="product-main-image"><img :src="product.image" :alt="product.name" /><span><Snowflake :size="14" />{{ product.storage }}配送</span></div>
        <div class="product-thumb active"><img :src="product.image" alt="商品主图缩略图" /></div>
      </div>

      <div class="product-info-column">
        <span class="detail-deal-label">团长精选 · 冷链直送</span>
        <h1>{{ product.name }}</h1>
        <p class="detail-summary">{{ product.summary }}</p>
        <div class="detail-leader-panel">
          <img :src="leader.avatar" :alt="`${leader.name}团长头像`" />
          <div><span><strong>{{ leader.name }}团长</strong><BadgeCheck :size="16" /></span><small>{{ leader.title }} · {{ leader.area }}</small></div>
          <RouterLink :to="`/leaders/${leader.id}`">查看详情<ChevronRight :size="15" /></RouterLink>
        </div>
        <dl class="product-facts">
          <div><dt>规格</dt><dd>{{ product.spec }}</dd></div>
          <div><dt>温控</dt><dd>{{ product.storage }}冷链</dd></div>
          <div v-if="isAuthenticated"><dt>库存</dt><dd>现货 {{ product.stock }} 件</dd></div>
        </dl>
      </div>

      <aside class="buy-box">
        <template v-if="isAuthenticated">
          <div class="buy-price"><span>¥</span><strong>{{ product.price.toFixed(2) }}</strong></div>
        </template>
        <div v-else class="buy-gated-message"><LockKeyhole :size="20" /><strong>关注团长后查看专属价格</strong><span>登录后关注该团长，即可查看价格和购买商品</span></div>
        <div class="delivery-promise"><Truck :size="19" /><div><strong>{{ product.delivery }}</strong><span>配送至 上海市浦东新区</span></div></div>
        <template v-if="isAuthenticated">
          <div class="stock-status"><Check :size="17" />有货，冷链备货中</div>
          <label class="buy-quantity">数量<QuantityStepper v-model="quantity" :max="product.stock" /></label>
          <button class="btn btn-cart w-100" type="button" @click="add"><Check v-if="added" :size="18" /><ShoppingCart v-else :size="18" />{{ added ? '已加入购物车' : '加入购物车' }}</button>
          <button class="btn btn-buy w-100" type="button" @click="buyNow">立即购买</button>
        </template>
        <RouterLink v-else class="btn btn-buy w-100 gated-login-button" :to="authLink">登录并关注团长</RouterLink>
        <small class="buy-box-guarantee"><ShieldCheck :size="15" />平台交易保障 · 团长身份已认证</small>
      </aside>
    </section>

    <section class="detail-info-band">
      <div><PackageCheck :size="23" /><span><strong>下单即备货</strong><small>支付成功后立即进入履约流程</small></span></div>
      <div><Clock3 :size="23" /><span><strong>配送时效</strong><small>{{ product.delivery }}</small></span></div>
      <div><MapPin :size="23" /><span><strong>社区履约</strong><small>{{ leader.area }}</small></span></div>
      <div><Snowflake :size="23" /><span><strong>冷链到家</strong><small>温控方式：{{ product.storage }}</small></span></div>
    </section>

    <section class="product-description-section">
      <h2>商品详情</h2>
      <div class="description-grid"><div><h3>商品亮点</h3><p>{{ product.summary }}</p></div><div><h3>收货提示</h3><p>收到商品后请及时检查外包装及温度状态，并按照商品标注方式冷藏保存。</p></div><div><h3>配送说明</h3><p>订单支付成功后立即进入常规备货与冷链配送流程。</p></div></div>
    </section>

    <section class="home-section px-0">
      <div class="section-title-row"><div><h2>你可能还喜欢</h2></div></div>
      <div class="product-grid"><ProductCard v-for="item in related" :key="item.id" :product="item" /></div>
    </section>
  </div>
</template>

<style scoped>
.product-detail-main { display: grid; grid-template-columns: minmax(300px, .95fr) minmax(330px, 1fr) 300px; gap: 28px; padding: 24px; border: 1px solid var(--line); background: #fff; }
.product-gallery { display: grid; grid-template-columns: 62px minmax(0, 1fr); grid-template-rows: auto; gap: 10px; align-items: start; }
.product-main-image { position: relative; grid-column: 2; aspect-ratio: 1 / 1; overflow: hidden; background: #eef1ef; }
.product-main-image img { width: 100%; height: 100%; object-fit: cover; }
.product-main-image > span { position: absolute; left: 10px; bottom: 10px; display: inline-flex; align-items: center; gap: 5px; padding: 5px 8px; border-radius: 4px; background: rgba(255, 255, 255, .94); color: #2463a7; font-size: 10px; font-weight: 700; }
.product-thumb { grid-column: 1; grid-row: 1; aspect-ratio: 1 / 1; padding: 2px; border: 2px solid var(--brand); }
.product-thumb img { width: 100%; height: 100%; object-fit: cover; }
.product-info-column { min-width: 0; }
.detail-deal-label { display: inline-flex; margin-bottom: 9px; padding: 4px 7px; border-radius: 3px; background: #fce9e7; color: var(--danger); font-size: 10px; font-weight: 750; }
.product-info-column h1 { margin: 0; font-size: 25px; font-weight: 800; line-height: 1.35; }
.detail-summary { margin: 9px 0 18px; color: var(--muted); line-height: 1.65; }
.detail-leader-panel { display: grid; grid-template-columns: 48px minmax(0, 1fr) auto; gap: 10px; align-items: center; padding: 12px; border: 1px solid #cfe1d8; background: #f2f8f5; }
.detail-leader-panel > img { width: 48px; height: 48px; border-radius: 50%; object-fit: cover; }
.detail-leader-panel > div { min-width: 0; }
.detail-leader-panel > div > span { display: flex; align-items: center; gap: 4px; }
.detail-leader-panel svg { color: var(--brand); }
.detail-leader-panel small { display: block; margin-top: 3px; overflow: hidden; color: var(--muted); font-size: 9px; text-overflow: ellipsis; white-space: nowrap; }
.detail-leader-panel > a { display: inline-flex; align-items: center; color: var(--brand); font-size: 10px; font-weight: 700; text-decoration: none; }
.product-facts { margin: 15px 0 0; }
.product-facts > div { display: grid; grid-template-columns: 62px 1fr; padding: 10px 0; border-bottom: 1px solid var(--line); }
.product-facts dt { color: var(--muted); font-size: 10px; }
.product-facts dd { margin: 0; font-size: 11px; }
.buy-box { align-self: start; padding: 18px; border: 1px solid #cfd7d3; border-radius: 6px; box-shadow: 0 3px 10px rgba(23, 33, 29, .08); }
.buy-price { display: flex; align-items: baseline; color: var(--danger); }
.buy-price > span { font-size: 16px; }
.buy-price strong { font-size: 30px; }
.buy-price { margin-bottom: 15px; }
.buy-gated-message { display: flex; flex-direction: column; gap: 5px; margin-bottom: 15px; padding: 13px; border: 1px solid #cfe1d8; background: #f2f8f5; color: var(--brand); }
.buy-gated-message strong { color: var(--ink); font-size: 13px; }
.buy-gated-message span { color: var(--muted); font-size: 10px; line-height: 1.5; }
.gated-login-button { margin-top: 8px; }
.delivery-promise { display: flex; gap: 8px; margin-bottom: 12px; color: var(--brand); }
.delivery-promise div { display: flex; min-width: 0; flex-direction: column; }
.delivery-promise strong { color: var(--ink); font-size: 11px; }
.delivery-promise span { margin-top: 2px; color: var(--muted); font-size: 9px; }
.stock-status { display: flex; align-items: center; gap: 5px; margin-bottom: 14px; color: var(--brand); font-size: 11px; font-weight: 700; }
.buy-quantity { display: flex; align-items: center; justify-content: space-between; margin: 15px 0; color: var(--muted); font-size: 10px; }
.buy-box > .btn { margin-top: 8px; }
.buy-box-guarantee { display: flex; align-items: center; gap: 4px; margin-top: 13px; color: var(--muted); font-size: 9px; }
.detail-info-band { display: grid; grid-template-columns: repeat(4, 1fr); margin-top: 16px; border: 1px solid var(--line); background: #fff; }
.detail-info-band > div { display: flex; min-height: 78px; align-items: center; justify-content: center; gap: 9px; border-right: 1px solid var(--line); color: var(--brand); }
.detail-info-band > div:last-child { border-right: 0; }
.detail-info-band span { display: flex; flex-direction: column; }
.detail-info-band strong { color: var(--ink); font-size: 12px; }
.detail-info-band small { color: var(--muted); font-size: 9px; }
.product-description-section { margin-top: 16px; padding: 22px; border: 1px solid var(--line); background: #fff; }
.product-description-section > h2 { margin: 0 0 16px; font-size: 18px; }
.description-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 20px; }
.description-grid h3 { margin: 0 0 7px; font-size: 12px; }
.description-grid p { margin: 0; color: var(--muted); font-size: 10px; line-height: 1.7; }

@media (max-width: 1199.98px) {
  .product-detail-main { grid-template-columns: minmax(270px, .9fr) minmax(300px, 1fr); }
  .buy-box { grid-column: 2; }
  .product-gallery { grid-row: span 2; }
}

@media (max-width: 991.98px) {
  .product-detail-main { grid-template-columns: minmax(280px, .9fr) minmax(300px, 1fr); gap: 20px; }
  .buy-box { grid-column: 1 / -1; }
  .product-gallery { grid-row: auto; }
  .detail-info-band { grid-template-columns: repeat(2, 1fr); }
  .detail-info-band > div:nth-child(2) { border-right: 0; }
  .detail-info-band > div:nth-child(-n+2) { border-bottom: 1px solid var(--line); }
  .description-grid { grid-template-columns: 1fr; }
}

@media (max-width: 767.98px) {
  .product-detail-main { grid-template-columns: 1fr; gap: 18px; padding: 14px; }
  .product-gallery { grid-template-columns: 48px minmax(0, 1fr); }
  .product-info-column h1 { font-size: 21px; }
  .buy-box { grid-column: auto; }
  .detail-info-band { grid-template-columns: 1fr 1fr; }
  .detail-info-band > div { min-height: 68px; padding: 7px; }
}
</style>
