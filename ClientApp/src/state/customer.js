import { computed, ref } from 'vue'

const storedCustomerId = Number.parseInt(localStorage.getItem('groupB.customerId') ?? '1', 10)
const customerId = ref(Number.isInteger(storedCustomerId) && storedCustomerId > 0 ? storedCustomerId : 1)

export function useCustomerContext() {
  const setCustomerId = (value) => {
    const parsed = Number.parseInt(value, 10)
    if (!Number.isInteger(parsed) || parsed <= 0) return false

    customerId.value = parsed
    localStorage.setItem('groupB.customerId', String(parsed))
    return true
  }

  return {
    customerId: computed(() => customerId.value),
    setCustomerId,
  }
}
