<script setup>
import { Apple, Beef, Fish, Leaf, Milk, ShoppingBasket } from '@lucide/vue'
import { computed } from 'vue'
import { useShop } from '../state/shop'
import { useCustomerContext } from '../state/customer'

const props = defineProps({
  product: { type: Object, required: true },
})

const { leaderById, isLeaderFollowed } = useShop()
const { isAuthenticated } = useCustomerContext()
const activeLeader = computed(() => leaderById(props.product.leaderId))
const cardImages = computed(() => {
  const images = Array.isArray(props.product.images) ? props.product.images.filter(Boolean) : []
  return [...new Set(images.length ? images : [props.product.image].filter(Boolean))].slice(0, 2)
})

// 品类标识：不同图标 + 颜色 + 背景填充圆角
const categoryStyles = [
  { pattern: /果|fruit/i, icon: Apple, color: '#e2574c', bg: '#fdeceb' },
  { pattern: /菜|豆|vegetable/i, icon: Leaf, color: '#2e9e5b', bg: '#e9f7ef' },
  { pattern: /肉|禽|蛋|meat|egg/i, icon: Beef, color: '#c2571a', bg: '#fbeee6' },
  { pattern: /海|水产|fish|seafood/i, icon: Fish, color: '#2463a7', bg: '#e8f0fb' },
  { pattern: /乳|奶|烘焙|dairy|bakery/i, icon: Milk, color: '#b8860b', bg: '#fdf6e8' },
]
const defaultCategoryStyle = { icon: ShoppingBasket, color: '#6b7280', bg: '#f1f2f2' }
const categoryStyle = computed(() => {
  const name = String(props.product.category ?? '')
  return categoryStyles.find((item) => item.pattern.test(name)) ?? defaultCategoryStyle
})

const productLink = computed(() => `/products/${props.product.id}`)
const canViewPrice = computed(() => isAuthenticated.value && isLeaderFollowed(props.product.leaderId))

function displayPrice(value) {
  return Number(value).toFixed(2).replace(/\.00$/, '')
}

function handleImageError(event, index) {
  const image = event.target
  if (index === 0 && props.product.fallbackImage && image.dataset.fallbackApplied !== 'true') {
    image.dataset.fallbackApplied = 'true'
    image.classList.add('fallback-photo-tint')
    image.src = props.product.fallbackImage
    return
  }
  image.style.display = 'none'
}
</script>

<template>
  <article class="product-card social-product-card">
    <RouterLink v-if="activeLeader" class="product-card-leader" :to="`/leaders/${activeLeader.id}`"
      :aria-label="`查看${activeLeader.name}详情`">
      <img :src="activeLeader.avatar" :alt="`${activeLeader.name}头像`" />
      <span><strong>{{ activeLeader.name }}</strong></span>
    </RouterLink>

    <div class="product-card-divider"></div>

    <RouterLink class="product-card-entry" :to="productLink" :aria-label="`查看${product.name}`">
      <div class="product-card-body social-product-card-body">
        <p class="product-card-description">
          <strong>{{ product.name }}</strong>
          {{ product.summary }}
        </p>

        <div class="product-card-meta">
          <span class="product-category-badge" :style="{ color: categoryStyle.color, background: categoryStyle.bg }">
            <component :is="categoryStyle.icon" :size="13" />
            {{ product.category }}
          </span>
          <span :class="`product-storage storage-type-${(product.storageType || 'CHILLED').toLowerCase()}`">{{ product.storage }}</span>
        </div>

        <div v-if="canViewPrice" class="social-product-price">
          <span>¥</span><strong>{{ displayPrice(product.price) }}</strong>
        </div>
        <div v-else class="social-product-price-gated">关注团长后查看专属价格</div>

        <div class="product-card-media">
          <img v-for="(image, index) in cardImages" :key="image" :src="image"
            :class="{ 'fallback-photo-tint': image === product.fallbackImage }"
            :alt="index === 0 ? product.name : `${product.name}商品图${index + 1}`" loading="lazy"
            @error="handleImageError($event, index)" />
        </div>

        <div class="product-card-group-status">
          <span><strong>当前在团</strong></span>
          <span>库存 {{ product.stock }} 件</span>
        </div>
      </div>
    </RouterLink>
  </article>
</template>

<style scoped>
.product-card {
  min-width: 0;
  overflow: hidden;
  border: 1px solid #eaeded;
  border-radius: 4px;
  background: #fff;
  transition: border-color .14s ease, box-shadow .14s ease, transform .14s ease-in-out;
}

