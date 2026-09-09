<script setup>
import { ArrowUp } from '@lucide/vue'
import { onBeforeUnmount, onMounted, ref } from 'vue'

const visible = ref(false)

function onScroll() {
  visible.value = window.scrollY > 0
}

function backToTop() {
  window.scrollTo({ top: 0, behavior: 'smooth' })
}

onMounted(() => {
  onScroll()
  window.addEventListener('scroll', onScroll, { passive: true })
})

onBeforeUnmount(() => {
  window.removeEventListener('scroll', onScroll)
})
</script>

<template>
  <Transition name="fade">
    <button
      v-if="visible"
      class="back-to-top-float"
      type="button"
      aria-label="返回顶部"
      title="返回顶部"
      @click="backToTop"
    >
      <ArrowUp :size="22" :stroke-width="2.4" />
    </button>
  </Transition>
</template>

<style scoped>
.back-to-top-float {
  position: fixed;
  right: 24px;
  bottom: 88px;
  z-index: 1030;
  display: inline-flex;
  width: 44px;
  height: 44px;
  align-items: center;
  justify-content: center;
  padding: 0;
  border: 1px solid rgba(255, 255, 255, .18);
  border-radius: 50%;
  background: var(--amber-hover);
  color: var(--ink);
  box-shadow: 0 4px 14px rgba(20, 29, 25, .28);
  cursor: pointer;
  transition: background .2s ease, transform .2s ease, box-shadow .2s ease;
}

.back-to-top-float:hover {
  background: var(--amber);
  box-shadow: 0 6px 18px rgba(20, 29, 25, .34);
  transform: translateY(-2px);
}

.back-to-top-float:active {
  transform: translateY(0);
}

.fade-enter-active,
.fade-leave-active {
  transition: opacity .2s ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}
</style>
