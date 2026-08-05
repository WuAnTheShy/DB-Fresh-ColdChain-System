<script setup>
import { Clock3, ShieldCheck, Snowflake, Truck } from '@lucide/vue'
import ProductCard from '../components/ProductCard.vue'
import { useShop } from '../state/shop'

const { categories, products } = useShop()
</script>

<template>
  <div class="home-page">
    <section class="amazon-promo-grid" aria-label="今日精选">
      <RouterLink class="amazon-promo-card promo-cherry" to="/products/1?leader=1">
        <div class="promo-copy"><h1>产地冷链<br />车厘子礼盒</h1></div>
        <img :src="products[0].image" alt="车厘子礼盒" />
        <strong>点击选购</strong>
      </RouterLink>
      <RouterLink class="amazon-promo-card promo-seafood" to="/products/2?leader=2">
        <div class="promo-copy"><h2>冰鲜三文鱼<br />低温锁鲜</h2></div>
        <img :src="products[1].image" alt="冰鲜三文鱼" />
        <strong>点击选购</strong>
      </RouterLink>
      <RouterLink class="amazon-promo-card promo-vegetable" to="/products/3?leader=1">
        <div class="promo-copy"><h2>有机蔬菜<br />新鲜搭配</h2></div>
        <img :src="products[2].image" alt="有机蔬菜组合" />
        <strong>点击选购</strong>
      </RouterLink>
      <RouterLink class="amazon-promo-card promo-delivery" to="/search">
        <div class="promo-copy"><h2>放心下单<br />新鲜到家</h2></div>
        <div class="delivery-visual"><Snowflake :size="76" /><Truck :size="122" /></div>
        <strong>查看全部团购</strong>
      </RouterLink>
    </section>

    <section class="service-strip store-container" aria-label="服务承诺">
      <div><Snowflake :size="22" /><span><strong>全程冷链</strong></span></div>
      <div><ShieldCheck :size="22" /><span><strong>品质严选</strong></span></div>
      <div><Clock3 :size="22" /><span><strong>准时发货</strong></span></div>
      <div><Truck :size="22" /><span><strong>送货上门</strong></span></div>
    </section>

    <section class="home-section amazon-home-panel category-section store-container">
      <div class="section-title-row"><div><h2>按品类选购</h2></div></div>
      <div class="category-grid">
        <RouterLink v-for="category in categories" :key="category.slug" :to="`/category/${category.slug}`" class="category-tile">
          <img :src="category.image" :alt="category.name" />
          <span><strong>{{ category.name }}</strong></span>
        </RouterLink>
      </div>
    </section>

    <section class="home-section amazon-home-panel store-container">
      <div class="section-title-row"><div><h2>今日特价</h2></div></div>
      <div class="product-grid">
        <ProductCard v-for="product in products" :key="product.id" :product="product" />
      </div>
    </section>

  </div>
</template>
