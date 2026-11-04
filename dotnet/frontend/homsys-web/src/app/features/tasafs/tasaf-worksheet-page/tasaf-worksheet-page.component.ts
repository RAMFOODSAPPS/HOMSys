import { Component, computed, effect, inject, OnDestroy, OnInit, signal, untracked } from '@angular/core';
import { DatePipe, DecimalPipe, PercentPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { SelectModule } from 'primeng/select';
import { ConfirmationService, MessageService } from 'primeng/api';
import { GlobalToolbarService } from '../../../core/services/global-toolbar.service';
import { TabBarService } from '../../../core/services/tab-bar.service';
import { TasafService } from '../../../core/services/tasaf.service';
import { TasafWorksheetService } from '../../../core/services/tasaf-worksheet.service';
import { TASAF_STATUS_SEVERITY, TasafListItem } from '../../../core/models/tasaf.model';
import {
  blankListingFeeLine, blankNearExLine, computeListingFee, computeNearEx, LISTING_FEE_BASES,
  SaveTasafWorksheet, TasafNearExLine, TasafWorksheet, TasafWorksheetListItem, TasafWorksheetType, WORKSHEET_TYPES
} from '../../../core/models/tasaf-worksheet.model';

/** This page's tab key (ROUTE_META). */
const TAB = '/tasafs/worksheets';

/** Editor model: a saved worksheet, or a new one (id 0). */
type Draft = SaveTasafWorksheet & Partial<TasafWorksheet> & { id: number };

/**
 * TASAF Worksheets (/tasafs/worksheets): the "LF Breakdown" and "Near Ex" sheets, filled out by the proponent and
 * attached to a TASAF (optional, no approval of their own). history.state: { worksheetId } opens one;
 * { newType, attachTo } starts a new one that is attached to that TASAF on its first save.
 */
@Component({
  selector: 'app-tasaf-worksheet-page',
  standalone: true,
  imports: [FormsModule, DatePipe, DecimalPipe, PercentPipe, TableModule, TagModule, ButtonModule, InputTextModule,
            InputNumberModule, SelectModule],
  template: `
    @if (!ws()) {
      <div class="ws-bar">
        <p-button label="New Listing Fee" icon="pi pi-plus" size="small" (onClick)="startNew('ListingFee')" />
        <p-button label="New Near Ex Moveout" icon="pi pi-plus" size="small" severity="secondary" (onClick)="startNew('NearEx')" />
        <span class="muted">Fill out and attach to a TASAF — no separate approval; it's approved with the TASAF.</span>
      </div>
      <p-table [value]="filteredList()" [loading]="loading()" [paginator]="true" [rows]="15" styleClass="p-datatable-sm" dataKey="id">
        <ng-template pTemplate="header">
          <tr>
            <th pSortableColumn="typeLabel">Type <p-sortIcon field="typeLabel" /></th>
            <th pSortableColumn="title">Title <p-sortIcon field="title" /></th>
            <th class="num">Rows</th>
            <th pSortableColumn="total" class="num">Total <p-sortIcon field="total" /></th>
            <th pSortableColumn="tasafRefNo">Attached to <p-sortIcon field="tasafRefNo" /></th>
            <th pSortableColumn="proponentName">Proponent <p-sortIcon field="proponentName" /></th>
            <th pSortableColumn="createdAt">Created <p-sortIcon field="createdAt" /></th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-w>
          <tr class="clickable" (click)="open(w.id)">
            <td>{{ w.typeLabel }}</td>
            <td>{{ w.title }}</td>
            <td class="num">{{ w.lineCount }}</td>
            <td class="num">{{ w.total | number:'1.2-2' }}</td>
            <td>
              @if (w.tasafId) { {{ w.tasafRefNo || ('#' + w.tasafId) }} <p-tag [value]="w.tasafStatus" [severity]="severity(w.tasafStatus)" /> }
              @else { <span class="muted">Not attached</span> }
            </td>
            <td>{{ w.proponentName }}</td>
            <td>{{ w.createdAt | date:'MMM d, y' }}</td>
          </tr>
        </ng-template>
        <ng-template pTemplate="emptymessage"><tr><td colspan="7" class="muted">No worksheets yet.</td></tr></ng-template>
      </p-table>
    } @else {
      @let w = ws()!;
      <div class="ws-bar">
        <h2>{{ typeLabel(w.type) }} <small class="muted">— sheet "{{ sheetName(w.type) }}"</small></h2>
        <span class="grow"></span>
        @if (!editable()) {
          <span class="muted"><i class="pi pi-lock"></i>
            @if (w.locked) { Locked — its TASAF is {{ w.tasafStatus }}. }
            @else if (w.canEdit) { Read-only — click Edit in the toolbar to change it. }
            @else { Read-only. }
          </span>
        }
      </div>

      <div class="ws-head">
        <label>Title <input pInputText [(ngModel)]="w.title" [placeholder]="typeLabel(w.type)" maxlength="200" [disabled]="!editable()" /></label>
        <div class="ws-attach">
          <span class="lbl">Attached to TASAF</span>
          @if (w.tasafId) {
            <span><a href="javascript:void(0)" (click)="openTasaf(w.tasafId)"><strong>{{ w.tasafRefNo || ('#' + w.tasafId) }}</strong></a> {{ w.tasafTitle }}
              <p-tag [value]="w.tasafStatus ?? ''" [severity]="severity(w.tasafStatus)" /></span>
            @if (w.locked) { <span class="muted"><i class="pi pi-lock"></i> Locked while the TASAF is {{ w.tasafStatus }}.</span> }
            @else if (editable()) { <p-button label="Detach" icon="pi pi-times" size="small" severity="secondary" [text]="true" (onClick)="detach()" /> }
          } @else if (editable()) {
            <p-select [options]="attachable()" [(ngModel)]="attachTo" optionLabel="label" optionValue="id" placeholder="Pick one of your Draft / Returned TASAFs"
              [filter]="true" filterBy="label" [showClear]="true" appendTo="body" styleClass="ws-attach-select" />
            <p-button label="Attach" icon="pi pi-paperclip" size="small" [disabled]="!attachTo" (onClick)="attach()" />
          } @else { <span class="muted">Not attached</span> }
        </div>
      </div>

      <div class="ws-nav">
        <span class="muted small">Scroll the table:</span>
        <p-button icon="pi pi-angle-double-left" [text]="true" size="small" (onClick)="wsScroll.scrollTo({ left: 0, behavior: 'smooth' })" />
        <p-button icon="pi pi-angle-left" [text]="true" size="small" (onClick)="wsScroll.scrollBy({ left: -400, behavior: 'smooth' })" />
        <p-button icon="pi pi-angle-right" [text]="true" size="small" (onClick)="wsScroll.scrollBy({ left: 400, behavior: 'smooth' })" />
        <p-button icon="pi pi-angle-double-right" [text]="true" size="small" (onClick)="wsScroll.scrollTo({ left: wsScroll.scrollWidth, behavior: 'smooth' })" />
        <span class="muted small">(or Shift + mouse wheel)</span>
      </div>
      <div class="ws-scroll" #wsScroll>
        @if (w.type === 'ListingFee') {
          <table class="ws-grid">
            <thead><tr>
              <th class="c-md">Sales Group</th><th class="c-md">Channel</th><th class="c-lg">Account / Store</th><th class="c-md">Category</th><th class="c-md">Prod No.</th><th class="c-xl">SKU</th><th class="num c-num">LF / SKU</th>
              <th class="num c-sm"># of SKUs</th><th class="num c-sm"># of Doors</th><th class="c-xl">TOTAL is</th><th class="num">TOTAL</th>
              <th class="num c-num">Projected Annual Sales</th><th class="num">CTS</th><th class="c-lg">Remarks</th><th></th>
            </tr></thead>
            <tbody>
              @for (l of w.listingFeeLines; track $index; let i = $index) {
                <tr>
                  <td [title]="l.salesGroup"><p-select [options]="pick('sales-groups', l.salesGroup)" [(ngModel)]="l.salesGroup" [showClear]="true" [filter]="true" placeholder="Select" appendTo="body" styleClass="ws-sel" [disabled]="!editable()" /></td>
                  <td [title]="l.channel"><p-select [options]="pick('channels', l.channel)" [(ngModel)]="l.channel" [showClear]="true" [filter]="true" placeholder="Select" appendTo="body" styleClass="ws-sel" [disabled]="!editable()" /></td>
                  <td [title]="l.account"><input pInputText [(ngModel)]="l.account" [disabled]="!editable()" /></td>
                  <td [title]="l.category"><p-select [options]="pick('product-groups', l.category)" [(ngModel)]="l.category" [showClear]="true" [filter]="true" placeholder="Select" appendTo="body" styleClass="ws-sel" [disabled]="!editable()" /></td>
                  <td [title]="l.sku"><p-select [options]="prodOpts(l.category, l.prodNo, l.sku)" [ngModel]="l.prodNo || null" (ngModelChange)="onProd(l, $event)" optionLabel="label" optionValue="value" [showClear]="true" [filter]="true" filterBy="label" placeholder="Select" appendTo="body" styleClass="ws-sel" [panelStyle]="{ width: '26rem' }" [disabled]="!editable()"><ng-template let-o pTemplate="selectedItem">{{ o.value }}</ng-template></p-select></td>
                  <td [title]="l.sku"><textarea class="p-inputtext wrap" rows="1" [(ngModel)]="l.sku" maxlength="100" [disabled]="!editable()"></textarea></td>
                  <td><p-inputNumber [(ngModel)]="l.lfPerSku" (ngModelChange)="calcLf(l)" [minFractionDigits]="2" [maxFractionDigits]="2" [min]="0" inputStyleClass="numin" [disabled]="!editable()" /></td>
                  <td><p-inputNumber [(ngModel)]="l.skuCount" (ngModelChange)="calcLf(l)" [min]="0" inputStyleClass="numin sm" [disabled]="!editable()" /></td>
                  <td><p-inputNumber [(ngModel)]="l.doorCount" (ngModelChange)="calcLf(l)" [min]="0" inputStyleClass="numin sm" [disabled]="!editable()" /></td>
                  <td><p-select [options]="bases" [(ngModel)]="l.basis" (ngModelChange)="calcLf(l)" optionLabel="label" optionValue="value" appendTo="body" [disabled]="!editable()" /></td>
                  <td class="num calc">{{ l.total | number:'1.2-2' }}</td>
                  <td><p-inputNumber [(ngModel)]="l.projectedAnnualSales" (ngModelChange)="calcLf(l)" [minFractionDigits]="2" [maxFractionDigits]="2" [min]="0" inputStyleClass="numin" [disabled]="!editable()" /></td>
                  <td class="num calc">{{ l.cts == null ? '—' : (l.cts | percent:'1.0-0') }}</td>
                  <td [title]="l.remarks"><textarea class="p-inputtext wrap" rows="1" [(ngModel)]="l.remarks" maxlength="500" [disabled]="!editable()"></textarea></td>
                  <td>@if (editable()) { <p-button icon="pi pi-trash" [text]="true" severity="danger" size="small" (onClick)="w.listingFeeLines.splice(i, 1)" /> }</td>
                </tr>
              }
            </tbody>
            <tfoot><tr>
              <td colspan="10" class="num"><strong>Total</strong></td>
              <td class="num"><strong>{{ lfTotal() | number:'1.2-2' }}</strong></td>
              <td class="num"><strong>{{ lfSales() | number:'1.2-2' }}</strong></td><td colspan="3"></td>
            </tr></tfoot>
          </table>
        } @else {
          <table class="ws-grid">
            <thead><tr>
              <th class="c-md">Sales Group</th><th class="c-lg">Account / Store</th><th class="c-md">Category</th><th class="c-md">Prod No.</th><th class="c-xl">SKU</th><th class="num c-num">LSP per Case</th>
              <th class="num c-num">LSP per Pc</th><th class="num c-sm">Pcs/Cs</th><th class="c-md">Expiry (Month-Year)</th><th class="num c-sm">Invty in Cases</th>
              <th class="num c-sm">Invty in Pieces</th><th class="num">Total in Pcs</th><th class="num">Total Value</th><th class="c-lg">Moveout Scheme</th>
              <th class="num c-sm">Discount / Rebate</th><th class="num">Moveout Cost</th><th></th>
            </tr></thead>
            <tbody>
              @for (l of w.nearExLines; track $index; let i = $index) {
                <tr>
                  <td [title]="l.salesGroup"><p-select [options]="pick('sales-groups', l.salesGroup)" [(ngModel)]="l.salesGroup" [showClear]="true" [filter]="true" placeholder="Select" appendTo="body" styleClass="ws-sel" [disabled]="!editable()" /></td>
                  <td [title]="l.account"><input pInputText [(ngModel)]="l.account" [disabled]="!editable()" /></td>
                  <td [title]="l.category"><p-select [options]="pick('product-groups', l.category)" [(ngModel)]="l.category" [showClear]="true" [filter]="true" placeholder="Select" appendTo="body" styleClass="ws-sel" [disabled]="!editable()" /></td>
                  <td [title]="l.sku"><p-select [options]="prodOpts(l.category, l.prodNo, l.sku)" [ngModel]="l.prodNo || null" (ngModelChange)="onProd(l, $event)" optionLabel="label" optionValue="value" [showClear]="true" [filter]="true" filterBy="label" placeholder="Select" appendTo="body" styleClass="ws-sel" [panelStyle]="{ width: '26rem' }" [disabled]="!editable()"><ng-template let-o pTemplate="selectedItem">{{ o.value }}</ng-template></p-select></td>
                  <td [title]="l.sku"><textarea class="p-inputtext wrap" rows="1" [(ngModel)]="l.sku" maxlength="100" [disabled]="!editable()"></textarea></td>
                  <td><p-inputNumber [(ngModel)]="l.lspPerCase" (ngModelChange)="calcNe(l)" [minFractionDigits]="2" [maxFractionDigits]="2" [min]="0" inputStyleClass="numin" [disabled]="!editable()" /></td>
                  <td><p-inputNumber [(ngModel)]="l.lspPerPc" (ngModelChange)="calcNe(l)" [minFractionDigits]="2" [maxFractionDigits]="2" [min]="0" inputStyleClass="numin" [disabled]="!editable()" /></td>
                  <td><p-inputNumber [(ngModel)]="l.pcsPerCase" (ngModelChange)="calcNe(l)" [min]="0" inputStyleClass="numin sm" [disabled]="!editable()" /></td>
                  <td><input type="month" class="p-inputtext" [ngModel]="monthOf(l)" (ngModelChange)="setMonth(l, $event)" [disabled]="!editable()" /></td>
                  <td><p-inputNumber [(ngModel)]="l.invCases" (ngModelChange)="calcNe(l)" [min]="0" inputStyleClass="numin sm" [disabled]="!editable()" /></td>
                  <td><p-inputNumber [(ngModel)]="l.invPieces" (ngModelChange)="calcNe(l)" [min]="0" inputStyleClass="numin sm" [disabled]="!editable()" /></td>
                  <td class="num calc">{{ l.totalPcs | number }}</td>
                  <td class="num calc">{{ l.totalValue | number:'1.2-2' }}</td>
                  <td [title]="l.moveoutScheme"><input pInputText [(ngModel)]="l.moveoutScheme" [disabled]="!editable()" /></td>
                  <td><p-inputNumber [ngModel]="pctOf(l)" (ngModelChange)="setPct(l, $event)" suffix="%" [min]="0" [max]="100" [maxFractionDigits]="2" inputStyleClass="numin sm" [disabled]="!editable()" /></td>
                  <td class="num calc">{{ l.moveoutCost | number:'1.2-2' }}</td>
                  <td>@if (editable()) { <p-button icon="pi pi-trash" [text]="true" severity="danger" size="small" (onClick)="w.nearExLines.splice(i, 1)" /> }</td>
                </tr>
              }
            </tbody>
            <tfoot><tr>
              <td colspan="11" class="num"><strong>Total</strong></td>
              <td class="num"><strong>{{ nePcs() | number }}</strong></td>
              <td class="num"><strong>{{ neValue() | number:'1.2-2' }}</strong></td><td colspan="2"></td>
              <td class="num"><strong>{{ neCost() | number:'1.2-2' }}</strong></td><td></td>
            </tr></tfoot>
          </table>
        }
      </div>
      @if (editable()) { <p-button label="Add Row" icon="pi pi-plus" size="small" [text]="true" (onClick)="addRow()" /> }
      <p class="muted small">
        @if (w.type === 'ListingFee') { TOTAL = LF/SKU × # of Doors (package for all SKUs) or × # of SKUs (package for all doors); CTS = TOTAL ÷ Projected Annual Sales. }
        @else { Total in Pcs = Cases × Pcs/Cs + Pieces · Total Value = Cases × LSP per Case + Pieces × LSP per Pc · Moveout Cost = Total Value × Discount / Rebate. }
      </p>
    }
  `,
  styles: [`
    .ws-bar { display: flex; align-items: center; gap: .5rem; margin-bottom: .5rem; flex-wrap: wrap; }
    .ws-bar h2 { margin: 0; font-size: 1rem; }
    .grow { flex: 1; }
    .muted { color: var(--p-text-muted-color); }
    .small { font-size: .78rem; }
    .num { text-align: right; }
    .clickable { cursor: pointer; }
    .ws-head { display: flex; gap: 1.25rem; flex-wrap: wrap; align-items: center; margin-bottom: .5rem; font-size: .8rem; }
    .ws-head label { display: flex; align-items: center; gap: .5rem; }
    .ws-head label input { width: 16rem; padding: .3rem .5rem; font-size: .8rem; }
    .ws-attach { display: flex; align-items: center; gap: .5rem; flex-wrap: wrap; }
    .ws-attach .lbl { font-size: .8rem; color: var(--p-text-muted-color); }
    :host ::ng-deep .ws-attach-select { min-width: 22rem; }
    .ws-nav { display: flex; align-items: center; gap: .15rem; }
    .ws-scroll { overflow-x: scroll; max-width: 100%; padding-bottom: 2px; }
    .ws-scroll::-webkit-scrollbar { height: 12px; }
    .ws-scroll::-webkit-scrollbar-thumb { background: #c9a3a3; border-radius: 6px; }
    .ws-scroll::-webkit-scrollbar-track { background: #f3e6e6; }
    /* SKU / Remarks grow to show the whole text (no cut-off). */
    .ws-grid textarea.wrap { width: 100%; box-sizing: border-box; resize: none; field-sizing: content; min-height: 1.6rem; white-space: pre-wrap; font: inherit; line-height: 1.25; }
    .ws-grid { border-collapse: collapse; font-size: .76rem; width: max-content; min-width: 100%; }
    .ws-grid th { background: #f3e6e6; font-weight: 600; padding: .3rem .35rem; border: 1px solid #e5e5e5; white-space: nowrap; }
    .ws-grid td { padding: 1px 2px; border: 1px solid #eee; }
    .ws-grid td.calc { background: #faf3f3; padding: 1px .4rem; white-space: nowrap; }
    .ws-grid tfoot td { background: #faf3f3; padding: .25rem .4rem; }
    /* Compact cells: every input / dropdown at ~1.6rem high */
    :host ::ng-deep .ws-grid .p-inputtext { padding: .2rem .35rem; font-size: .76rem; border-radius: 4px; }
    :host ::ng-deep .ws-grid .p-select { font-size: .76rem; }
    :host ::ng-deep .ws-grid .p-select-label { padding: .2rem .35rem; font-size: .76rem; }
    :host ::ng-deep .ws-grid .p-select-dropdown, :host ::ng-deep .ws-grid .p-select-clear-icon { width: 1.5rem; }
    :host ::ng-deep .ws-grid .p-button { width: 1.6rem; height: 1.6rem; padding: 0; }
    /* Every control fills its cell; the column widths come from the header classes. */
    .ws-grid input.p-inputtext, .ws-grid input[type=month] { width: 100%; box-sizing: border-box; }
    :host ::ng-deep .ws-grid .p-select, :host ::ng-deep .ws-grid .p-inputnumber { width: 100%; }
    :host ::ng-deep .ws-grid .numin { width: 100%; text-align: right; }
    .ws-grid th.c-sm  { min-width: 4.5rem; }
    .ws-grid th.c-num { min-width: 6.5rem; }
    .ws-grid th.c-md  { min-width: 10rem; }
    .ws-grid th.c-lg  { min-width: 13rem; }
    .ws-grid th.c-xl  { min-width: 17rem; }
    /* The clear × only on hover, so it never covers the value. */
    :host ::ng-deep .ws-grid .p-select-clear-icon { display: none; }
    :host ::ng-deep .ws-grid .p-select:hover .p-select-clear-icon { display: block; }
  `]
})
export class TasafWorksheetPageComponent implements OnInit, OnDestroy {
  private api      = inject(TasafWorksheetService);
  private tasafApi = inject(TasafService);
  private toolbar  = inject(GlobalToolbarService);
  private msg      = inject(MessageService);
  private confirm  = inject(ConfirmationService);
  private router   = inject(Router);
  private tabBar   = inject(TabBarService);

  protected readonly bases = LISTING_FEE_BASES;
  protected list     = signal<TasafWorksheetListItem[]>([]);
  protected loading  = signal(true);
  protected saving   = signal(false);
  protected ws       = signal<Draft | null>(null);
  /** My Draft / Returned TASAFs (attach targets). */
  private myTasafs   = signal<TasafListItem[]>([]);
  protected attachTo: number | null = null;
  /** TASAF to attach a new worksheet to after its first save (from the TASAF form). */
  private pendingAttach: number | null = null;

  /** A saved worksheet opens read-only; Edit (toolbar) unlocks it until the next Save or Cancel. New = always editable. */
  protected editing  = signal(false);
  protected editable = computed(() => { const w = this.ws(); return !!w && (w.id === 0 || (!!w.canEdit && this.editing())); });
  private readonlyNow = computed(() => !this.editable());
  private term = signal('');
  protected filteredList = computed(() => {
    const t = this.term().trim().toLowerCase();
    return t ? this.list().filter(w => `${w.typeLabel} ${w.title} ${w.tasafRefNo ?? ''} ${w.proponentName}`.toLowerCase().includes(t))
             : this.list();
  });

  constructor() {
    // Toolbar follows the view (list / worksheet) and the edit state.
    effect(() => { this.ws(); this.editing(); untracked(() => this.syncToolbar()); });
  }

  private syncToolbar() {
    const w = this.ws();
    if (!w) {
      this.toolbar.set({
        title:   'TASAF Worksheets',
        add:     { label: 'New Listing Fee', onClick: () => this.startNew('ListingFee') },
        refresh: { onClick: () => this.loadList(), loading: this.loading },
        search:  term => this.term.set(term),
        initialSearch: this.term(),
      });
      return;
    }
    this.toolbar.set({
      title:  `${this.typeLabel(w.type)} — ${w.id ? w.title : 'New'}`,
      save:   { onClick: () => this.save(), loading: this.saving, disabled: this.readonlyNow },
      ...(w.id && w.canEdit && !this.editing() ? { edit: { onClick: () => this.editing.set(true) } } : {}),
      cancel: { onClick: () => this.cancel() },
      add:    { onClick: () => this.startNew(w.type) },
      ...(w.id && w.canEdit ? { delete: { onClick: () => this.remove() } } : {}),
      ...(w.id ? { refresh: { onClick: () => this.open(w.id) } } : {}),
      list:   { onClick: () => this.backToList() },
    });
  }

  /** Dropdown lists by key ('sales-groups' / 'channels' / 'product-groups'), as names. */
  private lists = signal<Record<string, string[]>>({});
  private pickCache = new Map<string, string[]>();

  /** A dropdown's options; a saved value no longer in the list stays offered. Cached: p-select needs stable arrays. */
  protected pick(key: string, current: string | null | undefined): string[] {
    const base = this.lists()[key] ?? [];
    const cur = (current ?? '').trim();
    const ck = `${key}|${cur}|${base.length}`;
    let hit = this.pickCache.get(ck);
    if (!hit) {
      hit = cur && !base.includes(cur) ? [cur, ...base] : base;
      this.pickCache.set(ck, hit);
    }
    return hit;
  }

  // ── Prod No. picker: active SKUs of the row's Category (all active SKUs when none) — tasafs/product-options ──
  private catIdByName = new Map<string, number>();
  /** Products per category key ('all' or the category id), loaded once. */
  private products = signal<Record<string, { value: string; label: string; desc: string }[]>>({});
  private productReqs = new Set<string>();
  private prodCache = new Map<string, { value: string; label: string; desc: string }[]>();

  protected prodOpts(category: string, prodNo: string, sku: string) {
    const id = this.catIdByName.get(category);
    const key = id ? String(id) : 'all';
    const loaded = this.products()[key];
    if (!loaded && !this.productReqs.has(key)) {
      this.productReqs.add(key);
      this.tasafApi.productOptions({ productCategoryIds: id ? [id] : [], subCategoryCodes: [] }).subscribe({
        next: res => this.products.update(p => ({ ...p, [key]: (res.data ?? []).map(o => ({
          value: o.cProdNo, label: `${o.cProdNo} — ${o.prodDesc}${o.packSize ? ' ' + o.packSize : ''}`, desc: o.prodDesc })) })),
        error: () => this.productReqs.delete(key)
      });
    }
    const base = loaded ?? [];
    const ck = `${key}|${prodNo}|${base.length}`;
    let hit = this.prodCache.get(ck);
    if (!hit) {
      // A saved Prod No. outside this category's list stays offered.
      hit = prodNo && !base.some(o => o.value === prodNo) ? [{ value: prodNo, label: `${prodNo} — ${sku}`, desc: sku }, ...base] : base;
      this.prodCache.set(ck, hit);
    }
    return hit;
  }

  /** Picking a Prod No. fills SKU with its description (still editable). */
  protected onProd(l: { category: string; prodNo: string; sku: string }, prodNo: string | null) {
    l.prodNo = prodNo ?? '';
    if (!prodNo) return;
    const opt = this.prodOpts(l.category, l.prodNo, l.sku).find(o => o.value === prodNo);
    if (opt) l.sku = opt.desc;
  }

  /** Attach / detach changed only the link: take it from the server, keep the (possibly unsaved) rows on screen. */
  private keepLink(saved: TasafWorksheet) {
    this.ws.update(w => w && ({ ...w, tasafId: saved.tasafId, tasafRefNo: saved.tasafRefNo, tasafTitle: saved.tasafTitle,
                                tasafStatus: saved.tasafStatus, locked: saved.locked, canEdit: saved.canEdit }));
  }

  /** Cancel: a new worksheet goes back to the list; a saved one drops the edits and locks again. */
  private cancel() {
    const w = this.ws();
    if (!w) return;
    if (!w.id) { this.backToList(); return; }
    this.open(w.id);
  }
  protected attachable = computed(() => this.myTasafs()
    .filter(t => t.isMine && (t.status === 'Draft' || t.status === 'Returned'))
    .map(t => ({ id: t.id, label: `${t.refNo || '#' + t.id} — ${t.title}` })));

  // Totals are recomputed on change detection; the rows are mutated in place by the inputs.
  protected lfTotal = () => (this.ws()?.listingFeeLines ?? []).reduce((s, l) => s + (l.total ?? 0), 0);
  protected lfSales = () => (this.ws()?.listingFeeLines ?? []).reduce((s, l) => s + (l.projectedAnnualSales ?? 0), 0);
  protected nePcs   = () => (this.ws()?.nearExLines ?? []).reduce((s, l) => s + (l.totalPcs ?? 0), 0);
  protected neValue = () => (this.ws()?.nearExLines ?? []).reduce((s, l) => s + (l.totalValue ?? 0), 0);
  protected neCost  = () => (this.ws()?.nearExLines ?? []).reduce((s, l) => s + (l.moveoutCost ?? 0), 0);

  ngOnInit() {

    this.loadList();
    this.tasafApi.getAll().subscribe(res => this.myTasafs.set(res.data ?? []));
    // Sales Group / Channel / Category dropdowns = the TASAF form's own lists (stored as their names).
    this.tasafApi.getOptions().subscribe(res => {
      const l = res.data?.lists ?? {};
      this.lists.set({
        'sales-groups':   (l['sales-groups'] ?? []).map(o => o.code || o.name),
        channels:         (l['channels'] ?? []).map(o => o.name),
        'product-groups': (l['product-groups'] ?? []).map(o => o.name),
      });
      this.catIdByName = new Map((l['product-groups'] ?? []).map(o => [o.name, o.id]));
      this.pickCache.clear();
    });
    const st = history.state ?? {};
    if (st.wsDraft) {
      // Back from another tab: the worksheet that was open, with its unsaved edits.
      this.ws.set(st.wsDraft as Draft);
      this.editing.set(!!st.wsEditing);
      this.pendingAttach = st.wsPendingAttach ?? null;
      this.attachTo = st.wsAttachTo ?? null;
      this.tabBar.updateTabState(TAB, undefined);
    }
    else if (st.worksheetId) this.open(st.worksheetId);
    else if (st.newType) { this.pendingAttach = st.attachTo ?? null; this.startNew(st.newType); }
  }

  ngOnDestroy() {
    // Switching tabs destroys the page: keep the open worksheet on the tab so coming back restores it.
    const w = this.ws();
    this.tabBar.updateTabState(TAB, w
      ? { wsDraft: structuredClone(w), wsPendingAttach: this.pendingAttach, wsAttachTo: this.attachTo, wsEditing: this.editing() }
      : undefined);
    this.toolbar.clear();
  }

  protected typeLabel(t: TasafWorksheetType) { return WORKSHEET_TYPES.find(x => x.value === t)?.label ?? t; }
  protected sheetName(t: TasafWorksheetType) { return WORKSHEET_TYPES.find(x => x.value === t)?.sheet ?? ''; }
  protected severity(status: string | null | undefined) { return TASAF_STATUS_SEVERITY[status ?? ''] ?? 'secondary'; }

  private loadList() {
    this.loading.set(true);
    this.api.getAll().subscribe({
      next: res => { this.list.set(res.data ?? []); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  protected startNew(type: TasafWorksheetType) {
    const first = type === 'ListingFee' ? { listingFeeLines: [blankListingFeeLine()], nearExLines: [] }
                                        : { listingFeeLines: [], nearExLines: [blankNearExLine()] };
    this.editing.set(false);
    this.ws.set({ id: 0, type, title: '', ...first });
    this.attachTo = this.pendingAttach;
  }

  protected open(id: number) {
    this.api.getById(id).subscribe({
      next: res => { if (res.data) { this.editing.set(false); this.ws.set(res.data); this.attachTo = null; } },
      error: err => this.msg.add({ severity: 'error', summary: 'Worksheet', detail: err?.error?.message ?? 'Could not open the worksheet.' })
    });
  }

  protected backToList() { this.ws.set(null); this.pendingAttach = null; this.loadList(); }

  protected addRow() {
    const w = this.ws()!;
    if (w.type === 'ListingFee') w.listingFeeLines.push(blankListingFeeLine());
    else w.nearExLines.push(blankNearExLine());
  }

  protected calcLf(l: Parameters<typeof computeListingFee>[0]) { computeListingFee(l); }
  protected calcNe(l: TasafNearExLine) { computeNearEx(l); }

  /** Expiry is month-year: 'yyyy-MM' in the input, the 1st of the month in the model. */
  protected monthOf(l: TasafNearExLine) { return l.expiry ? l.expiry.slice(0, 7) : ''; }
  protected setMonth(l: TasafNearExLine, v: string) { l.expiry = v ? `${v}-01` : null; }
  protected pctOf(l: TasafNearExLine) { return l.discount == null ? null : Math.round(l.discount * 10000) / 100; }
  protected setPct(l: TasafNearExLine, v: number | null) { l.discount = v == null ? null : v / 100; computeNearEx(l); }

  protected save() {
    const w = this.ws()!;
    const dto: SaveTasafWorksheet = { type: w.type, title: w.title, listingFeeLines: w.listingFeeLines, nearExLines: w.nearExLines };
    this.saving.set(true);
    (w.id ? this.api.update(w.id, dto) : this.api.create(dto)).subscribe({
      next: res => {
        this.saving.set(false);
        if (!res.data) return;
        this.editing.set(false);
        this.ws.set(res.data);
        this.msg.add({ severity: 'success', summary: 'Saved', detail: `${res.data.title} saved.` });
        // Started from a TASAF: attach on the first save.
        if (!w.id && this.pendingAttach) { this.attachTo = this.pendingAttach; this.pendingAttach = null; this.attach(); }
      },
      error: err => { this.saving.set(false); this.msg.add({ severity: 'error', summary: 'Save failed', detail: err?.error?.message ?? 'Could not save.' }); }
    });
  }

  protected attach() {
    const w = this.ws()!;
    if (!this.attachTo) return;
    if (!w.id) { this.msg.add({ severity: 'info', summary: 'Attach', detail: 'Save the worksheet first.' }); return; }
    this.api.attach(w.id, this.attachTo).subscribe({
      next: res => { if (res.data) { this.keepLink(res.data); this.attachTo = null;
        this.msg.add({ severity: 'success', summary: 'Attached', detail: `Attached to ${res.data.tasafRefNo || 'the TASAF'}.` }); } },
      error: err => this.msg.add({ severity: 'error', summary: 'Attach failed', detail: err?.error?.message ?? 'Could not attach.' })
    });
  }

  protected detach() {
    const w = this.ws()!;
    this.api.detach(w.id).subscribe({
      next: res => { if (res.data) { this.keepLink(res.data); this.msg.add({ severity: 'info', summary: 'Detached', detail: 'The worksheet is no longer attached.' }); } },
      error: err => this.msg.add({ severity: 'error', summary: 'Detach failed', detail: err?.error?.message ?? 'Could not detach.' })
    });
  }

  protected remove() {
    const w = this.ws()!;
    // An attached worksheet is part of its TASAF: detach it first (the API refuses too).
    if (w.tasafId) {
      this.msg.add({ severity: 'warn', summary: 'Detach first',
        detail: `This worksheet is attached to TASAF ${w.tasafRefNo || '#' + w.tasafId}. Detach it first, then delete it.` });
      return;
    }
    this.confirm.confirm({
      header: 'Delete worksheet?', message: `"${w.title}" will be deleted.`,
      acceptLabel: 'Delete', rejectLabel: 'Cancel',
      accept: () => this.api.delete(w.id).subscribe({
        next: () => { this.msg.add({ severity: 'success', summary: 'Deleted', detail: `${w.title} deleted.` }); this.backToList(); },
        error: err => this.msg.add({ severity: 'error', summary: 'Delete failed', detail: err?.error?.message ?? 'Could not delete.' })
      })
    });
  }

  /** From the TASAF form: open the TASAF a worksheet is attached to. */
  protected openTasaf(id: number) { this.router.navigate(['/tasafs'], { state: { tasafId: id } }); }
}
