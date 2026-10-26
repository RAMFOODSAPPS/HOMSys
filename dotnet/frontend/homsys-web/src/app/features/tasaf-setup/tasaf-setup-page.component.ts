import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { SelectModule } from 'primeng/select';
import { MultiSelectModule } from 'primeng/multiselect';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { TextareaModule } from 'primeng/textarea';
import { MessageModule } from 'primeng/message';
import { ToastModule } from 'primeng/toast';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService, MessageService } from 'primeng/api';
import { GlobalToolbarService } from '../../core/services/global-toolbar.service';
import { ExportColumn, ExportService } from '../../core/services/export.service';
import { TasafMasterService } from '../../core/services/tasaf-master.service';
import {
  TasafApprovalMatrix, TasafApprovalRules, TasafLookupDto, TasafMasterMeta, TasafNumbering, TasafUserOption
} from '../../core/models/tasaf-master.model';
import { utc } from '../../core/utils/utc-date';
import { ActivatedRoute } from '@angular/router';

const LAST_MASTER_KEY = 'homsys_tasaf_setup_master';
/**
 * Lists edited on their own Master Data page instead of TASAF Setup (route data { masters: [...],
 * title }) — the same editor, showing only those lists.
 */
const MASTER_DATA_KEYS = ['product-groups'];
const MATRIX_KEY = 'approval-matrix';
const RULES_KEY = 'approval-rules';
const NUMBERING_KEY = 'numbering';

interface RuleCell { mode: 'none' | 'always' | 'over'; amount: number | null; }

