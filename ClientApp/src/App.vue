<script setup>
import { watch } from 'vue'
import AppHeader from './components/AppHeader.vue'
import MobileBottomNav from './components/MobileBottomNav.vue'
import ShopFooter from './components/ShopFooter.vue'
import { useCustomerContext } from './state/customer'
import { useShop } from './state/shop'

const { customerId } = useCustomerContext()
const { loadFollowedLeaders } = useShop()

watch(customerId, (id) => {
  loadFollowedLeaders(id).catch(() => {})
}, { immediate: true })
</script>

<template>
  <div class="store-shell">
    <AppHeader />
    <main class="store-main">
      <RouterView />
    </main>
    <ShopFooter />
    <MobileBottomNav />
  </div>
</template>
