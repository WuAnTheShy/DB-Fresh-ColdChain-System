<script setup>
import { ArrowRight, BadgeCheck, MapPin } from '@lucide/vue'
import { computed } from 'vue'
import { useShop } from '../state/shop'

const props = defineProps({ leader: { type: Object, required: true } })
const { products } = useShop()
const leaderProducts = computed(() => products.filter((product) => product.leaderIds.includes(props.leader.id)))
</script>

<template>
  <article class="leader-card">
    <div class="leader-card-top">
      <img :src="leader.avatar" :alt="`${leader.name}团长头像`" />
      <div>
        <div class="leader-name"><strong>{{ leader.name }}团长</strong><BadgeCheck :size="17" /></div>
        <span>{{ leader.title }}</span>
        <small><MapPin :size="13" />{{ leader.area }}</small>
      </div>
    </div>
    <p>{{ leader.description }}</p>
    <div class="leader-tags"><span v-for="tag in leader.tags" :key="tag">{{ tag }}</span></div>
    <div class="leader-product-preview">
      <img v-for="product in leaderProducts.slice(0, 3)" :key="product.id" :src="product.image" :alt="product.shortName" />
    </div>
    <div class="leader-card-footer">
      <span><strong>{{ leaderProducts.length }}</strong> 款正在带货</span>
      <RouterLink :to="`/leaders/${leader.id}`">进入主页<ArrowRight :size="15" /></RouterLink>
    </div>
  </article>
</template>
