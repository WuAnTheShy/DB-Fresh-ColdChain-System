<script setup>
import { BadgeCheck, Check, ChevronRight, MapPin, ShieldCheck, TicketPercent, Truck } from '@lucide/vue'
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import StoreBreadcrumb from '../components/StoreBreadcrumb.vue'
import { api } from '../services/api'
import { useCustomerContext } from '../state/customer'
import { useShop } from '../state/shop'

const router = useRouter()
const { customerId } = useCustomerContext()
const { selectedCartItems, selectedCartCount, selectedCartSubtotal, removeCartItems, setLastOrder } = useShop()
const loading = ref(true)
const saving = ref(false)
const error = ref('')
const addresses = ref([])
const coupons = ref([])
const form = reactive({ addressId: '', couponRecordId: '' })
const groups = computed(() => {
  const map = new Map()
  selectedCartItems.value.forEach((item) => {
    if (!map.has(item.leaderId)) map.set(item.leaderId, { leader: item.leader, items: [] })
    map.get(item.leaderId).items.push(item)
  })
  return [...map.values()]
})

async function loadAssets() {
  loading.value = true
  error.value = ''
  const [addressResult, couponResult] = await Promise.allSettled([
    api.getAddresses(customerId.value),
    api.getCoupons(customerId.value),
  ])
  addresses.value = addressResult.status === 'fulfilled' ? addressResult.value.addresses : []
  coupons.value = couponResult.status === 'fulfilled' ? couponResult.value.availableCoupons : []
  const defaultAddress = addresses.value.find((address) => address.isDefault === 1) ?? addresses.value[0]
  if (defaultAddress) form.addressId = String(defaultAddress.addressId)
  if (addressResult.status === 'rejected') error.value = addressResult.reason.message
  loading.value = false
}

async function submit() {
  if (!selectedCartItems.value.length) return
  saving.value = true
  error.value = ''
  try {
    const merged = new Map()
    selectedCartItems.value.forEach((item) => {
      const existing = merged.get(item.productId)
      merged.set(item.productId, existing
        ? { ...existing, quantity: existing.quantity + item.quantity }
        : {
            productId: item.productId,
            promoterId: item.leaderId,
            quantity: item.quantity,
            clientUnitPrice: item.product.price,
          })
    })
    const result = await api.createOrder({
      customerId: customerId.value,
      addressId: form.addressId,
      couponRecordId: form.couponRecordId || null,
      items: [...merged.values()],
    })
    setLastOrder({
      ...result,
      leaderGroups: groups.value.map((group) => ({ leaderId: group.leader.id, leaderName: group.leader.name })),
    })
    removeCartItems(selectedCartItems.value.map((item) => item.productId))
    router.push(`/order-success/${result.orders[0].orderId}`)
  } catch (requestError) {
    error.value = requestError.message
  } finally {
    saving.value = false
  }
}

onMounted(loadAssets)
</script>

