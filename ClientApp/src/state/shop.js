import { computed, reactive, ref } from 'vue'
import { api } from '../services/api'
import { avatarUrl } from '../assets/avatars'
import seasonalFruitImage from '../assets/categories/seasonal-fruit.jpg'
import vegetableTofuImage from '../assets/categories/vegetable-tofu.jpg'
import meatEggsImage from '../assets/categories/meat-eggs.jpg'
import seafoodImage from '../assets/categories/seafood.jpg'
import dairyBakeryImage from '../assets/categories/dairy-bakery.jpg'
import otherGroceryImage from '../assets/categories/other-grocery.jpg'

export const categories = reactive([])
export const leaders = reactive([])
export const products = reactive([])

const categoryVisuals = [
  { pattern: /果|fruit/i, icon: '水果', image: seasonalFruitImage },
  { pattern: /菜|豆|vegetable/i, icon: '蔬菜', image: vegetableTofuImage },
  { pattern: /肉|禽|蛋|meat|egg/i, icon: '鲜肉', image: meatEggsImage },
  { pattern: /海|水产|fish|seafood/i, icon: '水产', image: seafoodImage },
  { pattern: /乳|奶|烘焙|dairy|bakery/i, icon: '乳品', image: dairyBakeryImage },
]
const defaultCategoryVisual = { icon: '杂货', image: otherGroceryImage }
const leaderCovers = [seasonalFruitImage, vegetableTofuImage, seafoodImage]
const leadersLoading = ref(false)
const leadersLoaded = ref(false)
const leadersError = ref('')
const catalogLoading = ref(false)
const catalogLoaded = ref(false)
const catalogError = ref('')
const catalogUsingFallback = ref(false)
let leadersRequest = null
let catalogRequest = null

