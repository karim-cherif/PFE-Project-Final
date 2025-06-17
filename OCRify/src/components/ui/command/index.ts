import { Command as CommandPrimitive } from 'cmdk'
import { defineComponent, h } from 'vue'

export const Command = defineComponent({
  name: 'Command',
  setup(_, { slots }) {
    return () => h(CommandPrimitive, {}, slots)
  }
})

export const CommandInput = defineComponent({
  name: 'CommandInput',
  setup(_, { slots }) {
    return () => h(CommandPrimitive.Input, {}, slots)
  }
})

export const CommandList = defineComponent({
  name: 'CommandList',
  setup(_, { slots }) {
    return () => h(CommandPrimitive.List, {}, slots)
  }
})

export const CommandGroup = defineComponent({
  name: 'CommandGroup',
  setup(_, { slots }) {
    return () => h(CommandPrimitive.Group, {}, slots)
  }
})

export const CommandItem = defineComponent({
  name: 'CommandItem',
  setup(_, { slots }) {
    return () => h(CommandPrimitive.Item, {}, slots)
  }
})

export const CommandDialog = defineComponent({
  name: 'CommandDialog',
  setup(_, { slots }) {
    return () => h(CommandPrimitive.Dialog, {}, slots)
  }
})

export const CommandEmpty = defineComponent({
  name: 'CommandEmpty',
  setup(_, { slots }) {
    return () => h(CommandPrimitive.Empty, {}, slots)
  }
})

export const CommandSeparator = defineComponent({
  name: 'CommandSeparator',
  setup(_, { slots }) {
    return () => h(CommandPrimitive.Separator, {}, slots)
  }
})

export const CommandShortcut = defineComponent({
  name: 'CommandShortcut',
  setup(_, { slots }) {
    return () => h('kbd', { class: 'ml-auto text-xs tracking-widest text-muted-foreground' }, slots)
  }
})