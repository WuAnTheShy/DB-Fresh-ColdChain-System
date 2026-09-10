<script setup>
import { BadgeCheck, ChevronDown, ChevronLeft, ChevronRight, ChevronUp, PackageSearch, Search, ShoppingBag, Store } from '@lucide/vue'
import { onMounted, reactive, ref } from 'vue'
import StatusBadge from '../components/StatusBadge.vue'
import { api } from '../services/api'
import { useCustomerContext } from '../state/customer'

const { customerId } = useCustomerContext()
const loading = ref(true)
const error = ref('')
const result = ref({ orders: [], totalCount: 0, totalPages: 0 })
const expandedOrderIds = ref(new Set())
const filters = reactive({ status: '', keyword: '', page: 1, pageSize: 8 })
const tabs = [
  { value: '', label: '全部订单' },
  { value: 'PendingPayment', label: '待支付' },
  { value: 'Paid', label: '待发货' },
  { value: 'Shipped', label: '配送中' },
  { value: 'Completed', label: '已完成' },
  { value: 'Refunding', label: '退款售后' },
  { value: 'Refunded', label: '已退款' },
]

function money(value) { return `¥${Number(value ?? 0).toFixed(2)}` }
function date(value) { return value ? new Intl.DateTimeFormat('zh-CN', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) : '-' }
function shortDate(value) {
  if (!value) return '--.--'
  const dateValue = new Date(value)
  return `${String(dateValue.getMonth() + 1).padStart(2, '0')}.${String(dateValue.getDate()).padStart(2, '0')}`
}
function orderProducts(order) {
  if (Array.isArray(order.productItems) && order.productItems.length) return order.productItems
  return [{
    orderDetailId: `${order.orderId}-first`,
    productId: order.firstProductId,
    productName: order.firstProductName || '订单商品',
    imageUrl: order.firstProductImageUrl,
    quantity: order.firstProductQuantity || 1,
  }]
}
function visibleProducts(order) {
  const products = orderProducts(order)
  return expandedOrderIds.value.has(order.orderId) ? products : products.slice(0, 3)
}
function toggleProducts(orderId) {
  const next = new Set(expandedOrderIds.value)
  if (next.has(orderId)) next.delete(orderId)
  else next.add(orderId)
  expandedOrderIds.value = next
}
function handleImageError(event) {
  if (event.target.dataset.fallbackApplied) return
  event.target.dataset.fallbackApplied = 'true'
  event.target.src = '/images/homepic.png'
  event.target.classList.add('fallback-photo-tint')
}
function primaryAction(order) {
  const displayStatus = order.displayStatusCode || order.orderStatus
  if (displayStatus === 'REFUND_REVIEWING') return { label: '查看退款', to: `/orders/${order.orderId}`, prominent: true }
  if (order.orderStatus === 'PENDING_PAYMENT' && order.checkoutBatchId) {
    return { label: '立即支付', to: `/payment/${order.checkoutBatchId}`, prominent: true }
  }
  if (order.orderStatus === 'SHIPPED') return { label: '查看物流', to: `/orders/${order.orderId}`, prominent: false }
  if (displayStatus === 'COMPLETED') return { label: '评价', to: `/orders/${order.orderId}`, prominent: true }
  return { label: '查看详情', to: `/orders/${order.orderId}`, prominent: false }
}

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
          <div class="order-card-leader">
            <Store :size="19" />
            <RouterLink v-if="order.promoterId" :to="`/leaders/${order.promoterId}`">
              {{ order.promoterName || '社区认证团长' }}<ChevronRight :size="16" />
            </RouterLink>
            <strong v-else>{{ order.promoterName || '社区认证团长' }}</strong>
            <BadgeCheck :size="15" class="leader-verified" />
            <span>订单号 {{ order.orderNo }}</span>
          </div>
          <StatusBadge :status="order.displayStatusCode || order.orderStatus" :label="order.statusName" />
        </header>
        <div class="order-card-body">
          <div class="order-products">
            <RouterLink v-for="item in visibleProducts(order)" :key="item.orderDetailId" class="order-product"
              :to="`/orders/${order.orderId}`">
              <img :src="item.imageUrl || '/images/homepic.png'" :alt="item.productName" @error="handleImageError" />
              <span class="order-product-copy">
                <strong>{{ item.productName }}</strong>
                <small>团长带货 · 冷链履约</small>
              </span>
              <span class="order-product-aside">
                <strong v-if="item.unitPrice != null">{{ money(item.unitPrice) }}</strong>
                <small>×{{ item.quantity || 1 }}</small>
              </span>
            </RouterLink>
          </div>
          <button v-if="orderProducts(order).length > 3" class="order-products-toggle" type="button"
            :aria-expanded="expandedOrderIds.has(order.orderId)" @click="toggleProducts(order.orderId)">
            <template v-if="expandedOrderIds.has(order.orderId)">收起商品<ChevronUp :size="17" /></template>
            <template v-else>展开其余 {{ orderProducts(order).length - 3 }} 件商品<ChevronDown :size="17" /></template>
          </button>
          <div class="order-card-footer">
            <time :datetime="order.createdAt" :title="date(order.createdAt)">{{ shortDate(order.createdAt) }}</time>
            <div class="order-paid"><span>实付款</span><strong>{{ money(order.finalAmount) }}</strong></div>
            <div class="order-card-actions">
              <RouterLink v-if="primaryAction(order).label !== '查看详情'" class="order-action-link"
                :to="`/orders/${order.orderId}`">更多</RouterLink>
              <RouterLink v-if="order.orderStatus === 'COMPLETED'" class="btn order-action-button" to="/">再买一单</RouterLink>
              <RouterLink class="btn order-action-button" :class="{ prominent: primaryAction(order).prominent }"
                :to="primaryAction(order).to">{{ primaryAction(order).label }}</RouterLink>
            </div>
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
  border-radius: 10px;
  background: #fff;
  overflow: hidden;
}

