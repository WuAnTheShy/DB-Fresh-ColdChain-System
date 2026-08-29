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
    <RouterLink v-if="activeLeader" class="product-card-leader" :to="`/leaders/${activeLeader.id}`" :aria-label="`查看${activeLeader.name}团长详情`">
      <img :src="activeLeader.avatar" :alt="`${activeLeader.name}团长头像`" />
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
          <span class="rush-count"><Flame :size="15" fill="currentColor" />{{ rushCount }}人在抢</span>
          <span class="product-published">今日更新</span>
        </div>

        <div class="social-product-price">
          <span>¥</span><strong>{{ displayPrice(product.price) }}</strong><small>优惠后</small>
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

<style scoped>
.product-card { min-width: 0; overflow: hidden; border: 1px solid #eaeded; border-radius: 4px; background: #fff; transition: border-color .14s ease, box-shadow .14s ease; }
.product-card:hover { border-color: #bbbfbf; box-shadow: 0 2px 6px rgba(15, 17, 17, .14); }
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
.product-card-leader > img {
  width: 46px;
  height: 46px;
  flex: 0 0 46px;
  border-radius: 11px;
  object-fit: cover;
}
.product-card-leader > span {
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
.product-card-divider { height: 1px; background: #ededed; }
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
.product-card-description strong { font-weight: 760; }
.product-card-meta {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  margin-top: 10px;
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
.product-published { color: #9a9e9c; font-size: 11px; white-space: nowrap; }
.social-product-price {
  display: flex;
  align-items: flex-end;
  margin: 11px 0 13px;
  color: var(--brand);
  line-height: 1;
}
.social-product-price > span { margin-right: 3px; font-size: 18px; font-weight: 500; transform: translateY(-4px); }
.social-product-price strong { font-size: 34px; font-weight: 700; letter-spacing: -1px; }
.social-product-price small { margin-left: 7px; font-size: 18px; font-weight: 500; letter-spacing: 0; transform: translateY(-5.5px); }
.product-card-media {
  display: grid;
  overflow: hidden;
  aspect-ratio: 16 / 7.2;
  grid-template-columns: 1.45fr 1fr;
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
.social-product-card:hover .product-card-media img { transform: scale(1.025); }
.product-card-group-status {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  margin-top: 12px;
  color: #979c99;
  font-size: 11px;
}
.product-card-group-status span { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.product-card-group-status strong { color: #25a96b; font-size: 13px; font-weight: 700; }

@media (max-width: 767.98px) {
  .product-card-leader { padding: 13px 14px; }
  .product-card-leader > img { width: 42px; height: 42px; flex-basis: 42px; }
  .social-product-card .social-product-card-body { padding: 13px 14px; }
  .product-card-description { min-height: 45px; font-size: 14px; }
  .product-card-media { aspect-ratio: 16 / 7.6; }
}
</style>
