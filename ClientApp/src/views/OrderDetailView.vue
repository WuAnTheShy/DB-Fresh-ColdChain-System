<script setup>
import { BadgeCheck, Ban, CheckCircle2, ChevronLeft, MapPin, PackageCheck, RefreshCw, Truck } from '@lucide/vue'
import { computed, onMounted, ref } from 'vue'
import StatusBadge from '../components/StatusBadge.vue'
import StoreBreadcrumb from '../components/StoreBreadcrumb.vue'
import { api } from '../services/api'
import { useShop } from '../state/shop'

const props = defineProps({ id: { type: String, required: true } })
const { productById, leaderById } = useShop()
const loading = ref(true)
const acting = ref(false)
const detail = ref(null)
const error = ref('')
const success = ref('')
const timeline = computed(() => {
  const status = detail.value?.order?.orderStatus
  const progress = { PENDING_PAYMENT: 0, PAID: 1, SHIPPED: 2, COMPLETED: 3 }[status] ?? 0
  return [{ label: '订单已提交', done: true }, { label: '团长确认', done: progress >= 1 }, { label: '冷链配送', done: progress >= 2 }, { label: '订单完成', done: progress === 3 }]
})

function money(value) { return `¥${Number(value ?? 0).toFixed(2)}` }
function date(value) { return value ? new Intl.DateTimeFormat('zh-CN', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) : '-' }
function productImage(id) { return productById(id)?.image }
function fallbackLeader(id) { const product = productById(id); return product ? leaderById(product.leaderId) : null }

async function loadOrder() {
  loading.value = true; error.value = ''
  try { detail.value = await api.getOrder(props.id) } catch (requestError) { detail.value = null; error.value = requestError.message } finally { loading.value = false }
}
async function runAction(action, message) {
  acting.value = true; error.value = ''; success.value = ''
  try { await action(); success.value = message; await loadOrder() } catch (requestError) { error.value = requestError.message } finally { acting.value = false }
}
onMounted(loadOrder)
</script>

<template>
  <div class="store-container page-space order-detail-consumer">
    <StoreBreadcrumb :items="[{ label: '我的订单', to: '/orders' }, { label: detail?.order?.orderNo || `订单 #${id}` }]" />
    <div v-if="error" class="alert alert-danger">{{ error }}</div><div v-if="success" class="alert alert-success">{{ success }}</div>
    <div v-if="loading" class="store-loading"><span class="spinner-border spinner-border-sm"></span>正在读取订单详情</div>
    <template v-else-if="detail?.order">
      <section class="order-detail-head"><div><RouterLink to="/orders"><ChevronLeft :size="18" /></RouterLink><span><small>订单号 {{ detail.order.orderNo }}</small><h1>{{ detail.statusName }}</h1><p>下单时间 {{ date(detail.order.createdAt) }}</p></span></div><StatusBadge :status="detail.order.orderStatus" /></section>
      <section class="order-timeline"><div v-for="(step, index) in timeline" :key="step.label" :class="{ done: step.done }"><span><CheckCircle2 v-if="step.done" :size="18" />{{ index + 1 }}</span><strong>{{ step.label }}</strong></div></section>

      <div class="order-detail-layout">
        <div>
          <section class="order-consumer-section">
            <div class="consumer-section-title"><BadgeCheck :size="21" /><div><h2>{{ detail.promoterName ? `${detail.promoterName}团长带货` : '认证团长带货商品' }}</h2></div></div>
            <article v-for="item in detail.details" :key="item.orderDetailId" class="order-product-row">
              <img v-if="productImage(item.productId)" :src="productImage(item.productId)" :alt="item.productName" />
              <span v-else class="order-product-placeholder"><PackageCheck :size="24" /></span>
              <div><strong>{{ item.productName }}</strong><small v-if="fallbackLeader(item.productId)"><BadgeCheck :size="13" />{{ fallbackLeader(item.productId).name }}团长带货</small></div><span>{{ money(item.unitPrice) }} × {{ item.quantity }}</span><strong>{{ money(item.subTotal) }}</strong>
            </article>
          </section>
          <section class="order-consumer-section delivery-section"><div class="consumer-section-title"><Truck :size="21" /><div><h2>冷链配送</h2></div></div><div class="delivery-status-row"><span class="delivery-icon"><Truck :size="20" /></span><div><strong>{{ ['SHIPPED', 'COMPLETED'].includes(detail.order.orderStatus) ? '商品已进入配送流程' : '团长正在确认团购与备货' }}</strong><small>确认后将在此展示最新配送状态</small></div></div></section>
        </div>
        <aside>
          <section class="order-side-section"><div class="consumer-section-title"><MapPin :size="20" /><div><h2>收货信息</h2></div></div><dl><div><dt>收货人</dt><dd>{{ detail.order.receiverName }} {{ detail.order.receiverPhone }}</dd></div><div><dt>地址</dt><dd>{{ detail.order.shippingAddress }}</dd></div></dl></section>
          <section class="order-side-section"><h2>金额明细</h2><dl><div><dt>商品金额</dt><dd>{{ money(detail.order.totalAmount) }}</dd></div><div><dt>团购优惠</dt><dd>-{{ money(detail.order.discountAmount) }}</dd></div><div><dt>冷链运费</dt><dd>{{ money(detail.order.freightAmount) }}</dd></div><div class="order-pay-total"><dt>实付金额</dt><dd>{{ money(detail.order.finalAmount) }}</dd></div></dl></section>
          <div class="order-detail-actions"><button v-if="detail.canComplete" class="btn btn-buy" type="button" :disabled="acting" @click="runAction(() => api.transitionOrder(id, 'Completed'), '已确认收货')"><CheckCircle2 :size="17" />确认收货</button><button v-if="detail.canCancel" class="btn btn-outline-danger" type="button" :disabled="acting" @click="runAction(() => api.cancelOrder(id), '订单已取消')"><Ban :size="17" />取消订单</button><button class="btn btn-outline-secondary" type="button" @click="loadOrder"><RefreshCw :size="16" />刷新状态</button></div>
        </aside>
      </div>
    </template>
  </div>
