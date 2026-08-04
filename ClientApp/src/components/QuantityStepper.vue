<script setup>
import { Minus, Plus } from '@lucide/vue'

const props = defineProps({
  modelValue: { type: Number, required: true },
  min: { type: Number, default: 1 },
  max: { type: Number, default: 99 },
})
const emit = defineEmits(['update:modelValue'])

function setValue(value) {
  emit('update:modelValue', Math.max(props.min, Math.min(props.max, Number(value || props.min))))
}
</script>

<template>
  <div class="quantity-stepper">
    <button type="button" title="减少数量" :disabled="modelValue <= min" @click="setValue(modelValue - 1)"><Minus :size="15" /></button>
    <input :value="modelValue" type="number" :min="min" :max="max" aria-label="商品数量" @change="setValue($event.target.value)" />
    <button type="button" title="增加数量" :disabled="modelValue >= max" @click="setValue(modelValue + 1)"><Plus :size="15" /></button>
  </div>
</template>
