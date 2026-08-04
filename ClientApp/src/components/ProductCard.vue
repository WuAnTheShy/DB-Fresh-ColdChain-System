<script setup>
import { Flame } from '@lucide/vue'
import { computed } from 'vue'
import { useShop } from '../state/shop'

const props = defineProps({
  product: { type: Object, required: true },
  leaderId: { type: Number, default: null },
})

const { categories, leaderById, productRushCount } = useShop()
const activeLeader = computed(() => leaderById(props.leaderId ?? props.product.leaderIds[0]))
const category = computed(() => categories.find((item) => item.slug === props.product.category))
const rushCount = computed(() => productRushCount(props.product.id))
const productLink = computed(() => `/products/${props.product.id}?leader=${activeLeader.value?.id ?? ''}`)

function displayPrice(value) {
  return Number(value).toFixed(2).replace(/\.00$/, '')
}
</script>

<template>
  <article class="product-card social-product-card">
    <RouterLink class="product-card-entry" :to="productLink" :aria-label="`查看${product.name}`">
      <header v-if="activeLeader" class="product-card-leader">
        <img :src="activeLeader.avatar" :alt="`${activeLeader.name}团长头像`" />
        <span>
          <strong>{{ activeLeader.name }}</strong>
          <small>{{ activeLeader.title }} · 跟我一起团</small>
        </span>
      </header>

      <div class="product-card-divider"></div>

      <div class="product-card-body social-product-card-body">
        <p class="product-card-description">
          <strong>{{ product.name }}</strong>
          {{ product.summary }}
        </p>

        <div class="product-card-meta">
          <span class="rush-count"><Flame :size="15" fill="currentColor" />{{ rushCount }}人在抢</span>
          <span class="product-published">今日更新</span>
        </div>

        <div class="social-product-price">
          <span>¥</span><strong>{{ displayPrice(product.price) }}</strong><small>–{{ displayPrice(product.originalPrice) }}</small>
        </div>

        <div class="product-card-media">
          <img :src="product.image" :alt="product.name" loading="lazy" />
          <img :src="category?.image || product.image" :alt="`${product.shortName}货架陈列`" loading="lazy" />
        </div>

        <div class="product-card-group-status">
          <span><strong>{{ product.sold }}人跟团</strong> 正在进行</span>
          <span>{{ product.cutoff }}</span>
        </div>
      </div>
    </RouterLink>
  </article>
</template>
