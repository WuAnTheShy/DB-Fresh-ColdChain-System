<script setup>
import { AlertCircle, CheckCircle2, ChevronLeft, RotateCcw, ShieldCheck, X } from '@lucide/vue'
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { api } from '../services/api'

const props = defineProps({ id: { type: String, required: true } })
const orderDetail = ref(null)
const refunds = ref([])
const loading = ref(true)
const submitting = ref(false)
const cancellingRefundId = ref('')
const error = ref('')
const success = ref('')
const selections = reactive({})
const remark = ref('')
const reviewDialogOpen = ref(false)
const historyExpanded = ref(false)
const refundPreview = ref(null)
const previewLoading = ref(false)
let previewTimer = 0
let previewRequestId = 0

const order = computed(() => orderDetail.value?.order)
const selectedEntries = computed(() => (orderDetail.value?.details ?? [])
  .filter(item => selections[item.orderDetailId]?.selected)
  .map(item => ({ item, quantity: Number(selections[item.orderDetailId].quantity) })))
const activeRefunds = computed(() => refunds.value.filter(item => ['Pending', 'Approved'].includes(item.status)))
const pendingRefunds = computed(() => refunds.value.filter(item => item.status === 'Pending'))
const foldedRefunds = computed(() => refunds.value.filter(item => item.status !== 'Pending'))
const hasPendingWholeOrder = computed(() => refunds.value.some(item => (
  item.status === 'Pending' && !item.detailId
)))
const shipped = computed(() => ['SHIPPED', 'COMPLETED', 'REFUNDING', 'REFUND_REVIEWING'].includes(order.value?.orderStatus))
const isFullRefund = computed(() => (
  selectedEntries.value.length > 0 &&
  selectedEntries.value.length === orderDetail.value?.details.length &&
  selectedEntries.value.every(({ item, quantity }) => quantity === Number(item.quantity))
))
const refundScopeLabel = computed(() => isFullRefund.value ? '整单退款' : '部分退款')
const discountRate = computed(() => Math.max(0, Number(order.value?.totalAmount ?? 0) - Number(order.value?.discountAmount ?? 0)) / Math.max(0.01, Number(order.value?.totalAmount ?? 0)))
const estimatedGoodsRefund = computed(() => {
  if (!order.value) return 0
  if (isFullRefund.value) return Math.max(0, Number(order.value.finalAmount) - Number(order.value.freightAmount))
  return selectedEntries.value.reduce((sum, { item, quantity }) => (
    sum + Number(item.unitPrice ?? 0) * quantity * discountRate.value
  ), 0)
})
const estimatedRefund = computed(() => {
  if (refundPreview.value) return Number(refundPreview.value.refundAmount ?? 0)
  return estimatedGoodsRefund.value + (isFullRefund.value && !shipped.value ? Number(order.value?.freightAmount ?? 0) : 0)
})
function money(value) { return `¥${Number(value ?? 0).toFixed(2)}` }
function statusName(status) { return ({ Pending: '待平台审核', Approved: '审核通过', Rejected: '已驳回', Cancelled: '已取消' })[status] ?? status }
function refundProductName(refund) {
  if (!refund.detailId) return '整单退款'
  return orderDetail.value?.details.find(item => item.orderDetailId === refund.detailId)?.productName ?? '商品退款'
}
function occupiedQuantity(item) {
  return activeRefunds.value
    .filter(refund => refund.detailId === item.orderDetailId)
    .reduce((sum, refund) => sum + Number(refund.refundQty || 0), 0)
}
function remainingQuantity(item) { return Math.max(0, Number(item.quantity) - occupiedQuantity(item)) }
function clampQuantity(item) {
  const selection = selections[item.orderDetailId]
  const remaining = remainingQuantity(item)
  selection.quantity = remaining > 0
    ? Math.min(Math.max(1, Number(selection.quantity || 1)), remaining)
    : 0
}

const previewSignature = computed(() => JSON.stringify(selectedEntries.value.map(({ item, quantity }) => ({
  productID: item.productId,
  refundQty: quantity,
}))))

watch(previewSignature, (signature) => {
  window.clearTimeout(previewTimer)
  refundPreview.value = null
  const requestId = ++previewRequestId
  const items = JSON.parse(signature)
  if (!order.value || items.length === 0) {
    previewLoading.value = false
    return
  }
  previewLoading.value = true
  previewTimer = window.setTimeout(async () => {
    try {
      const result = await api.previewOrderRefund(props.id, { items })
      if (requestId === previewRequestId) refundPreview.value = result
    } catch {
      // 试算失败时保留前端商品金额估算，正式提交仍由服务端校验。
    } finally {
      if (requestId === previewRequestId) previewLoading.value = false
    }
  }, 250)
}, { flush: 'post' })

