<script setup>
const props = defineProps({ status: { type: String, required: true } })

const statusMap = {
  PENDING_PAYMENT: ['待支付', 'text-bg-secondary'],
  PAID: ['已支付', 'text-bg-primary'],
  SHIPPED: ['已发货', 'text-bg-info'],
  COMPLETED: ['已完成', 'text-bg-success'],
  CANCELLED: ['已取消', 'text-bg-dark'],
  REFUNDING: ['退款中', 'text-bg-warning'],
  REFUNDED: ['已退款', 'text-bg-light'],
}
</script>

<template>
  <span class="badge status-badge" :class="(statusMap[props.status] ?? ['未知', 'text-bg-light'])[1]">
    {{ (statusMap[props.status] ?? ['未知'])[0] }}
  </span>
</template>

<style scoped>
.status-badge { min-width: 58px; padding: 5px 7px; border-radius: 4px; font-size: 9px; }
</style>
