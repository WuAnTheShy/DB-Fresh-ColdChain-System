<script setup>
import { CheckCircle2, RefreshCw, TicketPercent } from '@lucide/vue'
import { onMounted, ref } from 'vue'
import StoreBreadcrumb from '../components/StoreBreadcrumb.vue'
import { api } from '../services/api'
import { useCustomerContext } from '../state/customer'

const { customerId } = useCustomerContext()
const loading = ref(true)
const claimingId = ref(null)
const center = ref(null)
const error = ref('')
const success = ref('')
function money(value) { return Number(value ?? 0).toFixed(0) }
function date(value) { return value ? new Intl.DateTimeFormat('zh-CN', { dateStyle: 'medium' }).format(new Date(value)) : '-' }
async function loadCoupons() { loading.value = true; error.value = ''; try { center.value = await api.getCoupons(customerId.value) } catch (requestError) { center.value = null; error.value = requestError.message } finally { loading.value = false } }
async function claim(couponId) { claimingId.value = couponId; error.value = ''; success.value = ''; try { await api.claimCoupon(customerId.value, couponId); success.value = '优惠券领取成功'; await loadCoupons() } catch (requestError) { error.value = requestError.message } finally { claimingId.value = null } }
onMounted(loadCoupons)
</script>

<template>
  <div class="store-container page-space coupons-page">
    <StoreBreadcrumb :items="[{ label: '领券中心' }]" />
    <div class="account-page-header"><div><TicketPercent :size="26" /><span><h1>领券中心</h1><p>领取团购优惠，结算时自动校验使用条件</p></span></div><button class="refresh-button" type="button" title="刷新优惠券" @click="loadCoupons"><RefreshCw :size="18" /></button></div>
    <div v-if="error" class="alert alert-danger">{{ error }}</div><div v-if="success" class="alert alert-success">{{ success }}</div>
    <div v-if="loading" class="store-loading"><span class="spinner-border spinner-border-sm"></span>正在读取优惠券</div>
    <template v-else-if="center">
      <section class="coupon-section"><div class="section-title-row"><div><h2>可领取优惠券</h2><p>{{ center.claimableCoupons.length }} 个团购优惠活动</p></div></div><div v-if="center.claimableCoupons.length" class="consumer-coupon-grid"><article v-for="coupon in center.claimableCoupons" :key="coupon.couponId" class="consumer-coupon-card"><div class="coupon-amount"><small>¥</small><strong>{{ money(coupon.discountAmount) }}</strong><span>满 ¥{{ money(coupon.minOrderAmount) }} 可用</span></div><div class="coupon-detail"><span class="coupon-type">全场团购券</span><h3>{{ coupon.couponName }}</h3><p>有效期至 {{ date(coupon.endTime) }}</p><small>剩余 {{ coupon.remainingQuantity }} 张</small></div><button class="btn" :class="coupon.hasClaimed ? 'btn-outline-secondary' : 'btn-cart'" type="button" :disabled="coupon.hasClaimed || claimingId === coupon.couponId" @click="claim(coupon.couponId)"><span v-if="claimingId === coupon.couponId" class="spinner-border spinner-border-sm"></span><template v-else>{{ coupon.hasClaimed ? '已领取' : '立即领取' }}</template></button></article></div><div v-else class="store-empty compact"><TicketPercent :size="30" /><strong>暂无可领取优惠券</strong></div></section>
      <section class="coupon-section"><div class="section-title-row"><div><h2>我的可用券</h2><p>{{ center.availableCoupons.length }} 张可在结算时使用</p></div></div><div v-if="center.availableCoupons.length" class="my-coupon-list"><div v-for="coupon in center.availableCoupons" :key="coupon.recordId"><CheckCircle2 :size="20" /><span><strong>{{ coupon.couponName }}</strong><small>满 ¥{{ money(coupon.minOrderAmount) }} 减 ¥{{ money(coupon.discountAmount) }} · {{ date(coupon.endTime) }} 到期</small></span><RouterLink to="/">去使用</RouterLink></div></div><div v-else class="store-empty compact"><TicketPercent :size="30" /><strong>暂无可用优惠券</strong></div></section>
    </template>
  </div>
</template>
