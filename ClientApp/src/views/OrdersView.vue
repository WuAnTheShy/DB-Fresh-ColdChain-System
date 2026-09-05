<script setup>
import { BadgeCheck, ChevronLeft, ChevronRight, PackageSearch, Search, ShoppingBag } from '@lucide/vue'
import { onMounted, reactive, ref } from 'vue'
import StatusBadge from '../components/StatusBadge.vue'
import { api } from '../services/api'
import { useCustomerContext } from '../state/customer'

const { customerId } = useCustomerContext()
const loading = ref(true)
const error = ref('')
const result = ref({ orders: [], totalCount: 0, totalPages: 0 })
const filters = reactive({ status: '', keyword: '', page: 1, pageSize: 8 })
const tabs = [
  { value: '', label: '全部订单' },
  { value: 'PendingPayment', label: '待支付' },
  { value: 'Paid', label: '待发货' },
  { value: 'Shipped', label: '配送中' },
  { value: 'Completed', label: '已完成' },
  { value: 'Refunding', label: '退款售后' },
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
    <div class="account-page-header">
      <div>
        <PackageSearch :size="26" /><span>
          <h1>我的订单</h1>
        </span>
      </div>
      <form class="order-search" @submit.prevent="search"><input v-model.trim="filters.keyword" type="search"
          placeholder="搜索订单号" /><button type="submit" title="搜索订单">
          <Search :size="18" />
        </button></form>
    </div>

    <nav class="order-tabs" aria-label="订单状态">
      <button v-for="tab in tabs" :key="String(tab.value)" type="button"
        :class="{ active: filters.status === tab.value }" @click="setStatus(tab.value)">{{ tab.label }}</button>
    </nav>

    <div v-if="error" class="alert alert-danger">{{ error }}</div>
    <div v-if="loading" class="store-loading"><span class="spinner-border spinner-border-sm"></span>正在读取订单</div>
    <div v-else-if="result.orders.length" class="order-card-list">
      <article v-for="order in result.orders" :key="order.orderId" class="consumer-order-card">
        <header>
          <div><span>{{ date(order.createdAt) }}</span><strong>订单号 {{ order.orderNo }}</strong></div>
          <StatusBadge :status="order.orderStatus" />
        </header>
        <div class="order-card-body">
          <div class="order-leader-identity"><span class="leader-order-avatar">团</span>
            <div><strong>{{ order.promoterName ? `${order.promoterName}团长` : '社区认证团长' }}</strong><small>
                <BadgeCheck :size="13" />团长带货订单
              </small></div>
          </div>
          <div class="order-card-metric"><span>商品数量</span><strong>{{ order.itemCount }} 件</strong></div>
          <div class="order-card-metric"><span>实付金额</span><strong>{{ money(order.finalAmount) }}</strong></div>
          <div class="order-card-actions">
            <RouterLink class="btn btn-sm btn-outline-secondary" :to="`/orders/${order.orderId}`">查看详情</RouterLink>
            <RouterLink v-if="order.orderStatus === 'COMPLETED'" class="btn btn-sm btn-cart" to="/">再次购买</RouterLink>
          </div>
        </div>
      </article>
      <div v-if="result.totalPages > 1" class="store-pagination"><button type="button" title="上一页"
          :disabled="filters.page <= 1" @click="changePage(filters.page - 1)">
          <ChevronLeft :size="18" />
        </button><span>{{ filters.page }} / {{ result.totalPages }}</span><button type="button" title="下一页"
          :disabled="filters.page >= result.totalPages" @click="changePage(filters.page + 1)">
          <ChevronRight :size="18" />
        </button></div>
    </div>
    <div v-else class="store-empty">
      <ShoppingBag :size="40" /><strong>暂时没有符合条件的订单</strong><span>浏览商品并购买第一件生鲜商品</span>
      <RouterLink class="btn btn-buy" to="/search">去逛全部商品</RouterLink>
    </div>
  </div>
</template>

<style scoped>
.order-search {
  display: grid;
  width: min(320px, 100%);
  height: 40px;
  grid-template-columns: 1fr 42px;
  overflow: hidden;
  border: 1px solid #cbd3cf;
  border-radius: 5px;
  background: #fff;
}

.order-search input,
.order-search button {
  border: 0;
  outline: 0;
  background: transparent;
}

.order-search input {
  min-width: 0;
  padding: 0 11px;
}

.order-search button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  background: #eef1ef;
}

.order-tabs {
  display: flex;
  margin-bottom: 15px;
  overflow-x: auto;
  border-bottom: 1px solid #bec8c3;
  background: #fff;
}

.order-tabs button {
  min-width: 90px;
  min-height: 47px;
  padding: 0 14px;
  border: 0;
  border-bottom: 3px solid transparent;
  background: transparent;
  color: #58645e;
  font-size: 11px;
  font-weight: 700;
}

.order-tabs button.active {
  border-bottom-color: var(--brand);
  color: var(--brand);
}

.order-card-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.consumer-order-card {
  border: 1px solid var(--line);
  background: #fff;
}

.consumer-order-card>header {
  display: flex;
  min-height: 46px;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 0 14px;
  border-bottom: 1px solid var(--line);
  background: #f7f9f8;
}

.consumer-order-card>header>div {
  display: flex;
  flex-wrap: wrap;
  gap: 14px;
  color: var(--muted);
  font-size: 9px;
}

.consumer-order-card>header strong {
  color: #435049;
}

.order-card-body {
  display: grid;
  grid-template-columns: minmax(210px, 1.2fr) repeat(2, minmax(90px, .5fr)) auto;
  gap: 18px;
  align-items: center;
  padding: 17px;
}

.order-leader-identity {
  display: flex;
  align-items: center;
  gap: 10px;
}

.leader-order-avatar {
  display: inline-flex;
  width: 42px;
  height: 42px;
  flex: 0 0 42px;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
  background: #dfeee7;
  color: var(--brand);
  font-weight: 800;
}

.order-leader-identity>div,
.order-card-metric {
  display: flex;
  flex-direction: column;
}

.order-leader-identity small {
  display: flex;
  align-items: center;
  gap: 3px;
  margin-top: 3px;
  color: var(--brand);
  font-size: 9px;
}

.order-card-metric span {
  color: var(--muted);
  font-size: 9px;
}

.order-card-metric strong {
  margin-top: 4px;
}

.order-card-actions {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: 7px;
}

.store-pagination {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
  margin: 10px 0;
}

.store-pagination button {
  display: inline-flex;
  width: 36px;
  height: 36px;
  align-items: center;
  justify-content: center;
  border: 1px solid #cad2ce;
  border-radius: 4px;
  background: #fff;
}

.store-pagination span {
  color: var(--muted);
  font-size: 10px;
}

@media (max-width: 991.98px) {
  .order-card-body {
    grid-template-columns: minmax(190px, 1fr) repeat(2, 90px);
  }

  .order-card-actions {
    grid-column: 1 / -1;
  }
}

@media (max-width: 767.98px) {
  .order-search {
    width: 100%;
  }

  .order-card-body {
    grid-template-columns: 1fr 1fr;
  }

  .order-leader-identity {
    grid-column: 1 / -1;
  }

  .order-card-actions {
    justify-content: flex-start;
  }
}
</style>
