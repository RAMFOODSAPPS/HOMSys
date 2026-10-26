import { AfterViewInit, Directive, ElementRef, OnDestroy, Renderer2, inject, input } from '@angular/core';

const STORE_PREFIX = 'homsys_colw_';
const MIN_PX = 48;

/**
 * Drag-to-resize columns for a plain <table>: a handle on each header's right edge.
 * Widths are kept per table in localStorage (key = the directive value), so each user's
 * layout survives reloads. Double-click a handle to reset that column.
 *
 *   <table class="lines" appResizableColumns="tasaf-products">…</table>
 */
@Directive({
  selector: 'table[appResizableColumns]',
  standalone: true,
  host: { class: 'rc-table' }
})
export class ResizableColumnsDirective implements AfterViewInit, OnDestroy {
  /** Storage key for this table's widths (empty = don't remember). */
  readonly appResizableColumns = input('');

  private el = inject<ElementRef<HTMLTableElement>>(ElementRef);
  private r = inject(Renderer2);
  private unlisten: (() => void)[] = [];
  private observer?: MutationObserver;

  ngAfterViewInit() {
    this.setup();
    // The header row can re-render (e.g. @if around the table) — re-attach handles then.
    this.observer = new MutationObserver(() => this.setup());
    this.observer.observe(this.el.nativeElement, { childList: true, subtree: false });
  }

  ngOnDestroy() {
    this.observer?.disconnect();
    this.unlisten.forEach(u => u());
  }

  private headers(): HTMLTableCellElement[] {
    const row = this.el.nativeElement.tHead?.rows[0];
    return row ? Array.from(row.cells) : [];
  }

  private setup() {
    const ths = this.headers();
    if (!ths.length || ths.every(th => th.querySelector(':scope > .rc-handle'))) return;
    const saved = this.load();
    if (saved.length === ths.length && saved.every(w => w > 0)) {
      ths.forEach((th, i) => th.style.width = `${saved[i]}px`);
      this.fitTable();
    }
    ths.forEach(th => {
      if (th.querySelector(':scope > .rc-handle')) return;
      const handle = this.r.createElement('span') as HTMLSpanElement;
      this.r.addClass(handle, 'rc-handle');
      this.r.setAttribute(handle, 'title', 'Drag to resize · double-click to reset all columns');
      this.r.appendChild(th, handle);
      this.unlisten.push(
        this.r.listen(handle, 'pointerdown', (e: PointerEvent) => this.start(e, th)),
        this.r.listen(handle, 'dblclick', () => this.reset()));
    });
  }

  /** Every column gets an explicit width and the table is exactly their sum, so dragging
   *  one column never squeezes the others (the block scrolls sideways when needed). */
  private freeze() {
    for (const h of this.headers()) if (!h.style.width) h.style.width = `${h.offsetWidth}px`;
    this.fitTable();
  }

  private fitTable() {
    const total = this.headers().reduce((s, h) => s + (parseInt(h.style.width, 10) || h.offsetWidth), 0);
    this.el.nativeElement.style.width = `${total}px`;
    this.el.nativeElement.style.minWidth = `${total}px`;
    this.el.nativeElement.classList.add('rc-fixed');
  }

  private reset() {
    for (const h of this.headers()) h.style.width = '';
    this.el.nativeElement.style.width = '';
    this.el.nativeElement.style.minWidth = '';
    this.el.nativeElement.classList.remove('rc-fixed');
    const key = this.appResizableColumns();
    if (key) localStorage.removeItem(STORE_PREFIX + key);
  }

  private start(e: PointerEvent, th: HTMLTableCellElement) {
    e.preventDefault();
    e.stopPropagation();
    this.freeze();
    const startX = e.clientX;
    const startW = th.offsetWidth;
    document.body.classList.add('rc-resizing');
    const move = this.r.listen('window', 'pointermove', (ev: PointerEvent) => {
      th.style.width = `${Math.max(MIN_PX, startW + ev.clientX - startX)}px`;
      this.fitTable();
    });
    const up = this.r.listen('window', 'pointerup', () => {
      move(); up();
      document.body.classList.remove('rc-resizing');
      this.save();
    });
  }

  private load(): number[] {
    const key = this.appResizableColumns();
    if (!key) return [];
    try { return JSON.parse(localStorage.getItem(STORE_PREFIX + key) ?? '[]'); } catch { return []; }
  }

  private save() {
    const key = this.appResizableColumns();
    if (!key) return;
    localStorage.setItem(STORE_PREFIX + key,
      JSON.stringify(this.headers().map(th => th.style.width ? parseInt(th.style.width, 10) : 0)));
  }
}
