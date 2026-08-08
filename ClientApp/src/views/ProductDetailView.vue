<script setup>
import { BadgeCheck, Check, ChevronRight, Clock3, MapPin, PackageCheck, ShieldCheck, ShoppingCart, Snowflake, Truck } from '@lucide/vue'
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import ProductCard from '../components/ProductCard.vue'
import QuantityStepper from '../components/QuantityStepper.vue'
import StoreBreadcrumb from '../components/StoreBreadcrumb.vue'
import { useShop } from '../state/shop'

const props = defineProps({ id: { type: String, required: true } })
const route = useRoute()
const router = useRouter()
const { products, productById, leaderById, recordProductEntry, addToCart } = useShop()
const product = computed(() => productById(props.id))
const requestedLeaderId = Number(route.query.leader)
const activeLeaderId = ref(product.value?.leaderIds.includes(requestedLeaderId) ? requestedLeaderId : product.value?.leaderIds[0])
const leader = computed(() => leaderById(activeLeaderId.value))
const quantity = ref(1)
const added = ref(false)
const progress = computed(() => product.value ? Math.min(100, Math.round((product.value.sold / product.value.target) * 100)) : 0)
const related = computed(() => products.filter((item) => item.id !== String(props.id)))

watch(() => props.id, (productId) => {
  if (productById(productId)) recordProductEntry(productId)
}, { immediate: true })

if (!product.value) router.replace('/search')

function add() {
  if (!product.value || !leader.value) return
  addToCart(product.value.id, leader.value.id, quantity.value)
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
        <span class="detail-deal-label">团长带货 · 限时团购</span>
        <h1>{{ product.name }}</h1>
        <p class="detail-summary">{{ product.summary }}</p>
        <div class="detail-leader-panel">
          <img :src="leader.avatar" :alt="`${leader.name}团长头像`" />
          <div><span><strong>{{ leader.name }}团长</strong><BadgeCheck :size="16" /></span><small>{{ leader.title }} · {{ leader.area }}</small></div>
          <RouterLink :to="`/leaders/${leader.id}`">查看详情<ChevronRight :size="15" /></RouterLink>
        </div>
        <div v-if="product.leaderIds.length > 1" class="leader-choice">
          <span>选择带货团长</span>
          <button v-for="leaderId in product.leaderIds" :key="leaderId" type="button" :class="{ active: activeLeaderId === leaderId }" @click="activeLeaderId = leaderId">
            <img :src="leaderById(leaderId).avatar" alt="" />{{ leaderById(leaderId).name }}团长
          </button>
        </div>
        <dl class="product-facts">
          <div><dt>规格</dt><dd>{{ product.spec }}</dd></div>
          <div><dt>温控</dt><dd>{{ product.storage }}冷链</dd></div>
          <div><dt>库存</dt><dd>现货 {{ product.stock }} 件</dd></div>
        </dl>
      </div>

      <aside class="buy-box">
        <div class="buy-price"><span>¥</span><strong>{{ product.price.toFixed(2) }}</strong><del>¥{{ product.originalPrice.toFixed(2) }}</del></div>
        <div class="discount-note">团购直降 ¥{{ (product.originalPrice - product.price).toFixed(2) }}</div>
        <div class="delivery-promise"><Truck :size="19" /><div><strong>{{ product.delivery }}</strong><span>配送至 上海市浦东新区</span></div></div>
        <div class="stock-status"><Check :size="17" />有货，冷链备货中</div>
        <div class="group-status"><div><span>{{ product.cutoff }}</span><strong>{{ product.sold }} / {{ product.target }} 件</strong></div><div class="progress"><div class="progress-bar" :style="{ width: `${progress}%` }"></div></div></div>
        <label class="buy-quantity">数量<QuantityStepper v-model="quantity" :max="product.stock" /></label>
        <button class="btn btn-cart w-100" type="button" @click="add"><Check v-if="added" :size="18" /><ShoppingCart v-else :size="18" />{{ added ? '已加入购物车' : '加入购物车' }}</button>
        <button class="btn btn-buy w-100" type="button" @click="buyNow">立即参团</button>
        <small class="buy-box-guarantee"><ShieldCheck :size="15" />平台交易保障 · 团长身份已认证</small>
      </aside>
    </section>

    <section class="detail-info-band">
      <div><PackageCheck :size="23" /><span><strong>团购说明</strong><small>达到成团条件后统一备货</small></span></div>
      <div><Clock3 :size="23" /><span><strong>截团透明</strong><small>{{ product.cutoff }}</small></span></div>
      <div><MapPin :size="23" /><span><strong>社区履约</strong><small>{{ leader.area }}</small></span></div>
      <div><Snowflake :size="23" /><span><strong>冷链到家</strong><small>温控方式：{{ product.storage }}</small></span></div>
    </section>

    <section class="product-description-section">
      <h2>商品详情</h2>
      <div class="description-grid"><div><h3>商品亮点</h3><p>{{ product.summary }}</p></div><div><h3>收货提示</h3><p>收到商品后请及时检查外包装及温度状态，并按照商品标注方式冷藏保存。</p></div><div><h3>团购进度</h3><p>当前已团 {{ product.sold }} 件，目标 {{ product.target }} 件。进度变化以页面实时展示为准。</p></div></div>
    </section>

    <section class="home-section px-0">
      <div class="section-title-row"><div><h2>你可能还喜欢</h2></div></div>
      <div class="product-grid"><ProductCard v-for="item in related" :key="item.id" :product="item" /></div>
    </section>
  </div>
</template>
