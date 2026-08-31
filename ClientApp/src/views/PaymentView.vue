<script setup>
import { Building2, CheckCircle2, Clock3, CreditCard, ShieldCheck, Smartphone } from '@lucide/vue'
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import StoreBreadcrumb from '../components/StoreBreadcrumb.vue'
import { api } from '../services/api'

const props = defineProps({ batchId: { type: String, required: true } })
const methods = [
  { value: 'WECHAT', label: '微信支付', note: '模拟微信确认支付', icon: Smartphone },
  { value: 'ALIPAY', label: '支付宝', note: '模拟支付宝确认支付', icon: Smartphone },
  { value: 'BANK_CARD', label: '银行卡', note: '选择银行后确认', icon: CreditCard },
]
const banks = ['中国工商银行', '中国农业银行', '中国银行', '中国建设银行', '交通银行', '招商银行', '中国邮政储蓄银行', '浦发银行']
const summary = ref(null)
const selectedMethod = ref('WECHAT')
const selectedBank = ref(banks[0])
const loading = ref(true)
const paying = ref(false)
const error = ref('')
const paymentResult = ref(null)
const now = ref(Date.now())
let timer

const secondsLeft = computed(() => Math.max(0, Math.floor((new Date(summary.value?.paymentExpiresAt ?? 0).getTime() - now.value) / 1000)))
const countdown = computed(() => `${String(Math.floor(secondsLeft.value / 60)).padStart(2, '0')}:${String(secondsLeft.value % 60).padStart(2, '0')}`)
const canPay = computed(() => summary.value?.orderStatus === 'PENDING_PAYMENT' && secondsLeft.value > 0 && !paymentResult.value)
function money(value) { return `¥${Number(value ?? 0).toFixed(2)}` }

async function loadBatch() {
  loading.value = true; error.value = ''
  try { summary.value = await api.getCheckoutBatch(props.batchId) } catch (requestError) { error.value = requestError.message } finally { loading.value = false }
}

async function confirmPayment() {
  paying.value = true; error.value = ''
  try {
    paymentResult.value = await api.payCheckoutBatch(props.batchId, {
      paymentMethod: selectedMethod.value,
      bankName: selectedMethod.value === 'BANK_CARD' ? selectedBank.value : null,
    })
    await loadBatch()
  } catch (requestError) {
    const message = requestError.message
    await loadBatch()
    error.value = message
  } finally { paying.value = false }
}

onMounted(() => { loadBatch(); timer = window.setInterval(() => { now.value = Date.now() }, 1000) })
onBeforeUnmount(() => window.clearInterval(timer))
</script>

<template>
  <div class="store-container page-space payment-page">
    <StoreBreadcrumb :items="[{ label: '我的订单', to: '/orders' }, { label: '模拟支付' }]" />
    <div v-if="error" class="alert alert-danger">{{ error }}</div>
    <div v-if="loading" class="store-loading"><span class="spinner-border spinner-border-sm"></span>正在读取结算批次</div>
    <section v-else-if="summary" class="payment-layout">
      <div class="payment-main">
        <header><div><small>模拟支付收银台</small><h1>支付整个结算批次</h1><p>{{ summary.childOrderCount }} 个团长子订单将一次性完成支付</p></div><ShieldCheck :size="42" /></header>
        <div v-if="paymentResult || summary.orderStatus === 'PAID'" class="payment-finished"><CheckCircle2 :size="52" /><h2>支付成功</h2><p>整个结算批次已支付，所有子订单已进入备货流程。</p><p v-if="paymentResult?.pointsEarned">本次实付商品金额累计 {{ paymentResult.pointsEarned }} 积分（不含运费）</p><strong v-if="paymentResult?.transactionNo">模拟交易号 {{ paymentResult.transactionNo }}</strong><RouterLink class="btn btn-buy" :to="`/orders/${summary.orders[0].orderId}`">查看订单</RouterLink></div>
        <template v-else>
          <section class="method-section"><h2>选择支付方式</h2><label v-for="method in methods" :key="method.value" class="method-card" :class="{ active: selectedMethod === method.value }"><input v-model="selectedMethod" type="radio" :value="method.value" /><component :is="method.icon" :size="24" /><span><strong>{{ method.label }}</strong><small>{{ method.note }}</small></span></label></section>
          <section v-if="selectedMethod === 'BANK_CARD'" class="bank-section"><label for="payment-bank"><Building2 :size="19" />选择银行</label><select id="payment-bank" v-model="selectedBank" class="form-select"><option v-for="bank in banks" :key="bank" :value="bank">{{ bank }}</option></select><p>仅展示银行列表并模拟确认，不采集卡号、密码或验证码。</p></section>
        </template>
      </div>
      <aside><div class="countdown" :class="{ expired: secondsLeft === 0 }"><Clock3 :size="20" /><span>支付剩余时间</span><strong>{{ countdown }}</strong></div><dl><div><dt>结算批次</dt><dd>{{ summary.checkoutBatchId }}</dd></div><div><dt>团长子订单</dt><dd>{{ summary.childOrderCount }} 个</dd></div><div class="pay-total"><dt>应付总额</dt><dd>{{ money(summary.finalAmount) }}</dd></div></dl><button v-if="!paymentResult && summary.orderStatus !== 'PAID'" class="btn btn-buy confirm-pay" type="button" :disabled="!canPay || paying" @click="confirmPayment"><span v-if="paying" class="spinner-border spinner-border-sm"></span>{{ secondsLeft === 0 ? '支付已超时' : '确认模拟支付' }}</button><p class="simulation-note">本项目使用模拟支付，不会发起真实扣款。</p></aside>
    </section>
  </div>