/** TASAF Setup — every TASAF master list (sales groups, chains, ... approval matrix) on one page. */
@Component({
  selector: 'app-tasaf-setup-page',
  standalone: true,
  providers: [MessageService, ConfirmationService],
  imports: [
    ReactiveFormsModule, FormsModule, DatePipe, DecimalPipe, TableModule, ButtonModule, TagModule, DialogModule,
    InputTextModule, InputNumberModule, SelectModule, MultiSelectModule, ToggleSwitchModule, TextareaModule,
    MessageModule, ToastModule, ConfirmDialogModule
  ],
  template: `
    <p-toast position="top-right" />
    <p-confirmDialog />

    <div class="setup">
      <nav class="masters">
        @for (m of metas(); track m.key) {
          <button type="button" class="master-btn" [class.active]="m.key === current()?.key" (click)="selectMaster(m)">
            {{ m.label }}
          </button>
        }
        @if (metas().length && !only) {
          <button type="button" class="master-btn matrix-btn" [class.active]="rulesMode()" (click)="selectRules()">
            <i class="pi pi-sliders-h"></i> Approval Rules
          </button>
          <button type="button" class="master-btn" [class.active]="matrixMode()" (click)="selectMatrix()">
            <i class="pi pi-sitemap"></i> Approval Matrix
          </button>
          <button type="button" class="master-btn" [class.active]="numberingMode()" (click)="selectNumbering()">
            <i class="pi pi-hashtag"></i> Numbering
          </button>
        }
      </nav>

      <div class="rows">
        @if (numberingMode()) {
          <p-message severity="info" styleClass="w-full mb-2"
            text="Document No. = YEAR - Sales Group - Proponent Initials - Proposal No. (e.g. 2026-RKA-ER-03). The proposal number is ONE continuous series across all TASAFs and never resets. It is assigned when a TASAF is first submitted. Set the next number here to continue an existing series (e.g. from the Excel files)." />
          <div class="num-card">
            <div class="num-row">
              <span>Last number used</span>
              <strong>
                @if (numbering()?.lastNumber != null) { {{ numbering()!.lastNumber }} <span class="muted">({{ numbering()!.lastRefNo }})</span> }
                @else { <span class="muted">None yet</span> }
              </strong>
            </div>
            <div class="num-row">
              <label for="numNext">Next number</label>
              <div>
                <p-inputNumber inputId="numNext" [ngModel]="numNext()" (ngModelChange)="numNext.set($event)" [ngModelOptions]="{ standalone: true }"
                  [min]="1" [max]="9999999" [useGrouping]="false" [showButtons]="true" inputStyleClass="num-in" />
                <small class="muted">Must be higher than the last number used.</small>
              </div>
            </div>
            <div class="num-row">
              <label for="numDigits">Minimum digits</label>
              <div>
                <p-inputNumber inputId="numDigits" [ngModel]="numDigits()" (ngModelChange)="numDigits.set($event)" [ngModelOptions]="{ standalone: true }"
                  [min]="1" [max]="6" [useGrouping]="false" [showButtons]="true" inputStyleClass="num-in" />
                <small class="muted">Zero-padding only — e.g. 2 → 01, 02 … 99, 100, 101. The series is never capped.</small>
              </div>
            </div>
            <div class="num-row">
              <span>Next Document No.</span>
              <strong class="num-preview">{{ numPreview() }}</strong>
            </div>
            @if (numbering()?.updatedBy) {
              <small class="muted">Last changed by {{ numbering()!.updatedBy }} on {{ utc(numbering()!.updatedAt) | date:'dd MMM yyyy h:mm a' }}</small>
            }
          </div>
          <div class="form-actions mt-2">
            @if (numberingDirty()) { <span class="unset">Unsaved changes</span> }
            <p-button label="Save Numbering" icon="pi pi-save" [loading]="saving()" [disabled]="!numberingDirty()" (onClick)="saveNumbering()" />
          </div>
        }
        @if (rulesMode()) {
          <p-message severity="info" styleClass="w-full mb-2"
            text="Which roles sign each approval type, in this order (left to right). 'Over amount' = the role signs only when the TASAF's total cost is MORE THAN the amount. Each Program Type is linked to one Approval Type." />
          <div class="matrix-wrap">
            <table class="matrix">
              <thead>
                <tr>
                  <th>Approval Type</th>
                  @for (r of rules()?.roles ?? []; track r.id) {
                    <th>{{ r.code || r.name }}<div class="role-sub">{{ r.name }}</div></th>
                  }
                </tr>
              </thead>
              <tbody>
                @for (t of rules()?.types ?? []; track t.id) {
                  <tr>
                    <td class="sg">{{ t.name }}<div class="role-sub">{{ programTypesOf(t.id) }}</div></td>
                    @for (r of rules()?.roles ?? []; track r.id) {
                      <td>
                        <p-select [options]="ruleModes" optionLabel="label" optionValue="value" styleClass="w-full" appendTo="body"
                          [ngModel]="rule(t.id, r.id).mode" (ngModelChange)="setRule(t.id, r.id, $event, rule(t.id, r.id).amount)"
                          [ngModelOptions]="{ standalone: true }" />
                        @if (rule(t.id, r.id).mode === 'over') {
                          <p-inputNumber [ngModel]="rule(t.id, r.id).amount" (ngModelChange)="setRule(t.id, r.id, 'over', $event)"
                            [ngModelOptions]="{ standalone: true }" [min]="0" [maxFractionDigits]="2" prefix="> "
                            styleClass="w-full mt-1" inputStyleClass="w-full" />
                        }
                      </td>
                    }
                  </tr>
                }
              </tbody>
            </table>
          </div>
          <div class="form-actions mt-2">
            @if (rulesDirty()) { <span class="unset">Unsaved changes</span> }
            <p-button label="Save Approval Rules" icon="pi pi-save" [loading]="saving()" (onClick)="saveRules()" />
          </div>
        }
        @if (matrixMode()) {
          <p-message severity="info" styleClass="w-full mb-2"
            text="Who signs each TASAF, per sales group. Which roles a TASAF needs comes from Approval Rules (by its program type's approval type and amount). Leave a cell empty when the position is vacant — that step is skipped." />
          <div class="matrix-wrap">
            <table class="matrix">
              <thead>
                <tr>
                  <th>Sales Group</th>
                  @for (r of matrix()?.roles ?? []; track r.id) {
                    <th>{{ r.code || r.name }}<div class="role-sub">{{ r.name }}</div></th>
                  }
                </tr>
              </thead>
              <tbody>
                @for (g of matrix()?.salesGroups ?? []; track g.id) {
                  <tr>
                    <td class="sg">{{ g.name }}</td>
                    @for (r of matrix()?.roles ?? []; track r.id) {
                      <td>
                        <p-select [options]="users()" optionLabel="fullName" optionValue="id" [filter]="true"
                          filterBy="fullName,username" [showClear]="true" placeholder="Vacant" appendTo="body"
                          styleClass="w-full" [ngModel]="cell(g.id, r.id)" (ngModelChange)="setCell(g.id, r.id, $event)"
                          [ngModelOptions]="{ standalone: true }" />
                      </td>
                    }
                  </tr>
                }
              </tbody>
            </table>
          </div>
          <div class="form-actions mt-2">
            @if (matrixDirty()) { <span class="unset">Unsaved changes</span> }
            <p-button label="Save Approval Matrix" icon="pi pi-save" [loading]="saving()" (onClick)="saveMatrix()" />
          </div>
        }
        @if (current(); as m) {
          @if (m.key === 'approval-roles') {
            <p-message severity="info" styleClass="w-full mb-2"
              text="Signing order = Order column. When each role signs is set in Approval Rules; who holds it per sales group in Approval Matrix." />
          }
          @if (m.key === 'approval-types') {
            <p-message severity="info" styleClass="w-full mb-2"
              text="One approval matrix per type. Link each Program Type to its Approval Type, then set the roles in Approval Rules." />
          }
          <div class="inactive-toggle">
            <p-toggleswitch inputId="showInactive" [ngModel]="showInactive()" (ngModelChange)="showInactive.set($event)" />
            <label for="showInactive">Show inactive</label>
          </div>
          <p-table [value]="filteredRows()" [loading]="loading()" [paginator]="true" [rows]="15"
            [rowsPerPageOptions]="[15, 30, 50]" [(selection)]="selected" selectionMode="single" dataKey="id"
            (onRowSelect)="noSelection.set(false)" (onRowUnselect)="noSelection.set(true)" styleClass="p-datatable-sm">
            <ng-template pTemplate="header">
              <tr>
                <th pSortableColumn="sortOrder" style="width:4.5rem">Order <p-sortIcon field="sortOrder" /></th>
                <th pSortableColumn="code" style="width:7rem">Code <p-sortIcon field="code" /></th>
                <th pSortableColumn="name">Name <p-sortIcon field="name" /></th>
                @if (m.parentLabel) { <th pSortableColumn="parentName">{{ m.parentLabel }} <p-sortIcon field="parentName" /></th> }
                @if (m.usersLabel) { <th pSortableColumn="userNames">{{ shortLabel(m.usersLabel) }} <p-sortIcon field="userNames" /></th> }
                @if (m.userLabel) { <th pSortableColumn="userName">{{ m.userLabel }} <p-sortIcon field="userName" /></th> }
                @if (m.amountLabel) { <th pSortableColumn="amount" class="num">{{ shortLabel(m.amountLabel) }} <p-sortIcon field="amount" /></th> }
                @if (m.textLabel) { <th>{{ shortLabel(m.textLabel) }}</th> }
                <th>Description</th>
                <th pSortableColumn="isActive" style="width:6rem">Status <p-sortIcon field="isActive" /></th>
              </tr>
            </ng-template>
            <ng-template pTemplate="body" let-r>
              <tr [pSelectableRow]="r" (dblclick)="openEdit(r)">
                <td>{{ r.sortOrder }}</td>
                <td>{{ r.code || '—' }}</td>
                <td>{{ r.name }}</td>
                @if (m.parentLabel) { <td>{{ r.parentName || '—' }}</td> }
                @if (m.usersLabel) {
                  <td>
                    @if (r.userNames) { {{ r.userNames }} } @else { <span class="unset">None</span> }
                  </td>
                }
                @if (m.userLabel) {
                  <td>
                    @if (r.userName) { {{ r.userName }} } @else { <span class="unset">Not assigned</span> }
                  </td>
                }
                @if (m.amountLabel) {
                  <td class="num">
                    @if (r.amount == null) { — }
                    @else if (m.key === 'program-types') { {{ r.amount | number:'1.0-2' }}% }
                    @else { {{ r.amount | number:'1.2-2' }} }
                  </td>
                }
                @if (m.textLabel) { <td class="kw">{{ r.text || '— all customers —' }}</td> }
                <td>{{ r.description || '—' }}</td>
                <td><p-tag [value]="r.isActive ? 'Active' : 'Inactive'" [severity]="r.isActive ? 'success' : 'danger'" /></td>
              </tr>
            </ng-template>
            <ng-template pTemplate="emptymessage">
              <tr><td colspan="8" class="text-center">No entries.</td></tr>
            </ng-template>
          </p-table>
        }
      </div>
    </div>

    <p-dialog [(visible)]="dialogVisible" [modal]="true" [style]="{ width: '480px' }"
      [header]="(editing() ? 'Edit ' : 'New ') + (current()?.label ?? '')">
      <form [formGroup]="form" (ngSubmit)="save()" autocomplete="off">
        @if (apiError()) {
          <p-message severity="error" styleClass="w-full mb-3" [text]="apiError()!" />
        }
        <div class="field">
          <label>Name <span class="required-star">*</span></label>
          <input pInputText formControlName="name" class="w-full" />
        </div>
        <div class="field-row">
          <div class="field">
            <label>Code <span class="optional-label">(optional)</span></label>
            <input pInputText formControlName="code" class="w-full" maxlength="20" />
          </div>
          <div class="field">
            <label>Order</label>
            <p-inputNumber formControlName="sortOrder" [useGrouping]="false" styleClass="w-full" inputStyleClass="w-full" />
          </div>
        </div>
        @if (current()?.parentLabel; as pl) {
          <div class="field">
            <label>{{ pl }}</label>
            <p-select formControlName="parentId" [options]="parentOptions()" optionLabel="name" optionValue="id"
              [showClear]="true" [filter]="true" filterBy="name" placeholder="— none —" styleClass="w-full" appendTo="body" />
          </div>
        }
        @if (current()?.usersLabel; as usl) {
          <div class="field">
            <label>{{ usl }}</label>
            <p-multiSelect formControlName="userIds" [options]="users()" optionLabel="fullName" optionValue="id"
              [filter]="true" filterBy="fullName,username" display="chip" placeholder="— none —"
              styleClass="w-full" appendTo="body">
              <ng-template let-u pTemplate="item">{{ u.fullName }} <span class="optional-label">({{ u.username }})</span></ng-template>
            </p-multiSelect>
          </div>
        }
        @if (current()?.userLabel; as ul) {
          <div class="field">
            <label>{{ ul }}</label>
            <p-select formControlName="userId" [options]="users()" optionLabel="fullName" optionValue="id"
              [showClear]="true" [filter]="true" filterBy="fullName,username" placeholder="— not assigned —"
              styleClass="w-full" appendTo="body">
              <ng-template let-u pTemplate="item">{{ u.fullName }} <span class="optional-label">({{ u.username }})</span></ng-template>
            </p-select>
          </div>
        }
        @if (current()?.amountLabel; as al) {
          <div class="field">
            <label>{{ al }}</label>
            <p-inputNumber formControlName="amount" mode="decimal" [minFractionDigits]="2" [maxFractionDigits]="2"
              [min]="0" styleClass="w-full" inputStyleClass="w-full" />
          </div>
        }
        @if (current()?.textLabel; as tl) {
          <div class="field">
            <label>{{ tl }}</label>
            <input pInputText formControlName="text" class="w-full" maxlength="500"
              placeholder="e.g. PUREGOLD  or  SUPERVALUE, SAVEMORE" />
            <small class="optional-label">Stores whose customer name contains any of these appear in the TASAF Cust Key dropdown for this chain. Use % as a wildcard.</small>
          </div>
        }
        <div class="field">
          <label>Description <span class="optional-label">(optional)</span></label>
          <textarea pTextarea formControlName="description" rows="2" class="w-full" maxlength="500"></textarea>
        </div>
        <div class="field-inline">
          <label>Active</label>
          <p-toggleswitch formControlName="isActive" />
        </div>
        <div class="form-actions">
          <p-button label="Cancel" [text]="true" severity="secondary" (onClick)="dialogVisible = false" />
          <p-button type="submit" [label]="editing() ? 'Save Changes' : 'Create'" [loading]="saving()" />
        </div>
      </form>
    </p-dialog>
  `,
  styles: [`
    .setup { display: grid; grid-template-columns: 210px 1fr; gap: 1rem; align-items: start; }
    .masters { display: flex; flex-direction: column; gap: 2px; background: var(--p-surface-card); border-radius: 8px; padding: 0.5rem; }
    .master-btn { text-align: left; border: 0; background: none; padding: 0.5rem 0.65rem; border-radius: 6px;
      font-size: 0.85rem; color: var(--p-text-color); cursor: pointer; }
    .master-btn:hover { background: var(--p-surface-hover); }
    .inactive-toggle { display: flex; align-items: center; justify-content: flex-end; gap: 0.5rem; margin-bottom: 0.5rem; font-size: 0.875rem; }
    .master-btn.active { background: rgba(128, 0, 0, 0.08); color: #800000; font-weight: 600; }
    .rows { min-width: 0; }
    .num { text-align: right; }
    .unset { color: var(--p-orange-600, #ea580c); font-style: italic; }
    .field { display: flex; flex-direction: column; gap: 0.35rem; margin-bottom: 0.9rem; }
    .field label { font-weight: 500; font-size: 0.875rem; }
    .field-row { display: grid; grid-template-columns: 1fr 8rem; gap: 0.75rem; }
    .field-inline { display: flex; align-items: center; gap: 0.5rem; margin-bottom: 1rem; }
    .form-actions { display: flex; justify-content: flex-end; gap: 0.5rem; }
    td.kw { font-size: 0.8rem; color: var(--p-text-muted-color); max-width: 22rem; }
    .num-card { background: var(--p-surface-card); border: 1px solid var(--p-surface-border); border-radius: 8px;
      padding: 1rem 1.25rem; max-width: 640px; display: flex; flex-direction: column; gap: 0.9rem; }
    .num-row { display: grid; grid-template-columns: 170px 1fr; align-items: center; gap: 1rem; font-size: 0.875rem; }
    .num-row > label, .num-row > span { font-weight: 500; }
    .num-row small { display: block; margin-top: 0.25rem; }
    .num-preview { color: #800000; font-size: 1.05rem; letter-spacing: 0.02em; }
    .muted { font-weight: 400; font-size: 0.75rem; color: var(--p-text-muted-color); }
    :host ::ng-deep .num-in { width: 9rem; }
    .required-star { color: var(--p-red-500, #ef4444); }
    .optional-label { font-weight: 400; font-size: 0.75rem; color: var(--p-text-muted-color); }
    .matrix-btn { margin-top: 0.4rem; border-top: 1px solid var(--p-surface-border); border-radius: 0 0 6px 6px; }
    .matrix-wrap { overflow-x: auto; background: var(--p-surface-card); border-radius: 8px; padding: 0.5rem; }
    .matrix { width: 100%; border-collapse: collapse; font-size: 0.85rem; }
    .matrix th { text-align: left; padding: 0.4rem; border-bottom: 1px solid var(--p-surface-border); min-width: 170px; }
    .matrix th:first-child { min-width: 90px; }
    .matrix td { padding: 0.3rem 0.4rem; }
    .matrix td.sg { font-weight: 600; }
    .role-sub { font-weight: 400; font-size: 0.7rem; color: var(--p-text-muted-color); }
    .mt-2 { margin-top: 0.75rem; align-items: center; }
    @media (max-width: 800px) { .setup { grid-template-columns: 1fr; } }
  `]
})
export class TasafSetupPageComponent implements OnInit, OnDestroy {
  private api        = inject(TasafMasterService);
  private fb         = inject(FormBuilder);
  private toolbar    = inject(GlobalToolbarService);
  private exportSvc  = inject(ExportService);
  private messageSvc = inject(MessageService);
  private confirmSvc = inject(ConfirmationService);
  private route      = inject(ActivatedRoute);

