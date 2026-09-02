<script setup>
import { BadgeCheck, Heart, MapPin, PackageCheck, UsersRound } from '@lucide/vue'
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import ProductCard from '../components/ProductCard.vue'
import { useShop } from '../state/shop'
import { useCustomerContext } from '../state/customer'

const props = defineProps({ id: { type: String, required: true } })
const route = useRoute()
const router = useRouter()
const { cart, leaderById, products, leadersLoading, leadersError, loadLeaders, loadCatalog, isLeaderFollowed, setLeaderFollowed } = useShop()
const { customerId, isAuthenticated } = useCustomerContext()
const leader = computed(() => leaderById(props.id))
const leaderProducts = computed(() => products.filter((product) => product.leaderId === String(props.id)))
const followed = computed(() => isLeaderFollowed(props.id))
const followPending = ref(false)
const followError = ref('')

onMounted(async () => {
  try {
    await Promise.all([loadLeaders(), loadCatalog()])
    if (!leader.value) await router.replace('/search')
  } catch {
    // 页面保留加载失败状态，允许消费者重试。
  }
})

async function handleFollow() {
  if (!isAuthenticated.value) {
    router.push({ name: 'auth', query: { redirect: route.fullPath } })
    return
  }
  followError.value = ''
  if (followed.value && cart.some((item) => item.leaderId === leader.value.id)) {
    followError.value = '购物车中仍有该团长的商品，请先清空相关商品后再取消关注'
    return
  }

  followPending.value = true
  try {
    await setLeaderFollowed(customerId.value, leader.value.id, !followed.value)
  } catch (error) {
    followError.value = error.message
  } finally {
    followPending.value = false
  }
}
</script>

<template>
  <div v-if="leader" class="leader-detail-page">
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
        <button class="btn leader-follow-button" :class="followed ? 'btn-light' : 'btn-buy'" type="button" :disabled="followPending" @click="handleFollow">
          <Heart :size="17" :fill="followed ? 'currentColor' : 'none'" />{{ !isAuthenticated ? '登录后关注' : followed ? '取消关注' : '关注团长' }}
        </button>
      </div>
    </section>

    <div v-if="followError" class="store-container leader-follow-alert alert alert-warning" role="alert">{{ followError }}</div>

    <div class="store-container leader-stat-row">
      <div><PackageCheck :size="20" /><span><strong>{{ leaderProducts.length }}</strong><small>正在带货</small></span></div>
      <div><UsersRound :size="20" /><span><strong>{{ followed ? '已关注' : '未关注' }}</strong><small>当前关注状态</small></span></div>
      <div><BadgeCheck :size="20" /><span><strong>已认证</strong><small>平台团长资质</small></span></div>
    </div>

    <div class="store-container home-section">
      <div class="section-title-row"><div><h2>{{ leader.name }}团长正在带货</h2></div></div>
      <div class="product-grid">
        <ProductCard v-for="product in leaderProducts" :key="product.id" :product="product" />
      </div>
    </div>
  </div>
  <div v-else-if="leadersLoading" class="store-container page-space leader-detail-state" role="status">正在读取团长信息…</div>
  <div v-else-if="leadersError" class="store-container page-space leader-detail-state" role="alert">
    <span>{{ leadersError }}</span>
    <button class="btn btn-outline-secondary" type="button" @click="loadLeaders(true)">重新加载</button>
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
.leader-follow-alert { margin-top: 14px; margin-bottom: 0; font-size: 12px; }
.leader-stat-row { display: grid; grid-template-columns: repeat(3, 1fr); border: 1px solid var(--line); border-top: 0; background: #fff; }
.leader-stat-row > div { display: flex; min-height: 76px; align-items: center; justify-content: center; gap: 9px; border-right: 1px solid var(--line); color: var(--brand); }
.leader-stat-row > div:last-child { border-right: 0; }
.leader-stat-row span { display: flex; flex-direction: column; }
.leader-stat-row strong { color: var(--ink); font-size: 14px; }
.leader-stat-row small { color: var(--muted); font-size: 9px; }
.leader-detail-state { display: flex; min-height: 240px; align-items: center; justify-content: center; gap: 12px; color: var(--muted); }

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
