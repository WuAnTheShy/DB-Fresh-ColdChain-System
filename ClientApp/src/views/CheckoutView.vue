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
const { cartItems, cartSubtotal, clearCart, setLastOrder } = useShop()
const loading = ref(true)
const saving = ref(false)
const error = ref('')
const addresses = ref([])
const coupons = ref([])
const form = reactive({ addressId: '', couponRecordId: '', deliveryWindow: '明日 14:00-18:00', substitution: 'refund' })
const groups = computed(() => {
  const map = new Map()
  cartItems.value.forEach((item) => {
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
  if (!cartItems.value.length) return
  saving.value = true
  error.value = ''
  try {
    const merged = new Map()
    cartItems.value.forEach((item) => merged.set(item.productId, (merged.get(item.productId) ?? 0) + item.quantity))
    const result = await api.createOrder({
      customerId: customerId.value,
      addressId: Number(form.addressId),
      couponRecordId: form.couponRecordId ? Number(form.couponRecordId) : null,
      items: [...merged].map(([productId, quantity]) => ({ productId, quantity })),
    })
    setLastOrder({ ...result, leaderGroups: groups.value.map((group) => ({ leaderId: group.leader.id, leaderName: group.leader.name })) })
    clearCart()
    router.push(`/order-success/${result.orderId}`)
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
    <div v-if="error" class="alert alert-danger" role="alert">{{ error }}</div>
    <div v-if="loading" class="store-loading"><span class="spinner-border spinner-border-sm"></span>正在准备结算信息</div>

    <form v-else-if="cartItems.length" class="checkout-layout" @submit.prevent="submit">
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
          <div class="checkout-form-grid"><label><span>配送时间</span><select v-model="form.deliveryWindow" class="form-select"><option>明日 09:00-12:00</option><option>明日 14:00-18:00</option><option>后日 09:00-12:00</option></select></label><label><span>缺货处理</span><select v-model="form.substitution" class="form-select"><option value="refund">缺货商品直接退款</option><option value="contact">由团长联系确认</option><option value="replace">接受同价替代商品</option></select></label></div>
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
        <dl><div><dt>商品金额</dt><dd>¥{{ cartSubtotal.toFixed(2) }}</dd></div><div><dt>优惠券</dt><dd>提交后确认</dd></div><div><dt>冷链运费</dt><dd>提交后确认</dd></div></dl>
        <div class="summary-total-row"><span>预计金额</span><strong>¥{{ cartSubtotal.toFixed(2) }}</strong></div>
        <button class="btn btn-buy w-100 checkout-button" type="submit" :disabled="saving || !form.addressId"><span v-if="saving" class="spinner-border spinner-border-sm"></span><template v-else>提交订单</template></button>
        <small><ShieldCheck :size="14" />提交即表示确认团购规则和配送安排</small>
      </aside>
    </form>

    <div v-else class="store-empty"><Truck :size="38" /><strong>没有需要结算的商品</strong><RouterLink class="btn btn-buy" to="/">返回商城</RouterLink></div>
  </div>
</template>
