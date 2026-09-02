<script setup>
import { AlertTriangle, ChevronLeft, RotateCcw } from '@lucide/vue'
import { onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { api } from '../services/api'

const props = defineProps({ batchId: { type: String, required: true } })
const router = useRouter()
const loading = ref(true); const saving = ref(false); const error = ref(''); const batch = ref(null)
const form = reactive({ remark: '' })
const money = (value) => `¥${Number(value ?? 0).toFixed(2)}`
onMounted(async () => { try { batch.value = await api.getCheckoutBatch(props.batchId) } catch (e) { error.value = e.message } finally { loading.value = false } })
async function submit() { saving.value = true; error.value = ''; try { await api.applyCheckoutBatchRefund(props.batchId, form); router.push('/orders') } catch (e) { error.value = e.message } finally { saving.value = false } }
</script>

<template>
  <div class="store-container page-space batch-refund-page">
    <div v-if="error" class="alert alert-danger">{{ error }}</div><div v-if="loading" class="store-loading"><span class="spinner-border spinner-border-sm"></span>正在读取结算批次</div>
    <form v-else-if="batch" class="batch-refund-card" @submit.prevent="submit"><div class="batch-refund-head"><RotateCcw :size="25" /><div><h1>申请整个结算批次退款</h1><p>本次会为 {{ batch.orders?.length ?? 0 }} 个团长子订单分别提交退款申请，由平台审核。</p></div></div><div class="refund-warning"><AlertTriangle :size="18" /><span>发货后的冷链运费不予退还；仅当本批次所有子订单均退款完成时，已使用优惠券才会返还。</span></div><dl><div><dt>商品金额</dt><dd>{{ money(batch.goodsAmount) }}</dd></div><div><dt>团购优惠</dt><dd>-{{ money(batch.discountAmount) }}</dd></div><div><dt>冷链运费</dt><dd>{{ money(batch.freightAmount) }}</dd></div><div><dt>批次实付</dt><dd>{{ money(batch.finalAmount) }}</dd></div></dl><label>退款原因<textarea v-model.trim="form.remark" class="form-control" maxlength="200" required placeholder="请说明退款原因" /></label><div class="batch-refund-actions"><RouterLink class="btn btn-outline-secondary" to="/orders"><ChevronLeft :size="16" />返回</RouterLink><button class="btn btn-outline-danger" :disabled="saving" type="submit"><span v-if="saving" class="spinner-border spinner-border-sm"></span><template v-else>提交整个批次退款申请</template></button></div></form>
  </div>
</template>

<style scoped>
.batch-refund-card { width: min(700px, 100%); margin: 10px auto; padding: 22px; border: 1px solid var(--line); background: #fff; }.batch-refund-head { display: flex; gap: 10px; color: var(--danger); }.batch-refund-head h1 { margin: 0; color: var(--ink); font-size: 21px; }.batch-refund-head p { margin: 5px 0 0; color: var(--muted); font-size: 10px; }.refund-warning { display: flex; gap: 7px; margin: 18px 0; padding: 12px; background: #fff6e8; color: #825c00; font-size: 10px; line-height: 1.5; }.batch-refund-card dl { margin: 0 0 16px; }.batch-refund-card dl > div { display: flex; justify-content: space-between; padding: 8px 0; border-bottom: 1px solid var(--line); font-size: 11px; }.batch-refund-card dt { color: var(--muted); }.batch-refund-card dd { margin: 0; }.batch-refund-card label { display: flex; flex-direction: column; gap: 6px; color: #4d5953; font-size: 10px; font-weight: 700; }.batch-refund-card textarea { min-height: 96px; resize: vertical; }.batch-refund-actions { display: flex; justify-content: flex-end; gap: 8px; margin-top: 17px; }
</style>