  /** Master Data page mode: only these lists (no approval / numbering screens). Null = TASAF Setup. */
  protected only      = (this.route.snapshot.data['masters'] as string[] | undefined) ?? null;
  private pageTitle   = (this.route.snapshot.data['title'] as string | undefined) ?? 'TASAF Setup';
  private lastKey     = this.only ? `${LAST_MASTER_KEY}:${this.only.join(',')}` : LAST_MASTER_KEY;

  protected metas         = signal<TasafMasterMeta[]>([]);
  protected current       = signal<TasafMasterMeta | null>(null);
  protected rows          = signal<TasafLookupDto[]>([]);
  protected parentOptions = signal<TasafLookupDto[]>([]);
  protected users         = signal<TasafUserOption[]>([]);
  protected searchTerm    = signal('');
  protected loading       = signal(false);
  protected saving        = signal(false);
  protected deleting      = signal(false);
  protected noSelection   = signal(true);
  protected editing       = signal<TasafLookupDto | null>(null);
  protected apiError      = signal<string | null>(null);
  protected selected: TasafLookupDto | null = null;
  protected dialogVisible = false;

  // Approval matrix view: "salesGroupId:roleId" → userId (null = vacant).
  protected matrixMode  = signal(false);
  protected matrix      = signal<TasafApprovalMatrix | null>(null);
  protected cells       = signal<Record<string, number | null>>({});
  private   savedCells  = '{}';
  protected matrixDirty = computed(() => JSON.stringify(this.cells()) !== this.savedCells);
  private   notMaster   = computed(() => !this.current());
  private   editOff     = computed(() => !this.current() || this.noSelection());

