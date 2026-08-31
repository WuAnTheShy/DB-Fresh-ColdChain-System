import broccoli from './broccoli.png'
import carrot from './carrot.png'
import cat from './cat.png'
import corn from './corn.png'
import fox from './fox.png'
import panda from './panda.png'
import rabbit from './rabbit.png'
import tomato from './tomato.png'

// 系统预置头像集合，id 与后端 Crm_Customers.Avatar 白名单保持一致。
export const presetAvatars = [
  { id: 'cat', name: '猫咪', src: cat },
  { id: 'rabbit', name: '兔子', src: rabbit },
  { id: 'panda', name: '熊猫', src: panda },
  { id: 'fox', name: '狐狸', src: fox },
  { id: 'carrot', name: '胡萝卜', src: carrot },
  { id: 'broccoli', name: '西兰花', src: broccoli },
  { id: 'tomato', name: '番茄', src: tomato },
  { id: 'corn', name: '玉米', src: corn },
]

export function avatarUrl(avatarId) {
  if (!avatarId) return ''
  return presetAvatars.find((item) => item.id === avatarId)?.src ?? ''
}
