<script setup>
import { BadgeCheck, Bell, MapPin, PackageCheck, UsersRound } from '@lucide/vue'
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import ProductCard from '../components/ProductCard.vue'
import StoreBreadcrumb from '../components/StoreBreadcrumb.vue'
import { useShop } from '../state/shop'

const props = defineProps({ id: { type: String, required: true } })
const router = useRouter()
const { leaderById, products } = useShop()
const leader = computed(() => leaderById(props.id))
const leaderProducts = computed(() => products.filter((product) => product.leaderIds.includes(Number(props.id))))
const followed = ref(false)

if (!leader.value) router.replace('/leaders')
</script>

<template>
  <div v-if="leader" class="leader-detail-page">
    <div class="store-container page-space pb-0"><StoreBreadcrumb :items="[{ label: '团长广场', to: '/leaders' }, { label: `${leader.name}团长` }]" /></div>
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
        <button class="btn leader-follow-button" :class="followed ? 'btn-light' : 'btn-buy'" type="button" @click="followed = !followed">
          <Bell :size="17" />{{ followed ? '已关注' : '关注团长' }}
        </button>
      </div>
    </section>

    <div class="store-container leader-stat-row">
      <div><PackageCheck :size="20" /><span><strong>{{ leaderProducts.length }}</strong><small>正在带货</small></span></div>
      <div><UsersRound :size="20" /><span><strong>{{ leader.following }}</strong><small>社区关注</small></span></div>
      <div><BadgeCheck :size="20" /><span><strong>已认证</strong><small>平台团长资质</small></span></div>
    </div>

    <div class="store-container home-section">
      <div class="section-title-row"><div><h2>{{ leader.name }}团长正在带货</h2><p>所有商品均显示截团进度与预计送达时间</p></div></div>
      <div class="product-grid">
        <ProductCard v-for="product in leaderProducts" :key="product.id" :product="product" :leader-id="leader.id" />
      </div>
    </div>
  </div>
</template>
