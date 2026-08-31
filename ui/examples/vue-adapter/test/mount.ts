import { createApp, defineComponent, h, type Component } from 'vue';

/** Mounts a component into a detached element and hands back the element and an unmount. */
export function mount(root: Component): { el: HTMLElement; unmount: () => void } {
  const el = document.createElement('div');
  document.body.appendChild(el);
  const app = createApp(root);
  app.mount(el);
  return { el, unmount: () => { app.unmount(); el.remove(); } };
}

/**
 * Mounts `child`'s setup under a parent that has run `provide`, and hands back whatever the setup
 * returned — the shape the provide/inject composables need, and the only place a test in this
 * package needs a component instance for anything but rendering.
 */
export function mountUnderProvider<T>(provide: () => void, child: () => T): { value: T; unmount: () => void } {
  let captured!: T;
  const Child = defineComponent({
    setup() {
      captured = child();
      return () => h('div');
    },
  });
  const Parent = defineComponent({
    setup() {
      provide();
      return () => h(Child);
    },
  });
  const { unmount } = mount(Parent);
  return { value: captured, unmount };
}