</template>

<style scoped>
.payment-layout { display: grid; grid-template-columns: minmax(0, 1fr) 330px; gap: 16px; align-items: start; }
.payment-main, .payment-layout > aside { border: 1px solid var(--line); background: #fff; }
.payment-main > header { display: flex; align-items: center; justify-content: space-between; padding: 24px; border-bottom: 1px solid var(--line); color: var(--brand); }
.payment-main header small { letter-spacing: 2px; }
.payment-main h1 { margin: 4px 0; color: var(--ink); font-size: 25px; }
.payment-main header p, .bank-section p { margin: 0; color: var(--muted); font-size: 10px; }
.method-section, .bank-section { padding: 22px 24px; }
.method-section h2 { margin: 0 0 14px; font-size: 16px; }
.method-card { display: flex; align-items: center; gap: 12px; margin-bottom: 10px; padding: 15px; border: 1px solid var(--line); cursor: pointer; }
.method-card.active { border-color: var(--brand); background: #f0f7f4; color: var(--brand); }
.method-card span { display: flex; flex-direction: column; }
.method-card small { margin-top: 2px; color: var(--muted); font-size: 9px; }
.bank-section { border-top: 1px solid var(--line); }
.bank-section label { display: flex; align-items: center; gap: 7px; margin-bottom: 9px; font-weight: 700; }
.bank-section p { margin-top: 8px; }
.payment-layout > aside { padding: 20px; }
.countdown { display: grid; grid-template-columns: auto 1fr; gap: 3px 8px; align-items: center; padding: 14px; background: #fff8e7; color: #9a6400; }
.countdown strong { grid-column: 2; font-size: 23px; }
.countdown.expired { background: #fbecec; color: var(--danger); }
.payment-layout dl { margin: 15px 0; }
.payment-layout dl > div { display: flex; justify-content: space-between; gap: 12px; padding: 9px 0; border-bottom: 1px solid var(--line); }
.payment-layout dt { color: var(--muted); font-size: 9px; }
.payment-layout dd { margin: 0; max-width: 190px; overflow-wrap: anywhere; font-size: 10px; text-align: right; }
.pay-total dd { color: var(--danger); font-size: 21px; font-weight: 800; }
.confirm-pay { width: 100%; justify-content: center; }
.simulation-note { margin: 10px 0 0; color: var(--muted); font-size: 9px; text-align: center; }
.payment-finished { display: flex; min-height: 390px; flex-direction: column; align-items: center; justify-content: center; padding: 25px; color: var(--brand); text-align: center; }
.payment-finished h2 { margin: 12px 0 5px; color: var(--ink); }
.payment-finished p { color: var(--muted); }
.payment-finished strong { margin-bottom: 18px; font-size: 10px; }
@media (max-width: 767.98px) { .payment-layout { grid-template-columns: 1fr; } }
</style>
