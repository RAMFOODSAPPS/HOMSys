import { Component, computed, effect, inject, OnDestroy, OnInit, signal, untracked } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NavigationEnd, Router } from '@angular/router';
import { filter, Observable, Subscription } from 'rxjs';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { SelectModule } from 'primeng/select';
import { TooltipModule } from 'primeng/tooltip';
import { DialogModule } from 'primeng/dialog';
import { TextareaModule } from 'primeng/textarea';
import { ConfirmationService, MessageService } from 'primeng/api';
import { GlobalToolbarService } from '../../core/services/global-toolbar.service';
import { AuthService } from '../../core/services/auth.service';
import { TabBarService } from '../../core/services/tab-bar.service';
import { TparService } from '../../core/services/tpar.service';
import { ApiResponse } from '../../core/models/auth.model';
import { Tpar, TPAR_STATUS_SEVERITY, TparTasaf } from '../../core/models/tpar.model';
import { FormLayoutService } from '../../core/services/form-layout.service';
import { FormLayoutItem, cellStyle, defaultItems, mergeLayout } from '../../core/models/form-layout.model';
import { FormDesignerComponent } from '../../shared/form-designer/form-designer.component';
import { TPAR_FIELDS, TPAR_FORM_KEY } from './tpar-layout';

/**
 * New TPAR (/tpars, permission `tpar`; user decision 2026-11-24) — the form; the TPAR List is /tpars/list. A TPAR opened
 * from the list gets its own tab (/tpars#id). Encoders enter TPARs (Trade Promo Analysis Reports)
 * against Approved TASAFs — several per TASAF, by different BAs. Draft → Submitted → Approved, or Cancelled (for now
 * the encoder who entered it, or an Admin, does each step). The toolbar always shows Save / Submit / Approve / Cancel,
 * greyed out when they don't apply to the TPAR in the form. Every TPAR but a Cancelled one holds its amount; the
 * TASAF is Completed when its Approved TPARs reach the approved amount. The TASAF form shows them read-only.
 */
const TAB = '/tpars';

