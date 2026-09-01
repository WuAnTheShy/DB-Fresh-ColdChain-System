import { computed, ref } from 'vue'
import { ApiError, api } from '../services/api'

const customer = ref(null)
const authReady = ref(false)
const defaultAddress = ref(null)
const deliveryAddressLoading = ref(false)
let initializationPromise = null
let deliveryAddressPromise = null

function normalizeCustomer(value) {
  if (!value?.customerId) return null
  return {
    customerId: String(value.customerId),
    customerName: String(value.customerName ?? ''),
    phone: String(value.phone ?? ''),
    avatar: value.avatar ? String(value.avatar) : '',
  }
}

export function useCustomerContext() {
  const setCustomer = (value) => {
    customer.value = normalizeCustomer(value)
    defaultAddress.value = null
    authReady.value = true
  }

  const clearCustomer = () => {
    customer.value = null
    defaultAddress.value = null
    authReady.value = true
  }

  const initializeAuth = async () => {
    if (authReady.value) return customer.value
    if (initializationPromise) return initializationPromise

    initializationPromise = api.getCurrentCustomer()
      .then((result) => {
        customer.value = normalizeCustomer(result)
        return customer.value
      })
      .catch((error) => {
        customer.value = null
        if (!(error instanceof ApiError) || error.status !== 401) throw error
        return null
      })
      .finally(() => {
        authReady.value = true
        initializationPromise = null
      })

    return initializationPromise
  }

  const loadDeliveryAddress = async (force = false) => {
    const customerId = customer.value?.customerId
    if (!customerId) {
      defaultAddress.value = null
      return null
    }
    if (defaultAddress.value && !force) return defaultAddress.value
    if (deliveryAddressPromise && !force) return deliveryAddressPromise

    deliveryAddressLoading.value = true
    deliveryAddressPromise = api.getAddresses(customerId)
      .then((result) => {
        const addresses = Array.isArray(result?.addresses) ? result.addresses : []
        defaultAddress.value = addresses.find((address) => address.isDefault === 1)
          ?? addresses[0]
          ?? null
        return defaultAddress.value
      })
      .finally(() => {
        deliveryAddressLoading.value = false
        deliveryAddressPromise = null
      })
    return deliveryAddressPromise
  }

  return {
    customerId: computed(() => customer.value?.customerId ?? ''),
    customerName: computed(() => customer.value?.customerName ?? ''),
    isAuthenticated: computed(() => Boolean(customer.value?.customerId)),
    deliveryLocation: computed(() => {
      if (!customer.value?.customerId) return '登录后设置地址'
      if (!defaultAddress.value) return '请设置收货地址'
      return [defaultAddress.value.province, defaultAddress.value.city, defaultAddress.value.district]
        .map((value) => String(value ?? '').trim())
        .filter(Boolean)
        .join('') || '请设置收货地址'
    }),
    deliveryAddressLoading,
    setCustomer,
    clearCustomer,
    initializeAuth,
    loadDeliveryAddress,
  }
}