</template>

<style scoped>
.order-detail-head { display: flex; min-height: 112px; align-items: center; justify-content: space-between; gap: 16px; padding: 20px; border: 1px solid var(--line); background: #fff; }
.order-detail-head > div { display: flex; align-items: center; gap: 12px; }
.order-detail-head > div > a { display: inline-flex; width: 36px; height: 36px; align-items: center; justify-content: center; border: 1px solid var(--line); border-radius: 4px; }
.order-detail-head small { color: var(--muted); font-size: 9px; }
.order-detail-head h1 { margin: 3px 0; font-size: 23px; }
.order-detail-head p { margin: 0; color: var(--muted); font-size: 9px; }
.order-timeline { display: grid; grid-template-columns: repeat(4, 1fr); margin: 12px 0; padding: 18px; border: 1px solid var(--line); background: #fff; }
.order-timeline > div { position: relative; display: flex; flex-direction: column; align-items: center; gap: 6px; color: #939b97; }
.order-timeline > div::after { position: absolute; top: 14px; left: calc(50% + 18px); width: calc(100% - 36px); height: 2px; background: #dce2df; content: ""; }
.order-timeline > div:last-child::after { display: none; }
.order-timeline > div.done { color: var(--brand); }
.order-timeline > div.done::after { background: #76ad98; }
.order-timeline > div > span { z-index: 1; display: inline-flex; width: 29px; height: 29px; align-items: center; justify-content: center; border: 2px solid currentColor; border-radius: 50%; background: #fff; font-size: 10px; }
.order-timeline strong { font-size: 10px; }
.order-detail-layout { display: grid; grid-template-columns: minmax(0, 1fr) 310px; gap: 14px; align-items: start; }
.order-detail-layout > div { display: flex; flex-direction: column; gap: 14px; }
.order-consumer-section, .order-side-section { padding: 17px; border: 1px solid var(--line); background: #fff; }
.consumer-section-title { display: flex; align-items: flex-start; gap: 9px; margin-bottom: 15px; color: var(--brand); }
.consumer-section-title > div { min-width: 0; flex: 1; }
.consumer-section-title h2 { margin: 0; color: var(--ink); font-size: 15px; }
.consumer-section-title p { margin: 3px 0 0; color: var(--muted); font-size: 9px; }
.order-product-row { display: grid; grid-template-columns: 72px minmax(140px, 1fr) 100px 90px; gap: 12px; align-items: center; padding: 12px 0; border-top: 1px solid var(--line); }
.order-product-row > img, .order-product-placeholder { width: 72px; height: 72px; object-fit: cover; }
.order-product-placeholder { display: inline-flex; align-items: center; justify-content: center; background: #eef1ef; color: var(--muted); }
.order-product-row > div { display: flex; min-width: 0; flex-direction: column; }
.order-product-row > div small { display: flex; align-items: center; gap: 3px; margin-top: 5px; color: var(--brand); font-size: 9px; }
.order-product-row > span { color: var(--muted); font-size: 10px; text-align: right; }
.order-product-row > strong { color: var(--danger); text-align: right; }
.delivery-status-row { display: flex; align-items: center; gap: 10px; padding-top: 4px; }
.delivery-icon { display: inline-flex; width: 42px; height: 42px; align-items: center; justify-content: center; border-radius: 50%; background: #e8f1f8; color: #2463a7; }
.delivery-status-row > div { display: flex; flex-direction: column; }
.delivery-status-row small { margin-top: 3px; color: var(--muted); font-size: 9px; }
.order-detail-layout > aside { display: flex; flex-direction: column; gap: 12px; }
.order-side-section h2 { margin: 0 0 14px; font-size: 15px; }
.order-side-section dl { margin: 0; }
.order-side-section dl > div { display: flex; justify-content: space-between; gap: 15px; padding: 8px 0; border-bottom: 1px solid var(--line); }
.order-side-section dt { color: var(--muted); font-size: 9px; font-weight: 500; }
.order-side-section dd { margin: 0; font-size: 10px; text-align: right; }
.order-pay-total dd { color: var(--danger); font-size: 18px !important; font-weight: 800; }
.order-detail-actions { display: flex; flex-direction: column; gap: 7px; }

@media (max-width: 767.98px) {
  .order-timeline { padding: 13px 5px; }
  .order-timeline strong { font-size: 8px; }
  .order-product-row { grid-template-columns: 60px minmax(0, 1fr) 76px; }
  .order-product-row > img, .order-product-placeholder { width: 60px; height: 60px; }
  .order-product-row > span { grid-column: 2; }
  .order-product-row > strong { grid-column: 3; grid-row: 1 / span 2; }
}
</style>
