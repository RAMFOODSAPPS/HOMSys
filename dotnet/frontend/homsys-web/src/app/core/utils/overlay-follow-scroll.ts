import { OverlayListenerOptions, OverlayOptions } from 'primeng/api';
import { DomHandler } from 'primeng/dom';

/**
 * Global PrimeNG overlay behaviour: an open dropdown / multiselect / autocomplete panel FOLLOWS its
 * field when the page scrolls, instead of closing.
 *
 * PrimeNG closes a panel on any scroll of a container around its field — even a 1px scroll the browser
 * makes by itself while the panel opens (focus / scroll-into-view). On a long form that is not scrolled
 * to the very bottom that scroll is possible, so dropdowns closed or flickered as they opened; at the
 * bottom of the page there was no room to scroll and they were stable. Now a scroll just re-aligns the
 * panel; it closes only once its field has scrolled out of view.
 */
const open = new Map<HTMLElement, HTMLElement>(); // panel → its field

function inView(field: HTMLElement, scroller: Element | Document): boolean {
  const r = field.getBoundingClientRect();
  const box = scroller instanceof Element
    ? scroller.getBoundingClientRect()
    : { top: 0, bottom: window.innerHeight };
  return r.bottom > box.top && r.top < box.bottom;
}

export const followScrollOverlayOptions: OverlayOptions = {
  onBeforeShow: e => { if (e?.overlay && e.target) open.set(e.overlay as HTMLElement, e.target as HTMLElement); },
  onBeforeHide: e => { if (e?.overlay) open.delete(e.overlay as HTMLElement); },
  listener: (event: Event, options?: OverlayListenerOptions) => {
    if (options?.type !== 'scroll') return options?.valid;
    const scroller = event.target as Element | Document;
    let hide = false;
    for (const [panel, field] of open) {
      if (!panel.isConnected || !field.isConnected) { open.delete(panel); continue; }
      if (!(scroller instanceof Document) && !scroller.contains(field)) continue;
      if (inView(field, scroller)) DomHandler.alignOverlay(panel, field, 'body', false);
      else hide = true;
    }
    return hide;
  }
};