async function load() {
  loading.value = true; error.value = ''
  try {
    const [detail, history] = await Promise.all([api.getOrder(props.id), api.getOrderRefunds(props.id)])
    orderDetail.value = detail
    refunds.value = history.refunds ?? []
    detail.details.forEach((item) => {
      if (!selections[item.orderDetailId]) selections[item.orderDetailId] = { selected: false, quantity: 1 }
      if (remainingQuantity(item) === 0) selections[item.orderDetailId].selected = false
      clampQuantity(item)
    })
  } catch (requestError) { error.value = requestError.message } finally { loading.value = false }
}

async function submitRefund() {
  submitting.value = true; error.value = ''; success.value = ''
  try {
    await api.applyOrderRefund(props.id, {
      items: selectedEntries.value.map(({ item, quantity }) => ({
        productID: item.productId,
        refundQty: quantity,
      })),
      remark: remark.value,
    })
    Object.values(selections).forEach((selection) => { selection.selected = false })
    remark.value = ''
    await load()
    reviewDialogOpen.value = true
  } catch (requestError) { error.value = requestError.message } finally { submitting.value = false }
}

async function cancelRefund(refundId) {
  cancellingRefundId.value = refundId; error.value = ''; success.value = ''
  try {
    const response = await api.cancelOrderRefund(props.id, refundId)
    success.value = response.message
    await load()
  } catch (requestError) { error.value = requestError.message } finally { cancellingRefundId.value = '' }
}
onMounted(load)
</script>

