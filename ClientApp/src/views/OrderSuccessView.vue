<script setup>
import { CheckCircle2, CreditCard, PackageSearch, Store } from '@lucide/vue'
import { useShop } from '../state/shop'

defineProps({ id: { type: String, required: true } })
const { lastOrder } = useShop()
</script>

<template>
  <div class="store-container order-success-page">
    <CheckCircle2 class="success-icon" :size="58" />
    <h1>下单成功</h1>
    <p>结算批次已按团长拆单，请在15分钟内完成支付。</p>
    <div v-if="lastOrder?.priceChanges?.length" class="alert alert-warning success-price-alert">商品价格已更新，本批次已按最新价格生成待支付订单。</div>
    <div v-if="lastOrder?.appliedCoupons?.some(item => item.wasAutoClaimed)" class="alert alert-success success-price-alert">已自动领取并使用本次优惠金额最大的优惠券。</div>
    <div class="success-order-card">
      <div><span>结算批次</span><strong>{{ lastOrder?.checkoutBatchId || `订单 #${id}` }}</strong></div>
      <div><span>实付金额</span><strong>¥{{ Number(lastOrder?.finalAmount ?? 0).toFixed(2) }}</strong></div>
      <div><span>团长子订单</span><strong>{{ lastOrder?.orders?.length ?? 1 }} 个</strong></div>
    </div>
    <div v-if="lastOrder?.leaderGroups?.length" class="success-leaders"><Store :size="18" /><span>带货团长：{{ lastOrder.leaderGroups.map((item) => `${item.leaderName}团长`).join('、') }}</span></div>
    <div class="success-actions"><RouterLink v-if="lastOrder?.checkoutBatchId" class="btn btn-buy" :to="`/payment/${lastOrder.checkoutBatchId}`"><CreditCard :size="17" />立即支付</RouterLink><RouterLink class="btn btn-outline-secondary" :to="`/orders/${id}`"><PackageSearch :size="17" />查看订单</RouterLink><RouterLink class="btn btn-outline-secondary" to="/">继续逛逛</RouterLink></div>
  </div>
</template>

<style scoped>
.order-success-page { display: flex; min-height: 610px; flex-direction: column; align-items: center; justify-content: center; text-align: center; }
.success-icon { color: var(--brand); }
.order-success-page h1 { margin: 13px 0 6px; font-size: 27px; font-weight: 800; }
.order-success-page > p { margin: 0 0 22px; color: var(--muted); }
.success-order-card { display: grid; width: min(620px, 100%); grid-template-columns: repeat(3, 1fr); border: 1px solid var(--line); background: #fff; }
.success-order-card > div { display: flex; min-height: 84px; flex-direction: column; align-items: center; justify-content: center; border-right: 1px solid var(--line); }
.success-order-card > div:last-child { border-right: 0; }
.success-order-card span { color: var(--muted); font-size: 9px; }
.success-order-card strong { margin-top: 5px; font-size: 14px; }
.success-leaders { display: flex; align-items: center; gap: 7px; margin-top: 15px; color: var(--brand); font-size: 11px; font-weight: 700; }
.success-price-alert { width: min(620px, 100%); font-size: 11px; }
.success-actions { display: flex; gap: 10px; margin-top: 22px; }

@media (max-width: 767.98px) {
  .success-order-card { grid-template-columns: 1fr; }
  .success-order-card > div { min-height: 62px; border-right: 0; border-bottom: 1px solid var(--line); }
  .success-order-card > div:last-child { border-bottom: 0; }
}
</style>