@Component({
  selector: 'app-tpar-page',
  standalone: true,
  imports: [DecimalPipe, FormsModule, TagModule, ButtonModule, InputTextModule, InputNumberModule,
            SelectModule, TooltipModule, DialogModule, TextareaModule, FormDesignerComponent],
  template: `
    <div class="page">
      <!-- ── The TPAR in the form: a new one, or one opened from the TPAR List (its own tab) ── -->
      @if (designing()) {
        <app-form-designer [formKey]="formKey" title="TPAR" [fields]="fields" [layout]="layout()"
          (saved)="layout.set($event); designing.set(false)" (closed)="designing.set(false)" />
      } @else {
      <section class="card">
        <div class="sec-head">
          <h3>{{ current() ? 'TPAR No. ' + current()!.tparNo : 'New TPAR' }}</h3>
          <p-tag [value]="current()?.status ?? 'New'" [severity]="current() ? tparSev(current()!.status) : 'info'" />
          @if (current() && !editable()) { <span class="muted"><i class="pi pi-lock"></i> {{ lockReason() }}</span> }
        </div>
        <!-- Field arrangement comes from the Form Designer layout (defaults in tpar-layout.ts). -->
        <div class="fl-grid">
        @for (it of shown(); track it.key) {
          <div class="fl-cell" [style]="cellStyle(it)">
          @switch (it.key) {
            @case ('tasaf') {
              <div class="field"><label>{{ cap(it) }} <span class="req">*</span></label>
                @if (current()) {
                  <div class="ro">{{ current()!.tasafRefNo }} — {{ current()!.tasafTitle }}
                    @if (canOpenTasaf) { <p-button icon="pi pi-external-link" [text]="true" size="small" pTooltip="Open TASAF" (onClick)="openTasaf(current()!.tasafId)" /> }</div>
                } @else {
                  <p-select [options]="openTasafs()" [ngModel]="tasafId()" (ngModelChange)="tasafId.set($event)" optionLabel="label" optionValue="id"
                    [filter]="true" filterBy="label" [showClear]="true" placeholder="Pick an Approved TASAF" appendTo="body" styleClass="w-full"
                    [emptyMessage]="'No Approved TASAF with a balance left for your servicing branches'" />
                }
              </div>
            }
            @case ('tparNo') {
              <div class="field"><label>{{ cap(it) }} <span class="req">*</span></label>
                <input pInputText [ngModel]="tparNo()" (ngModelChange)="tparNo.set($event)" maxlength="50" [disabled]="!editable()" class="w-full" /></div>
            }
            @case ('branch') {
              <div class="field"><label>{{ cap(it) }} <span class="req">*</span></label>
                <p-select [options]="branchOptions()" [ngModel]="siteId()" (ngModelChange)="siteId.set($event)" optionLabel="name" optionValue="siteId"
                  [filter]="branchOptions().length > 8" filterBy="name,code" [disabled]="!editable()" appendTo="body" styleClass="w-full"
                  [placeholder]="formTasaf() ? 'Pick the branch' : 'Pick the TASAF first'" [emptyMessage]="formTasaf() ? 'No branch available for this TASAF' : 'Pick the TASAF first'" /></div>
            }
            @case ('amount') {
              <div class="field"><label>{{ cap(it) }} <span class="req">*</span></label>
                <p-inputNumber [ngModel]="amount()" (ngModelChange)="amount.set($event)" [min]="0" [minFractionDigits]="2" [maxFractionDigits]="2"
                  [disabled]="!editable()" styleClass="w-full" inputStyleClass="w-full" /></div>
            }
            @case ('balance') {
              <div class="field"><label>{{ cap(it) }}</label>
                <div class="ro info">
                  @if (formTasaf(); as t) {
                    <span>Approved {{ t.approvedAmount | number:'1.2-2' }} · TPARs {{ t.tparTotal | number:'1.2-2' }}
                      (approved {{ t.approvedTotal | number:'1.2-2' }}) · <strong>Available {{ available() | number:'1.2-2' }}</strong>
                      @if (editable() && (amount() ?? 0) > available()) { <span class="bad"> — the amount is more than the balance.</span> }</span>
                  } @else { <span class="muted">Pick the TASAF.</span> }
                </div></div>
            }
            @case ('remarks') {
              <div class="field"><label>{{ cap(it) }}</label>
                <!-- Wraps and grows with the text (Enter adds a line); the designer height sets the starting size. -->
                <textarea pTextarea [autoResize]="true" [ngModel]="remarks()" (ngModelChange)="remarks.set($event)" maxlength="500"
                  [disabled]="!editable()" class="w-full remarks" [rows]="it.h > 1 ? it.h * 2 : 1"></textarea></div>
            }
            @case ('createdBy') {
              <div class="field"><label>{{ cap(it) }}</label><input pInputText readonly tabindex="-1" class="w-full" [value]="current()?.createdByName ?? ''" /></div>
            }
            @case ('createdAt') {
              <div class="field"><label>{{ cap(it) }}</label><input pInputText readonly tabindex="-1" class="w-full" [value]="when(current()?.createdAt)" /></div>
            }
            @case ('updatedBy') {
              <div class="field"><label>{{ cap(it) }}</label><input pInputText readonly tabindex="-1" class="w-full" [value]="current()?.updatedBy ?? ''" /></div>
            }
            @case ('updatedAt') {
              <div class="field"><label>{{ cap(it) }}</label><input pInputText readonly tabindex="-1" class="w-full" [value]="when(current()?.updatedAt)" /></div>
            }
            @case ('workflow') {
              <div class="field"><label>{{ cap(it) }}</label>
                <div class="ro info">
                  @if (current(); as p) {
                    @if (p.submittedAt || p.approvedAt || p.cancelledAt) {
                      <span>@if (p.submittedAt) { Submitted {{ when(p.submittedAt) }} }
                        @if (p.approvedAt) { · Approved by {{ p.approvedBy }} {{ when(p.approvedAt) }} }
                        @if (p.cancelledAt) { · Cancelled by {{ p.cancelledBy }} {{ when(p.cancelledAt) }} }</span>
                    } @else { <span class="muted">Draft — not submitted yet.</span> }
                  } @else { <span class="muted">New — not saved yet.</span> }
                </div></div>
            }
            @case ('help') { <small class="muted note">{{ cap(it) }}</small> }
          }
          </div>
        }
        </div>
      </section>
      }

      <p-dialog header="Revert TPAR to Draft" [visible]="revertOpen()" (visibleChange)="revertOpen.set($event)" [modal]="true" [style]="{ width: '32rem' }">
        <p class="muted">TPAR No. {{ current()?.tparNo }} ({{ current()?.status }}) goes back to <strong>Draft</strong> so it can be changed, then submitted and approved again. It keeps holding its amount.</p>
        <label>Reason <span class="req">*</span></label>
        <textarea pTextarea rows="3" maxlength="500" class="w-full" [ngModel]="revertReason()" (ngModelChange)="revertReason.set($event)" placeholder="What needs to be changed?"></textarea>
        <ng-template pTemplate="footer">
          <p-button label="Keep it" severity="secondary" [text]="true" (onClick)="revertOpen.set(false)" />
          <p-button label="Revert to Draft" icon="pi pi-replay" severity="warn" [disabled]="!revertReason().trim() || acting()" [loading]="acting()" (onClick)="doRevert()" />
        </ng-template>
      </p-dialog>
    </div>
  `,
  styles: [`
    .page { padding: 0.25rem; }
    .card { background: var(--p-surface-card); border-radius: 8px; padding: 1rem 1.25rem; margin-bottom: 0.85rem; }
    .card h3 { margin: 0; font-size: 0.95rem; color: #800000; }
    .sec-head { display: flex; align-items: center; gap: 0.75rem; margin-bottom: 0.6rem; flex-wrap: wrap; }
    .fl-grid { display: grid; grid-template-columns: repeat(12, minmax(0, 1fr)); gap: 0.75rem 1rem; }
    .fl-cell { grid-column: var(--c) / span var(--w); grid-row: var(--r) / span var(--h); min-width: 0; }
    .note { display: block; }
    .remarks { resize: none; overflow: hidden; line-height: 1.4; }
    /* Inline labels: same width everywhere, so the boxes line up down the form. */
    .field { display: flex; flex-direction: row; align-items: center; gap: 0.75rem; }
    .field > label { flex: 0 0 7rem; font-weight: 500; font-size: 0.85rem; }
    .field > :not(label) { flex: 1 1 auto; min-width: 0; }
    .field:has(textarea) { align-items: flex-start; }
    .field:has(textarea) > label { padding-top: 0.6rem; }
    .ro { display: flex; align-items: center; min-height: 2.4rem; font-weight: 600; }
    .info { font-size: 0.85rem; font-weight: normal; }
    .muted { color: var(--p-text-muted-color); }
    .bad { color: #c62828; }
    .req { color: #c62828; }
    @media (max-width: 800px) { .fl-cell { grid-column: 1 / -1; grid-row: auto; } .field { flex-direction: column; align-items: stretch; } .field > label { flex: none; } }
  `]
})
export class TparPageComponent implements OnInit, OnDestroy {
  private api     = inject(TparService);
  private toolbar = inject(GlobalToolbarService);
  private msg     = inject(MessageService);
  private confirm = inject(ConfirmationService);
  private router  = inject(Router);
  private auth    = inject(AuthService);

