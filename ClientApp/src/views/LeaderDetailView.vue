<script setup>
import { BadgeCheck, Heart, MapPin, PackageCheck, UsersRound } from '@lucide/vue'
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import ProductCard from '../components/ProductCard.vue'
import StoreBreadcrumb from '../components/StoreBreadcrumb.vue'
import { useShop } from '../state/shop'
import { useCustomerContext } from '../state/customer'

const props = defineProps({ id: { type: String, required: true } })
const route = useRoute()
const router = useRouter()
const { leaderById, products, isLeaderFollowed, toggleLeaderFollow } = useShop()
const { isAuthenticated } = useCustomerContext()
const leader = computed(() => leaderById(props.id))
const leaderProducts = computed(() => products.filter((product) => product.leaderId === Number(props.id)))
const followed = computed(() => isLeaderFollowed(props.id))

if (!leader.value) router.replace('/search')

function handleFollow() {
  if (!isAuthenticated.value) {
    router.push({ name: 'auth', query: { redirect: route.fullPath } })
    return
  }
  toggleLeaderFollow(leader.value.id)
}
</script>

<template>
  <div v-if="leader" class="leader-detail-page">
    <div class="store-container page-space pb-0">
      <StoreBreadcrumb :items="[{ label: '全部商品', to: '/search' }, { label: `${leader.name}团长` }]" />
    </div>

    <section class="leader-profile-band">
      <img class="leader-cover" :src="leader.cover" alt="" />
      <div class="leader-cover-shade"></div>
      <div class="store-container leader-profile-content">
        <img class="leader-profile-avatar" :src="leader.avatar" :alt="`${leader.name}团长头像`" />
        <div class="leader-profile-copy">
          <span class="verified-label"><BadgeCheck :size="16" />平台认证团长</span>
          <h1>{{ leader.name }}团长</h1>
          <p>{{ leader.description }}</p>
          <span class="leader-area"><MapPin :size="16" />{{ leader.area }}</span>
        </div>
        <button class="btn leader-follow-button" :class="followed ? 'btn-light' : 'btn-buy'" type="button" @click="handleFollow">
          <Heart :size="17" :fill="followed ? 'currentColor' : 'none'" />{{ !isAuthenticated ? '登录后关注' : followed ? '取消关注' : '关注团长' }}
        </button>
      </div>
    </section>

    <div class="store-container leader-stat-row">
      <div><PackageCheck :size="20" /><span><strong>{{ leaderProducts.length }}</strong><small>正在带货</small></span></div>
      <div><UsersRound :size="20" /><span><strong>{{ leader.following + (followed ? 1 : 0) }}</strong><small>社区关注</small></span></div>
      <div><BadgeCheck :size="20" /><span><strong>已认证</strong><small>平台团长资质</small></span></div>
    </div>

    <div class="store-container home-section">
      <div class="section-title-row"><div><h2>{{ leader.name }}团长正在带货</h2></div></div>
      <div class="product-grid">
        <ProductCard v-for="product in leaderProducts" :key="product.id" :product="product" />
      </div>
    </div>
  </div>
</template>

<style scoped>
.leader-profile-band { position: relative; min-height: 300px; overflow: hidden; background: #17211d; }
.leader-cover { position: absolute; inset: 0; width: 100%; height: 100%; object-fit: cover; opacity: .45; }
.leader-cover-shade { position: absolute; inset: 0; background: rgba(18, 28, 23, .62); }
.leader-profile-content { position: relative; z-index: 1; display: grid; min-height: 300px; grid-template-columns: 126px minmax(0, 1fr) auto; gap: 24px; align-items: center; color: #fff; }
.leader-profile-avatar { width: 126px; height: 126px; border: 4px solid #fff; border-radius: 50%; object-fit: cover; }
.leader-profile-copy { min-width: 0; }
.verified-label { display: inline-flex; align-items: center; gap: 5px; margin-bottom: 9px; color: #f2c45d; font-size: 11px; font-weight: 750; }
.leader-profile-copy h1 { margin: 0 0 9px; font-size: 32px; font-weight: 800; }
.leader-profile-copy p { max-width: 650px; margin: 0 0 13px; color: #e0e7e3; font-size: 13px; line-height: 1.65; }
.leader-area { display: inline-flex; align-items: center; gap: 5px; color: #c9d4cf; font-size: 11px; }
.leader-follow-button { min-width: 120px; }
.leader-stat-row { display: grid; grid-template-columns: repeat(3, 1fr); border: 1px solid var(--line); border-top: 0; background: #fff; }
.leader-stat-row > div { display: flex; min-height: 76px; align-items: center; justify-content: center; gap: 9px; border-right: 1px solid var(--line); color: var(--brand); }
.leader-stat-row > div:last-child { border-right: 0; }
.leader-stat-row span { display: flex; flex-direction: column; }
.leader-stat-row strong { color: var(--ink); font-size: 14px; }
.leader-stat-row small { color: var(--muted); font-size: 9px; }

@media (max-width: 767.98px) {
  .leader-profile-content { min-height: 330px; grid-template-columns: 82px 1fr; gap: 15px; padding: 25px 0; }
  .leader-profile-avatar { width: 82px; height: 82px; }
  .leader-profile-copy h1 { font-size: 25px; }
  .leader-profile-copy p { display: -webkit-box; overflow: hidden; -webkit-box-orient: vertical; -webkit-line-clamp: 3; }
  .leader-follow-button { grid-column: 1 / -1; justify-self: start; }
  .leader-stat-row > div { padding: 8px; text-align: center; }
  .leader-stat-row > div > svg { display: none; }
}
</style>
