import { createRouter, createWebHistory } from 'vue-router'
import HomeView from './views/HomeView.vue'
import SearchView from './views/SearchView.vue'
import LeaderDetailView from './views/LeaderDetailView.vue'
import ProductDetailView from './views/ProductDetailView.vue'
import CartView from './views/CartView.vue'
import CheckoutView from './views/CheckoutView.vue'
import OrderSuccessView from './views/OrderSuccessView.vue'
import OrdersView from './views/OrdersView.vue'
import OrderDetailView from './views/OrderDetailView.vue'
import CouponsView from './views/CouponsView.vue'
import AddressesView from './views/AddressesView.vue'
import CustomerView from './views/CustomerView.vue'

const router = createRouter({
  history: createWebHistory('/app/'),
  routes: [
    { path: '/', name: 'home', component: HomeView },
    { path: '/search', name: 'search', component: SearchView },
    { path: '/category/:slug', name: 'category', component: SearchView },
    { path: '/leaders/:id', name: 'leader-detail', component: LeaderDetailView, props: true },
    { path: '/products/:id', name: 'product-detail', component: ProductDetailView, props: true },
    { path: '/deals', redirect: '/search?deal=today' },
    { path: '/cart', name: 'cart', component: CartView },
    { path: '/checkout', name: 'checkout', component: CheckoutView },
    { path: '/order-success/:id', name: 'order-success', component: OrderSuccessView, props: true },
    { path: '/orders', name: 'orders', component: OrdersView },
    { path: '/orders/:id', name: 'order-detail', component: OrderDetailView, props: true },
    { path: '/coupons', name: 'coupons', component: CouponsView },
    { path: '/addresses', name: 'addresses', component: AddressesView },
    { path: '/profile', name: 'profile', component: CustomerView },
    { path: '/customer', redirect: '/profile' },
    { path: '/orders/new', redirect: '/checkout' },
    { path: '/:pathMatch(.*)*', redirect: '/' },
  ],
  scrollBehavior: () => ({ top: 0 }),
})

export default router