  // Approval rules view: "typeId:roleId" → { mode, amount }.
  protected rulesMode  = signal(false);
  protected rules      = signal<TasafApprovalRules | null>(null);
  protected ruleCells  = signal<Record<string, RuleCell>>({});
  private   savedRules = '{}';
  protected rulesDirty = computed(() => JSON.stringify(this.ruleCells()) !== this.savedRules);
  protected programTypes = signal<TasafLookupDto[]>([]);

  // Numbering view: RefNo proposal-number series.
  protected numberingMode  = signal(false);
  protected numbering      = signal<TasafNumbering | null>(null);
  protected numNext        = signal<number | null>(null);
  protected numDigits      = signal<number | null>(null);
  protected numberingDirty = computed(() => !!this.numbering() &&
    (this.numNext() !== this.numbering()!.nextNumber || this.numDigits() !== this.numbering()!.minDigits));
  protected numPreview = computed(() => {
    const n = this.numNext() ?? 1;
    const year = new Date(Date.now() + 8 * 3600_000).getUTCFullYear(); // Asia/Manila
    return `${year}-RKA-ER-${String(n).padStart(this.numDigits() ?? 2, '0')}`;
  });
  protected readonly utc = utc;
  protected readonly ruleModes = [
    { label: 'Not required', value: 'none' },
    { label: 'Always',       value: 'always' },
    { label: 'Over amount',  value: 'over' },
  ];

