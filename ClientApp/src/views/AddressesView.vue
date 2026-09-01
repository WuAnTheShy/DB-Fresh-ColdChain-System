<script setup>
import { Check, MapPin, Pencil, Plus, Save, Star, Trash2, X } from '@lucide/vue'
import { onMounted, reactive, ref } from 'vue'
import StoreBreadcrumb from '../components/StoreBreadcrumb.vue'
import { api } from '../services/api'
import { useCustomerContext } from '../state/customer'

const { customerId, loadDeliveryAddress } = useCustomerContext()
const loading = ref(true)
const saving = ref(false)
const editorOpen = ref(false)
const error = ref('')
const success = ref('')
const addresses = ref([])
const form = reactive(emptyAddress())

function emptyAddress() { return { addressId: null, customerId: customerId.value, receiverName: '', phone: '', province: '', city: '', district: '', detailAddress: '', isDefault: false } }
function openCreate() { Object.assign(form, emptyAddress()); editorOpen.value = true; error.value = '' }
function openEdit(address) { Object.assign(form, { addressId: address.addressId, customerId: customerId.value, receiverName: address.receiverName, phone: address.phone, province: address.province, city: address.city, district: address.district, detailAddress: address.detailAddress, isDefault: address.isDefault === 1 }); editorOpen.value = true; error.value = '' }
async function loadAddresses() { loading.value = true; error.value = ''; try { const result = await api.getAddresses(customerId.value); addresses.value = result.addresses; await loadDeliveryAddress(true) } catch (requestError) { error.value = requestError.message; addresses.value = [] } finally { loading.value = false } }
async function saveAddress() { saving.value = true; error.value = ''; success.value = ''; try { if (form.addressId) { await api.updateAddress(customerId.value, form.addressId, { ...form }); success.value = '收货地址已更新' } else { await api.createAddress(customerId.value, { ...form }); success.value = '收货地址已新增' } editorOpen.value = false; await loadAddresses() } catch (requestError) { error.value = requestError.message } finally { saving.value = false } }
async function setDefault(addressId) { error.value = ''; try { await api.setDefaultAddress(customerId.value, addressId); success.value = '默认地址已更新'; await loadAddresses() } catch (requestError) { error.value = requestError.message } }
async function removeAddress(address) { if (!window.confirm(`确认删除 ${address.receiverName} 的收货地址？`)) return; error.value = ''; try { await api.deleteAddress(customerId.value, address.addressId); success.value = '收货地址已删除'; await loadAddresses() } catch (requestError) { error.value = requestError.message } }
onMounted(loadAddresses)
</script>

<template>
  <div class="store-container page-space addresses-page">
    <StoreBreadcrumb :items="[{ label: '个人中心', to: '/profile' }, { label: '收货地址' }]" />
    <div class="account-page-header"><div><MapPin :size="26" /><span><h1>收货地址</h1></span></div><button class="btn btn-buy" type="button" @click="openCreate"><Plus :size="17" />新增地址</button></div>
    <div v-if="error" class="alert alert-danger">{{ error }}</div><div v-if="success" class="alert alert-success alert-dismissible">{{ success }}<button type="button" class="btn-close" aria-label="关闭" @click="success = ''"></button></div>

    <section v-if="editorOpen" class="address-editor">
      <header><div><h2>{{ form.addressId ? '编辑收货地址' : '新增收货地址' }}</h2></div><button type="button" title="关闭编辑" @click="editorOpen = false"><X :size="19" /></button></header>
      <form class="address-form" @submit.prevent="saveAddress"><label><span>收货人</span><input v-model.trim="form.receiverName" class="form-control" maxlength="50" required /></label><label><span>手机号码</span><input v-model.trim="form.phone" class="form-control" pattern="1[0-9]{10}" maxlength="20" required /></label><label><span>省份</span><input v-model.trim="form.province" class="form-control" maxlength="50" required /></label><label><span>城市</span><input v-model.trim="form.city" class="form-control" maxlength="50" required /></label><label><span>区县</span><input v-model.trim="form.district" class="form-control" maxlength="50" required /></label><label class="wide"><span>详细地址</span><input v-model.trim="form.detailAddress" class="form-control" maxlength="200" required /></label><label class="address-default-check wide"><input v-model="form.isDefault" class="form-check-input" type="checkbox" />设为默认收货地址</label><div class="address-form-actions wide"><button class="btn btn-outline-secondary" type="button" @click="editorOpen = false">取消</button><button class="btn btn-buy" type="submit" :disabled="saving"><span v-if="saving" class="spinner-border spinner-border-sm"></span><Save v-else :size="17" />保存地址</button></div></form>
    </section>

    <div v-if="loading" class="store-loading"><span class="spinner-border spinner-border-sm"></span>正在读取地址</div>
    <div v-else-if="addresses.length" class="consumer-address-list"><article v-for="address in addresses" :key="address.addressId" :class="{ default: address.isDefault === 1 }"><span class="address-pin"><MapPin :size="22" /></span><div><div class="address-person"><strong>{{ address.receiverName }}</strong><span>{{ address.phone }}</span><small v-if="address.isDefault === 1"><Check :size="12" />默认地址</small></div><p>{{ address.province }}{{ address.city }}{{ address.district }} {{ address.detailAddress }}</p></div><div class="address-actions"><button v-if="address.isDefault !== 1" type="button" title="设为默认地址" @click="setDefault(address.addressId)"><Star :size="18" /></button><button type="button" title="编辑地址" @click="openEdit(address)"><Pencil :size="18" /></button><button class="danger" type="button" title="删除地址" @click="removeAddress(address)"><Trash2 :size="18" /></button></div></article></div>
    <div v-else class="store-empty"><MapPin :size="40" /><strong>还没有收货地址</strong><span>添加地址后即可安排冷链配送</span><button class="btn btn-buy" type="button" @click="openCreate"><Plus :size="17" />新增地址</button></div>
  </div>
