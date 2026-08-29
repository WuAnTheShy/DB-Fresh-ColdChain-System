<script setup>
import { Heart, House, PackageSearch, ShoppingCart, UserRound } from '@lucide/vue'
import { useShop } from '../state/shop'

const { cartCount } = useShop()
const items = [
  { label: '首页', to: '/', icon: House },
  { label: '关注', to: '/following', icon: Heart },
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

<style scoped>
.mobile-bottom-nav { position: fixed; inset: auto 0 0; z-index: 1040; display: grid; height: 62px; grid-template-columns: repeat(5, 1fr); border-top: 1px solid var(--line); background: #fff; box-shadow: 0 -2px 8px rgba(20, 29, 25, .08); }
.mobile-bottom-nav a { display: flex; min-width: 0; flex-direction: column; align-items: center; justify-content: center; gap: 2px; color: #65716b; text-decoration: none; }
.mobile-bottom-nav a.router-link-exact-active { color: var(--brand); }
.mobile-bottom-nav small { font-size: 10px; }
.mobile-nav-icon { position: relative; display: inline-flex; }
.mobile-cart-count { position: absolute; top: -8px; right: -10px; min-width: 17px; padding: 1px 4px; border-radius: 9px; background: var(--danger); color: #fff; font-size: 9px; text-align: center; }
</style>
