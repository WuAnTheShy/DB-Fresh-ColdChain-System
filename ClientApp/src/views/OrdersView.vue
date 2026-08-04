<script setup>
import { BadgeCheck, ChevronLeft, ChevronRight, PackageSearch, Search, ShoppingBag } from '@lucide/vue'
import { onMounted, reactive, ref } from 'vue'
import StatusBadge from '../components/StatusBadge.vue'
import StoreBreadcrumb from '../components/StoreBreadcrumb.vue'
import { api } from '../services/api'
import { useCustomerContext } from '../state/customer'

const { customerId } = useCustomerContext()
const loading = ref(true)
const error = ref('')
const result = ref({ orders: [], totalCount: 0, totalPages: 0 })
const filters = reactive({ status: '', keyword: '', page: 1, pageSize: 8 })
const tabs = [
  { value: '', label: '全部订单' },
  { value: 0, label: '待支付' },
  { value: 1, label: '待发货' },
  { value: 2, label: '配送中' },
  { value: 3, label: '已完成' },
  { value: 5, label: '退款售后' },
]

function money(value) { return `¥${Number(value ?? 0).toFixed(2)}` }
function date(value) { return value ? new Intl.DateTimeFormat('zh-CN', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) : '-' }

async function loadOrders() {
  loading.value = true
  error.value = ''
  try {
    result.value = await api.getOrders({ customerId: customerId.value, status: filters.status, keyword: filters.keyword, page: filters.page, pageSize: filters.pageSize })
  } catch (requestError) {
    error.value = requestError.message
    result.value = { orders: [], totalCount: 0, totalPages: 0 }
  } finally {
    loading.value = false
  }
}

function setStatus(status) { filters.status = status; filters.page = 1; loadOrders() }
function search() { filters.page = 1; loadOrders() }
function changePage(page) { if (page < 1 || page > result.value.totalPages) return; filters.page = page; loadOrders() }
onMounted(loadOrders)
</script>

<template>
  <div class="store-container page-space orders-page">
    <StoreBreadcrumb :items="[{ label: '我的订单' }]" />
    <div class="account-page-header"><div><PackageSearch :size="26" /><span><h1>我的订单</h1></span></div><form class="order-search" @submit.prevent="search"><input v-model.trim="filters.keyword" type="search" placeholder="搜索订单号" /><button type="submit" title="搜索订单"><Search :size="18" /></button></form></div>

    <nav class="order-tabs" aria-label="订单状态">
      <button v-for="tab in tabs" :key="String(tab.value)" type="button" :class="{ active: filters.status === tab.value }" @click="setStatus(tab.value)">{{ tab.label }}</button>
    </nav>

    <div v-if="error" class="alert alert-danger">{{ error }}</div>
    <div v-if="loading" class="store-loading"><span class="spinner-border spinner-border-sm"></span>正在读取订单</div>
    <div v-else-if="result.orders.length" class="order-card-list">
      <article v-for="order in result.orders" :key="order.orderId" class="consumer-order-card">
        <header><div><span>{{ date(order.createdAt) }}</span><strong>订单号 {{ order.orderNo }}</strong></div><StatusBadge :status="order.orderStatus" /></header>
        <div class="order-card-body">
          <div class="order-leader-identity"><span class="leader-order-avatar">团</span><div><strong>{{ order.promoterName ? `${order.promoterName}团长` : '社区认证团长' }}</strong><small><BadgeCheck :size="13" />团长带货订单</small></div></div>
          <div class="order-card-metric"><span>商品数量</span><strong>{{ order.itemCount }} 件</strong></div>
          <div class="order-card-metric"><span>实付金额</span><strong>{{ money(order.finalAmount) }}</strong></div>
          <div class="order-card-actions"><RouterLink class="btn btn-sm btn-outline-secondary" :to="`/orders/${order.orderId}`">查看详情</RouterLink><RouterLink v-if="order.orderStatus === 3" class="btn btn-sm btn-cart" to="/">再次购买</RouterLink></div>
        </div>
      </article>
      <div v-if="result.totalPages > 1" class="store-pagination"><button type="button" title="上一页" :disabled="filters.page <= 1" @click="changePage(filters.page - 1)"><ChevronLeft :size="18" /></button><span>{{ filters.page }} / {{ result.totalPages }}</span><button type="button" title="下一页" :disabled="filters.page >= result.totalPages" @click="changePage(filters.page + 1)"><ChevronRight :size="18" /></button></div>
    </div>
    <div v-else class="store-empty"><ShoppingBag :size="40" /><strong>暂时没有符合条件的订单</strong><span>跟随喜欢的团长参加第一场团购</span><RouterLink class="btn btn-buy" to="/leaders">去逛团长广场</RouterLink></div>
  </div>
</template>
