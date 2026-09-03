<script setup>
import { Bell, CheckCircle2, Clock3, PackageSearch, RotateCcw } from '@lucide/vue'
import { onMounted, ref } from 'vue'
import { api } from '../services/api'
import { useCustomerContext } from '../state/customer'

const { customerId } = useCustomerContext()
const loading = ref(true)
const error = ref('')
const messages = ref([])
const icon = (type) => type === 'REFUND' ? RotateCcw : type === 'ORDER' ? PackageSearch : Bell
const date = (value) => new Date(value).toLocaleString('zh-CN', { hour12: false })
onMounted(async () => {
  try { messages.value = (await api.getMessages(customerId.value)).messages ?? [] } catch (e) { error.value = e.message } finally { loading.value = false }
})
</script>

<template>
  <div class="store-container page-space messages-page">
    <div class="messages-title"><Bell :size="25" /><div><h1>消息中心</h1><p>订单、支付和退款状态会长期保留在这里</p></div></div>
    <div v-if="error" class="alert alert-danger">{{ error }}</div>
    <div v-else-if="loading" class="store-loading"><span class="spinner-border spinner-border-sm"></span>正在加载消息</div>
    <section v-else-if="messages.length" class="messages-list"><article v-for="message in messages" :key="message.messageId"><component :is="icon(message.messageType)" :size="21" /><div><strong>{{ message.title }}</strong><p>{{ message.content }}</p><small><Clock3 :size="13" />{{ date(message.createdAt) }}</small></div><RouterLink v-if="message.orderId" :to="`/orders/${message.orderId}`">查看订单</RouterLink></article></section>
    <div v-else class="store-empty"><CheckCircle2 :size="38" /><strong>暂无消息</strong><span>后续订单和退款状态会在这里显示</span></div>
  </div>
</template>

<style scoped>
.messages-title { display: flex; align-items: center; gap: 11px; margin-bottom: 17px; color: var(--brand); }.messages-title h1 { margin: 0; color: var(--ink); font-size: 23px; }.messages-title p { margin: 3px 0 0; color: var(--muted); font-size: 10px; }.messages-list { border: 1px solid var(--line); background: #fff; }.messages-list article { display: grid; grid-template-columns: 25px minmax(0, 1fr) auto; gap: 11px; align-items: center; padding: 15px; border-bottom: 1px solid var(--line); color: var(--brand); }.messages-list article:last-child { border-bottom: 0; }.messages-list article > div { min-width: 0; }.messages-list strong { color: var(--ink); font-size: 12px; }.messages-list p { margin: 5px 0; color: #536059; font-size: 10px; }.messages-list small { display: flex; align-items: center; gap: 4px; color: var(--muted); font-size: 9px; }.messages-list a { color: var(--brand); font-size: 10px; font-weight: 700; text-decoration: none; }
</style>
