<script setup>
import { AlertCircle, ChevronLeft, RotateCcw, ShieldCheck } from '@lucide/vue'
import { computed, onMounted, ref } from 'vue'
import { api } from '../services/api'

const props = defineProps({ id: { type: String, required: true } })
const orderDetail = ref(null)
const refunds = ref([])
const loading = ref(true)
const submitting = ref(false)
const error = ref('')
const success = ref('')
const refundType = ref('partial')
const selectedDetailId = ref('')
const quantity = ref(1)
const remark = ref('')

const order = computed(() => orderDetail.value?.order)
const selectedDetail = computed(() => orderDetail.value?.details.find(item => item.orderDetailId === selectedDetailId.value))
const hasPending = computed(() => refunds.value.some(item => item.status === 'Pending'))
const shipped = computed(() => ['SHIPPED', 'COMPLETED', 'REFUNDING'].includes(order.value?.orderStatus))
const discountRate = computed(() => Math.max(0, Number(order.value?.totalAmount ?? 0) - Number(order.value?.discountAmount ?? 0)) / Math.max(0.01, Number(order.value?.totalAmount ?? 0)))
const estimatedRefund = computed(() => {
  if (!order.value) return 0
  if (refundType.value === 'full') return Math.max(0, Number(order.value.finalAmount) - (shipped.value ? Number(order.value.freightAmount) : 0))
  return Number(selectedDetail.value?.unitPrice ?? 0) * quantity.value * discountRate.value
})
function money(value) { return `¥${Number(value ?? 0).toFixed(2)}` }
function statusName(status) { return ({ Pending: '待平台审核', Approved: '审核通过', Rejected: '已驳回' })[status] ?? status }
function clampQuantity() { quantity.value = Math.min(Math.max(1, Number(quantity.value || 1)), Number(selectedDetail.value?.quantity ?? 1)) }

async function load() {
  loading.value = true; error.value = ''
  try {
    const [detail, history] = await Promise.all([api.getOrder(props.id), api.getOrderRefunds(props.id)])
    orderDetail.value = detail
    refunds.value = history.refunds ?? []
    if (!selectedDetailId.value && detail.details.length) selectedDetailId.value = detail.details[0].orderDetailId
    clampQuantity()
  } catch (requestError) { error.value = requestError.message } finally { loading.value = false }
}

async function submitRefund() {
  submitting.value = true; error.value = ''; success.value = ''
  try {
    const response = await api.applyOrderRefund(props.id, {
      productID: refundType.value === 'partial' ? selectedDetail.value?.productId : null,
      refundQty: refundType.value === 'partial' ? quantity.value : 0,
      remark: remark.value,
    })
    success.value = response.message
    remark.value = ''
    await load()
  } catch (requestError) { error.value = requestError.message } finally { submitting.value = false }
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
        <div v-if="hasPending" class="pending-warning"><AlertCircle :size="20" /><span>该团长子订单已有待审核申请，请等待平台处理。</span></div>
        <form @submit.prevent="submitRefund">
          <fieldset :disabled="hasPending || submitting">
            <legend>选择退款范围</legend>
            <label class="refund-type"><input v-model="refundType" type="radio" value="partial" /><span><strong>部分退款</strong><small>选择单个商品和退款数量</small></span></label>
            <label class="refund-type"><input v-model="refundType" type="radio" value="full" /><span><strong>本团长子订单整单退款</strong><small>{{ shipped ? '商品金额可退，发货后运费不退' : '支付金额原路模拟退回' }}</small></span></label>
            <div v-if="refundType === 'partial'" class="partial-grid"><label>退款商品<select v-model="selectedDetailId" class="form-select" @change="clampQuantity"><option v-for="item in orderDetail.details" :key="item.orderDetailId" :value="item.orderDetailId">{{ item.productName }}（{{ money(item.unitPrice) }} × {{ item.quantity }}）</option></select></label><label>退款数量<input v-model.number="quantity" class="form-control" type="number" min="1" :max="selectedDetail?.quantity || 1" @input="clampQuantity" /></label></div>
            <label class="reason-label">退款原因<textarea v-model.trim="remark" class="form-control" rows="4" maxlength="200" required placeholder="请说明商品问题或退款原因"></textarea><small>{{ remark.length }}/200</small></label>
            <button class="btn btn-buy submit-refund" type="submit" :disabled="!remark || hasPending || submitting"><span v-if="submitting" class="spinner-border spinner-border-sm"></span>提交平台审核</button>
          </fieldset>
        </form>
      </main>
      <aside><section><h2>预计退款</h2><strong class="refund-amount">{{ money(estimatedRefund) }}</strong><dl><div><dt>商品金额</dt><dd>{{ money(order.totalAmount) }}</dd></div><div><dt>优惠分摊</dt><dd>-{{ money(order.discountAmount) }}</dd></div><div><dt>原订单运费</dt><dd>{{ money(order.freightAmount) }}</dd></div></dl><p v-if="shipped">发货后运费始终不退还；部分退款按该商品已支付金额自动计算。</p></section><section><h2>平台审核记录</h2><div v-if="!refunds.length" class="empty-history">暂无退款申请</div><article v-for="record in refunds" :key="record.refundId"><span>{{ statusName(record.status) }}</span><strong>{{ money(record.refundAmount) }}</strong><small>{{ record.remark }}</small></article></section><div class="review-note"><ShieldCheck :size="19" />所有申请均由平台审核</div></aside>
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
.refund-type { display: flex; align-items: center; gap: 10px; margin-bottom: 9px; padding: 14px; border: 1px solid var(--line); cursor: pointer; }.refund-type span { display: flex; flex-direction: column; }.refund-type small { color: var(--muted); font-size: 9px; }
.partial-grid { display: grid; grid-template-columns: 1fr 140px; gap: 12px; margin: 16px 0; }.partial-grid label, .reason-label { font-size: 10px; font-weight: 700; }.partial-grid select, .partial-grid input, .reason-label textarea { margin-top: 6px; }
.reason-label { position: relative; display: block; margin-top: 16px; }.reason-label > small { position: absolute; right: 8px; bottom: 6px; color: var(--muted); }.submit-refund { margin-top: 15px; }
.pending-warning { display: flex; align-items: center; gap: 8px; margin: 18px 22px 0; padding: 12px; background: #fff7df; color: #8a6200; font-size: 10px; }
.refund-layout aside { display: flex; flex-direction: column; gap: 12px; }.refund-layout aside > section { padding: 18px; }.refund-layout aside h2 { margin: 0 0 10px; font-size: 15px; }.refund-amount { color: var(--danger); font-size: 28px; }.refund-layout dl { margin: 12px 0 0; }.refund-layout dl > div { display: flex; justify-content: space-between; padding: 7px 0; border-top: 1px solid var(--line); }.refund-layout dt { color: var(--muted); font-size: 9px; }.refund-layout dd { margin: 0; font-size: 10px; }.refund-layout aside p, .empty-history { color: var(--muted); font-size: 9px; }
.refund-layout aside article { display: grid; grid-template-columns: 1fr auto; gap: 4px; padding: 10px 0; border-top: 1px solid var(--line); }.refund-layout aside article span { color: var(--brand); font-size: 9px; font-weight: 700; }.refund-layout aside article small { grid-column: 1 / -1; color: var(--muted); }.review-note { display: flex; align-items: center; gap: 7px; padding: 13px; background: #edf6f2; color: var(--brand); font-size: 10px; font-weight: 700; }
@media (max-width: 767.98px) { .refund-layout { grid-template-columns: 1fr; }.partial-grid { grid-template-columns: 1fr; } }
</style>
