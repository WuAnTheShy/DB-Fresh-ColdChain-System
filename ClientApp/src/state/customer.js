import { computed, ref } from 'vue'

const defaultCustomerId = '10000000000000000000000000000001'
const storedCustomerId = localStorage.getItem('groupB.customerId')?.trim()
const customerId = ref(storedCustomerId && storedCustomerId.length <= 36 ? storedCustomerId : defaultCustomerId)

export function useCustomerContext() {
  const setCustomerId = (value) => {
    const normalized = String(value ?? '').trim()
    if (!normalized || normalized.length > 36) return false

    customerId.value = normalized
    localStorage.setItem('groupB.customerId', normalized)
    return true
  }

  return {
    customerId: computed(() => customerId.value),
    setCustomerId,
  }
}