  /** Off = active rows only (the default). */
  protected showInactive = signal(false);

  protected filteredRows = computed(() => {
    const term = this.searchTerm().toLowerCase().trim();
    const rows = this.showInactive() ? this.rows() : this.rows().filter(r => r.isActive);
    if (!term) return rows;
    return rows.filter(r =>
      r.name.toLowerCase().includes(term) ||
      r.code.toLowerCase().includes(term) ||
      (r.description ?? '').toLowerCase().includes(term) ||
      (r.parentName ?? '').toLowerCase().includes(term) ||
      (r.userName ?? '').toLowerCase().includes(term) ||
      (r.userNames ?? '').toLowerCase().includes(term));
  });

  protected form = this.fb.group({
    name:        ['', Validators.required],
    code:        [''],
    sortOrder:   [0 as number | null],
    description: [''],
    isActive:    [true],
    parentId:    [null as number | null],
    userId:      [null as number | null],
    userIds:     [[] as number[]],
    amount:      [null as number | null],
    text:        [""]
  });

  ngOnInit() {
    this.setToolbar();
    this.api.getMeta().subscribe(res => {
      const all = res.data ?? [];
      const metas = this.only ? all.filter(m => this.only!.includes(m.key)) : all.filter(m => !MASTER_DATA_KEYS.includes(m.key));
      this.metas.set(metas);
      const last = localStorage.getItem(this.lastKey);
      if (!this.only) {
        if (last === MATRIX_KEY) { this.selectMatrix(); return; }
        if (last === RULES_KEY) { this.selectRules(); return; }
        if (last === NUMBERING_KEY) { this.selectNumbering(); return; }
      }
      const first = metas.find(m => m.key === last) ?? metas[0];
      if (first) this.selectMaster(first);
    });
    this.api.getUsers().subscribe(res => this.users.set(res.data ?? []));
  }