<template>
  <div class="store-container page-space checkout-page">
    <StoreBreadcrumb :items="[{ label: '购物车', to: '/cart' }, { label: '确认订单' }]" />
    <h1 class="checkout-title">确认订单</h1>
    <section v-if="selectedCartItems.length" class="checkout-batch-overview" aria-label="结算批次概览">
      <div><span>本次结算批次</span><strong>{{ groups.length }} 个团长子订单</strong></div>
      <div><span>已选商品</span><strong>{{ selectedCartCount }} 件</strong></div>
      <div><span>商品总额</span><strong>¥{{ selectedCartSubtotal.toFixed(2) }}</strong></div>
      <small>提交后按团长拆分订单；整个批次库存充足时才能确认下单</small>
    </section>
    <div v-if="error" class="alert alert-danger" role="alert">{{ error }}</div>
    <div v-if="loading" class="store-loading"><span class="spinner-border spinner-border-sm"></span>正在准备结算信息</div>

    <form v-else-if="selectedCartItems.length" class="checkout-layout" @submit.prevent="submit">
      <div class="checkout-sections">
        <section class="checkout-section">
          <div class="checkout-section-title"><MapPin :size="21" /><div><h2>收货地址</h2></div><RouterLink to="/addresses">管理地址<ChevronRight :size="15" /></RouterLink></div>
          <div v-if="addresses.length" class="address-choice-grid">
            <label v-for="address in addresses" :key="address.addressId" :class="{ selected: form.addressId === String(address.addressId) }">
              <input v-model="form.addressId" type="radio" :value="String(address.addressId)" />
              <span><strong>{{ address.receiverName }} {{ address.phone }}</strong><small>{{ address.province }}{{ address.city }}{{ address.district }} {{ address.detailAddress }}</small></span><Check :size="18" />
            </label>
          </div>
          <div v-else class="inline-empty">当前账户没有可用地址。<RouterLink to="/addresses">新增收货地址</RouterLink></div>
        </section>

        <section class="checkout-section">
          <div class="checkout-section-title"><Truck :size="21" /><div><h2>配送安排</h2></div></div>
          <div class="delivery-policy"><Truck :size="18" /><span><strong>平台统一安排冷链配送</strong><small>本批次不可选择配送时间；任一商品缺货时，整个结算批次无法确认下单。</small></span></div>
        </section>

        <section class="checkout-section">
          <div class="checkout-section-title"><BadgeCheck :size="21" /><div><h2>团长带货商品</h2></div></div>
          <div v-for="group in groups" :key="group.leader.id" class="checkout-leader-group">
            <header><img :src="group.leader.avatar" alt="" /><strong>{{ group.leader.name }}团长</strong><BadgeCheck :size="15" /><span>{{ group.leader.area }}</span></header>
            <div v-for="item in group.items" :key="`${item.productId}-${item.leaderId}`" class="checkout-item"><img :src="item.product.image" :alt="item.product.name" /><div><strong>{{ item.product.name }}</strong><span>{{ item.product.spec }} · {{ item.product.delivery }}</span></div><span>× {{ item.quantity }}</span><strong>¥{{ (item.product.price * item.quantity).toFixed(2) }}</strong></div>
          </div>
        </section>

        <section class="checkout-section">
          <div class="checkout-section-title"><TicketPercent :size="21" /><div><h2>优惠券</h2></div></div>
          <select v-model="form.couponRecordId" class="form-select coupon-select"><option value="">不使用优惠券</option><option v-for="coupon in coupons" :key="coupon.recordId" :value="String(coupon.recordId)">{{ coupon.couponName }} · 减 ¥{{ coupon.discountAmount.toFixed(2) }}</option></select>
        </section>
      </div>

      <aside class="checkout-summary">
        <h2>付款明细</h2>
        <dl><div><dt>商品金额</dt><dd>¥{{ selectedCartSubtotal.toFixed(2) }}</dd></div><div><dt>团长子订单</dt><dd>{{ groups.length }} 个</dd></div><div><dt>优惠券</dt><dd>提交后确认</dd></div><div><dt>冷链运费</dt><dd>按团长分别计算</dd></div></dl>
        <div class="summary-total-row"><span>预计金额</span><strong>¥{{ selectedCartSubtotal.toFixed(2) }}</strong></div>
        <button class="btn btn-buy w-100 checkout-button" type="submit" :disabled="saving || !form.addressId"><span v-if="saving" class="spinner-border spinner-border-sm"></span><template v-else>提交订单</template></button>
        <small><ShieldCheck :size="14" />提交即表示确认订单信息和配送安排</small>
      </aside>
    </form>

    <div v-else class="store-empty"><Truck :size="38" /><strong>没有需要结算的商品</strong><RouterLink class="btn btn-buy" to="/">返回商城</RouterLink></div>
  </div>
</template>

