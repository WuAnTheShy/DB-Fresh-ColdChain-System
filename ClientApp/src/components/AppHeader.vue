<script setup>
import { LogOut, MapPin, Menu, Search, ShoppingCart, X } from '@lucide/vue'
import { ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useShop } from '../state/shop'
import { api } from '../services/api'
import { useCustomerContext } from '../state/customer'

const router = useRouter()
const route = useRoute()
const { categories, cartCount } = useShop()
const { customerName, isAuthenticated, deliveryLocation, clearCustomer } = useCustomerContext()
const keyword = ref(String(route.query.q ?? ''))
const loggingOut = ref(false)

watch(() => route.query.q, (value) => {
  keyword.value = String(value ?? '')
})

function search() {
  router.push({ path: '/search', query: keyword.value.trim() ? { q: keyword.value.trim() } : {} })
}

async function logout() {
  loggingOut.value = true
  try {
    await api.logoutCustomer()
    clearCustomer()
    await router.push('/')
  } finally {
    loggingOut.value = false
  }
}
</script>

<template>
  <header class="store-header">
    <div class="header-primary">
      <div class="header-inner header-primary-inner">
        <button class="header-icon-button d-lg-none" type="button" data-bs-toggle="offcanvas"
          data-bs-target="#mobileMenu" aria-label="打开菜单" title="打开菜单">
          <Menu :size="22" />
        </button>

        <RouterLink class="store-brand" to="/" aria-label="鲜邻团首页">
          <span class="brand-word">鲜邻团</span>
          <span class="brand-smile" aria-hidden="true"></span>
          <small>fresh</small>
        </RouterLink>

        <RouterLink class="delivery-location d-none d-xl-flex" :to="isAuthenticated ? '/addresses' : '/auth'" title="管理配送地址">
          <MapPin :size="19" />
          <span><small>配送至</small><strong>{{ deliveryLocation }}</strong></span>
        </RouterLink>

        <form class="global-search" role="search" @submit.prevent="search">
          <select class="search-category d-none d-md-block" aria-label="商品分类">
            <option>全部</option>
            <option v-for="category in categories" :key="category.slug">{{ category.name }}</option>
          </select>
          <input v-model="keyword" type="search" placeholder="搜索鲜邻团" aria-label="搜索" />
          <button type="submit" aria-label="提交搜索" title="搜索">
            <Search :size="21" />
          </button>
        </form>

        <div class="header-account-area d-none d-md-flex">
          <RouterLink class="header-account" :to="isAuthenticated ? '/profile' : '/auth'">
            <span><small>{{ isAuthenticated ? `你好，${customerName}` : '你好，请登录' }}</small><strong>账户与会员</strong></span>
          </RouterLink>
          <button v-if="isAuthenticated" class="header-logout" type="button" :disabled="loggingOut" title="退出登录" aria-label="退出登录" @click="logout"><LogOut :size="17" /></button>
        </div>
        <RouterLink class="header-account d-none d-lg-flex" to="/orders">
          <span><small>退换货</small><strong>与订单</strong></span>
        </RouterLink>
        <RouterLink class="header-cart" to="/cart" aria-label="购物车">
          <ShoppingCart :size="28" />
          <span class="cart-count">{{ cartCount }}</span>
          <strong class="d-none d-xl-inline">购物车</strong>
        </RouterLink>
      </div>
    </div>

    <nav class="header-secondary d-none d-lg-block" aria-label="商城主导航">
      <div class="header-inner secondary-inner">
        <RouterLink to="/search">
          <Menu :size="17" />全部分类
        </RouterLink>
        <RouterLink to="/following">我的关注</RouterLink>
        <RouterLink v-for="category in categories" :key="category.slug" :to="`/category/${category.slug}`">{{
          category.name }}</RouterLink>
        <RouterLink to="/coupons">领券中心</RouterLink>
      </div>
    </nav>

    <div id="mobileMenu" class="offcanvas offcanvas-start mobile-menu" tabindex="-1">
      <div class="offcanvas-header">
        <div class="store-brand"><span class="brand-word">鲜邻团</span><span class="brand-smile"
            aria-hidden="true"></span><small>fresh</small></div>
        <button class="header-icon-button" type="button" data-bs-dismiss="offcanvas" aria-label="关闭菜单">
          <X :size="22" />
        </button>
      </div>
      <div class="offcanvas-body">
        <p class="mobile-menu-label">商城导航</p>
        <RouterLink to="/following" data-bs-dismiss="offcanvas">我的关注</RouterLink>
        <RouterLink v-for="category in categories" :key="category.slug" :to="`/category/${category.slug}`"
          data-bs-dismiss="offcanvas">{{ category.name }}</RouterLink>
        <RouterLink to="/coupons" data-bs-dismiss="offcanvas">领券中心</RouterLink>
        <RouterLink to="/addresses" data-bs-dismiss="offcanvas">收货地址</RouterLink>
        <RouterLink :to="isAuthenticated ? '/profile' : '/auth'" data-bs-dismiss="offcanvas">{{ isAuthenticated ? `${customerName}的账户` : '登录 / 注册' }}</RouterLink>
        <button v-if="isAuthenticated" class="mobile-logout" type="button" data-bs-dismiss="offcanvas" :disabled="loggingOut" @click="logout"><LogOut :size="17" />退出登录</button>
      </div>
    </div>
  </header>
