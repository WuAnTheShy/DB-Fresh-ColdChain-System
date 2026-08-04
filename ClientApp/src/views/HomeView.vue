<script setup>
import { ArrowRight, Clock3, MapPin, ShieldCheck, Snowflake, Truck } from '@lucide/vue'
import LeaderCard from '../components/LeaderCard.vue'
import ProductCard from '../components/ProductCard.vue'
import { useShop } from '../state/shop'

const { categories, leaders, products } = useShop()
</script>

<template>
  <div class="store-container home-page">
    <section class="shop-hero">
      <img :src="products[0].image" alt="当季车厘子团购" />
      <div class="shop-hero-overlay">
        <span class="hero-kicker">林晓晴团长今日主推</span>
        <h1>产地冷链车厘子<br />今晚 22:00 截团</h1>
        <p>JJ级 2.5kg礼盒，明日送达社区。已团 286 件，接近成团目标。</p>
        <div class="hero-actions">
          <RouterLink class="btn btn-buy" to="/products/1?leader=1">立即参团</RouterLink>
          <RouterLink class="btn btn-light" to="/leaders/1">查看团长主页</RouterLink>
        </div>
      </div>
      <aside class="hero-delivery d-none d-lg-block">
        <span><MapPin :size="18" /></span>
        <small>当前配送区域</small>
        <strong>上海市浦东新区</strong>
        <p>今日 22:00 前下单，最快明日送达</p>
      </aside>
    </section>

    <section class="service-strip" aria-label="服务承诺">
      <div><Snowflake :size="22" /><span><strong>全程冷链</strong><small>温控履约可追踪</small></span></div>
      <div><ShieldCheck :size="22" /><span><strong>团长严选</strong><small>平台认证团长带货</small></span></div>
      <div><Clock3 :size="22" /><span><strong>准时截团</strong><small>进度与时间透明</small></span></div>
      <div><Truck :size="22" /><span><strong>社区到家</strong><small>配送信息随时查看</small></span></div>
    </section>

    <section class="home-section category-section">
      <div class="section-title-row"><div><h2>按品类选购</h2><p>正在开团的冷链生鲜</p></div><RouterLink to="/search">查看全部<ArrowRight :size="16" /></RouterLink></div>
      <div class="category-grid">
        <RouterLink v-for="(category, index) in categories" :key="category.slug" :to="`/category/${category.slug}`" class="category-tile">
          <img :src="products[index].image" :alt="category.name" />
          <span><strong>{{ category.name }}</strong><small>查看在团商品</small></span>
        </RouterLink>
      </div>
    </section>

    <section class="home-section">
      <div class="section-title-row"><div><h2>今天值得跟的团</h2><p>团长亲选，截团时间清晰可见</p></div><RouterLink to="/group-buys">全部团购<ArrowRight :size="16" /></RouterLink></div>
      <div class="product-grid">
        <ProductCard v-for="product in products" :key="product.id" :product="product" />
      </div>
    </section>

    <section class="home-section leader-feature-section">
      <div class="section-title-row"><div><h2>认识你的团长</h2><p>先看团长，再选他正在带货的商品</p></div><RouterLink to="/leaders">团长广场<ArrowRight :size="16" /></RouterLink></div>
      <div class="leader-grid">
        <LeaderCard v-for="leader in leaders" :key="leader.id" :leader="leader" />
      </div>
    </section>
  </div>
</template>
