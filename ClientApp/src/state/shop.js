import { computed, reactive, ref } from 'vue'
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
    id: 1,
    name: '林晓晴',
    title: '社区生鲜团长',
    area: '浦东新区 · 花木街道',
    avatar: 'https://images.unsplash.com/photo-1494790108377-be9c29b29330?auto=format&fit=crop&w=240&q=85',
    cover: 'https://images.unsplash.com/photo-1528821128474-27f963b062bf?auto=format&fit=crop&w=1200&q=85',
    description: '每天精选当季果蔬，严选冷链到家。开团前亲自试吃，截团后同步配送进度。',
    tags: ['平台认证', '果蔬优选'],
    following: 1280,
  },
  {
    id: 2,
    name: '陈海峰',
    title: '海鲜冷链团长',
    area: '徐汇区 · 田林街道',
    avatar: 'https://images.unsplash.com/photo-1500648767791-00dcc994a43e?auto=format&fit=crop&w=240&q=85',
    cover: 'https://images.unsplash.com/photo-1547592180-85f173990554?auto=format&fit=crop&w=1200&q=85',
    description: '专注冰鲜水产和家庭餐桌，冷链时效透明，按团同步到货与提货信息。',
    tags: ['平台认证', '水产专营'],
    following: 936,
  },
  {
    id: 3,
    name: '周婉宁',
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
    id: 'P1',
    slug: 'cherries',
    name: '智利进口车厘子礼盒',
    shortName: '车厘子',
    category: 'fruit',
    spec: 'JJ级 · 2.5kg礼盒',
    price: 50,
    originalPrice: 69.9,
    image: 'https://images.unsplash.com/photo-1528821128474-27f963b062bf?auto=format&fit=crop&w=800&q=88',
    storage: '冷藏',
    leaderIds: [1, 3],
    sold: 286,
    target: 300,
    stock: 100,
    cutoff: '今天 22:00 截团',
    delivery: '明日 16:00 前送达',
    publishedAt: '2026-08-05T09:20:00+08:00',
    summary: '果径饱满、脆甜多汁，产地冷链直达，适合家庭分享。',
  },
  {
    id: 'P2',
    slug: 'salmon',
    name: '冰鲜三文鱼中段',
    shortName: '三文鱼',
    category: 'seafood',
    spec: '去皮去刺 · 500g',
    price: 80,
    originalPrice: 98,
    image: 'https://images.unsplash.com/photo-1547592180-85f173990554?auto=format&fit=crop&w=800&q=88',
    storage: '冷藏',
    leaderIds: [2],
    sold: 117,
    target: 150,
    stock: 50,
    cutoff: '明天 10:00 截团',
    delivery: '后日 12:00 前送达',
    publishedAt: '2026-08-05T08:35:00+08:00',
    summary: '肉质细腻，家庭小包装，低温锁鲜运输，开盒即可分切烹饪。',
  },
  {
    id: 'P3',
    slug: 'organic-vegetables',
    name: '一周有机蔬菜组合',
    shortName: '有机蔬菜',
    category: 'vegetable',
    spec: '6种搭配 · 约2.5kg',
    price: 20,
    originalPrice: 29.9,
    image: 'https://images.unsplash.com/photo-1540420773420-3366772f4999?auto=format&fit=crop&w=800&q=88',
    storage: '冷藏',
    leaderIds: [1, 3],
    sold: 368,
    target: 400,
    stock: 200,
    cutoff: '今天 20:00 截团',
    delivery: '明日 12:00 前送达',
    publishedAt: '2026-08-04T18:10:00+08:00',
    summary: '当日搭配叶菜与根茎菜，一次备齐家庭一周的基础蔬菜。',
  },
]

const rawCart = JSON.parse(localStorage.getItem('freshMall.cart') ?? '[]')
const rawRushCounts = JSON.parse(localStorage.getItem('freshMall.rushCounts') ?? '{}')
const rawFollowedLeaderIds = JSON.parse(localStorage.getItem('freshMall.followedLeaderIds') ?? '[]')
function normalizeProductId(id) {
  const value = String(id ?? '').trim()
  return /^P\d+$/.test(value) ? value : `P${value}`
}

const cart = reactive(Array.isArray(rawCart)
  ? rawCart.map((item) => ({ ...item, productId: normalizeProductId(item.productId) }))
  : [])
const rushCounts = reactive(Object.fromEntries(products.map((product) => [
  product.id,
  Math.max(product.sold, Number(rawRushCounts[product.id]) || product.sold),
])))
const selectedLeaderId = ref(Number(sessionStorage.getItem('freshMall.leaderId')) || 1)
const followedLeaderIds = ref(Array.isArray(rawFollowedLeaderIds)
  ? [...new Set(rawFollowedLeaderIds.map(Number).filter((id) => leaders.some((leader) => leader.id === id)))]
  : [])
const lastOrder = ref(JSON.parse(sessionStorage.getItem('freshMall.lastOrder') ?? 'null'))

function persistCart() {
  localStorage.setItem('freshMall.cart', JSON.stringify(cart))
}

function productById(id) {
  return products.find((product) => product.id === normalizeProductId(id))
}

function leaderById(id) {
  return leaders.find((leader) => leader.id === Number(id))
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
  return followedLeaderIds.value.includes(Number(leaderId))
}

function toggleLeaderFollow(leaderId) {
  const id = Number(leaderId)
  if (!leaderById(id)) return false
  const index = followedLeaderIds.value.indexOf(id)
  if (index >= 0) followedLeaderIds.value.splice(index, 1)
  else followedLeaderIds.value.push(id)
  localStorage.setItem('freshMall.followedLeaderIds', JSON.stringify(followedLeaderIds.value))
  return isLeaderFollowed(id)
}

function addToCart(productId, leaderId, quantity = 1) {
  const product = productById(productId)
  const leader = leaderById(leaderId)
  if (!product || !leader || !product.leaderIds.includes(leader.id)) return false

  const existing = cart.find((item) => item.productId === product.id && item.leaderId === leader.id)
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

function updateQuantity(productId, leaderId, quantity) {
  const item = cart.find((entry) => entry.productId === normalizeProductId(productId) && entry.leaderId === Number(leaderId))
  const product = productById(productId)
  if (!item || !product) return
  item.quantity = Math.max(1, Math.min(product.stock, Number(quantity || 1)))
  persistCart()
}

function removeFromCart(productId, leaderId) {
  const index = cart.findIndex((item) => item.productId === normalizeProductId(productId) && item.leaderId === Number(leaderId))
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
    lastOrder,
    productById,
    leaderById,
    productRushCount,
    recordProductEntry,
    isLeaderFollowed,
    toggleLeaderFollow,
    addToCart,
    updateQuantity,
    removeFromCart,
    clearCart,
    setLastOrder,
  }
}