  ngOnDestroy() {
    this.toolbar.clear();
  }

  private setToolbar() {
    this.toolbar.set({
      title:   this.pageTitle,
      add:     { onClick: () => this.openNew(), disabled: this.notMaster },
      edit:    { onClick: () => this.selected && this.openEdit(this.selected), disabled: this.editOff },
      delete:  { onClick: () => this.confirmDelete(), disabled: this.editOff, loading: this.deleting },
      refresh: { onClick: () => this.matrixMode() ? this.loadMatrix() : this.rulesMode() ? this.loadRules()
                                 : this.numberingMode() ? this.loadNumbering() : this.loadRows(), loading: this.loading },
      search:  term => this.searchTerm.set(term),
      export:  { pdf: () => this.export('pdf'), excel: () => this.export('excel'), csv: () => this.export('csv') },
    });
  }

  /** Switch the right-hand panel: a master list, the rules grid, or the people matrix. */
  private setView(view: 'master' | 'rules' | 'matrix' | 'numbering', key: string) {
    this.matrixMode.set(view === 'matrix');
    this.rulesMode.set(view === 'rules');
    this.numberingMode.set(view === 'numbering');
    if (view !== 'master') this.current.set(null);
    this.selected = null;
    this.noSelection.set(true);
    localStorage.setItem(this.lastKey, key);
  }

  // ── Approval rules ─────────────────────────────────────────
  selectRules() {
    this.setView('rules', RULES_KEY);
    this.loadRules();
  }

