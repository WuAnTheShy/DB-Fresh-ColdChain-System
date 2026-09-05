<script setup>
import { AlertTriangle, BadgeCheck, Ban, Check, CheckCircle2, ChevronLeft, Clock3, CreditCard, MapPin, PackageCheck, RefreshCw, RotateCcw, Snowflake, Truck, X } from '@lucide/vue'
import { computed, onMounted, ref } from 'vue'
import StatusBadge from '../components/StatusBadge.vue'
import { api } from '../services/api'
import { useShop } from '../state/shop'

const props = defineProps({ id: { type: String, required: true } })
const { productById, leaderById } = useShop()
const loading = ref(true)
const acting = ref(false)
const detail = ref(null)
const error = ref('')
const success = ref('')
const cancelDialogOpen = ref(false)
const timeline = computed(() => {
  const status = detail.value?.order?.orderStatus
  const progress = { PENDING_PAYMENT: 0, PAID: 1, SHIPPED: 2, COMPLETED: 3 }[status] ?? 0
  return [{ label: '订单已提交', done: true }, { label: '商家备货', done: progress >= 1 }, { label: '冷链配送', done: progress >= 2 }, { label: '订单完成', done: progress === 3 }]
})

function money(value) { return `¥${Number(value ?? 0).toFixed(2)}` }
function date(value) { return value ? new Intl.DateTimeFormat('zh-CN', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) : '-' }
function productImage(id) { return productById(id)?.image }
function fallbackLeader(id) { const product = productById(id); return product ? leaderById(product.leaderId) : null }
function temperatureName(value) { return ({ CHILLED: '冷藏', FROZEN: '冷冻', AMBIENT: '常温' })[value] ?? value ?? '未标注' }

async function loadOrder() {
  loading.value = true; error.value = ''
  try { detail.value = await api.getOrder(props.id) } catch (requestError) { detail.value = null; error.value = requestError.message } finally { loading.value = false }
}
async function runAction(action, message) {
  acting.value = true; error.value = ''; success.value = ''
  try { await action(); success.value = message; await loadOrder() } catch (requestError) { error.value = requestError.message } finally { acting.value = false }
}
function cancelPendingOrder() {
  cancelDialogOpen.value = true
}
function confirmCancelOrder() {
  cancelDialogOpen.value = false
  runAction(() => api.cancelOrder(props.id), '订单已取消')
}
onMounted(loadOrder)
</script>

