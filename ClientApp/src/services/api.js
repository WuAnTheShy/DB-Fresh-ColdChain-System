export class ApiError extends Error {
  constructor(message, status, traceId = null, validationErrors = {}) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.traceId = traceId
    this.validationErrors = validationErrors
  }
}

async function request(path, options = {}) {
  const headers = new Headers(options.headers)
  if (options.body && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }

  const controller = new AbortController()
  const timeoutId = window.setTimeout(() => controller.abort(), 12000)
  let response
  try {
    response = await fetch(path, {
      ...options,
      headers,
      signal: options.signal ?? controller.signal,
    })
  } catch (error) {
    if (error?.name === 'AbortError') {
      throw new ApiError('服务器响应超时，请检查数据库连接后重试', 408)
    }
    throw new ApiError('无法连接服务器，请稍后重试', 0)
  } finally {
    window.clearTimeout(timeoutId)
  }
  const contentType = response.headers.get('content-type') ?? ''
  const payload = response.status === 204
    ? null
    : contentType.includes('application/json')
      ? await response.json()
      : await response.text()

  if (!response.ok) {
    const validationErrors = payload?.errors ?? {}
    const firstValidationMessage = Object.values(validationErrors).flat()[0]
    throw new ApiError(
      payload?.message ?? payload?.title ?? firstValidationMessage ?? '请求失败，请稍后重试',
      response.status,
      payload?.traceId,
      validationErrors,
    )
  }

  return payload
}

function jsonBody(value) {
  return JSON.stringify(value)
}

export const api = {
  getPromoters: () => request('/api/promoters'),
  getCurrentCustomer: () => request('/api/auth/customer/me'),
  loginCustomer: (payload) => request('/api/auth/customer/login', {
    method: 'POST',
    body: jsonBody(payload),
  }),
  registerCustomer: (payload) => request('/api/auth/customer/register', {
    method: 'POST',
    body: jsonBody(payload),
  }),
  sendCustomerPasswordResetCode: (payload) => request('/api/auth/customer/password-reset/code', {
    method: 'POST',
    body: jsonBody(payload),
  }),
  resetCustomerPassword: (payload) => request('/api/auth/customer/password-reset', {
    method: 'POST',
    body: jsonBody(payload),
  }),
  logoutCustomer: () => request('/api/auth/customer/logout', {
    method: 'POST',
  }),
  getCustomer: (customerId) => request(`/api/customers/${customerId}`),
  updateCustomer: (customerId, payload) => request(`/api/customers/${customerId}`, {
    method: 'PUT',
    body: jsonBody(payload),
  }),
  getAddresses: (customerId) => request(`/api/customers/${customerId}/addresses`),
  createAddress: (customerId, payload) => request(`/api/customers/${customerId}/addresses`, {
    method: 'POST',
    body: jsonBody(payload),
  }),
  updateAddress: (customerId, addressId, payload) => request(`/api/customers/${customerId}/addresses/${addressId}`, {
    method: 'PUT',
    body: jsonBody(payload),
  }),
  deleteAddress: (customerId, addressId) => request(`/api/customers/${customerId}/addresses/${addressId}`, {
    method: 'DELETE',
  }),
  setDefaultAddress: (customerId, addressId) => request(`/api/customers/${customerId}/addresses/${addressId}/default`, {
    method: 'PUT',
  }),
  getFollowingPromoters: (customerId) => request(`/api/customers/${customerId}/following`),
  followPromoter: (customerId, promoterId) => request(`/api/customers/${customerId}/following/${encodeURIComponent(promoterId)}`, {
    method: 'POST',
  }),
  unfollowPromoter: (customerId, promoterId) => request(`/api/customers/${customerId}/following/${encodeURIComponent(promoterId)}`, {
    method: 'DELETE',
  }),
  getCoupons: (customerId) => request(`/api/customers/${customerId}/coupons`),
  getMessages: (customerId) => request(`/api/customers/${customerId}/messages`),
  claimCoupon: (customerId, couponId) => request(`/api/customers/${customerId}/coupons/${couponId}/claim`, {
    method: 'POST',
  }),
  getOrders: (query = {}) => {
    const search = new URLSearchParams()
    Object.entries(query).forEach(([key, value]) => {
      if (value !== '' && value !== null && value !== undefined) search.set(key, value)
    })
    return request(`/api/orders?${search.toString()}`)
  },
  getOrder: (orderId) => request(`/api/orders/${orderId}`),
  createOrder: (payload) => request('/api/orders', {
    method: 'POST',
    body: jsonBody(payload),
  }),
  getCheckoutBatch: (checkoutBatchId) => request(`/api/orders/batches/${encodeURIComponent(checkoutBatchId)}`),
  payCheckoutBatch: (checkoutBatchId, payload) => request(`/api/orders/batches/${encodeURIComponent(checkoutBatchId)}/pay`, {
    method: 'POST',
    body: jsonBody(payload),
  }),
  transitionOrder: (orderId, targetStatus) => request(`/api/orders/${orderId}/transition`, {
    method: 'POST',
    body: jsonBody({ targetStatus }),
  }),
  cancelOrder: (orderId) => request(`/api/orders/${orderId}/cancel`, {
    method: 'POST',
  }),
  confirmOrderItemReceipt: (orderId, orderDetailId) => request(`/api/orders/${orderId}/items/${orderDetailId}/confirm-receipt`, {
    method: 'POST',
  }),
  getOrderRefunds: (orderId) => request(`/api/orders/${orderId}/refunds`),
  applyOrderRefund: (orderId, payload) => request(`/api/orders/${orderId}/refunds`, {
    method: 'POST',
    body: jsonBody(payload),
  }),
  applyCheckoutBatchRefund: (batchId, payload) => request(`/api/orders/batches/${encodeURIComponent(batchId)}/refunds`, {
    method: 'POST', body: jsonBody(payload),
  }),
}