  private tabBar  = inject(TabBarService);
  private layoutApi = inject(FormLayoutService);
  private datePipe  = new DatePipe('en-US');

  // ── Form Designer layout (Admin: toolbar Design) ──
  protected readonly formKey   = TPAR_FORM_KEY;
  protected readonly fields    = TPAR_FIELDS;
  protected readonly cellStyle = cellStyle;
  private readonly fieldDefs   = new Map(TPAR_FIELDS.map(d => [d.key, d]));
  protected layout    = signal<FormLayoutItem[]>(defaultItems(TPAR_FIELDS));
  protected shown     = computed(() => this.layout().filter(i => !i.hidden));
  protected designing = signal(false);
  private navSub?: Subscription;
  /** '/tpars' for a new TPAR, '/tpars#42' while one is open (its own tab). */
  private currentTabKey = TAB;

  /** Approved + Completed TASAFs: the picker (open ones) and the balance line. */
  protected tasafs       = signal<TparTasaf[]>([]);
  protected loading      = signal(false);
  protected saving       = signal(false);
  protected acting       = signal(false);
  /** Only users of the TASAF module can open the TASAF itself. */
  protected canOpenTasaf = this.auth.isAdmin() || this.auth.hasPermission('tasaf');

  // ── The form ──
  /** The saved TPAR in the form; null = a new one. */
  protected current = signal<Tpar | null>(null);
  protected tasafId = signal<number | null>(null);
  protected tparNo  = signal('');
  protected amount  = signal<number | null>(null);
  protected remarks = signal('');
  protected siteId  = signal<number | null>(null);