<style scoped>
.checkout-title { margin: 0 0 18px; font-size: 25px; font-weight: 800; }
.checkout-batch-overview { display: grid; grid-template-columns: repeat(3, 1fr); gap: 1px; margin-bottom: 14px; overflow: hidden; border: 1px solid #b8d4c8; border-radius: 8px; background: #b8d4c8; }
.checkout-batch-overview > div { display: flex; min-height: 72px; flex-direction: column; justify-content: center; padding: 12px 16px; background: #f2f8f5; }
.checkout-batch-overview span { color: var(--muted); font-size: 10px; }
.checkout-batch-overview strong { margin-top: 4px; color: var(--ink); font-size: 16px; }
.checkout-batch-overview > small { grid-column: 1 / -1; padding: 9px 15px; background: #fff8e8; color: #765d1b; font-size: 10px; }
.checkout-sections { display: flex; min-width: 0; flex-direction: column; gap: 14px; }
.checkout-section { padding: 18px; border: 1px solid var(--line); background: #fff; }
.checkout-section-title { display: flex; align-items: flex-start; gap: 9px; margin-bottom: 15px; color: var(--brand); }
.checkout-section-title > div { min-width: 0; flex: 1; }
.checkout-section-title h2 { margin: 0; color: var(--ink); font-size: 15px; }
.checkout-section-title p { margin: 3px 0 0; color: var(--muted); font-size: 9px; }
.checkout-section-title > a { display: inline-flex; align-items: center; color: var(--brand); font-size: 10px; font-weight: 700; text-decoration: none; }
.address-choice-grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 10px; }
.address-choice-grid label { position: relative; display: grid; grid-template-columns: 1fr 18px; min-height: 80px; padding: 12px; border: 1px solid #ccd4d0; border-radius: 5px; cursor: pointer; }
.address-choice-grid label.selected { border: 2px solid var(--brand); padding: 11px; background: #f3f8f6; }
.address-choice-grid input { position: absolute; opacity: 0; }
.address-choice-grid label > svg { display: none; color: var(--brand); }
.address-choice-grid label.selected > svg { display: block; }
.address-choice-grid span { display: flex; min-width: 0; flex-direction: column; }
.address-choice-grid strong { font-size: 11px; }
.address-choice-grid small { margin-top: 5px; color: var(--muted); font-size: 9px; line-height: 1.45; }
.checkout-form-grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 12px; }
.checkout-form-grid label { display: flex; flex-direction: column; gap: 6px; }
.checkout-form-grid label > span { color: #4d5953; font-size: 10px; font-weight: 700; }
.delivery-policy { display: flex; align-items: flex-start; gap: 10px; padding: 13px; border: 1px solid #d5e5dd; background: #f7faf8; color: var(--brand); }
.delivery-policy > span { display: flex; flex-direction: column; }
.delivery-policy strong { color: var(--ink); font-size: 11px; }
.delivery-policy small { margin-top: 4px; color: var(--muted); font-size: 9px; line-height: 1.5; }
.checkout-leader-group { margin-bottom: 10px; border: 1px solid var(--line); }
.checkout-leader-group:last-child { margin-bottom: 0; }
.checkout-leader-group > header { display: flex; min-height: 45px; align-items: center; gap: 6px; padding: 0 12px; background: #f3f8f6; }
.checkout-leader-group > header img { width: 27px; height: 27px; border-radius: 50%; object-fit: cover; }
.checkout-leader-group > header svg { color: var(--brand); }
.checkout-leader-group > header span { margin-left: auto; color: var(--muted); font-size: 9px; }
.checkout-item { display: grid; grid-template-columns: 54px minmax(130px, 1fr) 45px 76px; gap: 10px; align-items: center; padding: 10px 12px; border-top: 1px solid var(--line); }
.checkout-item > img { width: 54px; height: 54px; object-fit: cover; }
.checkout-item > div { display: flex; min-width: 0; flex-direction: column; }
.checkout-item > div strong { overflow: hidden; font-size: 11px; text-overflow: ellipsis; white-space: nowrap; }
.checkout-item > div span { margin-top: 4px; color: var(--muted); font-size: 9px; }
.checkout-item > span, .checkout-item > strong { font-size: 10px; text-align: right; }
.coupon-select { max-width: 420px; }
.summary-total-row { display: flex; align-items: baseline; justify-content: space-between; gap: 10px; margin-top: 15px; padding-top: 15px; border-top: 1px solid var(--line); }
.summary-total-row strong { color: var(--danger); font-size: 23px; }
.checkout-button { margin-top: 16px; }
.checkout-summary { position: sticky; top: 130px; padding: 19px; border: 1px solid #cfd7d3; border-radius: 6px; background: #fff; box-shadow: 0 3px 10px rgba(23, 33, 29, .07); }
.checkout-summary h2 { margin: 0 0 16px; font-size: 17px; }
.checkout-summary dl { margin: 0; }
.checkout-summary dl > div { display: flex; justify-content: space-between; gap: 12px; margin-bottom: 10px; font-size: 11px; }
.checkout-summary dt { color: var(--muted); font-weight: 500; }
.checkout-summary dd { margin: 0; }
.checkout-summary > small { display: flex; align-items: flex-start; gap: 4px; margin-top: 11px; color: var(--muted); font-size: 9px; line-height: 1.45; }

@media (max-width: 767.98px) {
  .checkout-batch-overview { grid-template-columns: 1fr; }
  .checkout-batch-overview > small { grid-column: auto; }
  .address-choice-grid, .checkout-form-grid { grid-template-columns: 1fr; }
  .checkout-item { grid-template-columns: 48px minmax(0, 1fr) 55px; }
  .checkout-item > img { width: 48px; height: 48px; }
  .checkout-item > span { grid-column: 2; }
  .checkout-item > strong { grid-column: 3; grid-row: 1 / span 2; }
}
</style>
