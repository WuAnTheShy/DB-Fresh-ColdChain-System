<script setup>
import { Heart, UsersRound } from '@lucide/vue'
import { computed } from 'vue'
import FollowedLeadersBar from '../components/FollowedLeadersBar.vue'
import ProductCard from '../components/ProductCard.vue'
import { useShop } from '../state/shop'

const { followedLeaderIds, followingError, followingLoading, leaderById, products } = useShop()

const followedLeaders = computed(() => followedLeaderIds.value.map(leaderById).filter(Boolean))
const publishedTimestamp = (value) => {
  const timestamp = new Date(value).getTime()
  return Number.isFinite(timestamp) ? timestamp : 0
}
const feedItems = computed(() => followedLeaders.value
  .flatMap((leader) => products
    .filter((product) => product.leaderId === leader.id)
    .map((product) => ({ leader, product, publishedAt: product.publishedAt })))
  .sort((left, right) => publishedTimestamp(right.publishedAt) - publishedTimestamp(left.publishedAt)))

function formatFeedTime(value) {
  const date = new Date(value)
  if (!Number.isFinite(date.getTime())) return '发布时间未知'
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
    <div class="listing-header">
      <div><span class="title-icon">
          <Heart :size="23" />
        </span><span>
          <h1>我的关注</h1>
        </span></div>
      <span v-if="followedLeaders.length"><UsersRound :size="16" />共 {{ followedLeaders.length }} 位团长</span>
    </div>

    <div v-if="followingError" class="alert alert-danger" role="alert">{{ followingError }}</div>
    <div v-else-if="followingLoading" class="store-loading"><span
        class="spinner-border text-success"></span><span>正在加载关注列表…</span></div>
    <div v-else-if="followedLeaders.length" class="following-layout">
      <aside class="following-sidebar">
        <FollowedLeadersBar :leaders="followedLeaders" />
      </aside>

      <section v-if="feedItems.length" class="following-feed following-main" aria-label="关注商品时间流">
        <p class="following-feed-title">最新带货商品</p>
        <article v-for="item in feedItems" :key="`${item.leader.id}-${item.product.id}`" class="following-feed-item">
          <div class="following-feed-time"><time :datetime="item.publishedAt">{{ formatFeedTime(item.publishedAt)
              }}</time></div>
          <div class="following-feed-card">
            <ProductCard :product="item.product" />
          </div>
        </article>
        <p class="list-end-tip">到底了~</p>
      </section>

      <div v-else class="store-empty following-empty following-main">
        <Heart :size="42" />
        <strong>关注团长暂无在团商品</strong>
        <span>关注团长暂时没有上架商品，先去逛逛其他好物吧</span>
        <RouterLink class="btn btn-buy" to="/search">浏览全部商品</RouterLink>
      </div>
    </div>

    <div v-else class="store-empty following-empty">
      <Heart :size="42" />
      <strong>还没有关注团长</strong>
      <span>从商品卡片点击团长头像，进入详情后即可关注</span>
      <RouterLink class="btn btn-buy" to="/search">浏览全部商品</RouterLink>
    </div>
  </div>
</template>

<style scoped>
.following-page {
  padding-bottom: 20px;
}

.following-layout {
  display: grid;
  grid-template-columns: 210px minmax(0, 1fr);
  gap: 16px;
  align-items: start;
}

.following-sidebar,
.following-main {
  min-width: 0;
}

.following-feed {
  position: relative;
}

.following-feed-title {
  display: flex;
  min-height: 44px;
  align-items: center;
  margin: 0 0 14px;
  padding: 0 14px;
  border: 1px solid var(--line);
  border-radius: 8px;
  background: #fff;
  font-size: 12px;
  font-weight: 650;
}

.following-feed-item {
  display: grid;
  grid-template-columns: 118px minmax(0, 1fr);
  gap: 24px;
  align-items: start;
  max-width: 780px;
  padding-bottom: 24px;
  margin: 0 auto;
}

.following-feed-time {
  position: relative;
  min-height: 100%;
  padding: 17px 23px 0 0;
  color: #737a77;
  font-size: 11px;
  text-align: right;
  white-space: nowrap;
}

.following-feed-time::before {
  position: absolute;
  top: 0;
  right: 5px;
  bottom: -24px;
  width: 1px;
  background: #d8dfdc;
  content: "";
}

.following-feed-time::after {
  position: absolute;
  top: 20px;
  right: 0;
  width: 11px;
  height: 11px;
  border: 3px solid #fff;
  border-radius: 50%;
  background: var(--brand);
  box-shadow: 0 0 0 1px #a9c8be;
  content: "";
}

.following-feed-item:last-child .following-feed-time::before {
  bottom: calc(100% - 26px);
}

.following-feed-card {
  min-width: 0;
}

.following-feed-card .social-product-card {
  width: 100%;
}

.following-empty {
  border-radius: 12px;
}

.following-empty>svg {
  color: var(--brand);
}

@media (max-width: 991.98px) {
  .following-layout {
    grid-template-columns: 185px minmax(0, 1fr);
  }
}

@media (max-width: 767.98px) {
  .following-layout {
    grid-template-columns: 1fr;
    gap: 12px;
  }

  .following-feed {
    max-width: none;
  }

  .following-feed-title {
    min-height: 0;
    margin-bottom: 10px;
    padding: 10px 12px;
  }

  .following-feed-item {
    display: block;
    padding-bottom: 18px;
  }

  .following-feed-time {
    min-height: 0;
    padding: 0 0 8px 16px;
    text-align: left;
  }

  .following-feed-time::before {
    display: none;
  }

  .following-feed-time::after {
    top: 3px;
    left: 0;
    width: 8px;
    height: 8px;
    border-width: 2px;
  }
}

</style>
