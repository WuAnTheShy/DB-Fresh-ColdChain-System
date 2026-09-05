<script setup>
import { BadgeCheck, Check, ChevronRight, Coins, MapPin, ShieldCheck, TicketPercent, Truck } from '@lucide/vue'
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import QuantityStepper from '../components/QuantityStepper.vue'
import { api } from '../services/api'
import { useCustomerContext } from '../state/customer'
import { useShop } from '../state/shop'

const router = useRouter()
const { customerId } = useCustomerContext()
const { selectedCartItems, selectedCartSubtotal, updateQuantity, removeCartItems, setLastOrder } = useShop()
const loading = ref(true)
const saving = ref(false)
const error = ref('')
const addresses = ref([])
const coupons = ref([])
const claimableCoupons = ref([])
const pointsBalance = ref(0)
const freightAmount = ref(0)
const freightLoading = ref(false)
const freightError = ref('')
let freightRequestSequence = 0
const form = reactive({ addressId: '', pointsToUse: 0 })
const isSpecialCoupon = (coupon) => String(coupon?.couponType ?? '').toUpperCase() === 'SPECIAL'
const eligibleCoupons = computed(() => [
  ...coupons.value,
  ...claimableCoupons.value.filter(item => !item.hasClaimed && item.remainingQuantity > 0),
].filter(item => Number(item.minOrderAmount) <= selectedCartSubtotal.value))
const bestCoupon = (special) => eligibleCoupons.value
  .filter(item => isSpecialCoupon(item) === special)
  .sort((left, right) => Number(right.discountAmount) - Number(left.discountAmount))[0]
const automaticCoupons = computed(() => [bestCoupon(false), bestCoupon(true)].filter(Boolean))
const couponDiscount = computed(() => automaticCoupons.value.reduce((sum, item) => sum + Number(item.discountAmount), 0))
const maxPointsToUse = computed(() => Math.min(
  pointsBalance.value,
  Math.floor(Math.max(0, selectedCartSubtotal.value - couponDiscount.value) * 0.10) * 100,
))
const pointsDiscount = computed(() => Number(form.pointsToUse || 0) / 100)
function clampPoints() { form.pointsToUse = Math.floor(Math.min(Math.max(0, Number(form.pointsToUse || 0)), maxPointsToUse.value) / 100) * 100 }
const groups = computed(() => {
  const map = new Map()
  selectedCartItems.value.forEach((item) => {
    if (!map.has(item.leaderId)) map.set(item.leaderId, { leader: item.leader, items: [] })
    map.get(item.leaderId).items.push(item)
  })
  return [...map.values()]
})
const estimatedTotal = computed(() => Math.max(
  0,
  selectedCartSubtotal.value - couponDiscount.value - pointsDiscount.value + freightAmount.value,
))

function checkoutItemsPayload() {
  return selectedCartItems.value.map(item => ({
    productId: item.product.productId,
    promoterId: item.leaderId,
    supplierId: String(item.supplierId || item.product.supplierId || ''),
    quantity: item.quantity,
    clientUnitPrice: item.product.price,
  }))
}

// 供应商是交易身份的一部分：任一待结算商品缺失供应商信息时应就地拦截，
// 给出明确中文提示，避免请求发到后端才被 "供应商ID不能为空" 拒绝。
function missingSupplierItems() {
  return selectedCartItems.value.filter(item =>
    !String(item.supplierId || item.product.supplierId || '').trim())
}

async function loadFreightQuote() {
  const requestSequence = ++freightRequestSequence
  freightError.value = ''
  if (!form.addressId || !selectedCartItems.value.length) {
    freightAmount.value = 0
    freightLoading.value = false
    return
  }

  const missingItems = missingSupplierItems()
  if (missingItems.length) {
    freightLoading.value = false
    freightError.value = '部分商品缺少供应商信息，请返回商城重新加入购物车后重试'
    return
  }

  freightLoading.value = true
  try {
    const result = await api.quoteCheckoutFreight({
      customerId: customerId.value,
      addressId: form.addressId,
      items: checkoutItemsPayload(),
    })
    if (requestSequence !== freightRequestSequence) return
    freightAmount.value = Number(result.freightAmount ?? 0)
  } catch (requestError) {
    if (requestSequence !== freightRequestSequence) return
    freightAmount.value = 0
    freightError.value = requestError.message
  } finally {
    if (requestSequence === freightRequestSequence) freightLoading.value = false
  }
}

