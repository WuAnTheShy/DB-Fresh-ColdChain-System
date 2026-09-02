<script setup>
import { ArrowRight, BadgeCheck, LockKeyhole, ShoppingCart, Trash2, Truck } from '@lucide/vue'
import { computed } from 'vue'
import QuantityStepper from '../components/QuantityStepper.vue'
import { useShop } from '../state/shop'

const {
  cartItems,
  cartCount,
  selectedCartCount,
  selectedCartSubtotal,
  updateQuantity,
  removeFromCart,
  setCartItemSelected,
  setLeaderCartSelected,
  setAllCartSelected,
} = useShop()
const allSelected = computed(() => cartItems.value.length > 0 && cartItems.value.every((item) => item.selected))
const groups = computed(() => {
  const map = new Map()
  cartItems.value.forEach((item) => {
    if (!map.has(item.leaderId)) map.set(item.leaderId, { leader: item.leader, items: [] })
    map.get(item.leaderId).items.push(item)
  })
  return [...map.values()].map((group) => ({
    ...group,
    selected: group.items.every((item) => item.selected),
    selectedCount: group.items.filter((item) => item.selected).reduce((sum, item) => sum + item.quantity, 0),
  }))
})
</script>

<template>
  <div class="store-container page-space cart-page">
    <div class="cart-title"><div><ShoppingCart :size="25" /><h1>购物车</h1></div><span>共 {{ cartCount }} 件商品</span></div>

    <div v-if="cartItems.length" class="cart-layout">
      <div class="cart-groups">
        <div class="cart-select-toolbar">
          <label><input class="form-check-input" type="checkbox" :checked="allSelected" @change="setAllCartSelected($event.target.checked)" />全选</label>
          <span>已选 {{ selectedCartCount }} 件商品</span>
        </div>
        <section v-for="group in groups" :key="group.leader.id" class="cart-leader-group">
          <header>
            <label class="cart-group-check"><input class="form-check-input" type="checkbox" :checked="group.selected" @change="setLeaderCartSelected(group.leader.id, $event.target.checked)" /><span class="visually-hidden">选择{{ group.leader.name }}团长全部商品</span></label>
            <RouterLink class="cart-leader-identity" :to="`/leaders/${group.leader.id}`"><img :src="group.leader.avatar" alt="" /><strong>{{ group.leader.name }}团长</strong><BadgeCheck :size="16" /></RouterLink>
            <span>{{ group.leader.area }}</span>
          </header>
          <article v-for="item in group.items" :key="`${item.productId}-${item.leaderId}`" class="cart-item">
            <label class="cart-item-check"><input class="form-check-input" type="checkbox" :checked="item.selected" @change="setCartItemSelected(item.productId, $event.target.checked)" /><span class="visually-hidden">选择{{ item.product.name }}</span></label>
            <RouterLink :to="`/products/${item.product.id}`"><img :src="item.product.image" :alt="item.product.name" /></RouterLink>
            <div class="cart-item-main">
              <RouterLink :to="`/products/${item.product.id}`">{{ item.product.name }}</RouterLink>
              <span>{{ item.product.spec }} · {{ item.product.storage }}</span>
              <small><Truck :size="13" />{{ item.product.delivery }}</small>
            </div>
            <div class="cart-unit-price">¥{{ item.product.price.toFixed(2) }}</div>
            <QuantityStepper :model-value="item.quantity" :max="item.product.stock" @update:model-value="updateQuantity(item.productId, $event)" />
            <strong class="cart-line-total">¥{{ (item.product.price * item.quantity).toFixed(2) }}</strong>
            <button class="cart-remove" type="button" title="移出购物车" @click="removeFromCart(item.productId)"><Trash2 :size="18" /></button>
          </article>
          <footer><span>本团长已选 {{ group.selectedCount }} 件商品</span><strong>结算时将按团长生成独立子订单</strong></footer>
        </section>
      </div>

      <aside class="cart-summary">
        <h2>订单汇总</h2>
        <dl><div><dt>已选商品</dt><dd>{{ selectedCartCount }} 件</dd></div><div><dt>商品小计</dt><dd>¥{{ selectedCartSubtotal.toFixed(2) }}</dd></div><div><dt>预计运费</dt><dd>结算时分别计算</dd></div></dl>
        <div class="summary-total-row"><span>预计合计</span><strong>¥{{ selectedCartSubtotal.toFixed(2) }}</strong></div>
        <RouterLink v-if="selectedCartCount" class="btn btn-buy w-100 checkout-button" to="/checkout">去结算<ArrowRight :size="18" /></RouterLink>
        <button v-else class="btn btn-secondary w-100 checkout-button" type="button" disabled>请先选择商品</button>
        <small><LockKeyhole :size="14" />价格和库存将在提交时由服务器确认</small>
      </aside>
    </div>

    <div v-else class="store-empty cart-empty"><ShoppingCart :size="42" /><strong>购物车还是空的</strong><span>浏览正在销售的生鲜商品</span><RouterLink class="btn btn-buy" to="/search">去逛全部商品</RouterLink></div>
  </div>
