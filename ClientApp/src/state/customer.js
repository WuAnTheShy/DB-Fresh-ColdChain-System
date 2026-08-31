import { computed, ref } from 'vue'
import { ApiError, api } from '../services/api'

const customer = ref(null)
const authReady = ref(false)
let initializationPromise = null

function normalizeCustomer(value) {
  if (!value?.customerId) return null
  return {
    customerId: String(value.customerId),
    customerName: String(value.customerName ?? ''),
    phone: String(value.phone ?? ''),
<<<<<<< HEAD
=======
    avatar: value.avatar ? String(value.avatar) : '',
>>>>>>> origin/dev-groupC
  }
}

export function useCustomerContext() {
  const setCustomer = (value) => {
    customer.value = normalizeCustomer(value)
    authReady.value = true
  }

  const clearCustomer = () => {
    customer.value = null
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

  return {
    customerId: computed(() => customer.value?.customerId ?? ''),
    customerName: computed(() => customer.value?.customerName ?? ''),
    isAuthenticated: computed(() => Boolean(customer.value?.customerId)),
    setCustomer,
    clearCustomer,
    initializeAuth,
  }
}