async function loadAssets() {
  loading.value = true
  error.value = ''
  const [addressResult, couponResult, profileResult] = await Promise.allSettled([
    api.getAddresses(customerId.value),
    api.getCoupons(customerId.value),
    api.getCustomer(customerId.value),
  ])
  addresses.value = addressResult.status === 'fulfilled' ? addressResult.value.addresses : []
  coupons.value = couponResult.status === 'fulfilled' ? couponResult.value.availableCoupons : []
  claimableCoupons.value = couponResult.status === 'fulfilled' ? couponResult.value.claimableCoupons : []
  pointsBalance.value = profileResult.status === 'fulfilled' ? Number(profileResult.value.customer.points ?? 0) : 0
  const defaultAddress = addresses.value.find((address) => address.isDefault === 1) ?? addresses.value[0]
  if (defaultAddress) form.addressId = String(defaultAddress.addressId)
  if (addressResult.status === 'rejected') error.value = addressResult.reason.message
  loading.value = false
}

async function submit() {
  if (!selectedCartItems.value.length) return
  const missingItems = missingSupplierItems()
  if (missingItems.length) {
    error.value = `「${missingItems[0].product.name}」缺少供应商信息，请返回商城重新加入购物车`
    return
  }
  saving.value = true
  error.value = ''
  try {
    const merged = new Map()
    selectedCartItems.value.forEach((item) => {
      const orderProductId = item.product.productId
      const orderSupplierId = String(item.supplierId || item.product.supplierId || '')
      // 交易身份 = (团长, 商品, 供应商)：同商品不同供应商保持两条独立订单明细
      const orderLineKey = `${item.leaderId}\u001f${orderProductId}\u001f${orderSupplierId}`
      const existing = merged.get(orderLineKey)
      merged.set(orderLineKey, existing
        ? { ...existing, quantity: existing.quantity + item.quantity }
        : {
            productId: orderProductId,
            promoterId: item.leaderId,
            supplierId: orderSupplierId,
            quantity: item.quantity,
            clientUnitPrice: item.product.price,
          })
    })
    const result = await api.createOrder({
      customerId: customerId.value,
      addressId: form.addressId,
      couponRecordId: null,
      stackableCouponRecordId: null,
      pointsToUse: Number(form.pointsToUse || 0),
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
watch(
  () => [
    form.addressId,
    ...selectedCartItems.value.map(item => `${item.leaderId}:${item.product.productId}:${String(item.supplierId || item.product.supplierId || '')}:${item.quantity}:${item.product.price}`),
  ],
  loadFreightQuote,
)
</script>

<template>
  <div class="store-container page-space checkout-page">
    <h1 class="checkout-title">确认订单</h1>
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
          <div class="delivery-divider"></div>
          <div class="delivery-row">
            <div class="delivery-row-title"><Truck :size="21" /><h2>配送安排</h2></div>
            <div class="delivery-row-detail">
              <strong>冷链配送</strong>
              <div class="delivery-freight">
                <span>当前运费</span>
                <strong class="delivery-amount" v-if="freightLoading">计算中…</strong>
                <strong class="delivery-amount" v-else>¥{{ freightAmount.toFixed(2) }}</strong>
              </div>
            </div>
          </div>
          <div v-if="freightError" class="freight-error">{{ freightError }}，提交订单时将由服务端重新计算。</div>
        </section>

        <section class="checkout-section">
          <div class="checkout-section-title"><BadgeCheck :size="21" /><div><h2>团长带货商品</h2></div></div>
          <div v-for="group in groups" :key="group.leader.id" class="checkout-leader-group">
            <header><img :src="group.leader.avatar" alt="" /><strong>{{ group.leader.name }}团长</strong><BadgeCheck :size="15" /><span>{{ group.leader.area }}</span></header>
            <div v-for="item in group.items" :key="`${item.productId}-${item.leaderId}-${item.supplierId || ''}`" class="checkout-item"><img :src="item.product.image" :alt="item.product.name" :class="{ 'fallback-photo-tint': item.product.image === item.product.fallbackImage }" @error="$event.target.classList.add('fallback-photo-tint'); $event.target.src = item.product.fallbackImage" /><div class="checkout-item-info"><strong>{{ item.product.name }}</strong><span>{{ item.product.supplierName ? `${item.product.supplierName} · ` : '' }}{{ item.product.spec }} · {{ item.product.delivery }}</span></div><QuantityStepper :model-value="item.quantity" :max="item.product.stock" @update:model-value="updateQuantity(item.productId, $event)" /><strong>¥{{ (item.product.price * item.quantity).toFixed(2) }}</strong></div>
          </div>
        </section>

        <section class="checkout-section">
          <div class="checkout-section-title"><Coins :size="21" /><div><h2>积分抵扣</h2><p>每100积分抵扣1元，最多抵扣优惠后商品金额的10%，不含运费</p></div></div>
          <div class="points-redeem"><label>使用积分<input v-model.number="form.pointsToUse" class="form-control" type="number" min="0" :max="maxPointsToUse" step="100" @input="clampPoints" /></label><span>可用 {{ pointsBalance }}，本次最多 {{ maxPointsToUse }}</span><strong>- ¥{{ pointsDiscount.toFixed(2) }}</strong></div>
        </section>

        <section class="checkout-section">
          <div class="checkout-section-title"><TicketPercent :size="21" /><div><h2>优惠券</h2><p>下单时自动领取并使用优惠最大的普通券；特殊券可额外叠加一张</p></div></div>
          <div v-if="automaticCoupons.length" class="automatic-coupons">
            <div v-for="coupon in automaticCoupons" :key="coupon.recordId || coupon.couponId"><span>{{ isSpecialCoupon(coupon) ? '特殊叠加券' : '普通券' }}</span><strong>{{ coupon.couponName }}</strong><em>- ¥{{ Number(coupon.discountAmount).toFixed(2) }}</em></div>
          </div>
          <div v-else class="inline-empty">本次订单暂无符合条件的优惠券</div>
        </section>
      </div>

      <aside class="checkout-summary">
        <h2>付款明细</h2>
        <dl><div><dt>商品金额</dt><dd>¥{{ selectedCartSubtotal.toFixed(2) }}</dd></div><div><dt>自动优惠</dt><dd>- ¥{{ couponDiscount.toFixed(2) }}</dd></div><div><dt>积分抵扣</dt><dd>- ¥{{ pointsDiscount.toFixed(2) }}</dd></div><div><dt>冷链运费</dt><dd>{{ freightLoading ? '计算中…' : `¥${freightAmount.toFixed(2)}` }}</dd></div></dl>
        <div class="summary-total-row"><span>预计金额</span><strong>¥{{ estimatedTotal.toFixed(2) }}</strong></div>
        <button class="btn btn-buy w-100 checkout-button" type="submit" :disabled="saving || freightLoading || !form.addressId"><span v-if="saving" class="spinner-border spinner-border-sm"></span><template v-else>提交订单</template></button>
        <small><ShieldCheck :size="14" />提交即表示确认订单信息和配送安排</small>
      </aside>
    </form>

    <div v-else class="store-empty"><Truck :size="38" /><strong>没有需要结算的商品</strong><RouterLink class="btn btn-buy" to="/">返回商城</RouterLink></div>
  </div>
</template>

<style scoped>
.checkout-title { margin: 0 0 18px; font-size: 25px; font-weight: 800; }
.checkout-layout { display: flex; flex-direction: column; }
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
.delivery-divider { margin: 18px 0; border-top: 1px solid var(--line); }
.delivery-row { display: flex; min-height: 32px; align-items: center; justify-content: space-between; gap: 20px; }
.delivery-row-title, .delivery-row-detail { display: flex; align-items: center; }
.delivery-row-title { gap: 9px; color: var(--brand); }
.delivery-row-title h2 { margin: 0; color: var(--ink); font-size: 15px; }
.delivery-row-detail { margin-left: auto; gap: 18px; white-space: nowrap; }
.delivery-row-detail > strong:first-child { padding: 7px 11px; border: 1px solid #cfe1d8; border-radius: 999px; background: #f2f8f5; color: var(--brand); font-size: 11px; }
.delivery-freight { display: flex; min-width: 70px; flex-direction: column; align-items: flex-end; gap: 2px; }
.delivery-freight > span { color: var(--muted); font-size: 9px; }
.delivery-freight .delivery-amount { color: var(--danger); font-size: 14px; text-align: right; }
.freight-error { margin-top: 8px; color: #9a6700; font-size: 9px; }
.points-redeem { display: grid; grid-template-columns: 180px 1fr auto; gap: 12px; align-items: end; }.points-redeem label { font-size: 10px; font-weight: 700; }.points-redeem input { margin-top: 6px; }.points-redeem span { padding-bottom: 9px; color: var(--muted); font-size: 9px; }.points-redeem strong { padding-bottom: 7px; color: var(--danger); }
.checkout-leader-group { margin-bottom: 10px; border: 1px solid var(--line); }
.checkout-leader-group:last-child { margin-bottom: 0; }
.checkout-leader-group > header { display: flex; min-height: 45px; align-items: center; gap: 6px; padding: 0 12px; background: #f3f8f6; }
.checkout-leader-group > header img { width: 27px; height: 27px; border-radius: 50%; object-fit: cover; }
.checkout-leader-group > header svg { color: var(--brand); }
.checkout-leader-group > header span { margin-left: auto; color: var(--muted); font-size: 9px; }
.checkout-item { display: grid; grid-template-columns: 54px minmax(130px, 1fr) 120px 76px; gap: 10px; align-items: center; padding: 10px 12px; border-top: 1px solid var(--line); }
.checkout-item > img { width: 54px; height: 54px; object-fit: cover; }
.checkout-item-info { display: flex; min-width: 0; flex-direction: column; }
.checkout-item-info strong { overflow: hidden; font-size: 11px; text-overflow: ellipsis; white-space: nowrap; }
.checkout-item-info span { margin-top: 4px; color: var(--muted); font-size: 9px; }
.checkout-item > strong { font-size: 10px; text-align: right; }
.automatic-coupons { display: grid; gap: 8px; }.automatic-coupons > div { display: grid; grid-template-columns: 86px minmax(0, 1fr) auto; gap: 10px; align-items: center; padding: 11px 13px; border: 1px solid #d6e5de; background: #f7faf8; }.automatic-coupons span { color: var(--brand); font-size: 9px; font-weight: 700; }.automatic-coupons strong { font-size: 11px; }.automatic-coupons em { color: var(--danger); font-size: 11px; font-style: normal; font-weight: 800; }
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
  .address-choice-grid, .checkout-form-grid { grid-template-columns: 1fr; }
  .delivery-row { gap: 10px; }
  .delivery-row-detail { gap: 8px; }
  .delivery-freight { min-width: 62px; }
  .checkout-item { grid-template-columns: 48px minmax(0, 1fr) 76px; }
  .checkout-item > img { width: 48px; height: 48px; }
  .checkout-item .quantity-stepper { grid-column: 2; width: 110px; }
  .checkout-item > strong { grid-column: 3; grid-row: 1 / span 2; }
}
</style>
