<script setup>
const props = defineProps({
  status: { type: String, required: true },
  label: { type: String, default: '' },
})

const statusMap = {
  PENDING: ['待发货', 'text-bg-secondary'],
  PACKING: ['备货中', 'text-bg-primary'],
  PENDING_PAYMENT: ['待支付', 'text-bg-secondary'],
  PAID: ['已支付', 'text-bg-primary'],
  SHIPPED: ['已发货', 'text-bg-info'],
  IN_TRANSIT: ['运输中', 'text-bg-info'],
  OUT_FOR_DELIVERY: ['派送中', 'text-bg-primary'],
  DELIVERED: ['已签收', 'text-bg-success'],
  EXCEPTION: ['物流异常', 'text-bg-danger'],
  RETURNING: ['退回中', 'text-bg-warning'],
  RETURNED: ['已退回', 'text-bg-dark'],
  COMPLETED: ['已完成', 'text-bg-success'],
  CANCELLED: ['已取消', 'text-bg-dark'],
  REFUNDING: ['退款中', 'text-bg-warning'],
  REFUNDED: ['已退款', 'text-bg-light'],
  REFUND_REVIEWING: ['退款审核中', 'text-bg-warning'],
  REFUND_APPROVED: ['退款已通过', 'text-bg-success'],
  REFUND_REJECTED: ['退款已驳回', 'text-bg-secondary'],
  REFUND_CANCELLED: ['已取消申请', 'text-bg-dark'],
}
</script>

<template>
  <span class="badge status-badge" :class="(statusMap[props.status] ?? ['未知', 'text-bg-light'])[1]">
    {{ props.label || (statusMap[props.status] ?? ['未知'])[0] }}
  </span>
</template>

<style scoped>
.status-badge { min-width: 58px; padding: 5px 7px; border-radius: 4px; font-size: 9px; }
</style>