<template>
  <div class="store-container page-space order-detail-consumer">
    <div v-if="error" class="alert alert-danger">{{ error }}</div>
    <div v-if="success" class="alert alert-success">{{ success }}</div>
    <div v-if="loading" class="store-loading"><span class="spinner-border spinner-border-sm"></span>正在读取订单详情</div>
    <template v-else-if="detail?.order">
      <section class="order-detail-head">
        <div>
          <RouterLink to="/orders">
            <ChevronLeft :size="18" />
          </RouterLink><span><small>订单号 {{ detail.order.orderNo }}</small>
            <h1>{{ detail.statusName }}</h1>
            <p>下单时间 {{ date(detail.order.createdAt) }}</p>
          </span>
        </div>
        <StatusBadge :status="detail.displayStatusCode || detail.order.orderStatus" :label="detail.statusName" />
      </section>
      <section class="order-timeline">
        <div v-for="(step, index) in timeline" :key="step.label" :class="{ done: step.done }"><span>
            <Check v-if="step.done" :size="15" :stroke-width="3.2" />
          </span><strong>{{ step.label }}</strong></div>
      </section>

      <div class="order-detail-layout">
        <div>
          <section class="order-consumer-section">
            <div class="consumer-section-title">
              <BadgeCheck :size="21" />
              <div>
                <h2>{{ detail.promoterName ? `${detail.promoterName}团长带货` : '认证团长带货商品' }}</h2>
              </div>
            </div>
            <article v-for="item in detail.details" :key="item.orderDetailId" class="order-product-row">
              <img v-if="productImage(item.productId)" :src="productImage(item.productId)" :alt="item.productName" />
              <span v-else class="order-product-placeholder">
                <PackageCheck :size="24" />
              </span>
              <div><strong>{{ item.productName }}</strong><small v-if="fallbackLeader(item.productId)">
                  <BadgeCheck :size="13" />{{ fallbackLeader(item.productId).name }}团长带货
                </small><small v-if="item.receiptStatus === 'RECEIVED'" class="receipt-done">
                  <CheckCircle2 :size="13" />已确认收货 · {{ date(item.receivedAt) }}
                </small><button v-else-if="item.canConfirmReceipt" class="btn btn-sm btn-buy receipt-button"
                  type="button" :disabled="acting"
                  @click="runAction(() => api.confirmOrderItemReceipt(id, item.orderDetailId), `${item.productName}已确认收货`)">
                  <CheckCircle2 :size="14" />确认该商品收货
                </button></div><span>{{ money(item.unitPrice) }} × {{ item.quantity }}</span><strong>{{
                  money(item.subTotal) }}</strong>
            </article>
          </section>
          <section class="order-consumer-section delivery-section">
            <div class="consumer-section-title">
              <Truck :size="21" />
              <div>
                <h2>冷链配送</h2>
                <p>不同供应商的商品可能拆分为多个包裹配送</p>
              </div>
            </div>
            <div v-if="!detail.packages?.length" class="delivery-status-row"><span class="delivery-icon">
                <Truck :size="20" />
              </span>
              <div><strong>订单已进入备货流程</strong><small>发货后将在此展示最新配送状态</small></div>
            </div>
            <article v-for="parcel in detail.packages" :key="parcel.packageNumber" class="parcel-card">
              <header>
                <div><strong>包裹 {{ parcel.packageNumber }}</strong><small>{{ parcel.itemIds.length }} 件订单明细 · {{
                  temperatureName(parcel.logistics.packageTemperature) }}</small></div>
                <StatusBadge :status="parcel.logistics.statusCode" />
              </header>
              <div v-if="parcel.logistics.hasException" class="parcel-exception">
                <AlertTriangle :size="17" /><span><strong>配送异常</strong>{{ parcel.logistics.exceptionMessage }}</span>
              </div>
              <dl class="parcel-facts">
                <div>
                  <dt>承运商</dt>
                  <dd>{{ parcel.logistics.carrierName || '待分配' }}</dd>
                </div>
                <div>
                  <dt>运单号</dt>
                  <dd>{{ parcel.logistics.trackingNo || '待生成' }}</dd>
                </div>
                <div>
                  <dt>
                    <Snowflake :size="13" />运输温区
                  </dt>
                  <dd>{{ temperatureName(parcel.logistics.packageTemperature) }}</dd>
                </div>
                <div>
                  <dt>
                    <Clock3 :size="13" />预计送达
                  </dt>
                  <dd>{{ date(parcel.logistics.estimatedArrivalAt) }}</dd>
                </div>
              </dl>
              <div v-if="parcel.logistics.events?.length" class="parcel-events">
                <div v-for="event in parcel.logistics.events" :key="event.eventId" class="parcel-event"
                  :class="{ danger: event.isTemperatureException }">
                  <span></span>
                  <div>
                    <header><strong>{{ event.statusName }}</strong><time>{{ date(event.occurredAt) }}</time></header>
                    <p>{{ event.description }}</p><small>{{ event.location || '位置待更新' }}<b
                        v-if="event.temperatureCelsius != null">{{ Number(event.temperatureCelsius).toFixed(1)
                        }}℃</b></small>
                  </div>
                </div>
              </div>
              <div v-else class="parcel-pending">当前状态：{{ parcel.logistics.statusName }}，暂无更多物流轨迹</div>
            </article>
          </section>
        </div>
        <aside>
          <section class="order-side-section">
            <div class="consumer-section-title">
              <MapPin :size="20" />
              <div>
                <h2>收货信息</h2>
              </div>
            </div>
            <dl>
              <div>
                <dt>收货人</dt>
                <dd>{{ detail.order.receiverName }} {{ detail.order.receiverPhone }}</dd>
              </div>
              <div>
                <dt>地址</dt>
                <dd>{{ detail.order.shippingAddress }}</dd>
              </div>
            </dl>
          </section>
          <section class="order-side-section">
            <h2>金额明细</h2>
            <dl>
              <div>
                <dt>商品金额</dt>
                <dd>{{ money(detail.order.totalAmount) }}</dd>
              </div>
              <div>
                <dt>团购优惠</dt>
                <dd>-{{ money(detail.order.discountAmount) }}</dd>
              </div>
              <div v-if="detail.order.pointsUsed">
                <dt>积分抵扣（{{ detail.order.pointsUsed }}积分）</dt>
                <dd>-{{ money(detail.order.pointsDiscountAmount) }}</dd>
              </div>
              <div>
                <dt>冷链运费</dt>
                <dd>{{ money(detail.order.freightAmount) }}</dd>
              </div>
              <div v-if="detail.freightQuote" class="freight-rule">
                <dt>计费依据</dt>
                <dd>{{ detail.freightQuote.ruleSummary }}<small>{{ detail.freightQuote.province }} {{
                  detail.freightQuote.city }} {{ detail.freightQuote.district }}</small></dd>
              </div>
              <div class="order-pay-total">
                <dt>实付金额</dt>
                <dd>{{ money(detail.order.finalAmount) }}</dd>
              </div>
            </dl>
          </section>
          <div class="order-detail-actions">
            <RouterLink v-if="detail.order.orderStatus === 'PENDING_PAYMENT' && detail.order.checkoutBatchId"
              class="btn btn-buy" :to="`/payment/${detail.order.checkoutBatchId}`">
              <CreditCard :size="17" />支付整个结算批次
            </RouterLink>
            <RouterLink v-if="['PAID', 'SHIPPED', 'COMPLETED', 'REFUNDING'].includes(detail.order.orderStatus)"
              class="btn btn-outline-danger" :to="`/orders/${id}/refund`">
              <RotateCcw :size="16" />申请退款
            </RouterLink><button v-if="detail.order.orderStatus === 'PENDING_PAYMENT'" class="btn btn-outline-danger"
              type="button" :disabled="acting" @click="cancelPendingOrder">
              <Ban :size="17" />取消订单
            </button><button class="btn btn-outline-secondary" type="button" @click="loadOrder">
              <RefreshCw :size="16" />刷新状态
            </button>
          </div>
        </aside>
      </div>
    </template>

    <div v-if="cancelDialogOpen" class="cancel-dialog-backdrop" role="presentation" @click.self="cancelDialogOpen = false">
      <section class="cancel-dialog" role="dialog" aria-modal="true" aria-labelledby="cancel-dialog-title">
        <button class="cancel-dialog-close" type="button" aria-label="关闭" @click="cancelDialogOpen = false"><X :size="20" /></button>
        <span class="cancel-dialog-icon"><Ban :size="30" /></span>
        <h2 id="cancel-dialog-title">取消待支付订单？</h2>
        <p v-if="detail?.order?.checkoutBatchId">将同时关闭同批次的其他子订单，并归还已使用的优惠券与积分。</p>
        <p v-else>取消后将归还已使用的优惠券与积分。</p>
        <div><button class="btn btn-outline-secondary" type="button" :disabled="acting" @click="cancelDialogOpen = false">再想想</button><button class="btn btn-outline-danger" type="button" :disabled="acting" @click="confirmCancelOrder"><span v-if="acting" class="spinner-border spinner-border-sm"></span>确认取消</button></div>
      </section>
    </div>
  </div>