</template>

<style scoped>
.store-header {
  position: sticky;
  top: 0;
  z-index: 1040;
  box-shadow: 0 2px 8px rgba(12, 18, 15, .16);
}

.header-primary {
  background: var(--brand);
  color: #fff;
}

.header-primary-inner {
  display: flex;
  min-height: 70px;
  align-items: center;
  gap: 18px;
}

.delivery-location,
.header-account,
.header-cart {
  display: flex;
  flex: 0 0 auto;
  align-items: center;
  gap: 7px;
  border: 1px solid transparent;
  background: transparent;
  color: #fff;
  text-decoration: none;
}

.header-account-area { position: relative; align-items: center; }
.header-account-area .header-account { padding-right: 31px; }
.header-logout { position: absolute; right: 4px; display: inline-flex; width: 27px; height: 36px; align-items: center; justify-content: center; border: 0; background: transparent; color: #ddd; }
.header-logout:hover { color: var(--amber); }

.delivery-location {
  padding: 7px;
}

.delivery-location:hover,
.header-account:hover,
.delivery-location:hover small,
.header-account:hover small,
.header-cart:hover {
  background: var(--amber-hover);
  color: #000;
}

.delivery-location span,
.header-account span {
  display: flex;
  flex-direction: column;
  text-align: left;
}

.delivery-location small,
.header-account small {
  color: #b9c2be;
  font-size: 10px;
  line-height: 1.2;
}

.delivery-location strong,
.header-account strong {
  font-size: 12px;
  white-space: nowrap;
}

.global-search {
  display: grid;
  min-width: 220px;
  flex: 1 1 auto;
  grid-template-columns: auto 1fr 48px;
  height: 44px;
  overflow: hidden;
  border: 3px solid transparent;
  border-radius: 7px;
  background: #fff;
}

.global-search:focus-within {
  border-color: var(--amber);
}

.global-search select,
.global-search input,
.global-search button {
  height: 100%;
  border: 0;
  outline: 0;
}

.search-category {
  width: 82px;
  padding: 0 8px;
  border-right: 1px solid #d9dedb !important;
  background: #eef1ef;
  color: #4f5b55;
  font-size: 11px;
}

.global-search input {
  min-width: 0;
  padding: 0 13px;
  color: var(--ink);
}

.global-search button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  background: var(--amber);
  color: #2e2209;
}

.global-search button:hover {
  background: var(--amber-hover);
}

.header-account {
  min-height: 43px;
  padding: 5px 7px;
  border-radius: 4px;
}

