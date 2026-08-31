import { computed, reactive, ref } from 'vue'
import { api } from '../services/api'
import seasonalFruitImage from '../assets/categories/seasonal-fruit.jpg'
import vegetableTofuImage from '../assets/categories/vegetable-tofu.jpg'
import meatEggsImage from '../assets/categories/meat-eggs.jpg'
import seafoodImage from '../assets/categories/seafood.jpg'
import dairyBakeryImage from '../assets/categories/dairy-bakery.jpg'
import otherGroceryImage from '../assets/categories/other-grocery.jpg'

export const categories = [
  { slug: 'fruit', name: '时令水果', icon: '樱桃', image: seasonalFruitImage },
  { slug: 'vegetable', name: '蔬菜豆品', icon: '青菜', image: vegetableTofuImage },
  { slug: 'meat-eggs', name: '肉禽蛋品', icon: '鲜肉', image: meatEggsImage },
  { slug: 'seafood', name: '海鲜水产', icon: '三文鱼', image: seafoodImage },
  { slug: 'dairy-bakery', name: '乳品烘焙', icon: '牛奶', image: dairyBakeryImage },
  { slug: 'other', name: '其他', icon: '杂货', image: otherGroceryImage },
]

export const leaders = [
  {
    id: 'PRO_2563c9557c564d86b015b90876c3d8f4',
    name: '张三',
    title: '社区生鲜团长',
    area: '浦东新区 · 花木街道',
    avatar: 'https://images.unsplash.com/photo-1494790108377-be9c29b29330?auto=format&fit=crop&w=240&q=85',
    cover: 'https://images.unsplash.com/photo-1528821128474-27f963b062bf?auto=format&fit=crop&w=1200&q=85',
    description: '每天精选当季果蔬，严选冷链到家。上架前亲自试吃，下单后同步配送进度。',
    tags: ['平台认证', '果蔬优选'],
    following: 1280,
  },
  {
    id: 'PRO_52d9f7b7cf2843a7b076732fb33374f0',
    name: '长四',
    title: '海鲜冷链团长',
    area: '徐汇区 · 田林街道',
    avatar: 'https://images.unsplash.com/photo-1500648767791-00dcc994a43e?auto=format&fit=crop&w=240&q=85',
    cover: 'https://images.unsplash.com/photo-1547592180-85f173990554?auto=format&fit=crop&w=1200&q=85',
    description: '专注冰鲜水产和家庭餐桌，冷链时效透明，按团同步到货与提货信息。',
    tags: ['平台认证', '水产专营'],
    following: 936,
  },
  {
    id: 'PRO_9fb79e69831b4a50899999c0f438c734',
    name: '宋张',
    title: '家庭餐桌团长',
    area: '闵行区 · 古美街道',
    avatar: 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&w=240&q=85',
    cover: 'https://images.unsplash.com/photo-1540420773420-3366772f4999?auto=format&fit=crop&w=1200&q=85',
    description: '面向家庭日常采购，主推高复购、小份量和高性价比的生鲜组合。',
    tags: ['平台认证', '家庭精选'],
    following: 764,
  },
]

export const products = [
  {
    id: 'PROD-3004',
    slug: 'shine-muscat',
    name: '阳光玫瑰葡萄 2kg',
    shortName: '阳光玫瑰葡萄',
    category: 'fruit',
    spec: '精品果 · 2kg礼盒',
    price: 50,
    image: 'https://images.unsplash.com/photo-1528821128474-27f963b062bf?auto=format&fit=crop&w=800&q=88',
    storage: '冷藏',
    leaderId: 'PRO_2563c9557c564d86b015b90876c3d8f4',
    sold: 286,
    stock: 100,
    delivery: '明日 16:00 前送达',
    publishedAt: '2026-08-05T09:20:00+08:00',
    summary: '颗粒饱满、清甜多汁，产地冷链直达，适合家庭分享。',
  },
  {
    id: 'PROD-3002',
    slug: 'salmon',
    name: '智利三文鱼中段 500g',
    shortName: '三文鱼',
    category: 'seafood',
    spec: '去皮去刺 · 500g',
    price: 80,
    image: 'https://images.unsplash.com/photo-1547592180-85f173990554?auto=format&fit=crop&w=800&q=88',
    storage: '冷藏',
    leaderId: 'PRO_52d9f7b7cf2843a7b076732fb33374f0',
    sold: 117,
    stock: 50,
    delivery: '后日 12:00 前送达',
    publishedAt: '2026-08-05T08:35:00+08:00',
    summary: '肉质细腻，家庭小包装，低温锁鲜运输，开盒即可分切烹饪。',
  },
  {
    id: 'PROD-3008',
    slug: 'sweet-corn',
    name: '鲜食水果甜玉米 2.5kg',
    shortName: '水果甜玉米',
    category: 'vegetable',
    spec: '家庭装 · 2.5kg',
    price: 20,
    image: 'https://images.unsplash.com/photo-1540420773420-3366772f4999?auto=format&fit=crop&w=800&q=88',
    storage: '冷藏',
    leaderId: 'PRO_2563c9557c564d86b015b90876c3d8f4',
    sold: 368,
    stock: 200,
    delivery: '明日 12:00 前送达',
    publishedAt: '2026-08-04T18:10:00+08:00',
    summary: '颗粒饱满、清甜脆嫩，适合蒸煮、煲汤和家庭日常搭配。',
  },
]

