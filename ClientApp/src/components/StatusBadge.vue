<script setup>
const props = defineProps({ status: { type: Number, required: true } })

const statusMap = {
  0: ['待支付', 'text-bg-secondary'],
  1: ['已支付', 'text-bg-primary'],
  2: ['已发货', 'text-bg-info'],
  3: ['已完成', 'text-bg-success'],
  4: ['已取消', 'text-bg-dark'],
  5: ['退款中', 'text-bg-warning'],
  6: ['已退款', 'text-bg-light'],
}
</script>

<template>
  <span class="badge status-badge" :class="(statusMap[props.status] ?? ['未知', 'text-bg-light'])[1]">
    {{ (statusMap[props.status] ?? ['未知'])[0] }}
  </span>
</template>
