<script setup>
import { watch } from 'vue'
import AppHeader from './components/AppHeader.vue'
import BackToTopButton from './components/BackToTopButton.vue'
import MobileBottomNav from './components/MobileBottomNav.vue'
import ShopFooter from './components/ShopFooter.vue'
import { useCustomerContext } from './state/customer'
import { useShop } from './state/shop'

const { customerId, loadDeliveryAddress } = useCustomerContext()
const { loadFollowedLeaders, loadLeaders, loadCatalog } = useShop()

watch(customerId, (id) => {
  Promise.all([loadLeaders(), loadCatalog(), loadDeliveryAddress()])
    .then(() => loadFollowedLeaders(id))
    .catch(() => {})
}, { immediate: true })
</script>

<template>
  <svg class="app-filter-definitions" aria-hidden="true">
    <defs>
      <filter id="fallback-photo-blue-overlay" x="0" y="0" width="100%" height="100%" color-interpolation-filters="sRGB">
        <feFlood flood-color="#d6ecf8" flood-opacity="0.2" result="blueOverlay" />
        <feComposite in="blueOverlay" in2="SourceAlpha" operator="in" result="clippedOverlay" />
        <feMerge>
          <feMergeNode in="SourceGraphic" />
          <feMergeNode in="clippedOverlay" />
        </feMerge>
      </filter>
    </defs>
  </svg>
  <div class="store-shell">
    <AppHeader />
    <main class="store-main">
      <RouterView />
    </main>
    <ShopFooter />
    <BackToTopButton />
    <MobileBottomNav />
  </div>
</template>
