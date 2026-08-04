<script setup>
import AppHeader from './components/AppHeader.vue'
import MobileBottomNav from './components/MobileBottomNav.vue'
import ShopFooter from './components/ShopFooter.vue'
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
