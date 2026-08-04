<script setup>
import { BadgeCheck, ChevronRight, Coins, MapPin, PackageSearch, Save, TicketPercent, UserRound } from '@lucide/vue'
import { onMounted, reactive, ref } from 'vue'
import StoreBreadcrumb from '../components/StoreBreadcrumb.vue'
import { ApiError, api } from '../services/api'
import { useCustomerContext } from '../state/customer'

const { customerId } = useCustomerContext()
const loading = ref(true)
const saving = ref(false)
const notFound = ref(false)
const error = ref('')
const success = ref('')
const profile = ref(null)
const form = reactive({ customerId: 0, customerName: '', phone: '', email: '' })
function money(value) { return `¥${Number(value ?? 0).toFixed(2)}` }

async function loadProfile() {
  loading.value = true; error.value = ''; notFound.value = false
  try {
    profile.value = await api.getCustomer(customerId.value)
    const customer = profile.value.customer
    Object.assign(form, { customerId: customer.customerId, customerName: customer.customerName, phone: customer.phone, email: customer.email ?? '' })
  } catch (requestError) { notFound.value = requestError instanceof ApiError && requestError.status === 404; if (!notFound.value) error.value = requestError.message } finally { loading.value = false }
}
async function saveProfile() {
  saving.value = true; error.value = ''; success.value = ''
  try { await api.updateCustomer(customerId.value, { ...form, email: form.email || null }); success.value = '账户资料已更新'; await loadProfile() } catch (requestError) { error.value = requestError.message } finally { saving.value = false }
}
onMounted(loadProfile)
</script>

<template>
  <div class="store-container page-space profile-page">
    <StoreBreadcrumb :items="[{ label: '个人中心' }]" />
    <div v-if="error" class="alert alert-danger">{{ error }}</div><div v-if="success" class="alert alert-success">{{ success }}</div>
    <div v-if="loading" class="store-loading"><span class="spinner-border spinner-border-sm"></span>正在读取账户信息</div>
    <div v-else-if="notFound" class="store-empty"><UserRound :size="40" /><strong>当前演示消费者尚未建档</strong><span>请先通过系统初始数据建立消费者账号</span></div>
    <template v-else-if="profile">
      <section class="profile-banner"><span class="profile-avatar"><UserRound :size="34" /></span><div><small>欢迎回来</small><h1>{{ profile.customer.customerName }}</h1><p><BadgeCheck :size="15" />{{ profile.memberLevel?.levelName || '基础会员' }} · 团长团购消费者</p></div></section>
      <section class="profile-metrics"><RouterLink to="/orders"><PackageSearch :size="22" /><span><strong>我的订单</strong><small>查看团购进度</small></span><ChevronRight :size="17" /></RouterLink><RouterLink to="/coupons"><TicketPercent :size="22" /><span><strong>优惠券</strong><small>领取和使用</small></span><ChevronRight :size="17" /></RouterLink><div><Coins :size="22" /><span><strong>{{ profile.customer.points }} 积分</strong><small>累计消费 {{ money(profile.customer.totalSpent) }}</small></span></div><RouterLink to="/addresses"><MapPin :size="22" /><span><strong>收货地址</strong><small>{{ profile.addresses.length }} 条地址</small></span><ChevronRight :size="17" /></RouterLink></section>
      <div class="profile-content-grid">
        <section class="account-section"><div class="account-section-head"><div><h2>账户资料</h2><p>维护用于订单联系的基础信息</p></div></div><form class="account-form" @submit.prevent="saveProfile"><label><span>姓名</span><input v-model.trim="form.customerName" class="form-control" maxlength="100" required /></label><label><span>手机号码</span><input v-model.trim="form.phone" class="form-control" maxlength="20" pattern="1[0-9]{10}" required /></label><label><span>电子邮箱</span><input v-model.trim="form.email" class="form-control" type="email" maxlength="100" /></label><button class="btn btn-buy" type="submit" :disabled="saving"><span v-if="saving" class="spinner-border spinner-border-sm"></span><Save v-else :size="17" />保存资料</button></form></section>
        <aside class="account-section"><div class="account-section-head"><div><h2>常用收货地址</h2><p>结算时可直接选择</p></div><RouterLink to="/addresses">管理</RouterLink></div><div v-if="profile.addresses.length" class="profile-address-list"><div v-for="address in profile.addresses.slice(0, 3)" :key="address.addressId"><MapPin :size="17" /><span><strong>{{ address.receiverName }} {{ address.phone }}</strong><small>{{ address.city }}{{ address.district }} {{ address.detailAddress }}</small></span></div></div><div v-else class="inline-empty">暂无收货地址</div></aside>
      </div>
    </template>
  </div>
</template>
