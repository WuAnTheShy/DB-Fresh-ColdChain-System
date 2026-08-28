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
            <RouterLink class="cart-leader-identity" :to="`/leaders/${group.leader.id}`"><img :src="group.leader.avatar" alt="" /><strong>{{ group.leader.name }}团长</strong><BadgeCheck :size="16" /></RouterLink>
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

    <div v-else class="store-empty cart-empty"><ShoppingCart :size="42" /><strong>购物车还是空的</strong><span>浏览正在开团的生鲜商品</span><RouterLink class="btn btn-buy" to="/search">去逛全部商品</RouterLink></div>
  </div>
</template>

<style scoped>
.cart-groups { display: flex; min-width: 0; flex-direction: column; gap: 14px; }
.cart-leader-group { border: 1px solid var(--line); background: #fff; }
.cart-leader-group > header { display: flex; min-height: 56px; align-items: center; justify-content: space-between; gap: 12px; padding: 0 16px; border-bottom: 1px solid var(--line); background: #f7faf8; }
.cart-leader-identity { display: flex; align-items: center; gap: 6px; color: var(--ink); text-decoration: none; }
.cart-leader-group > header img { width: 30px; height: 30px; border-radius: 50%; object-fit: cover; }
.cart-leader-group > header svg { color: var(--brand); }
.cart-leader-group > header > span { color: var(--muted); font-size: 9px; }
.cart-item { display: grid; grid-template-columns: 94px minmax(160px, 1fr) 80px 120px 90px 34px; gap: 12px; align-items: center; padding: 16px; border-bottom: 1px solid var(--line); }
.cart-item > a > img { width: 94px; height: 94px; object-fit: cover; }
.cart-item-main { display: flex; min-width: 0; flex-direction: column; }
.cart-item-main > a { display: -webkit-box; overflow: hidden; font-weight: 700; text-decoration: none; -webkit-box-orient: vertical; -webkit-line-clamp: 2; }
.cart-item-main > span { margin-top: 5px; color: var(--muted); font-size: 10px; }
.cart-item-main > small { display: flex; align-items: center; gap: 4px; margin-top: 9px; color: var(--brand); font-size: 9px; }
.cart-unit-price { color: #4e5a54; font-size: 11px; }
.cart-line-total { color: var(--danger); text-align: right; }
.cart-remove, .refresh-button { display: inline-flex; width: 34px; height: 34px; align-items: center; justify-content: center; border: 0; border-radius: 4px; background: transparent; color: #69746f; }
.cart-remove:hover { background: #fff0ef; color: var(--danger); }
.cart-leader-group > footer { display: flex; min-height: 42px; align-items: center; justify-content: space-between; padding: 0 16px; background: #fafbfa; color: var(--muted); font-size: 9px; }
.cart-leader-group > footer strong { color: #745910; }
.cart-summary { position: sticky; top: 130px; padding: 19px; border: 1px solid #cfd7d3; border-radius: 6px; background: #fff; box-shadow: 0 3px 10px rgba(23, 33, 29, .07); }
.cart-summary h2 { margin: 0 0 16px; font-size: 17px; }
.cart-summary dl { margin: 0; }
.cart-summary dl > div { display: flex; justify-content: space-between; gap: 12px; margin-bottom: 10px; font-size: 11px; }
.cart-summary dt { color: var(--muted); font-weight: 500; }
.cart-summary dd { margin: 0; }
.saving { color: var(--brand); }
.summary-total-row { display: flex; align-items: baseline; justify-content: space-between; gap: 10px; margin-top: 15px; padding-top: 15px; border-top: 1px solid var(--line); }
.summary-total-row strong { color: var(--danger); font-size: 23px; }
.checkout-button { margin-top: 16px; }
.cart-summary > small { display: flex; align-items: flex-start; gap: 4px; margin-top: 11px; color: var(--muted); font-size: 9px; line-height: 1.45; }
.cart-empty { min-height: 420px; }

@media (max-width: 1199.98px) {
  .cart-item { grid-template-columns: 80px minmax(140px, 1fr) 110px 80px 34px; }
  .cart-item > a > img { width: 80px; height: 80px; }
  .cart-unit-price { display: none; }
}

@media (max-width: 991.98px) {
  .cart-summary { position: static; }
}

@media (max-width: 767.98px) {
  .cart-item { grid-template-columns: 70px minmax(0, 1fr) 34px; gap: 9px; align-items: start; }
  .cart-item > a > img { width: 70px; height: 70px; }
  .cart-item-main { min-height: 70px; }
  .cart-item .quantity-stepper { grid-column: 2; width: 110px; }
  .cart-line-total { grid-column: 3; grid-row: 2; align-self: center; }
  .cart-remove { grid-column: 3; grid-row: 1; }
  .cart-leader-group > footer { align-items: flex-start; flex-direction: column; justify-content: center; gap: 3px; }
}
</style>