</template>

<style scoped>
.order-detail-head {
  display: flex;
  min-height: 112px;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 20px;
  border: 1px solid var(--line);
  background: #fff;
}

.order-detail-head>div {
  display: flex;
  align-items: center;
  gap: 12px;
}

.order-detail-head>div>a {
  display: inline-flex;
  width: 36px;
  height: 36px;
  align-items: center;
  justify-content: center;
  border: 1px solid var(--line);
  border-radius: 4px;
}

.order-detail-head small {
  color: var(--muted);
  font-size: 9px;
}

.order-detail-head h1 {
  margin: 3px 0;
  font-size: 23px;
}

.order-detail-head p {
  margin: 0;
  color: var(--muted);
  font-size: 9px;
}

.order-timeline {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  margin: 12px 0;
  padding: 18px;
  border: 1px solid var(--line);
  background: #fff;
  --timeline-green: #3ba35e;
}

.order-timeline>div {
  position: relative;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
  color: #939b97;
}

.order-timeline>div::after {
  position: absolute;
  top: 14px;
  left: calc(50% + 18px);
  width: calc(100% - 36px);
  height: 2px;
  background: #e2e7e4;
  content: "";
}

.order-timeline>div:last-child::after {
  display: none;
}

.order-timeline>div.done {
  color: #247349;
  font-weight: 600;
}

