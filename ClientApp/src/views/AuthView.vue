<script setup>
import { Eye, EyeOff, KeyRound, LogIn, ShieldCheck, Smartphone, UserPlus } from '@lucide/vue'
import { computed, onBeforeUnmount, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { api } from '../services/api'
import { useCustomerContext } from '../state/customer'
import { presetAvatars } from '../assets/avatars'

const route = useRoute()
const router = useRouter()
const { setCustomer } = useCustomerContext()

const supportedModes = ['login', 'register', 'reset']
const mode = ref(supportedModes.includes(route.query.mode) ? route.query.mode : 'login')
const submitting = ref(false)
const sendingCode = ref(false)
const showPassword = ref(false)
const error = ref('')
const success = ref('')

const loginForm = reactive({ phone: '', password: '' })
const registerForm = reactive({
  customerName: '',
  phone: '',
  email: '',
  avatar: presetAvatars[Math.floor(Math.random() * presetAvatars.length)].id,
  password: '',
  confirmPassword: '',
})
const resetForm = reactive({ phone: '', verificationId: '', code: '', newPassword: '', confirmPassword: '' })
const simulatedCode = ref('')
const resendSeconds = ref(0)
let resendTimer = null

const isRegister = computed(() => mode.value === 'register')
const isReset = computed(() => mode.value === 'reset')
const title = computed(() => isRegister.value ? '创建消费者账号' : isReset.value ? '重置登录密码' : '欢迎回来')
const subtitle = computed(() => isRegister.value
  ? '填写资料后即可开始社区团购'
  : isReset.value ? '通过手机号和模拟短信验证码设置新密码' : '使用注册手机号和密码登录')

watch(() => route.query.mode, (value) => {
  mode.value = supportedModes.includes(value) ? value : 'login'
  error.value = ''
  success.value = ''
})

function switchMode(nextMode) {
  mode.value = nextMode
  error.value = ''
  success.value = ''
  return router.replace({
    name: 'auth',
    query: { ...route.query, mode: nextMode === 'login' ? undefined : nextMode },
  })
}

watch(() => resetForm.phone, () => {
  resetForm.verificationId = ''
  resetForm.code = ''
  simulatedCode.value = ''
})

function openReset() {
  resetForm.phone = loginForm.phone
  switchMode('reset')
}

async function sendCode() {
  sendingCode.value = true
  error.value = ''
  success.value = ''
  try {
    const result = await api.sendCustomerPasswordResetCode({ phone: resetForm.phone })
    resetForm.verificationId = result.verificationId
    simulatedCode.value = result.simulatedCode
    success.value = '模拟短信验证码已生成，请在5分钟内使用'
    resendSeconds.value = 60
    window.clearInterval(resendTimer)
    resendTimer = window.setInterval(() => {
      resendSeconds.value--
      if (resendSeconds.value <= 0) window.clearInterval(resendTimer)
    }, 1000)
  } catch (requestError) {
    error.value = requestError.message
  } finally {
    sendingCode.value = false
  }
}

onBeforeUnmount(() => window.clearInterval(resendTimer))

function destination() {
  const redirect = String(route.query.redirect ?? '')
  return redirect.startsWith('/') && !redirect.startsWith('//') ? redirect : '/profile'
}

async function submit() {
  submitting.value = true
  error.value = ''
  success.value = ''
  try {
    if (isReset.value) {
      await api.resetCustomerPassword(resetForm)
      loginForm.phone = resetForm.phone
      loginForm.password = ''
      simulatedCode.value = ''
      await switchMode('login')
      success.value = '密码已重置，原登录会话已失效，请使用新密码登录'
      return
    }

    const result = isRegister.value
      ? await api.registerCustomer({
        ...registerForm,
        email: registerForm.email || null,
      })
      : await api.loginCustomer(loginForm)
    setCustomer(result)
    await router.replace(destination())
  } catch (requestError) {
    error.value = requestError.message
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="auth-page">
    <section class="auth-intro">
      <div class="auth-intro-content">
        <span class="auth-kicker">
          <ShieldCheck :size="16" /> 鲜邻团账户
        </span>
        <h1>从产地冷链，<br>到家门口的新鲜</h1>
        <p>登录后可管理订单、优惠券、积分和收货地址，并随时查看团购配送进度。</p>

      </div>
    </section>

    <section class="auth-panel" aria-labelledby="auth-title">
      <div class="auth-tabs" role="tablist" aria-label="账号入口">
        <button type="button" :class="{ active: mode === 'login' }" role="tab" :aria-selected="mode === 'login'"
          @click="switchMode('login')">登录</button>
        <button type="button" :class="{ active: isRegister }" role="tab" :aria-selected="isRegister"
          @click="switchMode('register')">注册</button>
      </div>

      <div class="auth-heading">
        <span class="auth-heading-icon">
          <UserPlus v-if="isRegister" :size="23" />
          <KeyRound v-else-if="isReset" :size="23" />
          <LogIn v-else :size="23" />
        </span>
        <div>
          <h2 id="auth-title">{{ title }}</h2>
          <p>{{ subtitle }}</p>
        </div>
      </div>

      <div v-if="error" class="alert alert-danger auth-alert" role="alert">{{ error }}</div>
      <div v-if="success" class="alert alert-success auth-alert" role="status">{{ success }}</div>

      <form class="auth-form" @submit.prevent="submit">
        <label v-if="isRegister">
          <span>姓名</span>
          <input v-model.trim="registerForm.customerName" class="form-control" autocomplete="name" maxlength="100"
            placeholder="请输入姓名" required />
        </label>

        <label v-if="!isReset">
          <span>手机号码</span>
          <input v-if="isRegister" v-model.trim="registerForm.phone" class="form-control" type="tel" inputmode="numeric"
            autocomplete="tel" maxlength="11" pattern="1[0-9]{10}" placeholder="11位中国大陆手机号" required />
          <input v-else v-model.trim="loginForm.phone" class="form-control" type="tel" inputmode="numeric"
            autocomplete="tel" maxlength="11" pattern="1[0-9]{10}" placeholder="11位中国大陆手机号" required />
        </label>
        <label v-else>
          <span>注册手机号码</span>
          <input v-model.trim="resetForm.phone" class="form-control" type="tel" inputmode="numeric" autocomplete="tel"
            maxlength="11" pattern="1[0-9]{10}" placeholder="11位中国大陆手机号" required />
        </label>

        <label v-if="isRegister">
          <span>电子邮箱 <small>选填</small></span>
          <input v-model.trim="registerForm.email" class="form-control" type="email" autocomplete="email"
            maxlength="100" placeholder="name@example.com" />
        </label>

        <template v-if="isReset">
          <label>
            <span>模拟短信验证码</span>
            <span class="code-field">
              <input v-model.trim="resetForm.code" class="form-control" inputmode="numeric" autocomplete="one-time-code"
                maxlength="6" pattern="[0-9]{6}" placeholder="请输入6位验证码" required />
              <button class="btn btn-outline-secondary" type="button" :disabled="sendingCode || resendSeconds > 0"
                @click="sendCode">
                {{ sendingCode ? '生成中…' : resendSeconds > 0 ? `${resendSeconds}秒后重发` : '获取验证码' }}
              </button>
            </span>
          </label>
          <div v-if="simulatedCode" class="simulated-code" role="status">
            <Smartphone :size="18" />
            <span>演示验证码</span>
            <strong>{{ simulatedCode }}</strong>
          </div>
          <label>
            <span>新密码</span>
            <span class="password-field">
              <input v-model="resetForm.newPassword" class="form-control" :type="showPassword ? 'text' : 'password'"
                autocomplete="new-password" minlength="8" maxlength="100" placeholder="至少8个字符" required />
              <button type="button" :aria-label="showPassword ? '隐藏密码' : '显示密码'" @click="showPassword = !showPassword">
                <EyeOff v-if="showPassword" :size="18" />
                <Eye v-else :size="18" />
              </button>
            </span>
          </label>
          <label>
            <span>确认新密码</span>
            <input v-model="resetForm.confirmPassword" class="form-control" :type="showPassword ? 'text' : 'password'"
              autocomplete="new-password" minlength="8" maxlength="100" placeholder="再次输入新密码" required />
          </label>
        </template>
        <label v-else>
          <span>密码</span>
          <span class="password-field">
            <input v-if="isRegister" v-model="registerForm.password" class="form-control"
              :type="showPassword ? 'text' : 'password'" autocomplete="new-password" minlength="8" maxlength="100"
              placeholder="至少8个字符" required />
            <input v-else v-model="loginForm.password" class="form-control" :type="showPassword ? 'text' : 'password'"
              autocomplete="current-password" minlength="8" maxlength="100" placeholder="至少8个字符" required />
            <button type="button" :aria-label="showPassword ? '隐藏密码' : '显示密码'" @click="showPassword = !showPassword">
              <EyeOff v-if="showPassword" :size="18" />
              <Eye v-else :size="18" />
            </button>
          </span>
        </label>

        <label v-if="isRegister">
          <span>确认密码</span>
          <input v-model="registerForm.confirmPassword" class="form-control" :type="showPassword ? 'text' : 'password'"
            autocomplete="new-password" minlength="8" maxlength="100" placeholder="再次输入密码" required />
        </label>

        <fieldset v-if="isRegister" class="auth-avatar-field">
          <legend>选择头像</legend>
          <div class="auth-avatar-grid">
            <button v-for="avatar in presetAvatars" :key="avatar.id" type="button"
              :class="{ active: registerForm.avatar === avatar.id }" :title="avatar.name"
              :aria-label="`选择${avatar.name}头像`" @click="registerForm.avatar = avatar.id">
              <img :src="avatar.src" :alt="avatar.name" />
            </button>
          </div>
        </fieldset>

        <button class="btn btn-buy auth-submit" type="submit"
          :disabled="submitting || (isReset && !resetForm.verificationId)">
          <span v-if="submitting" class="spinner-border spinner-border-sm"></span>
          <UserPlus v-else-if="isRegister" :size="18" />
          <KeyRound v-else-if="isReset" :size="18" />
          <LogIn v-else :size="18" />
          {{ submitting ? '正在提交…' : isRegister ? '注册并登录' : isReset ? '确认重置密码' : '登录' }}
        </button>
      </form>

      <p v-if="isReset" class="auth-switch">
        想起密码了？
        <button type="button" @click="switchMode('login')">返回登录</button>
      </p>
      <p v-else class="auth-switch">
        {{ isRegister ? '已经有账号？' : '还没有账号？' }}
        <button type="button" @click="switchMode(isRegister ? 'login' : 'register')">{{ isRegister ? '直接登录' : '立即注册'
          }}</button>
      </p>
      <p v-if="!isRegister && !isReset" class="auth-forgot"><button type="button" @click="openReset">忘记密码？</button></p>
    </section>
  </div>
</template>

<style scoped>
.auth-page {
  display: grid;
  width: min(980px, calc(100% - 30px));
  min-height: 620px;
  grid-template-columns: minmax(0, 1.05fr) minmax(390px, .95fr);
  margin: 34px auto 8px;
  overflow: hidden;
  border-radius: 12px;
  background: #fff;
  box-shadow: 0 10px 35px rgba(15, 17, 17, .15);
}

.auth-intro {
  position: relative;
  display: flex;
  align-items: center;
  overflow: hidden;
  padding: 48px;
  background: linear-gradient(145deg, rgba(10, 39, 31, .9), rgba(0, 95, 115, .86)), url('../assets/categories/vegetable-tofu.jpg') center / cover;
  color: #fff;
}

.auth-intro::after {
  position: absolute;
  inset: 0;
  background: radial-gradient(circle at 80% 10%, rgba(254, 189, 105, .32), transparent 38%);
  content: '';
}

.auth-intro-content {
  position: relative;
  z-index: 1;
}

.auth-kicker {
  display: inline-flex;
  align-items: center;
  gap: 7px;
  color: #ffd99d;
  font-size: 12px;
  font-weight: 750;
}

.auth-intro h1 {
  max-width: 430px;
  margin: 16px 0 14px;
  font-size: clamp(32px, 4vw, 48px);
  font-weight: 800;
  line-height: 1.14;
}

.auth-intro p {
  max-width: 430px;
  margin: 0;
  color: #d8e6e0;
  font-size: 14px;
  line-height: 1.75;
}


.auth-panel {
  padding: 38px 42px;
}

.auth-tabs {
  display: grid;
  grid-template-columns: 1fr 1fr;
  margin-bottom: 31px;
  border-bottom: 1px solid var(--line);
}

.auth-tabs button {
  min-height: 44px;
  border: 0;
  border-bottom: 3px solid transparent;
  background: transparent;
  color: var(--muted);
  font-weight: 700;
}

.auth-tabs button.active {
  border-bottom-color: var(--amber-hover);
  color: var(--ink);
}

.auth-heading {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 22px;
}

.auth-heading-icon {
  display: inline-flex;
  width: 46px;
  height: 46px;
  flex: 0 0 46px;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
  background: #e8f2ee;
  color: var(--brand);
}

.auth-heading h2 {
  margin: 0;
  font-size: 24px;
  font-weight: 800;
}

.auth-heading p {
  margin: 4px 0 0;
  color: var(--muted);
  font-size: 11px;
}

.auth-alert {
  margin-bottom: 16px;
  font-size: 12px;
}

.auth-form {
  display: grid;
  gap: 15px;
}

.auth-form label {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.auth-form label>span:first-child {
  color: #39443f;
  font-size: 11px;
  font-weight: 750;
}

.auth-form label small {
  color: #8a928e;
  font-weight: 500;
}

.password-field {
  position: relative;
  display: block;
}

.password-field input {
  padding-right: 46px;
}

.password-field button {
  position: absolute;
  top: 1px;
  right: 1px;
  display: inline-flex;
  width: 42px;
  height: 40px;
  align-items: center;
  justify-content: center;
  border: 0;
  background: transparent;
  color: #68736e;
}

.code-field {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 118px;
  gap: 8px;
}

.code-field button {
  white-space: nowrap;
  font-size: 11px;
}

.simulated-code {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 11px 13px;
  border: 1px dashed #63a88d;
  border-radius: 8px;
  background: #eef8f4;
  color: #326c58;
  font-size: 11px;
}

.simulated-code strong {
  margin-left: auto;
  color: var(--brand);
  font-size: 20px;
  letter-spacing: 4px;
}

.auth-avatar-field {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin: 0;
  padding: 0;
  border: 0;
}

.auth-avatar-field legend {
  padding: 0;
  color: #39443f;
  font-size: 11px;
  font-weight: 750;
}

.auth-avatar-grid {
  display: grid;
  grid-template-columns: repeat(8, 1fr);
  gap: 8px;
}

.auth-avatar-grid button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 3px;
  border: 2px solid transparent;
  border-radius: 50%;
  background: transparent;
  cursor: pointer;
  transition: border-color .15s, transform .15s;
}

.auth-avatar-grid button:hover {
  transform: scale(1.06);
}

.auth-avatar-grid button.active {
  border-color: var(--brand);
  box-shadow: 0 0 0 2px rgba(21, 128, 61, .18);
}

.auth-avatar-grid img {
  width: 100%;
  height: auto;
  border-radius: 50%;
}

@media (max-width: 479.98px) {
  .auth-avatar-grid {
    grid-template-columns: repeat(4, 1fr);
  }
}

.auth-submit {
  width: 100%;
  margin-top: 5px;
}

.auth-switch {
  margin: 22px 0 0;
  color: var(--muted);
  font-size: 11px;
  text-align: center;
}

.auth-switch button {
  border: 0;
  background: transparent;
  color: var(--brand);
  font-weight: 750;
}

.auth-forgot {
  margin: 9px 0 0;
  text-align: center;
}

.auth-forgot button {
  border: 0;
  background: transparent;
  color: #68736e;
  font-size: 11px;
  text-decoration: underline;
}

@media (max-width: 767.98px) {
  .auth-page {
    width: min(100% - 20px, 520px);
    min-height: 0;
    grid-template-columns: 1fr;
    margin-top: 14px;
  }

  .auth-intro {
    display: none;
  }

  .auth-panel {
    padding: 25px 21px 30px;
  }

  .auth-tabs {
    margin-bottom: 24px;
  }
}
</style>