  /** New, or a Draft the user may change. */
  protected editable = computed(() => !this.current() || this.current()!.canEdit);
  protected dirty = computed(() => {
    const c = this.current();
    return c ? c.tparNo !== this.tparNo().trim() || c.amount !== this.amount() || c.remarks !== this.remarks().trim()
               || (c.siteId ?? null) !== this.siteId()
             : !!(this.tasafId() || this.tparNo().trim() || this.amount() || this.remarks().trim());
  });
  /**
   * New TPAR: when the TASAF is picked (or changed), the branch starts as the user's own branch when the TASAF offers
   * it, or the TASAF's only branch; a branch the new TASAF doesn't offer is cleared.
   */
  private defaultBranch = effect(() => {
    if (this.current()) return;
    const opts = this.formTasaf()?.branches ?? [];
    const id = untracked(this.siteId);
    if (id && opts.some(o => o.siteId === id)) return;
    const mine = (this.auth.user()?.branchCode ?? '').toLowerCase();
    const pick = opts.find(o => mine && o.code.toLowerCase() === mine) ?? (opts.length === 1 ? opts[0] : null);
    this.siteId.set(pick?.siteId ?? null);
  });
  /** The TASAF's branches; a saved TPAR whose branch is no longer offered still shows it. */
  protected branchOptions = computed(() => {
    const opts = this.formTasaf()?.branches ?? [];
    const c = this.current();
    return c?.siteId && !opts.some(o => o.siteId === c.siteId)
      ? [...opts, { siteId: c.siteId, code: '', name: c.branchName }] : opts;
  });
  protected lockReason = computed(() => {
    const c = this.current()!;
    if (c.tasafStatus !== 'Approved') return `TASAF ${c.tasafRefNo} is ${c.tasafStatus}.`;
    if (!c.canCancel) return `Only ${c.createdByName} (who entered it) or an Admin can change it.`;
    return `${c.status} — only a Draft can be changed. Use Revert to bring it back to Draft.`;
  });

  /** TASAFs a TPAR can still be added to: Approved, a balance left, and a branch this user may pick (servicing branches). */
  protected openTasafs = computed(() => this.tasafs().filter(t => t.status === 'Approved' && t.balance > 0 && t.branches.length > 0)
    .map(t => ({ ...t, label: `${t.refNo} — ${t.title} (balance ${t.balance.toLocaleString('en-US', { minimumFractionDigits: 2 })})` })));
  protected formTasaf = computed(() => this.tasafs().find(t => t.id === (this.current()?.tasafId ?? this.tasafId())) ?? null);
  /** What this TPAR may use: the TASAF's balance, plus its own saved amount (unless Cancelled). */
  protected available = computed(() => {
    const c = this.current();
    return (this.formTasaf()?.balance ?? 0) + (c && c.status !== 'Cancelled' ? c.amount : 0);
  });

  // Toolbar buttons are always shown; these grey them out.
  private saveOff    = computed(() => !this.editable() || this.designing() || this.acting());
  private submitOff  = computed(() => !this.current()?.canSubmit || this.designing() || this.saving() || this.acting());
  private approveOff = computed(() => !this.current()?.canApprove || this.designing() || this.dirty() || this.acting());
  private cancelOff  = computed(() => !this.current()?.canCancel || this.designing() || this.acting());
  private revertOff  = computed(() => !this.current()?.canRevert || this.designing() || this.acting());
  protected revertOpen   = signal(false);
  protected revertReason = signal('');

  ngOnInit() {
    this.toolbar.set({
      title:     'New TPAR',
      save:      { onClick: () => this.save(), loading: this.saving, disabled: this.saveOff },
      submit:    { label: 'Submit TPAR', onClick: () => this.submit(), disabled: this.submitOff },
      approve:   { label: 'Approve TPAR', onClick: () => this.approve(), disabled: this.approveOff },
      cancelDoc: { label: 'Cancel TPAR', onClick: () => this.cancelTpar(), disabled: this.cancelOff },
      revert:    { label: 'Revert TPAR to Draft (to change it)', onClick: () => this.openRevert(), disabled: this.revertOff },
      add:       { onClick: () => this.newTpar() },
      refresh:   { onClick: () => this.reload(), loading: this.loading },
      list:      { onClick: () => this.router.navigate(['/tpars/list']) },
      // Form Designer: Admin only (the layout API saves for Admins only).
      ...(this.auth.isAdmin() ? { design: { onClick: () => this.designing.set(true), disabled: this.designing } } : {}),
    });
    this.layoutApi.get(TPAR_FORM_KEY).subscribe({
      next: res => this.layout.set(mergeLayout(TPAR_FIELDS, res.data?.layout)),
      error: () => {} // keep the default layout
    });
    this.loadTasafs();
    this.handleNavState();
    this.navSub = this.router.events.pipe(filter(e => e instanceof NavigationEnd)).subscribe(() => this.handleNavState());
    this.tabBar.registerDirtyChecker(TAB, () => this.dirty() && this.editable());
  }