</template>

<style scoped>
.cart-groups { display: flex; min-width: 0; flex-direction: column; gap: 14px; }
.cart-select-toolbar { display: flex; min-height: 44px; align-items: center; justify-content: space-between; padding: 0 14px; border: 1px solid var(--line); background: #fff; color: var(--muted); font-size: 11px; }
.cart-select-toolbar label, .cart-group-check, .cart-item-check { display: inline-flex; align-items: center; gap: 7px; cursor: pointer; }
.cart-leader-group { border: 1px solid var(--line); background: #fff; }
.cart-leader-group > header { display: flex; min-height: 56px; align-items: center; justify-content: space-between; gap: 12px; padding: 0 16px; border-bottom: 1px solid var(--line); background: #f7faf8; }
.cart-leader-identity { display: flex; align-items: center; gap: 6px; color: var(--ink); text-decoration: none; }
.cart-leader-group > header img { width: 30px; height: 30px; border-radius: 50%; object-fit: cover; }
.cart-leader-group > header svg { color: var(--brand); }
.cart-leader-group > header > span { color: var(--muted); font-size: 9px; }
.cart-item { display: grid; grid-template-columns: 24px 94px minmax(160px, 1fr) 80px 120px 90px 34px; gap: 12px; align-items: center; padding: 16px; border-bottom: 1px solid var(--line); }
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
.summary-total-row { display: flex; align-items: baseline; justify-content: space-between; gap: 10px; margin-top: 15px; padding-top: 15px; border-top: 1px solid var(--line); }
.summary-total-row strong { color: var(--danger); font-size: 23px; }
.checkout-button { margin-top: 16px; }
.cart-summary > small { display: flex; align-items: flex-start; gap: 4px; margin-top: 11px; color: var(--muted); font-size: 9px; line-height: 1.45; }
.cart-empty { min-height: 420px; }

@media (max-width: 1199.98px) {
  .cart-item { grid-template-columns: 24px 80px minmax(140px, 1fr) 110px 80px 34px; }
  .cart-item > a > img { width: 80px; height: 80px; }
  .cart-unit-price { display: none; }
}

@media (max-width: 991.98px) {
  .cart-summary { position: static; }
}

@media (max-width: 767.98px) {
  .cart-item { grid-template-columns: 22px 70px minmax(0, 1fr) 34px; gap: 9px; align-items: start; }
  .cart-item > a > img { width: 70px; height: 70px; }
  .cart-item-main { min-height: 70px; }
  .cart-item .quantity-stepper { grid-column: 3; width: 110px; }
  .cart-line-total { grid-column: 4; grid-row: 2; align-self: center; }
  .cart-remove { grid-column: 4; grid-row: 1; }
  .cart-leader-group > footer { align-items: flex-start; flex-direction: column; justify-content: center; gap: 3px; }
}
</style>