function fallbackAvatar(name) {
  const initial = (String(name ?? '团').trim().slice(0, 1) || '团')
    .replace(/[<>&"']/g, '') || '团'
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 96 96"><rect width="96" height="96" rx="20" fill="#e8f3ed"/><text x="48" y="59" text-anchor="middle" font-family="sans-serif" font-size="38" font-weight="700" fill="#176b46">${initial}</text></svg>`
  return `data:image/svg+xml;charset=UTF-8,${encodeURIComponent(svg)}`
}

function normalizeLeader(promoter, index) {
  const name = String(promoter.promoterName ?? '').trim() || '未命名团长'
  return {
    id: String(promoter.promoterId ?? '').trim(),
    name,
    title: '平台认证团长',
    area: '服务范围以结算地址为准',
    avatar: avatarUrl(promoter.avatar) || fallbackAvatar(name),
    cover: leaderCovers[index % leaderCovers.length],
    description: '该团长账号当前处于启用状态，可查看其正在带货的真实商品。',
    tags: ['平台认证'],
  }
}

function categoryVisual(name) {
  return categoryVisuals.find((item) => item.pattern.test(name)) ?? defaultCategoryVisual
}

function normalizeCategory(name) {
  const normalizedName = String(name ?? '').trim() || '其他'
  const visual = categoryVisual(normalizedName)
  return { slug: normalizedName, name: normalizedName, icon: visual.icon, image: visual.image }
}

// 温区展示文字映射（兼容英文枚举与中文值，大小写不敏感）
const storageLabels = {
  CHILLED: '冷藏',
  FROZEN: '冷冻',
  AMBIENT: '常温',
  冷藏: '冷藏',
  冷冻: '冷冻',
  常温: '常温',
}
const defaultStorageLabel = '冷链'

function storageLabel(value) {
  const raw = String(value ?? '').trim()
  if (!raw) return defaultStorageLabel
  return storageLabels[raw.toUpperCase()] ?? storageLabels[raw] ?? defaultStorageLabel
}

// 温区原始枚举（用于前端颜色区分，未知值归一为 CHILLED 同款展示）
function storageTypeOf(value) {
  const raw = String(value ?? '').trim()
  const upper = raw.toUpperCase()
  return upper === 'FROZEN' || upper === '冷冻' ? 'FROZEN'
    : upper === 'AMBIENT' || upper === '常温' ? 'AMBIENT'
    : 'CHILLED'
}

function normalizeProduct(item) {
  const categoryName = String(item.categoryName ?? '').trim() || '其他'
  const visual = categoryVisual(categoryName)
  const images = (Array.isArray(item.imageUrls) ? item.imageUrls : [])
    .map((url) => String(url ?? '').trim())
    .filter(Boolean)
  const unit = String(item.unit ?? '').trim()
  const storage = storageLabel(item.storageRequirement)
  const storageType = storageTypeOf(item.storageRequirement)
  const productName = String(item.productName ?? '').trim() || '未命名商品'
  return {
    id: String(item.catalogItemId ?? '').trim(),
    productId: String(item.productId ?? '').trim(),
    name: productName,
    shortName: productName,
    category: categoryName,
    spec: unit ? `计量单位：${unit}` : '规格以商品实际标注为准',
    price: Number(item.salePrice ?? 0),
    image: images[0] || visual.image,
    fallbackImage: visual.image,
    images: images.length ? images : [visual.image],
    storage,
    storageType,
    publishedAt: item.publishedAt ? String(item.publishedAt) : null,
    leaderId: String(item.promoterId ?? '').trim(),
    stock: Math.max(0, Number(item.availableStock ?? 0)),
    delivery: '支付后按订单安排冷链配送',
    summary: String(item.description ?? '').trim() || '团长在团商品，商品信息与售价均来自当前业务数据。',
    isFallback: Boolean(item.isFallback),
  }
}

function fallbackCatalogResult() {
  const promoterId = leaders[0]?.id ?? ''
  return {
    categories: ['时令水果', '海鲜水产', '蔬菜豆品'],
    products: [
      { catalogItemId: 'fallback:PROD-3004', productId: 'PROD-3004', productName: '阳光玫瑰葡萄 2kg', categoryName: '时令水果', unit: '2kg礼盒', storageRequirement: '冷藏', salePrice: 50, availableStock: 100, promoterId, description: '目录服务暂不可用，当前为演示兜底商品。', isFallback: true },
      { catalogItemId: 'fallback:PROD-3002', productId: 'PROD-3002', productName: '智利三文鱼中段 500g', categoryName: '海鲜水产', unit: '500g', storageRequirement: '冷藏', salePrice: 80, availableStock: 50, promoterId, description: '目录服务暂不可用，当前为演示兜底商品。', isFallback: true },
      { catalogItemId: 'fallback:PROD-3008', productId: 'PROD-3008', productName: '鲜食水果甜玉米 2.5kg', categoryName: '蔬菜豆品', unit: '2.5kg', storageRequirement: '冷藏', salePrice: 20, availableStock: 200, promoterId, description: '目录服务暂不可用，当前为演示兜底商品。', isFallback: true },
    ],
  }
}

function applyCatalogResult(result, usingFallback = false) {
  const normalizedProducts = (Array.isArray(result?.products) ? result.products : [])
    .map((item) => normalizeProduct({ ...item, isFallback: usingFallback || item.isFallback }))
    .filter((product) => product.id && product.productId && product.leaderId && product.price > 0 && product.stock > 0)
  const categoryNames = Array.isArray(result?.categories)
    ? result.categories
    : normalizedProducts.map((product) => product.category)
  const normalizedCategories = [...new Set(categoryNames
    .map((name) => String(name ?? '').trim()).filter(Boolean))].map(normalizeCategory)
  products.splice(0, products.length, ...normalizedProducts)
  categories.splice(0, categories.length, ...normalizedCategories)
  catalogUsingFallback.value = usingFallback
  reconcileCart()
  catalogLoaded.value = true
  return products
}

async function loadLeaders(force = false) {
  if (leadersLoaded.value && !force) return leaders
  if (leadersRequest && !force) return leadersRequest

  leadersLoading.value = true
  leadersError.value = ''
  leadersRequest = api.getPromoters()
    .then((result) => {
      const normalized = (Array.isArray(result) ? result : [])
        .map(normalizeLeader)
        .filter((leader) => leader.id)
      leaders.splice(0, leaders.length, ...normalized)
      leadersLoaded.value = true

      if (!selectedLeaderId.value || !leaderById(selectedLeaderId.value)) {
        selectedLeaderId.value = leaders[0]?.id ?? ''
      }
      return leaders
    })
    .catch((error) => {
      leadersError.value = error.message
      throw error
    })
    .finally(() => {
      leadersLoading.value = false
      leadersRequest = null
    })

  return leadersRequest
}

async function loadCatalog(force = false) {
  if (catalogLoaded.value && !force) return products
  if (catalogRequest && !force) return catalogRequest

  catalogLoading.value = true
  catalogError.value = ''
  catalogUsingFallback.value = false
  catalogRequest = api.getConsumerCatalog()
    .then((result) => {
      localStorage.setItem('freshMall.catalogCache', JSON.stringify(result))
      return applyCatalogResult(result)
    })
    .catch(async (error) => {
      catalogError.value = error.message
      await loadLeaders().catch(() => [])
      const cachedCatalog = readStoredJson('freshMall.catalogCache', null)
      return applyCatalogResult(cachedCatalog ?? fallbackCatalogResult(), true)
    })
    .finally(() => {
      catalogLoading.value = false
      catalogRequest = null
    })
  return catalogRequest
}

function readStoredJson(key, fallback) {
  try {
    return JSON.parse(localStorage.getItem(key) ?? JSON.stringify(fallback))
  } catch {
    return fallback
  }
}

const rawCart = readStoredJson('freshMall.cart', [])
const cart = reactive(Array.isArray(rawCart)
  ? rawCart.map((item) => ({
      productId: String(item.productId ?? ''),
      leaderId: String(item.leaderId ?? ''),
      quantity: Math.max(1, Number(item.quantity || 1)),
      selected: item.selected !== false,
    })).filter((item) => item.productId)
  : [])
const selectedLeaderId = ref(sessionStorage.getItem('freshMall.leaderId') || '')
const followedLeaderIds = ref([])
const followingLoading = ref(false)
const followingError = ref('')
const followingLoadedCustomerId = ref('')
const lastOrder = ref(readStoredJson('freshMall.lastOrder', null))

function persistCart() {
  localStorage.setItem('freshMall.cart', JSON.stringify(cart))
}

function productById(id, leaderId = '') {
  const value = String(id ?? '')
  return products.find((product) => product.id === value)
    ?? products.find((product) => product.productId === value &&
      (!leaderId || product.leaderId === String(leaderId)))
}

function leaderById(id) {
  return leaders.find((leader) => leader.id === String(id ?? ''))
}

function reconcileCart() {
  for (let index = cart.length - 1; index >= 0; index--) {
    const item = cart[index]
    const product = productById(item.productId, item.leaderId)
    if (!product) {
      cart.splice(index, 1)
      continue
    }
    item.productId = product.id
    item.leaderId = product.leaderId
    item.quantity = Math.min(product.stock, Math.max(1, item.quantity))
  }
  persistCart()
}

function isLeaderFollowed(leaderId) {
  return followedLeaderIds.value.includes(String(leaderId ?? ''))
}

async function loadFollowedLeaders(customerId, force = false) {
  const id = String(customerId ?? '')
  if (!id) {
    followedLeaderIds.value = []
    followingLoadedCustomerId.value = ''
    return
  }
  if (!force && followingLoadedCustomerId.value === id) return

  followingLoading.value = true
  followingError.value = ''
  try {
    const result = await api.getFollowingPromoters(id)
    followedLeaderIds.value = [...new Set((result.promoterIds ?? [])
      .map(String)
      .filter((leaderId) => leaderById(leaderId)))]
    followingLoadedCustomerId.value = id
  } catch (error) {
    followingError.value = error.message
    throw error
  } finally {
    followingLoading.value = false
  }
}

async function setLeaderFollowed(customerId, leaderId, shouldFollow) {
  const id = String(leaderId ?? '')
  if (!leaderById(id)) throw new Error('团长不存在')

  if (shouldFollow) await api.followPromoter(customerId, id)
  else await api.unfollowPromoter(customerId, id)

  const nextIds = new Set(followedLeaderIds.value)
  if (shouldFollow) nextIds.add(id)
  else nextIds.delete(id)
  followedLeaderIds.value = [...nextIds]
  followingLoadedCustomerId.value = String(customerId)
  return shouldFollow
}

function addToCart(catalogItemId, quantity = 1) {
  const product = productById(catalogItemId)
  const leader = leaderById(product?.leaderId)
  if (!product || !leader || product.isFallback) return false

  const existing = cart.find((item) => item.productId === product.id)
  if (existing) {
    existing.quantity = Math.min(product.stock, existing.quantity + Number(quantity || 1))
  } else {
    cart.push({
      productId: product.id,
      leaderId: leader.id,
      quantity: Math.min(product.stock, Math.max(1, Number(quantity || 1))),
      selected: true,
    })
  }
  selectedLeaderId.value = leader.id
  sessionStorage.setItem('freshMall.leaderId', String(leader.id))
  persistCart()
  return true
}

function updateQuantity(catalogItemId, quantity) {
  const item = cart.find((entry) => entry.productId === String(catalogItemId))
  const product = productById(catalogItemId)
  if (!item || !product) return
  item.quantity = Math.max(1, Math.min(product.stock, Number(quantity || 1)))
  persistCart()
}

function removeFromCart(catalogItemId) {
  const index = cart.findIndex((item) => item.productId === String(catalogItemId))
  if (index >= 0) cart.splice(index, 1)
  persistCart()
}

function setCartItemSelected(catalogItemId, selected) {
  const item = cart.find((entry) => entry.productId === String(catalogItemId))
  if (!item) return
  item.selected = Boolean(selected)
  persistCart()
}

function setLeaderCartSelected(leaderId, selected) {
  cart
    .filter((item) => item.leaderId === String(leaderId))
    .forEach((item) => { item.selected = Boolean(selected) })
  persistCart()
}

function setAllCartSelected(selected) {
  cart.forEach((item) => { item.selected = Boolean(selected) })
  persistCart()
}

function removeCartItems(catalogItemIds) {
  const ids = new Set(catalogItemIds.map(String))
  for (let index = cart.length - 1; index >= 0; index--) {
    if (ids.has(cart[index].productId)) cart.splice(index, 1)
  }
  persistCart()
}

function clearCart() {
  cart.splice(0, cart.length)
  persistCart()
}

function setLastOrder(order) {
  lastOrder.value = order
  sessionStorage.setItem('freshMall.lastOrder', JSON.stringify(order))
}

export function useShop() {
  const cartItems = computed(() => cart.map((item) => ({
    ...item,
    product: productById(item.productId, item.leaderId),
    leader: leaderById(item.leaderId),
  })).filter((item) => item.product && item.leader))
  const selectedCartItems = computed(() => cartItems.value.filter((item) => item.selected))

  return {
    categories,
    leaders,
    leadersLoading,
    leadersLoaded,
    leadersError,
    products,
    catalogLoading,
    catalogLoaded,
    catalogError,
    catalogUsingFallback,
    cart,
    cartItems,
    selectedCartItems,
    cartCount: computed(() => cartItems.value.reduce((sum, item) => sum + item.quantity, 0)),
    cartSubtotal: computed(() => cartItems.value.reduce((sum, item) => sum + item.product.price * item.quantity, 0)),
    selectedCartCount: computed(() => selectedCartItems.value.reduce((sum, item) => sum + item.quantity, 0)),
    selectedCartSubtotal: computed(() => selectedCartItems.value.reduce((sum, item) => sum + item.product.price * item.quantity, 0)),
    selectedLeaderId,
    followedLeaderIds,
    followingLoading,
    followingError,
    lastOrder,
    productById,
    leaderById,
    isLeaderFollowed,
    loadFollowedLeaders,
    loadLeaders,
    loadCatalog,
    setLeaderFollowed,
    addToCart,
    updateQuantity,
    removeFromCart,
    setCartItemSelected,
    setLeaderCartSelected,
    setAllCartSelected,
    removeCartItems,
    clearCart,
    setLastOrder,
  }
}
