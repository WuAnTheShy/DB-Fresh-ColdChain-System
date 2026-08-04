<script setup>
import { CheckCircle2, Clock3, Plus, Snowflake } from '@lucide/vue'
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
          <CheckCircle2 v-if="added" :size="19" />
          <Plus v-else :size="20" />
        </button>
      </div>
    </div>
  </article>
</template>
