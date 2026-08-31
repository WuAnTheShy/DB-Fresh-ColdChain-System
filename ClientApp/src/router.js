import { createRouter, createWebHistory } from 'vue-router'
import HomeView from './views/HomeView.vue'
import SearchView from './views/SearchView.vue'
import LeaderDetailView from './views/LeaderDetailView.vue'
import FollowingView from './views/FollowingView.vue'
import ProductDetailView from './views/ProductDetailView.vue'
import CartView from './views/CartView.vue'
import CheckoutView from './views/CheckoutView.vue'
import OrderSuccessView from './views/OrderSuccessView.vue'
import PaymentView from './views/PaymentView.vue'
import OrdersView from './views/OrdersView.vue'
import OrderDetailView from './views/OrderDetailView.vue'
import RefundApplyView from './views/RefundApplyView.vue'
import CouponsView from './views/CouponsView.vue'
import AddressesView from './views/AddressesView.vue'
import CustomerView from './views/CustomerView.vue'
import MessagesView from './views/MessagesView.vue'
import AuthView from './views/AuthView.vue'
import { useCustomerContext } from './state/customer'

const router = createRouter({
  history: createWebHistory('/app/'),
  routes: [
    { path: '/', name: 'home', component: HomeView },
    { path: '/auth', name: 'auth', component: AuthView },
    { path: '/search', name: 'search', component: SearchView },
    { path: '/category/:slug', name: 'category', component: SearchView },
    { path: '/leaders/:id', name: 'leader-detail', component: LeaderDetailView, props: true },
    { path: '/following', name: 'following', component: FollowingView, meta: { requiresAuth: true } },
    { path: '/products/:id', name: 'product-detail', component: ProductDetailView, props: true },
    { path: '/cart', name: 'cart', component: CartView, meta: { requiresAuth: true } },
    { path: '/checkout', name: 'checkout', component: CheckoutView, meta: { requiresAuth: true } },
    { path: '/order-success/:id', name: 'order-success', component: OrderSuccessView, props: true, meta: { requiresAuth: true } },
    { path: '/payment/:batchId', name: 'payment', component: PaymentView, props: true, meta: { requiresAuth: true } },
    { path: '/orders', name: 'orders', component: OrdersView, meta: { requiresAuth: true } },
    { path: '/orders/:id', name: 'order-detail', component: OrderDetailView, props: true, meta: { requiresAuth: true } },
    { path: '/orders/:id/refund', name: 'refund-apply', component: RefundApplyView, props: true, meta: { requiresAuth: true } },
    { path: '/coupons', name: 'coupons', component: CouponsView, meta: { requiresAuth: true } },
    { path: '/addresses', name: 'addresses', component: AddressesView, meta: { requiresAuth: true } },
    { path: '/profile', name: 'profile', component: CustomerView, meta: { requiresAuth: true } },
    { path: '/messages', name: 'messages', component: MessagesView, meta: { requiresAuth: true } },
    { path: '/customer', redirect: '/profile' },
    { path: '/orders/new', redirect: '/checkout' },
    { path: '/:pathMatch(.*)*', redirect: '/' },
  ],
  scrollBehavior: () => ({ top: 0 }),
})

router.beforeEach(async (to) => {
  const { initializeAuth, isAuthenticated } = useCustomerContext()
  try {
    await initializeAuth()
  } catch {
    if (to.meta.requiresAuth) {
      return { name: 'auth', query: { redirect: to.fullPath } }
    }
  }

  if (to.name === 'auth' && isAuthenticated.value) {
    return { path: '/profile' }
  }
  if (to.meta.requiresAuth && !isAuthenticated.value) {
    return { name: 'auth', query: { redirect: to.fullPath } }
  }
  return true
})

export default router
