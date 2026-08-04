<script setup>
import { ArrowRight, BadgeCheck, LockKeyhole, ShoppingCart, Trash2, Truck } from '@lucide/vue'
import { computed } from 'vue'
import QuantityStepper from '../components/QuantityStepper.vue'
import StoreBreadcrumb from '../components/StoreBreadcrumb.vue'
import { useShop } from '../state/shop'

const { cartItems, cartCount, cartSubtotal, updateQuantity, removeFromCart } = useShop()
const groups = computed(() => {
  const map = new Map()
  cartItems.value.forEach((item) => {
    if (!map.has(item.leaderId)) map.set(item.leaderId, { leader: item.leader, items: [] })
    map.get(item.leaderId).items.push(item)
  })
  return [...map.values()]
})
const savings = computed(() => cartItems.value.reduce((sum, item) => sum + (item.product.originalPrice - item.product.price) * item.quantity, 0))
</script>

<template>
  <div class="store-container page-space cart-page">
    <StoreBreadcrumb :items="[{ label: '购物车' }]" />
    <div class="cart-title"><div><ShoppingCart :size="25" /><h1>购物车</h1></div><span>共 {{ cartCount }} 件商品</span></div>

    <div v-if="cartItems.length" class="cart-layout">
      <div class="cart-groups">
        <section v-for="group in groups" :key="group.leader.id" class="cart-leader-group">
          <header>
            <RouterLink :to="`/leaders/${group.leader.id}`"><img :src="group.leader.avatar" alt="" /><strong>{{ group.leader.name }}团长</strong><BadgeCheck :size="16" /></RouterLink>
            <span>{{ group.leader.area }}</span>
          </header>
          <article v-for="item in group.items" :key="`${item.productId}-${item.leaderId}`" class="cart-item">
            <RouterLink :to="`/products/${item.product.id}?leader=${item.leader.id}`"><img :src="item.product.image" :alt="item.product.name" /></RouterLink>
            <div class="cart-item-main">
              <RouterLink :to="`/products/${item.product.id}?leader=${item.leader.id}`">{{ item.product.name }}</RouterLink>
              <span>{{ item.product.spec }} · {{ item.product.storage }}</span>
              <small><Truck :size="13" />{{ item.product.delivery }}</small>
            </div>
            <div class="cart-unit-price">¥{{ item.product.price.toFixed(2) }}</div>
            <QuantityStepper :model-value="item.quantity" :max="item.product.stock" @update:model-value="updateQuantity(item.productId, item.leaderId, $event)" />
            <strong class="cart-line-total">¥{{ (item.product.price * item.quantity).toFixed(2) }}</strong>
            <button class="cart-remove" type="button" title="移出购物车" @click="removeFromCart(item.productId, item.leaderId)"><Trash2 :size="18" /></button>
          </article>
          <footer><span>本团 {{ group.items.reduce((sum, item) => sum + item.quantity, 0) }} 件商品</span><strong>截团时间以各商品页面为准</strong></footer>
        </section>
      </div>

      <aside class="cart-summary">
        <h2>订单汇总</h2>
        <dl><div><dt>商品小计</dt><dd>¥{{ cartSubtotal.toFixed(2) }}</dd></div><div><dt>团购优惠</dt><dd class="saving">-¥{{ savings.toFixed(2) }}</dd></div><div><dt>预计运费</dt><dd>结算时计算</dd></div></dl>
        <div class="summary-total-row"><span>预计合计</span><strong>¥{{ cartSubtotal.toFixed(2) }}</strong></div>
        <RouterLink class="btn btn-buy w-100 checkout-button" to="/checkout">去结算<ArrowRight :size="18" /></RouterLink>
        <small><LockKeyhole :size="14" />价格、库存和优惠将在提交时由服务器确认</small>
      </aside>
    </div>

    <div v-else class="store-empty cart-empty"><ShoppingCart :size="42" /><strong>购物车还是空的</strong><span>从喜欢的团长主页挑选正在开团的商品</span><RouterLink class="btn btn-buy" to="/leaders">去逛团长广场</RouterLink></div>
  </div>
</template>
