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
const form = reactive({ customerId: '', customerName: '', phone: '', email: '' })
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
      <section class="profile-metrics"><RouterLink to="/orders"><PackageSearch :size="22" /><span><strong>我的订单</strong><small>查看订单进度</small></span><ChevronRight :size="17" /></RouterLink><RouterLink to="/coupons"><TicketPercent :size="22" /><span><strong>优惠券</strong><small>领取和使用</small></span><ChevronRight :size="17" /></RouterLink><div><Coins :size="22" /><span><strong>{{ profile.customer.points }} 积分</strong><small>累计消费 {{ money(profile.customer.totalSpent) }}</small></span></div><RouterLink to="/addresses"><MapPin :size="22" /><span><strong>收货地址</strong><small>{{ profile.addresses.length }} 条地址</small></span><ChevronRight :size="17" /></RouterLink></section>
      <div class="profile-content-grid">
        <section class="account-section"><div class="account-section-head"><div><h2>账户资料</h2></div></div><form class="account-form" @submit.prevent="saveProfile"><label><span>姓名</span><input v-model.trim="form.customerName" class="form-control" maxlength="100" required /></label><label><span>手机号码</span><input v-model.trim="form.phone" class="form-control" maxlength="20" pattern="1[0-9]{10}" required /></label><label><span>电子邮箱</span><input v-model.trim="form.email" class="form-control" type="email" maxlength="100" /></label><button class="btn btn-buy" type="submit" :disabled="saving"><span v-if="saving" class="spinner-border spinner-border-sm"></span><Save v-else :size="17" />保存资料</button></form></section>
        <aside class="account-section"><div class="account-section-head"><div><h2>常用收货地址</h2></div><RouterLink to="/addresses">管理</RouterLink></div><div v-if="profile.addresses.length" class="profile-address-list"><div v-for="address in profile.addresses.slice(0, 3)" :key="address.addressId"><MapPin :size="17" /><span><strong>{{ address.receiverName }} {{ address.phone }}</strong><small>{{ address.city }}{{ address.district }} {{ address.detailAddress }}</small></span></div></div><div v-else class="inline-empty">暂无收货地址</div></aside>
      </div>
    </template>
  </div>
</template>

<style scoped>
.profile-banner { display: flex; min-height: 142px; align-items: center; gap: 17px; padding: 23px; background: var(--header); color: #fff; }
.profile-avatar { display: inline-flex; width: 72px; height: 72px; align-items: center; justify-content: center; border-radius: 50%; background: #2d4f40; color: #dcece5; }
.profile-banner small { color: #aebbb5; }
.profile-banner h1 { margin: 2px 0 5px; font-size: 25px; }
.profile-banner p { display: flex; align-items: center; gap: 5px; margin: 0; color: #dfc272; font-size: 10px; }
.profile-metrics { display: grid; grid-template-columns: repeat(4, 1fr); border: 1px solid var(--line); border-top: 0; background: #fff; }
.profile-metrics > a, .profile-metrics > div { display: flex; min-width: 0; min-height: 82px; align-items: center; gap: 9px; padding: 13px; border-right: 1px solid var(--line); color: var(--brand); text-decoration: none; }
.profile-metrics > *:last-child { border-right: 0; }
.profile-metrics span { display: flex; min-width: 0; flex: 1; flex-direction: column; }
.profile-metrics strong { color: var(--ink); font-size: 12px; }
.profile-metrics small { margin-top: 3px; overflow: hidden; color: var(--muted); font-size: 9px; text-overflow: ellipsis; white-space: nowrap; }
.profile-content-grid { display: grid; grid-template-columns: minmax(0, 1.5fr) minmax(280px, .7fr); gap: 15px; margin-top: 16px; }
.account-section { padding: 18px; border: 1px solid var(--line); background: #fff; }
.account-section-head { display: flex; align-items: flex-start; justify-content: space-between; gap: 12px; margin-bottom: 15px; }
.account-section-head h2 { margin: 0; font-size: 16px; }
.account-section-head p { margin: 3px 0 0; color: var(--muted); font-size: 9px; }
.account-section-head a { color: var(--brand); font-size: 10px; font-weight: 700; text-decoration: none; }
.account-form { display: grid; grid-template-columns: repeat(2, 1fr); gap: 13px; }
.account-form label:first-child { grid-column: 1 / -1; }
.account-form button { grid-column: 1 / -1; justify-self: end; }
.account-form label { display: flex; flex-direction: column; gap: 6px; }
.account-form label > span { color: #4d5953; font-size: 10px; font-weight: 700; }
.profile-address-list { display: flex; flex-direction: column; }
.profile-address-list > div { display: flex; gap: 8px; padding: 10px 0; border-top: 1px solid var(--line); color: var(--brand); }
.profile-address-list span { display: flex; min-width: 0; flex-direction: column; }
.profile-address-list strong { color: var(--ink); font-size: 10px; }
.profile-address-list small { margin-top: 3px; color: var(--muted); font-size: 9px; line-height: 1.45; }

@media (max-width: 767.98px) {
  .profile-metrics { grid-template-columns: repeat(2, 1fr); }
  .profile-metrics > *:nth-child(2) { border-right: 0; }
  .profile-metrics > *:nth-child(-n+2) { border-bottom: 1px solid var(--line); }
  .account-form { grid-template-columns: 1fr; }
  .account-form label:first-child { grid-column: auto; }
  .account-form button { grid-column: auto; justify-self: stretch; }
}
</style>