  loadRules() {
    this.loading.set(true);
    this.api.getAll('program-types').subscribe(res => this.programTypes.set(res.data ?? []));
    this.api.getRules().subscribe({
      next: res => { this.applyRules(res.data ?? null); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  private applyRules(r: TasafApprovalRules | null) {
    this.rules.set(r);
    const map: Record<string, RuleCell> = {};
    for (const c of r?.rules ?? []) {
      map[`${c.approvalTypeId}:${c.roleId}`] = c.overAmount == null
        ? { mode: 'always', amount: null }
        : { mode: 'over', amount: c.overAmount };
    }
    this.ruleCells.set(map);
    this.savedRules = JSON.stringify(map);
  }

  rule(type: number, role: number): RuleCell {
    return this.ruleCells()[`${type}:${role}`] ?? { mode: 'none', amount: null };
  }

  setRule(type: number, role: number, mode: RuleCell['mode'], amount: number | null) {
    this.ruleCells.update(c => {
      const next = { ...c };
      if (mode === 'none') delete next[`${type}:${role}`];
      else next[`${type}:${role}`] = { mode, amount: mode === 'over' ? amount : null };
      return next;
    });
  }

  saveRules() {
    const r = this.rules();
    if (!r) return;
    const body = r.types.flatMap(t => r.roles.map(role => {
      const c = this.rule(t.id, role.id);
      return { approvalTypeId: t.id, roleId: role.id, required: c.mode !== 'none', overAmount: c.mode === 'over' ? (c.amount ?? 0) : null };
    }));
    this.saving.set(true);
    this.api.saveRules(body).subscribe({
      next: res => {
        this.saving.set(false);
        this.applyRules(res.data ?? null);
        this.messageSvc.add({ severity: 'success', summary: 'Saved', detail: 'Approval rules saved.' });
      },
      error: err => {
        this.saving.set(false);
        this.messageSvc.add({ severity: 'error', summary: 'Error', detail: err?.error?.message ?? 'Failed to save the approval rules.' });
      }
    });
  }

  /** Program types linked to an approval type, for the row caption. */
  programTypesOf(typeId: number) {
    const names = this.programTypes().filter(p => p.parentId === typeId && p.isActive).map(p => p.name);
    return names.length ? names.join(', ') : 'No program types linked';
  }

  // ── Numbering ──────────────────────────────────────────────
  selectNumbering() {
    this.setView('numbering', NUMBERING_KEY);
    this.loadNumbering();
  }

  loadNumbering() {
    this.loading.set(true);
    this.api.getNumbering().subscribe({
      next: res => { this.applyNumbering(res.data ?? null); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  private applyNumbering(n: TasafNumbering | null) {
    this.numbering.set(n);
    this.numNext.set(n?.nextNumber ?? null);
    this.numDigits.set(n?.minDigits ?? null);
  }

  saveNumbering() {
    const next = this.numNext();
    const digits = this.numDigits();
    if (!next || !digits) {
      this.messageSvc.add({ severity: 'warn', summary: 'Numbering', detail: 'Enter the next number and the minimum digits.' });
      return;
    }
    this.saving.set(true);
    this.api.saveNumbering(next, digits).subscribe({
      next: res => {
        this.saving.set(false);
        this.applyNumbering(res.data ?? null);
        this.messageSvc.add({ severity: 'success', summary: 'Saved', detail: `The next TASAF gets number ${res.data?.nextNumber}.` });
      },
      error: err => {
        this.saving.set(false);
        this.messageSvc.add({ severity: 'error', summary: 'Error', detail: err?.error?.message ?? 'Failed to save the numbering.' });
      }
    });
  }

  // ── Approval matrix ────────────────────────────────────────
  selectMatrix() {
    this.setView('matrix', MATRIX_KEY);
    this.loadMatrix();
  }

  loadMatrix() {
    this.loading.set(true);
    this.api.getMatrix().subscribe({
      next: res => { this.applyMatrix(res.data ?? null); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
  }

  private applyMatrix(m: TasafApprovalMatrix | null) {
    this.matrix.set(m);
    const map: Record<string, number | null> = {};
    for (const c of m?.cells ?? []) if (c.userId) map[`${c.salesGroupId}:${c.roleId}`] = c.userId;
    this.cells.set(map);
    this.savedCells = JSON.stringify(map);
  }

  cell(sg: number, role: number) { return this.cells()[`${sg}:${role}`] ?? null; }

  setCell(sg: number, role: number, userId: number | null) {
    this.cells.update(c => {
      const next = { ...c };
      if (userId) next[`${sg}:${role}`] = userId; else delete next[`${sg}:${role}`];
      return next;
    });
  }

  saveMatrix() {
    const m = this.matrix();
    if (!m) return;
    const body = m.salesGroups.flatMap(g => m.roles.map(r => ({ salesGroupId: g.id, roleId: r.id, userId: this.cell(g.id, r.id) })));
    this.saving.set(true);
    this.api.saveMatrix(body).subscribe({
      next: res => {
        this.saving.set(false);
        this.applyMatrix(res.data ?? null);
        this.messageSvc.add({ severity: 'success', summary: 'Saved', detail: 'Approval matrix saved.' });
      },
      error: err => {
        this.saving.set(false);
        this.messageSvc.add({ severity: 'error', summary: 'Error', detail: err?.error?.message ?? 'Failed to save the approval matrix.' });
      }
    });
  }

  shortLabel(label: string) { return label.split('(')[0].trim(); }

  selectMaster(m: TasafMasterMeta) {
    this.setView('master', m.key);
    this.current.set(m);
    this.loadRows();
  }

  loadRows() {
    const m = this.current();
    if (!m) return;
    this.loading.set(true);
    this.api.getAll(m.key).subscribe({
      next: res => { this.rows.set(res.data ?? []); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
    if (m.parentKey) {
      this.api.getAll(m.parentKey).subscribe(res => this.parentOptions.set((res.data ?? []).filter(p => p.isActive)));
    } else {
      this.parentOptions.set([]);
    }
  }

  openNew() {
    if (!this.current()) return;
    this.editing.set(null);
    this.apiError.set(null);
    const nextOrder = Math.max(0, ...this.rows().map(r => r.sortOrder)) + 1;
    this.form.reset({ name: '', code: '', sortOrder: nextOrder, description: '', isActive: true, parentId: null, userId: null, userIds: [], amount: null, text: '' });
    this.dialogVisible = true;
  }

  openEdit(r: TasafLookupDto) {
    this.editing.set(r);
    this.apiError.set(null);
    // Keep an inactive parent selectable when the row still points at it.
    if (r.parentId && !this.parentOptions().some(p => p.id === r.parentId)) {
      this.parentOptions.update(opts => [...opts, { ...r, id: r.parentId!, name: r.parentName ?? '' }]);
    }
    this.form.reset({
      name: r.name, code: r.code, sortOrder: r.sortOrder, description: r.description,
      isActive: r.isActive, parentId: r.parentId ?? null, userId: r.userId ?? null, userIds: r.userIds ?? [], amount: r.amount ?? null,
      text: r.text ?? ''
    });
    this.dialogVisible = true;
  }

  save() {
    const m = this.current();
    this.form.markAllAsTouched();
    if (!m || this.form.invalid) {
      this.apiError.set('Name is required.');
      return;
    }
    const v = this.form.getRawValue();
    const dto = {
      name: v.name ?? '', code: v.code ?? '', description: v.description ?? '',
      sortOrder: v.sortOrder ?? 0, isActive: v.isActive ?? true,
      parentId: v.parentId, userId: v.userId, amount: v.amount, text: v.text ?? '',
      userIds: m.usersLabel ? (v.userIds ?? []) : null
    };
    const editing = this.editing();
    this.saving.set(true);
    this.apiError.set(null);
    (editing ? this.api.update(m.key, editing.id, dto) : this.api.create(m.key, dto)).subscribe({
      next: () => {
        this.saving.set(false);
        this.dialogVisible = false;
        this.messageSvc.add({ severity: 'success', summary: 'Saved', detail: `${dto.name} saved.` });
        this.loadRows();
      },
      error: err => {
        this.saving.set(false);
        this.apiError.set(err?.error?.message ?? 'Failed to save.');
      }
    });
  }

  private confirmDelete() {
    const m = this.current();
    const r = this.selected;
    if (!m || !r) return;
    this.confirmSvc.confirm({
      message: `Delete <strong>${r.name}</strong> from ${m.label}? This cannot be undone.`,
      header: 'Confirm Delete',
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.deleting.set(true);
        this.api.delete(m.key, r.id).subscribe({
          next: () => {
            this.deleting.set(false);
            this.messageSvc.add({ severity: 'success', summary: 'Deleted', detail: `${r.name} deleted.` });
            this.selected = null;
            this.noSelection.set(true);
            this.loadRows();
          },
          error: err => {
            this.deleting.set(false);
            this.messageSvc.add({ severity: 'error', summary: 'Cannot delete', detail: err?.error?.message ?? 'Failed to delete.' });
          }
        });
      }
    });
  }

  private export(kind: 'pdf' | 'excel' | 'csv') {
    const m = this.current();
    if (!m) return;
    const cols: ExportColumn[] = [
      { header: 'Order', field: 'sortOrder' },
      { header: 'Code',  field: 'code' },
      { header: 'Name',  field: 'name' },
      ...(m.parentLabel ? [{ header: m.parentLabel, field: 'parentName' }] : []),
      ...(m.usersLabel  ? [{ header: this.shortLabel(m.usersLabel), field: 'userNames' }] : []),
      ...(m.userLabel   ? [{ header: m.userLabel,   field: 'userName' }] : []),
      ...(m.amountLabel ? [{ header: this.shortLabel(m.amountLabel), field: 'amount', formatter: (v: unknown) => v == null ? '' : String(v) }] : []),
      { header: 'Description', field: 'description' },
      { header: 'Status', field: 'isActive', formatter: v => v ? 'Active' : 'Inactive' },
    ];
    const rows = this.filteredRows() as unknown as Record<string, unknown>[];
    const title = this.only ? m.label : `TASAF ${m.label}`;
    if (kind === 'pdf') this.exportSvc.exportPdf(title, cols, rows);
    else if (kind === 'excel') this.exportSvc.exportExcel(title, cols, rows);
    else this.exportSvc.exportCsv(title, cols, rows);
  }
}