const rawCart = JSON.parse(localStorage.getItem('freshMall.cart') ?? '[]')
const rawRushCounts = JSON.parse(localStorage.getItem('freshMall.rushCounts') ?? '{}')
function normalizeProductId(id) {
  const value = String(id ?? '').trim()
  const legacyIds = { '1': 'PROD-3004', P1: 'PROD-3004', '2': 'PROD-3002', P2: 'PROD-3002', '3': 'PROD-3008', P3: 'PROD-3008' }
  return legacyIds[value] ?? value
}

const cart = reactive(Array.isArray(rawCart)
  ? rawCart.map((item) => {
      const productId = normalizeProductId(item.productId)
      const product = products.find((entry) => entry.id === productId)
      return { ...item, productId, leaderId: product?.leaderId ?? String(item.leaderId ?? '') }
    })
  : [])
const rushCounts = reactive(Object.fromEntries(products.map((product) => [
  product.id,
  Math.max(product.sold, Number(rawRushCounts[product.id]) || product.sold),
])))
const selectedLeaderId = ref(sessionStorage.getItem('freshMall.leaderId') || leaders[0].id)
const followedLeaderIds = ref([])
const followingLoading = ref(false)
const followingError = ref('')
const followingLoadedCustomerId = ref('')
const lastOrder = ref(JSON.parse(sessionStorage.getItem('freshMall.lastOrder') ?? 'null'))

function persistCart() {
  localStorage.setItem('freshMall.cart', JSON.stringify(cart))
}

function productById(id) {
  return products.find((product) => product.id === normalizeProductId(id))
}

function leaderById(id) {
  return leaders.find((leader) => leader.id === String(id ?? ''))
}

function productRushCount(productId) {
  const product = productById(productId)
  if (!product) return 0
  return Number(rushCounts[product.id] ?? product.sold)
}

function recordProductEntry(productId) {
  const product = productById(productId)
  if (!product) return 0
  rushCounts[product.id] = productRushCount(product.id) + 1
  localStorage.setItem('freshMall.rushCounts', JSON.stringify(rushCounts))
  return rushCounts[product.id]
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

function addToCart(productId, quantity = 1) {
  const product = productById(productId)
  const leader = leaderById(product?.leaderId)
  if (!product || !leader) return false

  const existing = cart.find((item) => item.productId === product.id)
  if (existing) {
    existing.quantity = Math.min(product.stock, existing.quantity + Number(quantity || 1))
  } else {
    cart.push({ productId: product.id, leaderId: leader.id, quantity: Math.max(1, Number(quantity || 1)) })
  }
  selectedLeaderId.value = leader.id
  sessionStorage.setItem('freshMall.leaderId', String(leader.id))
  persistCart()
  return true
}

function updateQuantity(productId, quantity) {
  const item = cart.find((entry) => entry.productId === normalizeProductId(productId))
  const product = productById(productId)
  if (!item || !product) return
  item.quantity = Math.max(1, Math.min(product.stock, Number(quantity || 1)))
  persistCart()
}

function removeFromCart(productId) {
  const index = cart.findIndex((item) => item.productId === normalizeProductId(productId))
  if (index >= 0) cart.splice(index, 1)
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
    product: productById(item.productId),
    leader: leaderById(item.leaderId),
  })).filter((item) => item.product && item.leader))

  return {
    categories,
    leaders,
    products,
    cart,
    cartItems,
    cartCount: computed(() => cart.reduce((sum, item) => sum + item.quantity, 0)),
    cartSubtotal: computed(() => cartItems.value.reduce((sum, item) => sum + item.product.price * item.quantity, 0)),
    selectedLeaderId,
    followedLeaderIds,
    followingLoading,
    followingError,
    lastOrder,
    productById,
    leaderById,
    productRushCount,
    recordProductEntry,
    isLeaderFollowed,
    loadFollowedLeaders,
    setLeaderFollowed,
    addToCart,
    updateQuantity,
    removeFromCart,
    clearCart,
    setLastOrder,
  }
}