.order-timeline>div.done::after {
  background: #a9d8b5;
}

.order-timeline>div>span {
  z-index: 1;
  display: inline-flex;
  width: 29px;
  height: 29px;
  align-items: center;
  justify-content: center;
  border: 2px solid #d9dfdc;
  border-radius: 50%;
  background: #fff;
  color: #fff;
  font-size: 10px;
  transition: border-color 0.2s ease, background 0.2s ease;
}

.order-timeline>div:not(.done)>span::after {
  width: 5px;
  height: 5px;
  border-radius: 50%;
  background: #c3cac7;
  content: "";
}

.order-timeline>div.done>span {
  border-color: var(--timeline-green);
  background: var(--timeline-green);
  box-shadow: 0 2px 5px rgba(59, 163, 94, 0.3);
}

.order-timeline strong {
  font-size: 10px;
}

.order-detail-layout {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 310px;
  gap: 14px;
  align-items: start;
}

.order-detail-layout>div {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.order-consumer-section,
.order-side-section {
  padding: 17px;
  border: 1px solid var(--line);
  background: #fff;
}

.consumer-section-title {
  display: flex;
  align-items: flex-start;
  gap: 9px;
  margin-bottom: 15px;
  color: var(--brand);
}

.consumer-section-title>div {
  min-width: 0;
  flex: 1;
}

.consumer-section-title h2 {
  margin: 0;
  color: var(--ink);
  font-size: 15px;
}

.consumer-section-title p {
  margin: 3px 0 0;
  color: var(--muted);
  font-size: 9px;
}

.order-product-row {
  display: grid;
  grid-template-columns: 72px minmax(140px, 1fr) 100px 90px;
  gap: 12px;
  align-items: center;
  padding: 12px 0;
  border-top: 1px solid var(--line);
}

.order-product-row>img,
.order-product-placeholder {
  width: 72px;
  height: 72px;
  object-fit: cover;
}

.order-product-placeholder {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  background: #eef1ef;
  color: var(--muted);
}

.order-product-row>div {
  display: flex;
  min-width: 0;
  flex-direction: column;
}

.order-product-row>div small {
  display: flex;
  align-items: center;
  gap: 3px;
  margin-top: 5px;
  color: var(--brand);
  font-size: 9px;
}

.order-product-row>div .receipt-done {
  color: #247349;
}

.receipt-button {
  align-self: flex-start;
  margin-top: 8px;
  padding: 5px 9px;
  font-size: 9px;
}

.order-product-row>span {
  color: var(--muted);
  font-size: 10px;
  text-align: right;
}

.order-product-row>strong {
  color: var(--danger);
  text-align: right;
}

.delivery-status-row {
  display: flex;
  align-items: center;
  gap: 10px;
  padding-top: 4px;
}

.delivery-icon {
  display: inline-flex;
  width: 42px;
  height: 42px;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
  background: #e8f1f8;
  color: #2463a7;
}

.delivery-status-row>div {
  display: flex;
  flex-direction: column;
}

.delivery-status-row small {
  margin-top: 3px;
  color: var(--muted);
  font-size: 9px;
}

.parcel-card {
  margin-top: 11px;
  border: 1px solid var(--line);
  background: #fbfcfb;
}

.parcel-card>header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 12px 14px;
  border-bottom: 1px solid var(--line);
  background: #f4f7f5;
}