.header-cart {
  position: relative;
  display: flex;
  min-height: 43px;
  flex: 0 0 auto;
  align-items: center;
  gap: 5px;
  padding: 5px 7px;
  border: 1px solid transparent;
  border-radius: 4px;
  color: #fff;
  text-decoration: none;
}

.header-cart strong {
  align-self: flex-end;
  padding-bottom: 4px;
  font-size: 12px;
}

.cart-count {
  position: absolute;
  top: 1px;
  left: 25px;
  min-width: 19px;
  color: var(--amber);
  font-size: 15px;
  font-weight: 800;
  text-align: center;
}

.header-icon-button {
  display: inline-flex;
  width: 40px;
  height: 40px;
  flex: 0 0 40px;
  align-items: center;
  justify-content: center;
  border: 0;
  border-radius: 4px;
  background: transparent;
  color: #fff;
}

.header-secondary {
  background: var(--brand);
  color: #f5f7f6;
}

.secondary-inner {
  display: flex;
  min-height: 38px;
  align-items: stretch;
  overflow: hidden;
}

.secondary-inner a {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  padding: 0 15px;
  border: 1px solid transparent;
  font-size: 12px;
  font-weight: 650;
  text-decoration: none;
  white-space: nowrap;
}

.secondary-inner a:hover,
.secondary-inner a.router-link-active {
  background: var(--amber-hover);
  color: #000;
}

.secondary-inner a.router-link-active {
    font-weight: 700;
}



.mobile-menu {
  --bs-offcanvas-width: 290px;
  background: var(--brand);
  color: #fff;
}

.mobile-menu .offcanvas-header {
  border-bottom: 1px solid #39453f;
}

.mobile-menu .offcanvas-body {
  display: flex;
  flex-direction: column;
  padding: 14px;
}

.mobile-menu .offcanvas-body a {
  padding: 12px 9px;
  border-bottom: 1px solid #303b36;
  color: #e8edeb;
  text-decoration: none;
}

.mobile-menu-label {
  margin: 4px 9px 8px;
  color: #8fa099;
  font-size: 11px;
  font-weight: 700;
}

.mobile-logout { display: flex; align-items: center; gap: 8px; padding: 12px 9px; border: 0; border-bottom: 1px solid #303b36; background: transparent; color: #e8edeb; text-align: left; }

/* Amazon-inspired storefront refresh */
.store-header {
  box-shadow: none;
}

.header-primary-inner {
  min-height: 60px;
  gap: 13px;
}

.delivery-location,
.header-account,
.header-cart {
  min-height: 50px;
  padding: 5px 8px;
  border-radius: 2px;
}



.delivery-location small,
.header-account small {
  color: #ccc;
  font-size: 11px;
}

.delivery-location strong,
.header-account strong {
  font-size: 13px;
}

.global-search {
  height: 44px;
  border-width: 3px;
  border-radius: 7px;
}

.search-category {
  width: 70px;
  background: #e6e6e6;
  color: #555;
}

.global-search button {
  background: #febd69;
}

.global-search button:hover {
  background: #f3a847;
}

.header-cart strong {
  font-size: 13px;
}

.cart-count {
  color: #f08804;
  font-size: 17px;
}

.secondary-inner {
  min-height: 39px;
}

.secondary-inner a {
  padding: 0 11px;
  font-size: 13px;
  font-weight: 500;
}

/* .secondary-inner a:first-child {
  font-weight: 700;
} */

@media (max-width: 991.98px) {
  .header-primary-inner {
    min-height: 64px;
  }

  .store-brand small {
    display: none;
  }
}

@media (max-width: 767.98px) {
  .header-primary-inner {
    display: grid;
    min-height: 108px;
    grid-template-columns: 40px auto 1fr auto;
    grid-template-rows: 50px 47px;
    gap: 0 8px;
    padding: 5px 0 6px;
  }

  .store-brand {
    grid-column: 2;
  }

  .global-search {
    grid-column: 1 / -1;
    grid-row: 2;
    height: 42px;
  }

  .header-cart {
    grid-column: 4;
    grid-row: 1;
  }

  .cart-count {
    left: 24px;
  }
}
</style>
