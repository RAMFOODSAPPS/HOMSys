import { Component, ElementRef, HostListener, OnInit, computed, inject, input, output, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { CheckboxModule } from 'primeng/checkbox';
import { ConfirmationService, MessageService } from 'primeng/api';
import { FormLayoutService } from '../../core/services/form-layout.service';
import {
  FormFieldDef, FormLayoutItem, GRID_COLS, bottomRow, byPositionTabs, clampItem, defaultItems,
  renumberTabs, resolveCollisions, sortByTab
} from '../../core/models/form-layout.model';

const ROW_PX = 52;

interface DragState { key: string; mode: 'move' | 'resize'; x: number; y: number; orig: FormLayoutItem; }

/**
 * VB-style form designer on a 12-column snap grid (Admin only — the host page gates it,
 * the API enforces it). Drag to move, drag the corner to resize, arrows nudge.
 * Saving stores the layout for everyone; Reset deletes it (back to the built-in default).
 */
@Component({
  selector: 'app-form-designer',
  standalone: true,
  imports: [FormsModule, ButtonModule, InputTextModule, CheckboxModule],
  template: `
    <div class="fd">
      <div class="fd-bar">
        <strong class="fd-title"><i class="pi pi-objects-column"></i> Form Designer — {{ title() }}</strong>
        <span class="muted">Drag to move · drag the corner to resize · arrow keys nudge (Shift = resize)</span>
        <span class="sp"></span>
        <p-button label="Tab order by position" icon="pi pi-sort-numeric-down" size="small" severity="secondary" [text]="true" (onClick)="autoTabs()" />
        <p-button label="Reset to default" icon="pi pi-replay" size="small" severity="secondary" [text]="true" (onClick)="confirmReset()" />
        <p-button label="Cancel" size="small" severity="secondary" [outlined]="true" (onClick)="closed.emit()" />
        <p-button label="Save Layout" icon="pi pi-save" size="small" [loading]="saving()" (onClick)="save()" />
      </div>

      <div class="fd-body">
        <div class="fd-canvas" #canvas [style.grid-template-rows]="'repeat(' + rows() + ', ' + rowPx + 'px)'"
             (pointerdown)="selKey.set(null)">
          @for (it of visible(); track it.key) {
            <div class="fd-item" [class.sel]="it.key === selKey()" [class.block]="def(it.key).kind === 'block'"
                 [class.note]="def(it.key).kind === 'note'" [class.dragging]="drag?.key === it.key"
                 [style.grid-column]="it.col + ' / span ' + it.w" [style.grid-row]="it.row + ' / span ' + it.h"
                 (pointerdown)="start($event, it, 'move')">
              <span class="tab" title="Tab order">{{ it.tab }}</span>
              <div class="cap">
                <i class="pi" [class]="icon(it.key)"></i>
                {{ it.label || def(it.key).label }}
                @if (def(it.key).required) { <span class="req">*</span> }
              </div>
              @if (def(it.key).hint) { <div class="hint">{{ def(it.key).hint }}</div> }
              <span class="rs" title="Resize" (pointerdown)="start($event, it, 'resize')"></span>
            </div>
          }
        </div>

        <aside class="fd-props">
          @if (sel(); as s) {
            <h4>{{ def(s.key).label }}</h4>
            @if (def(s.key).hint) { <div class="hint">{{ def(s.key).hint }}</div> }

            <label>{{ def(s.key).kind === 'note' ? 'Text' : 'Caption' }}</label>
            <input pInputText class="w-full" [ngModel]="s.label ?? ''" (ngModelChange)="patch({ key: s.key, label: $event })"
              [placeholder]="def(s.key).label" maxlength="300" />

            <div class="nums">
              <div><label>Column</label><input pInputText type="number" [value]="s.col" (change)="num(s, 'col', $event)" /></div>
              <div><label>Row</label><input pInputText type="number" [value]="s.row" (change)="num(s, 'row', $event)" /></div>
              <div><label>Width</label><input pInputText type="number" [value]="s.w" (change)="num(s, 'w', $event)" /></div>
              <div><label>Height</label><input pInputText type="number" [value]="s.h" (change)="num(s, 'h', $event)"
                [disabled]="minH(s.key) === maxH(s.key)" /></div>
              <div><label>Tab order</label><input pInputText type="number" [value]="s.tab" (change)="setTab(s.key, $event)" /></div>
            </div>

            <div class="chk">
              <p-checkbox [binary]="true" inputId="fdHidden" [ngModel]="!!s.hidden" (ngModelChange)="setHidden(s.key, $event)"
                [disabled]="!!def(s.key).required" />
              <label for="fdHidden">Hidden</label>
            </div>
            @if (def(s.key).required) { <div class="hint">Required to save or submit — can't be hidden.</div> }
          } @else {
            <p class="muted">Click a field to edit its caption, size, tab order or visibility.</p>
          }

          <h4 class="mt">Hidden fields</h4>
          @for (it of hidden(); track it.key) {
            <div class="hid">
              <span>{{ it.label || def(it.key).label }}</span>
              <p-button label="Show" size="small" [text]="true" (onClick)="setHidden(it.key, false)" />
            </div>
          } @empty { <p class="muted">None.</p> }
        </aside>
      </div>
    </div>
  `,
  styles: [`
    .fd { background: var(--p-surface-card); border-radius: 8px; padding: 0.75rem 1rem; }
    .fd-bar { display: flex; align-items: center; gap: 0.5rem; flex-wrap: wrap; margin-bottom: 0.75rem; }
    .fd-title { color: #800000; }
    .sp { flex: 1; }
    .muted, .hint { font-size: 0.75rem; color: var(--p-text-muted-color); }
    .fd-body { display: flex; gap: 1rem; align-items: flex-start; }
    .fd-canvas {
      flex: 1; min-width: 0; display: grid; grid-template-columns: repeat(${GRID_COLS}, minmax(0, 1fr));
      border: 1px solid #e2cccc; border-radius: 6px; user-select: none; touch-action: none;
      background-color: #fdfafa;
      background-image:
        linear-gradient(to right, rgba(128,0,0,0.07) 1px, transparent 1px),
        linear-gradient(to bottom, rgba(128,0,0,0.07) 1px, transparent 1px);
      background-size: calc(100% / ${GRID_COLS}) ${ROW_PX}px;
    }
    .fd-item {
      position: relative; margin: 3px; padding: 0.3rem 0.5rem 0.3rem 1.6rem; overflow: hidden;
      background: #fff; border: 1px solid #d9c3c3; border-radius: 5px; cursor: move; font-size: 0.8rem;
    }
    .fd-item.block { background: #f6eded; border-style: dashed; }
    .fd-item.note { background: transparent; border-style: dotted; font-style: italic; }
    .fd-item.sel { border: 2px solid #800000; box-shadow: 0 2px 8px rgba(128,0,0,0.18); z-index: 2; }
    .fd-item.dragging { opacity: 0.85; z-index: 3; }
    .cap { font-weight: 600; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .cap .pi { font-size: 0.75rem; color: #800000; margin-right: 0.2rem; }
    .fd-item .hint { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .tab {
      position: absolute; left: 3px; top: 4px; min-width: 1.1rem; height: 1.1rem; padding: 0 2px; border-radius: 3px;
      background: #800000; color: #fff; font-size: 0.65rem; display: flex; align-items: center; justify-content: center;
    }
    .rs {
      position: absolute; right: 0; bottom: 0; width: 12px; height: 12px; cursor: nwse-resize;
      background: linear-gradient(135deg, transparent 50%, #800000 50%);
    }
    .req { color: var(--p-red-500, #ef4444); }
    .fd-props { width: 260px; flex-shrink: 0; border-left: 1px solid var(--p-surface-border); padding-left: 1rem; }
    .fd-props h4 { margin: 0 0 0.35rem; font-size: 0.9rem; color: #800000; }
    .fd-props label { display: block; font-size: 0.78rem; font-weight: 500; margin: 0.5rem 0 0.2rem; }
    .nums { display: grid; grid-template-columns: 1fr 1fr; gap: 0 0.5rem; }
    .nums input { width: 100%; }
    .chk { display: flex; align-items: center; gap: 0.4rem; margin-top: 0.75rem; }
    .chk label { margin: 0; }
    .mt { margin-top: 1.25rem !important; }
    .hid { display: flex; align-items: center; justify-content: space-between; font-size: 0.8rem; }
  `]
})
export class FormDesignerComponent implements OnInit {
  private api        = inject(FormLayoutService);
  private messageSvc = inject(MessageService, { optional: true });
  private confirmSvc = inject(ConfirmationService, { optional: true });

  readonly formKey = input.required<string>();
  readonly title   = input('');
  readonly fields  = input.required<FormFieldDef[]>();
  /** The layout currently in use (copied; nothing changes until Save). */
  readonly layout  = input.required<FormLayoutItem[]>();
  /** New layout after Save / Reset. */
  readonly saved   = output<FormLayoutItem[]>();
  readonly closed  = output<void>();

  private canvas = viewChild.required<ElementRef<HTMLElement>>('canvas');

  protected readonly rowPx = ROW_PX;
  protected items   = signal<FormLayoutItem[]>([]);
  protected selKey  = signal<string | null>(null);
  protected saving  = signal(false);
  protected drag: DragState | null = null;

  protected visible = computed(() => this.items().filter(i => !i.hidden));
  protected hidden  = computed(() => this.items().filter(i => i.hidden));
  protected sel     = computed(() => this.items().find(i => i.key === this.selKey()) ?? null);
  protected rows    = computed(() => bottomRow(this.items()) + 3);
  private defs      = computed(() => new Map(this.fields().map(d => [d.key, d])));

  ngOnInit() { this.items.set(this.layout().map(i => ({ ...i }))); }

  def(key: string): FormFieldDef { return this.defs().get(key)!; }
  minH(key: string) { return this.def(key).minH ?? 1; }
  maxH(key: string) { return Math.max(this.minH(key), this.def(key).maxH ?? this.def(key).h); }
  icon(key: string) {
    const k = this.def(key).kind;
    return k === 'block' ? 'pi-table' : k === 'note' ? 'pi-info-circle' : 'pi-pen-to-square';
  }

  /** Replace one item (clamped); `resolve` pushes anything it now overlaps downwards. */
  patch(change: Partial<FormLayoutItem> & { key: string }, resolve = false) {
    this.items.update(list => {
      const next = list.map(i => i.key === change.key ? clampItem({ ...i, ...change }, this.def(i.key)) : i);
      return resolve ? resolveCollisions(next, change.key) : next;
    });
  }

  num(s: FormLayoutItem, prop: 'col' | 'row' | 'w' | 'h', e: Event) {
    const v = Number((e.target as HTMLInputElement).value);
    if (Number.isFinite(v)) this.patch({ key: s.key, [prop]: v }, true);
  }

  setTab(key: string, e: Event) {
    const n = Number((e.target as HTMLInputElement).value);
    if (!Number.isFinite(n)) return;
    const order = sortByTab(this.items());
    const it = order.find(i => i.key === key)!;
    const rest = order.filter(i => i.key !== key);
    rest.splice(Math.min(Math.max(Math.round(n) - 1, 0), rest.length), 0, it);
    this.items.set(rest.map((i, idx) => ({ ...i, tab: idx + 1 })));
  }

  setHidden(key: string, hidden: boolean) {
    if (hidden) { this.patch({ key, hidden: true }); return; }
    // Shown again → place at the bottom so it doesn't land on top of something.
    this.patch({ key, hidden: false, col: 1, row: bottomRow(this.items()) }, true);
    this.selKey.set(key);
  }

  autoTabs() { this.items.set(sortByTab(byPositionTabs(this.items()))); }

  // ── Mouse drag (move / resize), snapped to whole columns and rows ──
  start(e: PointerEvent, it: FormLayoutItem, mode: 'move' | 'resize') {
    if (e.button !== 0) return;
    e.stopPropagation();
    e.preventDefault();
    this.selKey.set(it.key);
    this.drag = { key: it.key, mode, x: e.clientX, y: e.clientY, orig: { ...it } };
  }

  @HostListener('window:pointermove', ['$event'])
  onMove(e: PointerEvent) {
    const d = this.drag;
    if (!d) return;
    const colW = this.canvas().nativeElement.clientWidth / GRID_COLS;
    const dc = Math.round((e.clientX - d.x) / colW);
    const dr = Math.round((e.clientY - d.y) / ROW_PX);
    const o = d.orig;
    this.patch(d.mode === 'move'
      ? { key: d.key, col: o.col + dc, row: o.row + dr }
      : { key: d.key, w: o.w + dc, h: o.h + dr });
  }

  @HostListener('window:pointerup')
  onUp() {
    if (!this.drag) return;
    const key = this.drag.key;
    this.drag = null;
    this.items.update(list => resolveCollisions(list, key));
  }

  @HostListener('window:keydown', ['$event'])
  onKey(e: KeyboardEvent) {
    const s = this.sel();
    if (!s || (e.target as HTMLElement)?.closest?.('input, textarea, select')) return;
    const step: Record<string, [number, number]> = { ArrowLeft: [-1, 0], ArrowRight: [1, 0], ArrowUp: [0, -1], ArrowDown: [0, 1] };
    const mv = step[e.key];
    if (!mv) return;
    e.preventDefault();
    this.patch(e.shiftKey
      ? { key: s.key, w: s.w + mv[0], h: s.h + mv[1] }
      : { key: s.key, col: s.col + mv[0], row: s.row + mv[1] }, true);
  }

  // ── Save / reset ──
  save() {
    const items = renumberTabs(this.items()).map(i => ({ ...i, label: i.label?.trim() || undefined }));
    this.saving.set(true);
    this.api.save(this.formKey(), { version: 1, items }).subscribe({
      next: () => {
        this.saving.set(false);
        this.messageSvc?.add({ severity: 'success', summary: 'Layout saved', detail: 'Everyone now sees the new layout.' });
        this.saved.emit(items);
      },
      error: err => {
        this.saving.set(false);
        this.messageSvc?.add({ severity: 'error', summary: 'Error', detail: err?.error?.message ?? 'Failed to save the layout.' });
      }
    });
  }

  confirmReset() {
    const run = () => this.api.reset(this.formKey()).subscribe({
      next: () => {
        this.messageSvc?.add({ severity: 'success', summary: 'Layout reset', detail: 'Back to the default layout.' });
        this.saved.emit(defaultItems(this.fields()));
      },
      error: err => this.messageSvc?.add({ severity: 'error', summary: 'Error', detail: err?.error?.message ?? 'Failed to reset.' })
    });
    if (!this.confirmSvc) { run(); return; }
    this.confirmSvc.confirm({
      header: 'Reset layout',
      message: 'Discard the saved layout and go back to the default for everyone?',
      icon: 'pi pi-replay',
      accept: run
    });
  }
}
