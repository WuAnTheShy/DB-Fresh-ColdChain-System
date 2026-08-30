<script setup>
function backToTop() {
  window.scrollTo({ top: 0, behavior: 'smooth' })
}
</script>

<template>
  <footer class="store-footer">
    <button class="back-to-top" type="button" @click="backToTop">返回顶部</button>
    <div class="footer-links store-container">
      <section><h2>了解我们</h2><RouterLink to="/profile">会员中心</RouterLink><RouterLink to="/following">我的关注</RouterLink><RouterLink to="/search">鲜邻严选</RouterLink><RouterLink to="/addresses">配送服务</RouterLink></section>
      <section><h2>商城服务</h2><RouterLink to="/search">供应商入驻</RouterLink><RouterLink to="/orders">订单服务</RouterLink></section>
      <section><h2>支付与优惠</h2><RouterLink to="/coupons">优惠券中心</RouterLink><RouterLink to="/cart">购物车</RouterLink><RouterLink to="/orders">订单查询</RouterLink></section>
      <section><h2>让我们帮助你</h2><RouterLink to="/profile">你的账户</RouterLink><RouterLink to="/addresses">配送地址</RouterLink><RouterLink to="/orders">售后与退款</RouterLink></section>
    </div>
    <div class="footer-brand-row">
      <RouterLink class="store-brand footer-brand" to="/"><span class="brand-word">鲜邻团</span><span class="brand-smile" aria-hidden="true"></span><small>fresh</small></RouterLink>
      <span>中国大陆 · 简体中文 · CNY ¥</span>
    </div>
    <div class="footer-legal"><span>冷链社区团购示范商城</span><span>© 2026 鲜邻团</span></div>
  </footer>
</template>

<style scoped>
.store-footer { margin-top: 18px; background: #232f3e; color: #ddd; }
.back-to-top { width: 100%; min-height: 48px; border: 0; background: #37475a; color: #fff; font-size: 12px; }
.back-to-top:hover { background: #485769; }
.footer-links { display: grid; grid-template-columns: repeat(4, minmax(150px, 1fr)); gap: 55px; padding: 42px max(40px, 8vw); background: #232f3e; }
.footer-links section { display: flex; flex-direction: column; align-items: flex-start; gap: 8px; }
.footer-links h2 { margin: 0 0 5px; color: #fff; font-size: 15px; }
.footer-links a { color: #ddd; font-size: 12px; text-decoration: none; }
.footer-links a:hover { color: #fff; text-decoration: underline; }
.footer-brand-row { display: flex; min-height: 92px; align-items: center; justify-content: center; gap: 45px; border-top: 1px solid #3a4553; background: #232f3e; }
.footer-brand { min-width: 112px; }
.footer-brand-row > span { padding: 8px 13px; border: 1px solid #848a91; border-radius: 3px; color: #ccc; font-size: 11px; }
.footer-legal { display: flex; min-height: 64px; align-items: center; justify-content: center; gap: 22px; background: #131a22; color: #aaa; font-size: 10px; }

@media (max-width: 767.98px) {
  .footer-links { grid-template-columns: repeat(2, 1fr); gap: 26px; padding: 30px 24px; }
  .footer-brand-row { flex-direction: column; gap: 6px; padding: 18px; }
  .footer-legal { padding-bottom: 58px; }
}
</style>
