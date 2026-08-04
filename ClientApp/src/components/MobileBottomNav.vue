<script setup>
import { House, PackageSearch, ShoppingCart, Store, UserRound } from '@lucide/vue'
import { useShop } from '../state/shop'

const { cartCount } = useShop()
const items = [
  { label: '首页', to: '/', icon: House },
  { label: '团长', to: '/leaders', icon: Store },
  { label: '购物车', to: '/cart', icon: ShoppingCart, cart: true },
  { label: '订单', to: '/orders', icon: PackageSearch },
  { label: '我的', to: '/profile', icon: UserRound },
]
</script>

<template>
  <nav class="mobile-bottom-nav d-md-none" aria-label="移动端导航">
    <RouterLink v-for="item in items" :key="item.to" :to="item.to">
      <span class="mobile-nav-icon">
        <component :is="item.icon" :size="21" />
        <span v-if="item.cart && cartCount" class="mobile-cart-count">{{ cartCount }}</span>
      </span>
      <small>{{ item.label }}</small>
    </RouterLink>
  </nav>
</template>
