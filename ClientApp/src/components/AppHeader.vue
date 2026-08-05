<script setup>
import { MapPin, Menu, Search, ShoppingCart, X } from '@lucide/vue'
import { ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useShop } from '../state/shop'

const router = useRouter()
const route = useRoute()
const { categories, cartCount } = useShop()
const keyword = ref(String(route.query.q ?? ''))

watch(() => route.query.q, (value) => {
  keyword.value = String(value ?? '')
})

function search() {
  router.push({ path: '/search', query: keyword.value.trim() ? { q: keyword.value.trim() } : {} })
}
</script>

<template>
  <header class="store-header">
    <div class="header-primary">
      <div class="header-inner header-primary-inner">
        <button
          class="header-icon-button d-lg-none"
          type="button"
          data-bs-toggle="offcanvas"
          data-bs-target="#mobileMenu"
          aria-label="打开菜单"
          title="打开菜单"
        >
          <Menu :size="22" />
        </button>

        <RouterLink class="store-brand" to="/" aria-label="鲜邻团首页">
          <span class="brand-word">鲜邻团</span>
          <span class="brand-smile" aria-hidden="true"></span>
          <small>fresh</small>
        </RouterLink>

        <button class="delivery-location d-none d-xl-flex" type="button" title="选择配送地址">
          <MapPin :size="19" />
          <span><small>配送至</small><strong>上海市浦东新区</strong></span>
        </button>

        <form class="global-search" role="search" @submit.prevent="search">
          <select class="search-category d-none d-md-block" aria-label="商品分类">
            <option>全部</option>
            <option v-for="category in categories" :key="category.slug">{{ category.name }}</option>
          </select>
          <input v-model="keyword" type="search" placeholder="搜索鲜邻团" aria-label="搜索" />
          <button type="submit" aria-label="提交搜索" title="搜索"><Search :size="21" /></button>
        </form>

        <RouterLink class="header-account d-none d-md-flex" to="/profile">
          <span><small>你好，请登录</small><strong>账户与会员</strong></span>
        </RouterLink>
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
        <RouterLink to="/search"><Menu :size="17" />全部分类</RouterLink>
        <RouterLink to="/deals">今日特价</RouterLink>
        <RouterLink v-for="category in categories" :key="category.slug" :to="`/category/${category.slug}`">{{ category.name }}</RouterLink>
        <RouterLink to="/coupons">领券中心</RouterLink>
      </div>
    </nav>

    <div id="mobileMenu" class="offcanvas offcanvas-start mobile-menu" tabindex="-1">
      <div class="offcanvas-header">
        <div class="store-brand"><span class="brand-word">鲜邻团</span><span class="brand-smile" aria-hidden="true"></span><small>fresh</small></div>
        <button class="header-icon-button" type="button" data-bs-dismiss="offcanvas" aria-label="关闭菜单"><X :size="22" /></button>
      </div>
      <div class="offcanvas-body">
        <p class="mobile-menu-label">商城导航</p>
        <RouterLink to="/deals" data-bs-dismiss="offcanvas">今日特价</RouterLink>
        <RouterLink v-for="category in categories" :key="category.slug" :to="`/category/${category.slug}`" data-bs-dismiss="offcanvas">{{ category.name }}</RouterLink>
        <RouterLink to="/coupons" data-bs-dismiss="offcanvas">领券中心</RouterLink>
        <RouterLink to="/addresses" data-bs-dismiss="offcanvas">收货地址</RouterLink>
      </div>
    </div>
  </header>
</template>
