<script setup>
import { Heart, UsersRound } from '@lucide/vue'
import { computed } from 'vue'
import ProductCard from '../components/ProductCard.vue'
import StoreBreadcrumb from '../components/StoreBreadcrumb.vue'
import { useShop } from '../state/shop'

const { followedLeaderIds, leaderById, products } = useShop()

const followedLeaders = computed(() => followedLeaderIds.value.map(leaderById).filter(Boolean))
const feedItems = computed(() => followedLeaders.value
  .flatMap((leader) => products
    .filter((product) => product.leaderIds.includes(leader.id))
    .map((product) => ({ leader, product, publishedAt: product.publishedAt })))
  .sort((left, right) => new Date(right.publishedAt) - new Date(left.publishedAt)))

function formatFeedTime(value) {
  const date = new Date(value)
  const now = new Date()
  const time = new Intl.DateTimeFormat('zh-CN', { hour: '2-digit', minute: '2-digit', hour12: false }).format(date)
  if (date.toDateString() === now.toDateString()) return `今天 ${time}`
  const yesterday = new Date(now)
  yesterday.setDate(now.getDate() - 1)
  if (date.toDateString() === yesterday.toDateString()) return `昨天 ${time}`
  return new Intl.DateTimeFormat('zh-CN', { month: 'numeric', day: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false }).format(date)
}
</script>

<template>
  <div class="store-container page-space following-page">
    <StoreBreadcrumb :items="[{ label: '我的关注' }]" />
    <div class="account-page-header following-page-header">
      <div><span class="title-icon"><Heart :size="23" /></span><span><h1>我的关注</h1><p>按发布时间查看关注团长的最新带货商品</p></span></div>
      <span v-if="followedLeaders.length"><UsersRound :size="16" />已关注 {{ followedLeaders.length }} 位团长</span>
    </div>

    <section v-if="feedItems.length" class="following-feed" aria-label="关注商品时间流">
      <article v-for="item in feedItems" :key="`${item.leader.id}-${item.product.id}`" class="following-feed-item">
        <div class="following-feed-time"><time :datetime="item.publishedAt">{{ formatFeedTime(item.publishedAt) }}</time></div>
        <div class="following-feed-card"><ProductCard :product="item.product" :leader-id="item.leader.id" /></div>
      </article>
    </section>

    <div v-else class="store-empty following-empty">
      <Heart :size="42" />
      <strong>还没有关注团长</strong>
      <span>从商品卡片点击团长头像，进入详情后即可关注</span>
      <RouterLink class="btn btn-buy" to="/deals">浏览今日特价</RouterLink>
    </div>
  </div>
</template>
