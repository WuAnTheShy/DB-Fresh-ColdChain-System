<script setup>
import { AlertTriangle, BadgeCheck, Ban, Check, CheckCircle2, ChevronLeft, Clock3, CreditCard, MapPin, RefreshCw, RotateCcw, Snowflake, Truck, X } from '@lucide/vue'
import { computed, onMounted, ref } from 'vue'
import StatusBadge from '../components/StatusBadge.vue'
import { api } from '../services/api'

const props = defineProps({ id: { type: String, required: true } })
const loading = ref(true)
const acting = ref(false)
const detail = ref(null)
const error = ref('')
const success = ref('')
const cancelDialogOpen = ref(false)
const receiptNoticeOpen = ref(false)
const shipmentNoticeOpen = ref(false)
const evaluationDialogOpen = ref(false)
const evaluationDialogMode = ref('edit')
const evaluatingItems = ref([])
const selectedEvaluationDimensions = ref([])
const evaluationError = ref('')
const evaluationDimensions = [
  { code: 'HIGH_QUALITY', name: '高品质' },
  { code: 'FAST_SHIPPING', name: '发货快' },
  { code: 'GOOD_PACKAGING', name: '包装完好' },
  { code: 'COST_EFFECTIVE', name: '性价比高' },
  { code: 'AFFORDABLE', name: '价格实惠' },
  { code: 'RELIABLE_PROMOTER', name: '团长靠谱' },
]
const timeline = computed(() => {
  const status = detail.value?.order?.orderStatus
  const progress = { PENDING_PAYMENT: 0, PAID: 1, SHIPPED: 2, COMPLETED: 3 }[status] ?? 0
  return [{ label: '订单已提交', done: true }, { label: '商家备货', done: progress >= 1 }, { label: '冷链配送', done: progress >= 2 }, { label: '订单完成', done: progress === 3 }]
})
const receivableItems = computed(() => detail.value?.details?.filter((item) => item.canConfirmReceipt) ?? [])
const evaluatableItems = computed(() => detail.value?.details?.filter((item) => item.canEvaluate) ?? [])
const evaluatedItems = computed(() => detail.value?.details?.filter((item) => item.isEvaluated) ?? [])
const canConfirmOrder = computed(() => {
  const items = detail.value?.details ?? []
  return receivableItems.value.length > 0 && items.every((item) => item.receiptStatus === 'RECEIVED' || item.canConfirmReceipt)
})
const showConfirmReceipt = computed(() => {
  const status = detail.value?.order?.orderStatus
  const items = detail.value?.details ?? []
  return status === 'SHIPPED' && items.some((item) => item.receiptStatus !== 'RECEIVED')
})
const showUrgeShipment = computed(() => detail.value?.order?.orderStatus === 'PAID')
const canEvaluateOrder = computed(() => {
  const items = detail.value?.details ?? []
  return evaluatableItems.value.length > 0 && items.every((item) => item.receiptStatus === 'RECEIVED')
})
const isOrderEvaluated = computed(() => {
  const items = detail.value?.details ?? []
  return items.length > 0 && items.every((item) => item.isEvaluated)
})