<template>
  <div class="store-container page-space refund-page">
    <RouterLink class="refund-back" :to="`/orders/${id}`"><ChevronLeft :size="17" />返回订单详情</RouterLink>
    <div v-if="error" class="alert alert-danger">{{ error }}</div><div v-if="success" class="alert alert-success">{{ success }}</div>
    <div v-if="loading" class="store-loading"><span class="spinner-border spinner-border-sm"></span>正在读取售后信息</div>
    <div v-else-if="order" class="refund-layout">
      <main>
        <header><RotateCcw :size="32" /><div><small>消费者售后</small><h1>提交退款申请</h1><p>申请提交后由平台审核，不会立即退款。</p></div></header>
        <div v-if="hasPendingWholeOrder" class="pending-warning"><AlertCircle :size="20" /><span>该订单已有整单退款申请，取消申请或等待平台处理后才能继续操作。</span></div>
        <div v-else-if="refunds.some(item => item.status === 'Pending')" class="partial-pending-note"><ShieldCheck :size="18" /><span>部分商品正在审核，其余商品及尚未申请的数量仍可继续退款。</span></div>
        <form @submit.prevent="submitRefund">
          <fieldset :disabled="hasPendingWholeOrder || submitting">
            <legend>填写退货信息</legend>
            <div class="refund-products">
              <article v-for="item in orderDetail.details" :key="item.orderDetailId" :class="{ selected: selections[item.orderDetailId]?.selected, exhausted: remainingQuantity(item) === 0 }">
                <label class="product-choice"><input v-model="selections[item.orderDetailId].selected" type="checkbox" :disabled="remainingQuantity(item) === 0" /><span><strong>{{ item.productName }}</strong><small>{{ money(item.unitPrice) }} × {{ item.quantity }} 件 · {{ remainingQuantity(item) > 0 ? `还可申请 ${remainingQuantity(item)} 件` : '已无可申请数量' }}</small></span></label>
                <label class="product-quantity"><span>退货数量</span><input v-model.number="selections[item.orderDetailId].quantity" class="form-control" type="number" min="1" :max="remainingQuantity(item)" :disabled="!selections[item.orderDetailId].selected || remainingQuantity(item) === 0" @input="clampQuantity(item)" /></label>
              </article>
            </div>
            <div class="scope-hint"><ShieldCheck :size="18" /><span>系统将根据退货数量自动识别退款方式，当前预计按<strong>{{ refundScopeLabel }}</strong>提交。</span></div>
            <label class="reason-label">退款原因<textarea v-model.trim="remark" class="form-control" rows="4" maxlength="200" required placeholder="请说明商品问题或退款原因"></textarea><small>{{ remark.length }}/200</small></label>
            <button class="btn btn-buy submit-refund" type="submit" :disabled="!remark || !selectedEntries.length || hasPendingWholeOrder || submitting"><span v-if="submitting" class="spinner-border spinner-border-sm"></span>提交平台审核</button>
          </fieldset>
        </form>
      </main>
      <aside>
        <section>
          <h2>预计退款</h2>
          <strong class="refund-amount">{{ previewLoading ? '计算中…' : money(estimatedRefund) }}</strong>
          <dl>
            <div><dt>商品退款</dt><dd>{{ refundPreview ? money(refundPreview.goodsRefundAmount) : money(estimatedGoodsRefund) }}</dd></div>
            <div><dt>退回运费</dt><dd>{{ previewLoading ? '计算中…' : money(refundPreview?.freightRefundAmount) }}</dd></div>
            <div><dt>原订单运费</dt><dd>{{ money(order.freightAmount) }}</dd></div>
          </dl>
          <p v-if="shipped">发货后运费始终不退还；部分退款按该商品已支付金额自动计算。</p>
          <p v-else>未发货部分退款会退回本次商品对应增加的运费。</p>
        </section>
        <section>
          <h2>平台审核记录</h2>
          <div v-if="!pendingRefunds.length" class="empty-history">暂无待审核申请</div>
          <article v-for="record in pendingRefunds" :key="record.refundId">
            <span>{{ statusName(record.status) }}</span><strong>{{ money(record.refundAmount) }}</strong>
            <small><b>{{ refundProductName(record) }}</b> · {{ record.refundQty ? `${record.refundQty} 件` : '全部商品' }}</small>
            <small>{{ record.remark }}</small>
            <button class="btn btn-buy cancel-refund-button" type="button" :disabled="cancellingRefundId === record.refundId" @click="cancelRefund(record.refundId)"><span v-if="cancellingRefundId === record.refundId" class="spinner-border spinner-border-sm"></span><template v-else>取消申请</template></button>
          </article>
          <button v-if="foldedRefunds.length" class="history-toggle" type="button" :aria-expanded="historyExpanded" @click="historyExpanded = !historyExpanded">
            {{ historyExpanded ? '收起其他记录' : `查看其他记录（${foldedRefunds.length}）` }}
          </button>
          <div v-if="historyExpanded" class="folded-history">
            <article v-for="record in foldedRefunds" :key="record.refundId">
              <span>{{ statusName(record.status) }}</span><strong>{{ money(record.refundAmount) }}</strong>
              <small><b>{{ refundProductName(record) }}</b> · {{ record.refundQty ? `${record.refundQty} 件` : '全部商品' }}</small>
              <small>{{ record.remark }}</small>
            </article>
          </div>
        </section>
        <div class="review-note"><ShieldCheck :size="19" />所有申请均由平台审核</div>
      </aside>
    </div>

    <div v-if="reviewDialogOpen" class="review-dialog-backdrop" role="presentation" @click.self="reviewDialogOpen = false">
      <section class="review-dialog" role="dialog" aria-modal="true" aria-labelledby="review-dialog-title">
        <button class="review-dialog-close" type="button" aria-label="关闭" @click="reviewDialogOpen = false"><X :size="20" /></button>
        <span class="review-dialog-icon"><CheckCircle2 :size="34" /></span>
        <h2 id="review-dialog-title">退款申请已提交</h2>
        <small>审核结果将在订单状态和消息中心显示，审核通过前不会立即退款。</small>
        <div><RouterLink class="btn btn-buy" :to="`/orders/${id}`">返回订单详情</RouterLink><button class="btn btn-outline-secondary" type="button" @click="reviewDialogOpen = false">留在当前页面</button></div>
      </section>
    </div>
  </div>
</template>

