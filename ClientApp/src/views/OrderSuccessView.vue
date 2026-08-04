<script setup>
import { CheckCircle2, PackageSearch, Store } from '@lucide/vue'
import { useShop } from '../state/shop'

defineProps({ id: { type: String, required: true } })
const { lastOrder } = useShop()
</script>

<template>
  <div class="store-container order-success-page">
    <CheckCircle2 class="success-icon" :size="58" />
    <h1>参团成功</h1>
    <p>订单已提交，团长将按截团时间统一安排冷链履约。</p>
    <div class="success-order-card">
      <div><span>订单号</span><strong>{{ lastOrder?.orderNo || `订单 #${id}` }}</strong></div>
      <div><span>实付金额</span><strong>¥{{ Number(lastOrder?.finalAmount ?? 0).toFixed(2) }}</strong></div>
      <div><span>获得积分</span><strong>{{ lastOrder?.pointsEarned ?? 0 }} 分</strong></div>
    </div>
    <div v-if="lastOrder?.leaderGroups?.length" class="success-leaders"><Store :size="18" /><span>带货团长：{{ lastOrder.leaderGroups.map((item) => `${item.leaderName}团长`).join('、') }}</span></div>
    <div class="success-actions"><RouterLink class="btn btn-buy" :to="`/orders/${id}`"><PackageSearch :size="17" />查看订单</RouterLink><RouterLink class="btn btn-outline-secondary" to="/">继续逛逛</RouterLink></div>
  </div>
</template>