.product-card:hover {
  border-color: #bbbfbf;
  box-shadow: 0 2px 6px rgba(15, 17, 17, .5);
  transform: translateY(-2px);
}

.social-product-card {
  display: flex;
  flex-direction: column;
  overflow: hidden;
  border: 1px solid #e7e7e7;
  border-radius: 14px;
  background: #fff;
  box-shadow: 0 4px 18px rgba(15, 17, 17, .06);
}

.social-product-card:hover {
  border-color: #d5d5d5;
  box-shadow: 0 8px 24px rgba(15, 17, 17, .1);
}

.product-card-entry {
  display: flex;
  flex: 1;
  flex-direction: column;
  height: auto;
  color: inherit;
  text-decoration: none;
}

.product-card-leader {
  display: flex;
  min-width: 0;
  align-items: center;
  gap: 12px;
  padding: 15px 16px;
  color: inherit;
  text-decoration: none;
}

.product-card-leader>img {
  width: 46px;
  height: 46px;
  flex: 0 0 46px;
  border-radius: 11px;
  object-fit: cover;
}

.product-card-leader>span {
  display: flex;
  min-width: 0;
  flex-direction: column;
}

.product-card-leader strong {
  overflow: hidden;
  color: #1f2321;
  font-size: 15px;
  font-weight: 650;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.product-card-leader small {
  margin-top: 3px;
  overflow: hidden;
  color: #777d7a;
  font-size: 11px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.product-card-divider {
  height: 1px;
  background: #ededed;
}

.social-product-card .social-product-card-body {
  min-height: 0;
  padding: 15px 16px 14px;
}

.product-card-description {
  display: -webkit-box;
  min-height: 48px;
  margin: 0;
  overflow: hidden;
  color: #242826;
  font-size: 15px;
  line-height: 1.6;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
}

.product-card-description strong {
  font-weight: 760;
}

.product-card-meta {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  margin-top: 10px;
}

.product-category-badge {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 3px 8px;
  border-radius: 999px;
  font-size: 11px;
  font-weight: 700;
  line-height: 1;
  white-space: nowrap;
}

/* 温控颜色区分：冷藏=蓝 / 冷冻=冰蓝 / 常温=暖橙 */
.product-storage {
  font-weight: 700;
}

.storage-type-chilled {
  color: #2463a7;
}

.storage-type-frozen {
  color: #0e7490;
}

.storage-type-ambient {
  color: #c2571a;
}

.rush-count {
  display: inline-flex;
  min-height: 26px;
  align-items: center;
  gap: 4px;
  padding: 2px 8px;
  border: 1px solid rgba(242, 102, 69, .42);
  border-radius: 4px;
  color: #f26645;
  font-size: 12px;
  font-weight: 600;
  line-height: 1;
}

.product-published {
  color: #9a9e9c;
  font-size: 11px;
  white-space: nowrap;
}

.social-product-price {
  display: flex;
  align-items: flex-end;
  margin: 11px 0 13px;
  color: var(--brand);
  line-height: 1;
}

.social-product-price>span {
  margin-right: 3px;
  font-size: 18px;
  font-weight: 500;
  transform: translateY(-4px);
}

.social-product-price strong {
  font-size: 34px;
  font-weight: 700;
  letter-spacing: -1px;
}

.social-product-price-gated {
  display: flex;
  min-height: 45px;
  align-items: center;
  margin: 11px 0 13px;
  color: var(--brand);
  font-size: 13px;
  font-weight: 700;
}

.product-card-media {
  display: grid;
  overflow: hidden;
  aspect-ratio: 16 / 7.2;
  grid-auto-columns: minmax(0, 1fr);
  grid-auto-flow: column;
  gap: 6px;
  border-radius: 7px;
  background: #f1f2f2;
}

.product-card-media img {
  width: 100%;
  height: 100%;
  min-width: 0;
  object-fit: cover;
  transition: transform .2s ease;
}

.social-product-card:hover .product-card-media img {
  transform: scale(1.025);
}

.product-card-group-status {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  margin-top: 12px;
  color: #979c99;
  font-size: 11px;
}

.product-card-group-status span {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.product-card-group-status strong {
  color: #25a96b;
  font-size: 13px;
  font-weight: 700;
}

@media (max-width: 767.98px) {
  .product-card-leader {
    padding: 13px 14px;
  }

  .product-card-leader>img {
    width: 42px;
    height: 42px;
    flex-basis: 42px;
  }

  .social-product-card .social-product-card-body {
    padding: 13px 14px;
  }

  .product-card-description {
    min-height: 45px;
    font-size: 14px;
  }

  .product-card-media {
    aspect-ratio: 16 / 7.6;
  }
}
</style>
