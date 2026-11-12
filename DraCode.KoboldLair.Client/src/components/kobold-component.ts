import { BaseComponent } from 'birko-web-core';

/**
 * Base for the KoboldLair views and components. Load data in onMount(), which runs once; wire the DOM in onUpdated(),
 * which runs after every render, with listen()/on(), whose handlers the next update() drops. Calling onMount() from
 * onUpdated() reloads, re-renders and recurses until the stack overflows.
 */
export abstract class KoboldComponent extends BaseComponent {
  /** listen() on the element matching `selector` in this component's shadow root, when it is rendered. */
  protected on<T extends Event = Event>(selector: string, event: string, handler: (e: T) => void): void {
    const el = this.$(selector);
    if (el) this.listen<T>(el, event, handler);
  }
}