</template>

<style scoped>
.address-editor { margin-bottom: 15px; padding: 18px; border: 1px solid #b9cdc4; border-top: 3px solid var(--brand); background: #fff; }
.address-editor > header { display: flex; align-items: flex-start; justify-content: space-between; margin-bottom: 15px; }
.address-editor h2 { margin: 0; font-size: 16px; }
.address-editor p { margin: 3px 0 0; color: var(--muted); font-size: 9px; }
.address-editor header button { display: inline-flex; width: 34px; height: 34px; align-items: center; justify-content: center; border: 0; border-radius: 4px; background: transparent; color: #69746f; }
.address-form { display: grid; grid-template-columns: repeat(3, 1fr); gap: 13px; }
.address-form .wide { grid-column: 1 / -1; }
.address-form label { display: flex; flex-direction: column; gap: 6px; }
.address-form label > span { color: #4d5953; font-size: 10px; font-weight: 700; }
.address-default-check { display: flex !important; flex-direction: row !important; align-items: center; gap: 7px !important; color: #4f5c56; font-size: 10px; }
.address-form-actions { display: flex; justify-content: flex-end; gap: 8px; }
.consumer-address-list { display: flex; flex-direction: column; border: 1px solid var(--line); background: #fff; }
.consumer-address-list article { display: grid; grid-template-columns: 46px minmax(0, 1fr) auto; gap: 12px; align-items: center; min-height: 100px; padding: 16px; border-bottom: 1px solid var(--line); }
.consumer-address-list article:last-child { border-bottom: 0; }
.consumer-address-list article.default { border-left: 3px solid var(--brand); padding-left: 13px; background: #f7faf8; }
.address-pin { display: inline-flex; width: 42px; height: 42px; align-items: center; justify-content: center; border-radius: 50%; background: #e7f2ed; color: var(--brand); }
.address-person { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.address-person > span { color: var(--muted); font-size: 10px; }
.address-person small { display: inline-flex; align-items: center; gap: 3px; padding: 3px 6px; border-radius: 3px; background: var(--brand); color: #fff; font-size: 8px; }
.consumer-address-list p { margin: 6px 0 0; color: #59665f; font-size: 11px; }
.address-actions { display: flex; gap: 3px; }
.address-actions button { display: inline-flex; width: 34px; height: 34px; align-items: center; justify-content: center; border: 0; border-radius: 4px; background: transparent; color: #69746f; }
.address-actions .danger:hover { background: #fff0ef; color: var(--danger); }

@media (max-width: 767.98px) {
  .address-form { grid-template-columns: 1fr; }
  .address-form .wide { grid-column: auto; }
  .consumer-address-list article { grid-template-columns: 38px minmax(0, 1fr); }
  .address-pin { width: 36px; height: 36px; }
  .address-actions { grid-column: 2; justify-content: flex-end; }
}
</style>