  ngOnDestroy() {
    this.tabBar.unregisterDirtyChecker(TAB);
    this.tabBar.unregisterDirtyChecker(this.currentTabKey);
    this.toolbar.clear();
    this.navSub?.unsubscribe();
  }

  tparSev(status: string) { return TPAR_STATUS_SEVERITY[status] ?? 'info'; }
  /** Server times are UTC without a zone marker. */
  utc(v: string | null) { return v ? (v.endsWith('Z') ? v : v + 'Z') : null; }
  /** Server UTC time as local "24 Nov 2026 3:05 PM"; blank when none. */
  when(v: string | null | undefined) { return v ? this.datePipe.transform(this.utc(v), 'dd MMM yyyy h:mm a') ?? '' : ''; }
  /** Caption: the designer's custom label, else the field's default. */
  cap(it: FormLayoutItem) { return it.label || this.fieldDefs.get(it.key)?.label || ''; }

  reload() {
    this.loadTasafs();
    const c = this.current();
    if (c) this.api.get(c.id).subscribe({ next: res => { if (res.data && !this.dirty()) this.fill(res.data); } });
  }

  loadTasafs() {
    this.loading.set(true);
    this.api.tasafs(true).subscribe({
      next: res => { this.loading.set(false); this.tasafs.set(res.data ?? []); },
      error: err => { this.loading.set(false); this.fail(err, 'Failed to load the TASAFs.'); }
    });
  }

  // ── Tabs / navigation (state from the TPAR List: tparId = open one, tparTasafId = new one for that TASAF) ──
  private handleNavState() {
    const state = history.state as Record<string, unknown>;
    const tparId = state?.['tparId'] as number | undefined;
    if (tparId) {
      if (tparId !== this.current()?.id)
        this.api.get(tparId).subscribe({
          next: res => { if (res.data) this.show(res.data); },
          error: err => this.fail(err, 'Failed to open the TPAR.')
        });
      return;
    }
    if (this.current()) this.resetForm();
    const tasafId = state?.['tparTasafId'] as number | undefined;
    if (tasafId) this.tasafId.set(tasafId);
  }

  /** A saved TPAR into the form, in its own tab. */
  private show(p: Tpar) {
    this.fill(p);
    const key = `${TAB}#${p.id}`;
    if (key !== this.currentTabKey) {
      this.tabBar.unregisterDirtyChecker(this.currentTabKey);
      this.currentTabKey = key;
    }
    this.tabBar.openTab({ key, label: `TPAR - ${p.tparNo}`, route: TAB, icon: 'pi-receipt', state: { tparId: p.id } });
    this.tabBar.registerDirtyChecker(key, () => this.dirty() && this.editable());
  }

  private resetForm() {
    this.tabBar.unregisterDirtyChecker(this.currentTabKey);
    this.currentTabKey = TAB;
    this.fill(null);
    this.tabBar.registerDirtyChecker(TAB, () => this.dirty() && this.editable());
  }

  openTasaf(id: number) { this.router.navigate(['/tasafs'], { state: { tasafId: id } }); }

  /** Toolbar +: a blank TPAR in the New TPAR tab. */
  newTpar() {
    this.guard(() => {
      if (this.current()) this.tabBar.switchToDefaultTab(TAB);
      this.resetForm();
      if (!this.openTasafs().length)
        this.msg.add({ severity: 'info', summary: 'TPAR', life: 6000,
          detail: 'There is no Approved TASAF with a balance left for your servicing branches. TPARs can be added only once a TASAF is Approved.' });
    });
  }

  private fill(p: Tpar | null) {
    this.current.set(p);
    this.tasafId.set(p?.tasafId ?? null);
    this.tparNo.set(p?.tparNo ?? '');
    this.amount.set(p?.amount ?? null);
    this.remarks.set(p?.remarks ?? '');
    this.siteId.set(p?.siteId ?? null);
  }