.consumer-order-card>header {
  display: flex;
  min-height: 54px;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 0 18px;
  border-bottom: 1px solid var(--line);
  background: #fff;
}

.order-card-leader {
  display: flex;
  min-width: 0;
  align-items: center;
  gap: 7px;
  color: #27312c;
}

.order-card-leader>a,
.order-card-leader>strong {
  display: inline-flex;
  min-width: 0;
  align-items: center;
  color: var(--ink);
  font-size: 14px;
  font-weight: 700;
  text-decoration: none;
}

.order-card-leader>a:hover {
  color: var(--brand);
}

.order-card-leader>span {
  margin-left: 9px;
  color: var(--muted);
  font-size: 10px;
  font-weight: 400;
}

.leader-verified {
  flex: 0 0 auto;
  color: #1c9b68;
}

.order-card-body {
  padding: 16px 18px 14px;
}

.order-products {
  display: flex;
  flex-direction: column;
}

.order-product {
  display: grid;
  grid-template-columns: 96px minmax(0, 1fr) auto;
  gap: 16px;
  align-items: start;
  padding: 12px 0;
  color: inherit;
  text-decoration: none;
}

.order-product:first-child {
  padding-top: 0;
}

.order-product:last-child {
  padding-bottom: 0;
}

.order-product+.order-product {
  border-top: 1px solid #eef0ef;
}

.order-product>img {
  width: 96px;
  height: 96px;
  border-radius: 9px;
  background: #f3f5f4;
  object-fit: cover;
}

.order-product-copy {
  display: flex;
  min-width: 0;
  flex-direction: column;
}

.order-product-copy>strong {
  display: -webkit-box;
  overflow: hidden;
  color: #161d19;
  font-size: 17px;
  line-height: 1.45;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
}

.order-product-copy>small {
  margin-top: 8px;
  color: #1c9b68;
  font-size: 12px;
}

.order-product-aside {
  display: flex;
  min-width: 82px;
  flex-direction: column;
  align-items: flex-end;
  gap: 5px;
  padding-top: 3px;
}

.order-product-aside>strong {
  color: #161d19;
  font-size: 15px;
}

.order-product-aside>small {
  color: #7a817d;
  font-size: 13px;
}

.order-products-toggle {
  display: flex;
  width: 100%;
  align-items: center;
  justify-content: center;
  gap: 5px;
  margin-top: 12px;
  padding: 9px;
  border: 0;
  border-top: 1px solid #eef0ef;
  background: transparent;
  color: #52605a;
  font-size: 12px;
}

.order-products-toggle:hover {
  color: var(--brand);
}

.order-card-footer {
  display: grid;
  min-height: 58px;
  grid-template-columns: 1fr auto auto;
  gap: 20px;
  align-items: center;
  margin-top: 12px;
  padding-top: 13px;
  border-top: 1px solid #eef0ef;
}

.order-card-footer>time {
  color: #7a817d;
  font-size: 12px;
}

.order-paid {
  display: flex;
  align-items: baseline;
  gap: 8px;
}

.order-paid>span {
  font-size: 13px;
}

.order-paid>strong {
  color: #161d19;
  font-size: 22px;
  letter-spacing: -.5px;
}

.order-card-actions {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  align-items: center;
  gap: 9px;
}

.order-action-link {
  padding: 7px 4px;
  color: #4e5752;
  font-size: 12px;
  text-decoration: none;
}

.order-action-button {
  min-width: 94px;
  min-height: 38px;
  padding: 7px 18px;
  border: 1px solid #d9dedb;
  border-radius: 8px;
  background: #f7f8f8;
  color: #222a26;
  font-size: 13px;
}

.order-action-button:hover {
  border-color: #bfc8c3;
  background: #eef1ef;
}

.order-action-button.prominent {
  border-color: #ffd9c9;
  background: #fff1e9;
  color: #e65d20;
}

.order-action-button.prominent:hover {
  background: #ffe7da;
  color: #cf4910;
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
  .order-card-footer {
    grid-template-columns: 1fr auto;
  }

  .order-card-actions {
    grid-column: 1 / -1;
  }
}

@media (max-width: 767.98px) {
  .order-search {
    width: 100%;
  }

  .consumer-order-card>header {
    align-items: flex-start;
    padding: 13px 14px;
  }

  .order-card-leader {
    flex-wrap: wrap;
  }

  .order-card-leader>span {
    width: 100%;
    margin-left: 26px;
  }

  .order-card-body {
    padding: 14px;
  }

  .order-product {
    grid-template-columns: 82px minmax(0, 1fr) auto;
    gap: 11px;
  }

  .order-product>img {
    width: 82px;
    height: 82px;
  }

  .order-product-copy>strong {
    font-size: 14px;
  }

  .order-card-footer {
    gap: 10px;
  }

  .order-paid>strong {
    font-size: 19px;
  }

  .order-card-actions {
    display: grid;
    grid-template-columns: auto repeat(2, minmax(0, 1fr));
  }

  .order-action-button {
    min-width: 0;
    padding-right: 12px;
    padding-left: 12px;
  }
}

@media (max-width: 419.98px) {
  .order-card-leader>span {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .order-product {
    grid-template-columns: 72px minmax(0, 1fr) auto;
  }

  .order-product>img {
    width: 72px;
    height: 72px;
  }

  .order-product-copy>small {
    margin-top: 4px;
  }

  .order-product-copy>small {
    margin-top: 5px;
    font-size: 11px;
  }

  .order-product-aside {
    min-width: 66px;
  }

  .order-product-aside>strong {
    font-size: 13px;
  }
}
</style>
