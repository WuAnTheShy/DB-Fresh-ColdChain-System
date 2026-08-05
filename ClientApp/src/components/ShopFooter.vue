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
      <section><h2>商城服务</h2><RouterLink to="/deals">今日特价</RouterLink><RouterLink to="/search">供应商入驻</RouterLink><RouterLink to="/orders">订单服务</RouterLink></section>
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
