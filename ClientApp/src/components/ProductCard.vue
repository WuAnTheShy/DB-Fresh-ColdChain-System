<script setup>
import { CheckCircle2, Clock3, ShoppingCart, Snowflake, Star } from '@lucide/vue'
import { computed, ref } from 'vue'
import { useShop } from '../state/shop'

const props = defineProps({
  product: { type: Object, required: true },
  leaderId: { type: Number, default: null },
})

const { leaderById, addToCart } = useShop()
const added = ref(false)
const activeLeader = computed(() => leaderById(props.leaderId ?? props.product.leaderIds[0]))
const progress = computed(() => Math.min(100, Math.round((props.product.sold / props.product.target) * 100)))
const rating = computed(() => [4.8, 4.7, 4.9][(props.product.id - 1) % 3])

function add(event) {
  event.preventDefault()
  event.stopPropagation()
  if (!activeLeader.value) return
  addToCart(props.product.id, activeLeader.value.id)
  added.value = true
  window.setTimeout(() => { added.value = false }, 1200)
}
</script>

<template>
  <article class="product-card">
    <RouterLink class="product-image-link" :to="`/products/${product.id}?leader=${activeLeader?.id}`">
      <img :src="product.image" :alt="product.name" loading="lazy" />
      <span class="temperature-badge"><Snowflake :size="12" />{{ product.storage }}</span>
      <span class="deal-badge">团购价</span>
    </RouterLink>
    <div class="product-card-body">
      <RouterLink class="product-title" :to="`/products/${product.id}?leader=${activeLeader?.id}`">{{ product.name }}</RouterLink>
      <span class="product-spec">{{ product.spec }}</span>
      <div class="product-rating" :aria-label="`评分 ${rating} 分`">
        <span><Star v-for="index in 5" :key="index" :size="13" fill="currentColor" /></span>
        <span class="rating-count">{{ rating }}（{{ product.sold }}）</span>
      </div>
      <RouterLink v-if="activeLeader" class="leader-byline" :to="`/leaders/${activeLeader.id}`">
        <img :src="activeLeader.avatar" alt="" />
        <span><strong>{{ activeLeader.name }}团长</strong> 带货</span>
        <CheckCircle2 :size="13" />
      </RouterLink>
      <div class="group-progress-line">
        <span><Clock3 :size="13" />{{ product.cutoff }}</span>
        <strong>已团 {{ product.sold }} 件</strong>
      </div>
      <div class="progress group-progress" role="progressbar" :aria-valuenow="progress" aria-valuemin="0" aria-valuemax="100">
        <div class="progress-bar" :style="{ width: `${progress}%` }"></div>
      </div>
      <div class="product-card-footer">
        <div class="price-block"><span>¥</span><strong>{{ product.price.toFixed(2) }}</strong><del>¥{{ product.originalPrice.toFixed(2) }}</del></div>
        <button class="quick-add" type="button" :title="added ? '已加入购物车' : '加入购物车'" @click="add">
          <CheckCircle2 v-if="added" :size="17" />
          <ShoppingCart v-else :size="17" />
          <span>{{ added ? '已加入' : '加入购物车' }}</span>
        </button>
      </div>
      <small class="prime-delivery">鲜邻会员免运费 · 最快明日送达</small>
    </div>
  </article>
</template>
