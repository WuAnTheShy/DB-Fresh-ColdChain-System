<script setup>
import { BadgeCheck, UsersRound } from '@lucide/vue'
import { computed } from 'vue'
import { useShop } from '../state/shop'

defineProps({
  leaders: { type: Array, default: () => [] },
})

const { products } = useShop()

const productCountByLeader = computed(() => {
  const counts = {}
  for (const product of products) {
    if (product.leaderId) counts[product.leaderId] = (counts[product.leaderId] ?? 0) + 1
  }
  return counts
})

function onSaleCount(leaderId) {
  return productCountByLeader.value[String(leaderId ?? '')] ?? 0
}
</script>

<template>
  <section class="followed-leaders-panel" aria-label="关注的团长列表">
    <header class="followed-leaders-head">
      <h2><UsersRound :size="14" />关注团长</h2>
    </header>
    <nav class="followed-leaders-list">
      <RouterLink
        v-for="leader in leaders"
        :key="leader.id"
        class="followed-leader-item"
        :to="`/leaders/${leader.id}`"
        :aria-label="`查看${leader.name}详情`"
      >
        <img class="followed-leader-avatar" :src="leader.avatar" :alt="`${leader.name}头像`" loading="lazy" />
        <span class="followed-leader-copy">
          <strong>{{ leader.name }}</strong>
          <small><BadgeCheck :size="12" />在团 {{ onSaleCount(leader.id) }} 件</small>
        </span>
      </RouterLink>
    </nav>
  </section>
</template>

<style scoped>
.followed-leaders-panel {
  overflow: hidden;
  border: 1px solid var(--line);
  border-radius: 8px;
  background: #fff;
}

.followed-leaders-head {
  display: flex;
  align-items: center;
  padding: 13px 14px;
  border-bottom: 1px solid var(--line);
}

.followed-leaders-head h2 {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  margin: 0;
  color: var(--ink);
  font-size: 13px;
  font-weight: 700;
}

.followed-leaders-head h2 svg {
  color: var(--brand);
}

.followed-leaders-list {
  display: flex;
  flex-direction: column;
  padding: 5px 0;
}

.followed-leader-item {
  display: flex;
  min-width: 0;
  align-items: center;
  gap: 10px;
  padding: 9px 14px;
  border-left: 2px solid transparent;
  color: var(--ink);
  text-decoration: none;
  transition: background .14s ease, border-color .14s ease;
}

.followed-leader-item:hover {
  border-left-color: var(--brand);
  background: #f2f7f4;
  color: var(--ink);
}

.followed-leader-avatar {
  width: 40px;
  height: 40px;
  flex: 0 0 40px;
  border-radius: 10px;
  object-fit: cover;
  background: #e8f3ed;
}

.followed-leader-copy {
  display: flex;
  min-width: 0;
  flex-direction: column;
}

.followed-leader-copy strong {
  overflow: hidden;
  font-size: 12.5px;
  font-weight: 700;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.followed-leader-copy small {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  margin-top: 2px;
  color: var(--brand);
  font-size: 10px;
  white-space: nowrap;
}

@media (max-width: 767.98px) {
  .followed-leaders-list {
    display: grid;
    gap: 4px;
    padding: 8px;
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .followed-leader-item {
    padding: 8px 10px;
    border: 1px solid var(--line);
    border-radius: 8px;
  }

  .followed-leader-item:hover {
    border-color: #9fb9ac;
  }
}
</style>