function money(value) { return `¥${Number(value ?? 0).toFixed(2)}` }
function date(value) { return value ? new Intl.DateTimeFormat('zh-CN', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) : '-' }
function handleProductImageError(event) {
  if (event.target.dataset.fallbackApplied) return
  event.target.dataset.fallbackApplied = 'true'
  event.target.src = '/images/homepic.png'
  event.target.classList.add('fallback-photo-tint')
}
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
function openEvaluation() {
  evaluationDialogMode.value = 'edit'
  evaluatingItems.value = [...evaluatableItems.value]
  selectedEvaluationDimensions.value = []
  evaluationError.value = ''
  evaluationDialogOpen.value = true
}
function urgeShipment() {
  shipmentNoticeOpen.value = true
}
function openEvaluationDetails() {
  evaluationDialogMode.value = 'view'
  evaluatingItems.value = [...evaluatedItems.value]
  evaluationError.value = ''
  evaluationDialogOpen.value = true
}
function closeEvaluation() {
  if (acting.value) return
  evaluationDialogOpen.value = false
  evaluatingItems.value = []
  selectedEvaluationDimensions.value = []
  evaluationError.value = ''
}
async function submitEvaluation() {
  if (evaluatingItems.value.length === 0 || selectedEvaluationDimensions.value.length === 0) {
    evaluationError.value = '请至少选择一项评价'
    return
  }
  acting.value = true
  evaluationError.value = ''
  error.value = ''
  success.value = ''
  try {
    await api.submitOrderEvaluation(props.id, {
      dimensions: selectedEvaluationDimensions.value,
    })
    success.value = '订单评价已提交'
    evaluationDialogOpen.value = false
    evaluatingItems.value = []
    selectedEvaluationDimensions.value = []
    await loadOrder()
  } catch (requestError) {
    evaluationError.value = requestError.message
    await loadOrder()
    evaluatingItems.value = [...evaluatableItems.value]
    if (evaluatingItems.value.length === 0) {
      evaluationDialogOpen.value = false
      selectedEvaluationDimensions.value = []
      success.value = '订单评价已提交'
    }
  } finally {
    acting.value = false
  }
}
async function confirmOrderReceipt() {
  if (!canConfirmOrder.value) {
    receiptNoticeOpen.value = true
    return
  }
  acting.value = true
  error.value = ''
  success.value = ''
  try {
    await api.confirmOrderReceipt(props.id)
    success.value = '订单已确认收货'
    await loadOrder()
  } catch (requestError) {
    error.value = requestError.message
    await loadOrder()
  } finally {
    acting.value = false
  }
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
                <h2>{{ detail.promoterName ? `${detail.promoterName}` : '认证团长' }}</h2>
              </div>
            </div>
            <article v-for="item in detail.details" :key="item.orderDetailId" class="order-product-row">
              <img :src="item.imageUrl || '/images/homepic.png'" :alt="item.productName"
                :class="{ 'fallback-photo-tint': !item.imageUrl }" @error="handleProductImageError" />
              <div><strong>{{ item.productName }}</strong></div><span>{{ money(item.unitPrice) }} × {{ item.quantity }}</span><strong>{{
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
            <button v-if="showConfirmReceipt" class="btn order-lifecycle-action" type="button"
              :disabled="acting" @click="confirmOrderReceipt">
              <CheckCircle2 :size="17" />确认收货
            </button>
            <button v-else-if="showUrgeShipment" class="btn order-lifecycle-action" type="button"
              :disabled="acting" @click="urgeShipment">
              <Clock3 :size="17" />催发货
            </button>
            <button v-else-if="canEvaluateOrder" class="btn order-lifecycle-action" type="button"
              :disabled="acting" @click="openEvaluation">
              <BadgeCheck :size="17" />去评价
            </button>
            <button v-else-if="isOrderEvaluated" class="btn order-lifecycle-action" type="button"
              :disabled="acting" @click="openEvaluationDetails">
              <BadgeCheck :size="17" />查看评价
            </button>
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

    <div v-if="receiptNoticeOpen" class="cancel-dialog-backdrop" role="presentation" @click.self="receiptNoticeOpen = false">
      <section class="cancel-dialog" role="alertdialog" aria-modal="true" aria-labelledby="receipt-notice-title">
        <button class="cancel-dialog-close" type="button" aria-label="关闭" @click="receiptNoticeOpen = false"><X :size="20" /></button>
        <span class="cancel-dialog-icon receipt-notice-icon"><AlertTriangle :size="30" /></span>
        <h2 id="receipt-notice-title">暂不能确认收货</h2>
        <p>订单包裹尚未全部签收，请在物流显示全部已签收后再确认收货。</p>
        <div><button class="btn btn-buy" type="button" @click="receiptNoticeOpen = false">我知道了</button></div>
      </section>
    </div>

    <div v-if="shipmentNoticeOpen" class="cancel-dialog-backdrop" role="presentation" @click.self="shipmentNoticeOpen = false">
      <section class="cancel-dialog" role="alertdialog" aria-modal="true" aria-labelledby="shipment-notice-title">
        <button class="cancel-dialog-close" type="button" aria-label="关闭" @click="shipmentNoticeOpen = false"><X :size="20" /></button>
        <span class="cancel-dialog-icon shipment-notice-icon"><Clock3 :size="30" /></span>
        <h2 id="shipment-notice-title">已催促发货</h2>
        <p>已为您催促尽快发货，请耐心等待物流更新。</p>
        <div><button class="btn btn-buy" type="button" @click="shipmentNoticeOpen = false">我知道了</button></div>
      </section>
    </div>

    <div v-if="evaluationDialogOpen" class="cancel-dialog-backdrop" role="presentation" @click.self="closeEvaluation">
      <section class="cancel-dialog evaluation-dialog" role="dialog" aria-modal="true" aria-labelledby="evaluation-dialog-title">
        <button class="cancel-dialog-close" type="button" aria-label="关闭" :disabled="acting" @click="closeEvaluation"><X :size="20" /></button>
        <h2 id="evaluation-dialog-title">{{ evaluationDialogMode === 'view' ? '查看评价' : '评价订单' }}</h2>
        <template v-if="evaluationDialogMode === 'view'">
          <div class="evaluation-records">
            <article v-for="item in evaluatingItems" :key="item.orderDetailId">
              <strong>{{ item.productName }}</strong>
              <time>{{ date(item.evaluatedAt) }}</time>
              <div>
                <span v-for="code in item.evaluationDimensions" :key="code">{{ evaluationDimensions.find((entry) => entry.code === code)?.name || code }}</span>
              </div>
            </article>
          </div>
          <div><button class="btn btn-buy" type="button" @click="closeEvaluation">关闭</button></div>
        </template>
        <template v-else>
        <p>{{ evaluatingItems.map((item) => item.productName).join('、') }}</p>
        <fieldset class="evaluation-options">
          <legend>请选择符合本次体验的评价（可多选）</legend>
          <label v-for="dimension in evaluationDimensions" :key="dimension.code"
            :class="{ selected: selectedEvaluationDimensions.includes(dimension.code) }">
            <input v-model="selectedEvaluationDimensions" type="checkbox" :value="dimension.code" />
            <Check :size="15" />{{ dimension.name }}
          </label>
        </fieldset>
        <div v-if="evaluationError" class="evaluation-dialog-error" role="alert">{{ evaluationError }}</div>
        <div><button class="btn btn-outline-secondary" type="button" :disabled="acting" @click="closeEvaluation">取消</button><button
            class="btn btn-buy" type="button" :disabled="acting || selectedEvaluationDimensions.length === 0"
            @click="submitEvaluation"><span v-if="acting" class="spinner-border spinner-border-sm"></span>提交评价</button></div>
        </template>
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
  -- timeline-green: #3ba35e;
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

.order-product-row>img {
  width: 72px;
  height: 72px;
  object-fit: cover;
}

.order-product-row>div {
  display: flex;
  min-width: 0;
  flex-direction: column;
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

.order-detail-actions .order-lifecycle-action {
  border: 1px solid var(--brand);
  background: transparent;
  color: var(--brand);
}

.order-detail-actions .order-lifecycle-action:hover,
.order-detail-actions .order-lifecycle-action:focus-visible {
  border-color: var(--brand);
  background: var(--brand);
  color: #fff;
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

  .order-product-row>img {
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

.evaluation-dialog {
  width: min(520px, 100%);
  text-align: left;
}

.evaluation-dialog>h2,
.evaluation-dialog>p {
  text-align: center;
}

.receipt-notice-icon,
.shipment-notice-icon {
  background: #e8f8f1;
  color: var(--brand);
}

.evaluation-records {
  display: grid;
  gap: 12px;
  margin-top: 18px;
}

.evaluation-records article {
  padding: 14px;
  border: 1px solid var(--line);
  border-radius: 12px;
  background: var(--soft);
}

.evaluation-records article>strong,
.evaluation-records article>time {
  display: block;
}

.evaluation-records article>time {
  margin-top: 3px;
  color: var(--muted);
  font-size: 10px;
}

.evaluation-records article>div {
  display: flex;
  flex-wrap: wrap;
  gap: 7px;
  margin-top: 10px;
}

.evaluation-records article span {
  padding: 5px 9px;
  border-radius: 999px;
  background: #e8f8f1;
  color: var(--brand);
  font-size: 10px;
  font-weight: 700;
}

.evaluation-options {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px;
  margin-top: 20px;
  padding: 0;
  border: 0;
}

.evaluation-options legend {
  grid-column: 1 / -1;
  margin-bottom: 2px;
  color: var(--muted);
  font-size: 10px;
}

.evaluation-options label {
  display: flex;
  min-height: 42px;
  align-items: center;
  gap: 7px;
  padding: 0 12px;
  border: 1px solid var(--line);
  border-radius: 6px;
  color: var(--ink);
  cursor: pointer;
  font-size: 11px;
  font-weight: 700;
}

.evaluation-options label.selected {
  border-color: #13b86c;
  background: #e8f8f1;
  color: #087a49;
}

.evaluation-options input {
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip: rect(0 0 0 0);
}

.evaluation-options label svg {
  opacity: 0;
}

.evaluation-options label.selected svg {
  opacity: 1;
}

.evaluation-dialog-error {
  justify-content: flex-start !important;
  margin-top: 12px !important;
  color: var(--danger);
  font-size: 10px;
}

@media (max-width: 479.98px) {
  .evaluation-options {
    grid-template-columns: 1fr;
  }
}
</style>
