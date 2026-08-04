<script setup>
import { ArrowRight, Clock3, ShieldCheck, Snowflake, Truck } from '@lucide/vue'
import LeaderCard from '../components/LeaderCard.vue'
import ProductCard from '../components/ProductCard.vue'
import { useShop } from '../state/shop'

const { categories, leaders, products } = useShop()
</script>

<template>
  <div class="home-page">
    <section class="amazon-promo-grid" aria-label="今日精选">
      <RouterLink class="amazon-promo-card promo-cherry" to="/products/1?leader=1">
        <div class="promo-copy"><span>林晓晴团长推荐</span><h1>产地冷链<br />车厘子礼盒</h1><p>今晚 22:00 截团</p></div>
        <img :src="products[0].image" alt="车厘子礼盒" />
        <strong>立即参团</strong>
      </RouterLink>
      <RouterLink class="amazon-promo-card promo-seafood" to="/products/2?leader=2">
        <div class="promo-copy"><span>今日鲜捕</span><h2>冰鲜三文鱼<br />低温锁鲜</h2><p>去皮去刺 · 家庭装</p></div>
        <img :src="products[1].image" alt="冰鲜三文鱼" />
        <strong>选购海鲜水产</strong>
      </RouterLink>
      <RouterLink class="amazon-promo-card promo-vegetable" to="/products/3?leader=1">
        <div class="promo-copy"><span>一周好菜</span><h2>有机蔬菜<br />新鲜搭配</h2><p>6 种时令蔬菜组合</p></div>
        <img :src="products[2].image" alt="有机蔬菜组合" />
        <strong>查看家庭菜篮</strong>
      </RouterLink>
      <RouterLink class="amazon-promo-card promo-delivery" to="/search">
        <div class="promo-copy"><span>鲜邻冷链</span><h2>放心下单<br />新鲜到家</h2><p>全程温控可追踪</p></div>
        <div class="delivery-visual"><Snowflake :size="76" /><Truck :size="122" /></div>
        <strong>查看全部在团商品</strong>
      </RouterLink>
    </section>

    <section class="service-strip store-container" aria-label="服务承诺">
      <div><Snowflake :size="22" /><span><strong>全程冷链</strong><small>温控履约可追踪</small></span></div>
      <div><ShieldCheck :size="22" /><span><strong>团长严选</strong><small>平台认证团长带货</small></span></div>
      <div><Clock3 :size="22" /><span><strong>准时截团</strong><small>进度与时间透明</small></span></div>
      <div><Truck :size="22" /><span><strong>社区到家</strong><small>配送信息随时查看</small></span></div>
    </section>

    <section class="home-section amazon-home-panel category-section store-container">
      <div class="section-title-row"><div><h2>按品类选购</h2><p>正在开团的冷链生鲜</p></div><RouterLink to="/search">查看全部<ArrowRight :size="16" /></RouterLink></div>
      <div class="category-grid">
        <RouterLink v-for="(category, index) in categories" :key="category.slug" :to="`/category/${category.slug}`" class="category-tile">
          <img :src="products[index].image" :alt="category.name" />
          <span><strong>{{ category.name }}</strong><small>查看在团商品</small></span>
        </RouterLink>
      </div>
    </section>

    <section class="home-section amazon-home-panel store-container">
      <div class="section-title-row"><div><h2>今天值得跟的团</h2><p>团长亲选，截团时间清晰可见</p></div><RouterLink to="/group-buys">全部团购<ArrowRight :size="16" /></RouterLink></div>
      <div class="product-grid">
        <ProductCard v-for="product in products" :key="product.id" :product="product" />
      </div>
    </section>

    <section class="home-section amazon-home-panel leader-feature-section store-container">
      <div class="section-title-row"><div><h2>认识你的团长</h2><p>先看团长，再选他正在带货的商品</p></div><RouterLink to="/leaders">团长广场<ArrowRight :size="16" /></RouterLink></div>
      <div class="leader-grid">
        <LeaderCard v-for="leader in leaders" :key="leader.id" :leader="leader" />
      </div>
    </section>
  </div>
</template>