<style scoped>
.refund-back { display: inline-flex; align-items: center; gap: 3px; margin-bottom: 12px; color: var(--brand); font-size: 10px; }
.refund-layout { display: grid; grid-template-columns: minmax(0, 1fr) 330px; gap: 16px; align-items: start; }
.refund-layout main, .refund-layout aside > section { border: 1px solid var(--line); background: #fff; }
.refund-layout main > header { display: flex; align-items: center; gap: 14px; padding: 22px; border-bottom: 1px solid var(--line); color: var(--brand); }
.refund-layout h1 { margin: 2px 0; color: var(--ink); font-size: 24px; }.refund-layout header p { margin: 0; color: var(--muted); font-size: 10px; }
.refund-layout form { padding: 22px; }.refund-layout fieldset { padding: 0; border: 0; }.refund-layout legend { margin-bottom: 12px; font-size: 15px; font-weight: 800; }
.refund-products { display: flex; flex-direction: column; gap: 9px; margin-bottom: 14px; }.refund-products article { display: grid; grid-template-columns: minmax(0, 1fr) 130px; gap: 14px; align-items: center; padding: 13px; border: 1px solid var(--line); background: #fff; }.refund-products article.selected { border-color: #74aa95; background: #f5faf7; }.refund-products article.exhausted { background: #f5f6f5; opacity: .7; }.product-choice { display: flex; align-items: center; gap: 10px; cursor: pointer; }.product-choice > input { width: 17px; height: 17px; accent-color: var(--brand); }.product-choice > span { display: flex; min-width: 0; flex-direction: column; }.product-choice strong { font-size: 11px; }.product-choice small { margin-top: 3px; color: var(--muted); font-size: 9px; }.product-quantity { display: grid; grid-template-columns: auto 72px; gap: 7px; align-items: center; color: var(--muted); font-size: 9px; }.product-quantity input { height: 34px; }.reason-label { font-size: 10px; font-weight: 700; }.reason-label textarea { margin-top: 6px; }
.scope-hint { display: flex; align-items: center; gap: 8px; padding: 12px; background: #edf6f2; color: #365d4e; font-size: 10px; }.scope-hint strong { margin: 0 3px; color: var(--brand); }
.reason-label { position: relative; display: block; margin-top: 16px; }.reason-label > small { position: absolute; right: 8px; bottom: 6px; color: var(--muted); }.submit-refund { margin-top: 15px; }
.pending-warning { display: flex; align-items: center; gap: 8px; margin: 18px 22px 0; padding: 12px; background: #fff7df; color: #8a6200; font-size: 10px; }
.partial-pending-note { display: flex; align-items: center; gap: 8px; margin: 18px 22px 0; padding: 12px; background: #edf6f2; color: #365d4e; font-size: 10px; }
.refund-layout aside { display: flex; flex-direction: column; gap: 12px; }.refund-layout aside > section { padding: 18px; }.refund-layout aside h2 { margin: 0 0 10px; font-size: 15px; }.refund-amount { color: var(--danger); font-size: 28px; }.refund-layout dl { margin: 12px 0 0; }.refund-layout dl > div { display: flex; justify-content: space-between; padding: 7px 0; border-top: 1px solid var(--line); }.refund-layout dt { color: var(--muted); font-size: 9px; }.refund-layout dd { margin: 0; font-size: 10px; }.refund-layout aside p, .empty-history { color: var(--muted); font-size: 9px; }
.refund-layout aside article { display: grid; grid-template-columns: 1fr auto; gap: 4px; padding: 10px 0; border-top: 1px solid var(--line); }.refund-layout aside article > span { color: var(--brand); font-size: 9px; font-weight: 700; }.refund-layout aside article small { grid-column: 1 / -1; color: var(--muted); }.cancel-refund-button { grid-column: 1 / -1; justify-self: start; margin-top: 7px; padding: 7px 14px; font-size: 9px; font-weight: 700; }.history-toggle { width: 100%; padding: 9px 0 2px; border: 0; border-top: 1px solid var(--line); background: transparent; color: var(--brand); font-size: 9px; font-weight: 700; text-align: left; }.folded-history { margin-top: 7px; }.review-note { display: flex; align-items: center; gap: 7px; padding: 13px; background: #edf6f2; color: var(--brand); font-size: 10px; font-weight: 700; }
.review-dialog-backdrop { position: fixed; z-index: 1080; display: grid; padding: 18px; background: rgba(10, 25, 20, .52); inset: 0; place-items: center; }.review-dialog { position: relative; width: min(440px, 100%); padding: 30px; border-radius: 10px; background: #fff; box-shadow: 0 20px 60px rgba(0, 0, 0, .22); text-align: center; }.review-dialog-close { position: absolute; top: 10px; right: 10px; display: inline-flex; padding: 5px; border: 0; background: transparent; color: var(--muted); }.review-dialog-icon { display: inline-flex; width: 58px; height: 58px; align-items: center; justify-content: center; border-radius: 50%; background: #e5f5ed; color: #248158; }.review-dialog h2 { margin: 14px 0 7px; font-size: 21px; }.review-dialog p { margin: 0; color: var(--ink); font-size: 11px; line-height: 1.7; }.review-dialog > small { display: block; margin-top: 8px; color: var(--muted); font-size: 9px; line-height: 1.6; }.review-dialog > div { display: flex; justify-content: center; gap: 8px; margin-top: 20px; }
@media (max-width: 767.98px) { .refund-layout { grid-template-columns: 1fr; }.refund-products article { grid-template-columns: 1fr; }.product-quantity { justify-content: start; } }
</style>
