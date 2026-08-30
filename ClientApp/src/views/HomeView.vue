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

    <section class="home-section amazon-home-panel store-container" aria-labelledby="all-products-title">
      <div class="section-title-row"><div><h2 id="all-products-title">今日推荐</h2></div></div>
      <div class="product-grid">
        <ProductCard v-for="product in products" :key="product.id" :product="product" />
      </div>
    </section>

  </div>
</template>

<style scoped>
.home-page { padding-bottom: 12px; }
.amazon-promo-grid { display: grid; width: min(1500px, calc(100% - 30px)); min-height: 485px; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 8px; margin: 10px auto 0; }
.amazon-promo-card { position: relative; display: block; min-width: 0; min-height: 485px; overflow: hidden; border-radius: 10px; background: #fff; color: #0f1111; text-decoration: none; box-shadow: 0 2px 5px rgba(15, 17, 17, .18); }
.amazon-promo-card:hover { color: #0f1111; }
.amazon-promo-card::after { position: absolute; inset: 0; z-index: 1; background: linear-gradient(180deg, rgba(255,255,255,.96) 0%, rgba(255,255,255,.72) 31%, rgba(255,255,255,.04) 58%, rgba(0,0,0,.48) 100%); content: ""; }
.amazon-promo-card > img { position: absolute; inset: 0; width: 100%; height: 100%; object-fit: cover; transition: transform .25s ease; }
.amazon-promo-card:hover > img { transform: scale(1.025); }
.promo-copy { position: relative; z-index: 2; padding: 20px 21px; }
.promo-copy span { color: #565959; font-size: 13px; }
.promo-copy h1, .promo-copy h2 { margin: 3px 0 7px; font-size: clamp(25px, 2.3vw, 36px); font-weight: 800; letter-spacing: -.8px; line-height: 1.05; }
.promo-copy p { margin: 0; font-size: 14px; }
.amazon-promo-card > strong { position: absolute; left: 20px; bottom: 18px; z-index: 2; color: #fff; font-size: 13px; font-weight: 700; }
.promo-seafood::after { background: linear-gradient(180deg, rgba(216,242,255,.97) 0%, rgba(216,242,255,.68) 31%, rgba(255,255,255,.02) 60%, rgba(0,38,65,.66) 100%); }
.promo-vegetable::after { background: linear-gradient(180deg, rgba(239,250,224,.98) 0%, rgba(239,250,224,.7) 31%, rgba(255,255,255,.02) 60%, rgba(29,64,11,.63) 100%); }
.promo-delivery { background: radial-gradient(circle at 70% 75%, #147cc0 0%, #07548b 26%, #06264b 65%, #04172f 100%); color: #fff; }
.promo-delivery:hover { color: #fff; }
.promo-delivery::after { background: linear-gradient(180deg, rgba(0,0,0,.7) 0%, rgba(0,0,0,.12) 45%, rgba(0,0,0,.38) 100%); }
.promo-delivery .promo-copy span { color: #9fddff; }
.promo-delivery .delivery-visual { position: absolute; right: 14px; bottom: 70px; z-index: 2; display: flex; align-items: end; gap: 3px; color: #8addff; filter: drop-shadow(0 12px 22px rgba(0,0,0,.35)); }

.service-strip { display: grid; grid-template-columns: repeat(4, 1fr); border: 1px solid var(--line); border-top: 0; background: #fff; margin-top: 18px; overflow: hidden; border: 0; border-radius: 8px; box-shadow: 0 1px 3px rgba(15,17,17,.15); }
.service-strip > div { display: flex; min-height: 78px; align-items: center; justify-content: center; gap: 11px; border-right: 1px solid var(--line); color: var(--brand); }
.service-strip > div:last-child { border-right: 0; }
.service-strip span { display: flex; flex-direction: column; }
.service-strip strong { color: var(--ink); font-size: 13px; }
.service-strip small { margin-top: 2px; color: var(--muted); font-size: 10px; }
.amazon-home-panel { margin-top: 18px; padding: 20px; border-radius: 8px; background: #fff; box-shadow: 0 1px 3px rgba(15,17,17,.13); }
.amazon-home-panel.home-section { padding-top: 20px; padding-bottom: 20px; }
.category-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 14px; }
.category-tile { position: relative; height: 138px; overflow: hidden; border: 1px solid var(--line); background: #1c2923; color: #fff; text-decoration: none; }
.category-tile img { width: 100%; height: 100%; object-fit: cover; opacity: .68; transition: transform .18s ease; }
.category-tile:hover img { transform: scale(1.025); }
.category-tile > span { position: absolute; inset: auto 0 0; display: flex; flex-direction: column; padding: 30px 17px 15px; background: rgba(20, 29, 25, .72); }
.category-tile strong { font-size: 16px; }
.category-tile small { margin-top: 2px; color: #dbe3df; font-size: 10px; }

/* Amazon-inspired storefront refresh 覆盖 */
.service-strip > div { min-height: 72px; color: #007185; }
.service-strip strong { font-size: 14px; }
.service-strip small { color: #565959; font-size: 11px; }
.category-grid { grid-template-columns: repeat(6, minmax(0, 1fr)); gap: 12px; }
.category-tile { height: 170px; border: 0; border-radius: 4px; background: #eaeded; }
.category-tile img { opacity: 1; }
.category-tile > span { padding: 50px 15px 15px; background: linear-gradient(180deg, transparent, rgba(0,0,0,.72)); }
.category-tile strong { font-size: 18px; }
.amazon-home-panel .product-grid { grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 18px; }

@media (max-width: 1199.98px) {
  .amazon-promo-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .amazon-promo-card { min-height: 420px; }
  .category-grid { grid-template-columns: repeat(3, minmax(0, 1fr)); }
  .amazon-home-panel .product-grid { grid-template-columns: repeat(3, minmax(0, 1fr)); }
}

@media (max-width: 991.98px) {
  .service-strip { grid-template-columns: repeat(2, 1fr); }
  .service-strip > div:nth-child(2) { border-right: 0; }
  .service-strip > div:nth-child(-n+2) { border-bottom: 1px solid var(--line); }
}

@media (max-width: 767.98px) {
  .store-container, .amazon-promo-grid { width: min(100% - 20px, 720px); }
  .amazon-promo-grid { grid-template-columns: repeat(4, 84vw); gap: 8px; padding: 0 0 5px; overflow-x: auto; scroll-snap-type: x mandatory; }
  .amazon-promo-card { min-height: 360px; scroll-snap-align: center; }
  .promo-copy h1, .promo-copy h2 { font-size: 27px; }
  .service-strip { margin-top: 10px; }
  .service-strip > div { min-height: 68px; padding: 8px; justify-content: flex-start; }
  .amazon-home-panel { margin-top: 10px; padding: 14px; }
  .category-grid { grid-template-columns: 1fr; gap: 9px; }
  .category-tile { height: 120px; }
  .amazon-home-panel .product-grid { grid-template-columns: 1fr; }
}
</style>