  /** Runs next, asking first when the form has unsaved changes. */
  private guard(next: () => void) {
    if (!this.dirty() || !this.editable()) { next(); return; }
    this.confirm.confirm({
      header: 'Discard changes?', message: 'The TPAR in the form has unsaved changes. Discard them?', icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Discard', rejectLabel: 'Keep editing', accept: next
    });
  }

  save(then?: (p: Tpar) => void) {
    if (this.saveOff() || this.saving()) return;
    const c = this.current();
    const tasafId = c?.tasafId ?? this.tasafId();
    const amount = this.amount();
    if (!tasafId) { this.warn('Pick the TASAF first.'); return; }
    if (!this.tparNo().trim()) { this.warn('TPAR No. is required.'); return; }
    if (!(amount && amount > 0)) { this.warn('Enter an amount above zero.'); return; }
    if (c && !this.dirty()) { then?.(c); return; }
    if (!this.siteId()) { this.warn('Pick the branch.'); return; }
    const body = { tasafId, siteId: this.siteId(), tparNo: this.tparNo().trim(), amount, remarks: this.remarks().trim() };
    this.saving.set(true);
    (c ? this.api.update(c.id, body) : this.api.create(body)).subscribe({
      next: res => {
        this.saving.set(false);
        this.show(res.data!);
        if (then) then(res.data!);
        else this.msg.add({ severity: 'success', summary: 'TPAR', detail: `TPAR No. ${res.data!.tparNo} saved as Draft.` });
        this.reload();
      },
      error: err => { this.saving.set(false); this.fail(err, 'Failed to save the TPAR.'); }
    });
  }

  /** Submit (unsaved changes to the Draft are saved first). */
  submit() {
    if (this.submitOff()) return;
    this.save(p => this.act(this.api.submit(p.id), r => `TPAR No. ${r.tparNo} submitted.`));
  }

  approve() {
    const c = this.current();
    if (!c || this.approveOff()) return;
    const t = this.formTasaf();
    const completes = !!t && t.approvedTotal + c.amount >= t.approvedAmount;
    this.confirm.confirm({
      header: 'Approve TPAR?', icon: 'pi pi-check',
      message: `Approve TPAR No. ${c.tparNo} (${c.amount.toLocaleString('en-US', { minimumFractionDigits: 2 })})?`
        + (completes ? ` The approved TPARs then reach the approved amount, so TASAF ${c.tasafRefNo} becomes Completed.` : ''),
      acceptLabel: 'Approve', rejectLabel: 'Cancel',
      accept: () => this.act(this.api.approve(c.id),
        r => `TPAR No. ${r.tparNo} approved.${r.tasafStatus === 'Completed' ? ` ${r.tasafRefNo} is now Completed.` : ''}`)
    });
  }

  cancelTpar() {
    const c = this.current();
    if (!c || this.cancelOff()) return;
    this.confirm.confirm({
      header: 'Cancel TPAR?', icon: 'pi pi-times-circle',
      message: `Cancel TPAR No. ${c.tparNo} (${c.status}, ${c.amount.toLocaleString('en-US', { minimumFractionDigits: 2 })})? `
        + `It is kept as Cancelled; its amount goes back to the TASAF's balance and the TPAR No. can be used again.`,
      acceptLabel: 'Cancel TPAR', rejectLabel: 'Keep it', acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.act(this.api.cancel(c.id), r => `TPAR No. ${r.tparNo} cancelled.`)
    });
  }

  openRevert() {
    if (this.revertOff()) return;
    this.revertReason.set('');
    this.revertOpen.set(true);
  }

  doRevert() {
    const c = this.current();
    const why = this.revertReason().trim();
    if (!c || !why) return;
    this.act(this.api.revert(c.id, why), r => { this.revertOpen.set(false); return `TPAR No. ${r.tparNo} is back to Draft — you can change it now.`; });
  }

  private act(call: Observable<ApiResponse<Tpar>>, done: (p: Tpar) => string) {
    this.acting.set(true);
    call.subscribe({
      next: res => {
        this.acting.set(false);
        this.show(res.data!);
        this.msg.add({ severity: 'success', summary: 'TPAR', detail: done(res.data!), life: 6000 });
        this.reload();
      },
      error: err => { this.acting.set(false); this.fail(err, 'Failed.'); }
    });
  }

  private warn(detail: string) { this.msg.add({ severity: 'warn', summary: 'TPAR', detail }); }
  private fail(err: any, fallback: string) {
    this.msg.add({ severity: 'error', summary: 'TPAR', detail: err?.error?.message ?? fallback, life: 8000 });
  }
}