.parcel-card>header>div {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.parcel-card>header small {
  color: var(--muted);
  font-size: 9px;
}

.parcel-exception {
  display: flex;
  gap: 8px;
  padding: 10px 14px;
  border-bottom: 1px solid #f1b8b8;
  background: #fff1f1;
  color: #a52d2d;
}

.parcel-exception span {
  display: flex;
  flex-direction: column;
  font-size: 10px;
}

.parcel-facts {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px 18px;
  margin: 0;
  padding: 12px 14px;
}

.parcel-facts>div {
  display: flex;
  justify-content: space-between;
  gap: 8px;
}

.parcel-facts dt {
  display: flex;
  align-items: center;
  gap: 4px;
  color: var(--muted);
  font-size: 9px;
  font-weight: 500;
}

.parcel-facts dd {
  margin: 0;
  font-size: 10px;
  text-align: right;
}

.parcel-events {
  padding: 2px 14px 12px;
}

.parcel-event {
  display: grid;
  grid-template-columns: 10px minmax(0, 1fr);
  gap: 8px;
  padding-top: 10px;
}

.parcel-event>span {
  position: relative;
  width: 8px;
  height: 8px;
  margin-top: 4px;
  border-radius: 50%;
  background: var(--brand);
}

.parcel-event>span::after {
  position: absolute;
  top: 8px;
  left: 3px;
  width: 1px;
  height: calc(100% + 32px);
  background: #cfd9d4;
  content: '';
}

.parcel-event:last-child>span::after {
  display: none;
}

.parcel-event.danger>span {
  background: var(--danger);
}

.parcel-event header {
  display: flex;
  justify-content: space-between;
  gap: 8px;
}

.parcel-event time {
  color: var(--muted);
  font-size: 8px;
}

.parcel-event p {
  margin: 2px 0;
  font-size: 10px;
}

.parcel-event small {
  display: flex;
  gap: 8px;
  color: var(--muted);
  font-size: 9px;
}

.parcel-event b {
  color: var(--brand);
}

.parcel-event.danger b {
  color: var(--danger);
}

.parcel-pending {
  padding: 11px 14px;
  border-top: 1px solid var(--line);
  color: var(--muted);
  font-size: 9px;
}

.order-detail-layout>aside {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.order-side-section h2 {
  margin: 0 0 14px;
  font-size: 15px;
}

.order-side-section dl {
  margin: 0;
}

.order-side-section dl>div {
  display: flex;
  justify-content: space-between;
  gap: 15px;
  padding: 8px 0;
  border-bottom: 1px solid var(--line);
}

.order-side-section dt {
  color: var(--muted);
  font-size: 9px;
  font-weight: 500;
}

.order-side-section dd {
  margin: 0;
  font-size: 10px;
  text-align: right;
}

.order-side-section .freight-rule {
  align-items: flex-start;
}

.freight-rule dd {
  max-width: 180px;
}

.freight-rule small {
  display: block;
  margin-top: 3px;
  color: var(--muted);
  font-size: 8px;
}

.order-pay-total dd {
  color: var(--danger);
  font-size: 18px !important;
  font-weight: 800;
}

.order-detail-actions {
  display: flex;
  flex-direction: column;
  gap: 7px;
}

@media (max-width: 767.98px) {
  .order-timeline {
    padding: 13px 5px;
  }

  .order-timeline strong {
    font-size: 8px;
  }

  .order-timeline>div>span {
    width: 24px;
    height: 24px;
  }

  .order-timeline>div::after {
    top: 11px;
    left: calc(50% + 15px);
    width: calc(100% - 30px);
  }

  .order-product-row {
    grid-template-columns: 60px minmax(0, 1fr) 76px;
  }

  .order-product-row>img,
  .order-product-placeholder {
    width: 60px;
    height: 60px;
  }

  .order-product-row>span {
    grid-column: 2;
  }

  .order-product-row>strong {
    grid-column: 3;
    grid-row: 1 / span 2;
  }

  .parcel-facts {
    grid-template-columns: 1fr;
  }
}

.cancel-dialog-backdrop {
  position: fixed;
  z-index: 1080;
  display: grid;
  padding: 18px;
  background: rgba(10, 25, 20, .52);
  inset: 0;
  place-items: center;
}

.cancel-dialog {
  position: relative;
  width: min(440px, 100%);
  padding: 30px;
  border-radius: 10px;
  background: #fff;
  box-shadow: 0 20px 60px rgba(0, 0, 0, .22);
  text-align: center;
}

.cancel-dialog-close {
  position: absolute;
  top: 10px;
  right: 10px;
  display: inline-flex;
  padding: 5px;
  border: 0;
  background: transparent;
  color: var(--muted);
}

.cancel-dialog-icon {
  display: inline-flex;
  width: 58px;
  height: 58px;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
  background: #fdeaea;
  color: var(--danger);
}

.cancel-dialog h2 {
  margin: 14px 0 7px;
  font-size: 21px;
}

.cancel-dialog p {
  margin: 0;
  color: var(--ink);
  font-size: 11px;
  line-height: 1.7;
}

.cancel-dialog>div {
  display: flex;
  justify-content: center;
  gap: 8px;
  margin-top: 20px;
}
</style>
