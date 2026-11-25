import { Component, computed, inject, OnDestroy, OnInit, signal, WritableSignal } from '@angular/core';
import { DatePipe, DecimalPipe, PercentPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NavigationEnd, Router } from '@angular/router';
import { filter, Observable, Subscription } from 'rxjs';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';
import { InputNumberModule } from 'primeng/inputnumber';
import { TextareaModule } from 'primeng/textarea';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmationService, MessageService } from 'primeng/api';
import { GlobalToolbarService } from '../../../core/services/global-toolbar.service';
import { TabBarService } from '../../../core/services/tab-bar.service';
import { ApiResponse } from '../../../core/models/auth.model';
import { parseIsoDate, TASAF_STATUS_SEVERITY, toIsoDate, utc } from '../../../core/models/tasaf.model';
import { shrinkImage } from '../../../core/utils/image-shrink';
import {
  POST_EVAL_STATUS_SEVERITY, PostEval, PostEvalAttachment, PostEvalListItem, SavePostEval, TasafPostEvalService
} from '../../../core/services/tasaf-post-eval.service';

/**
 * Post Evaluation (/tasafs/post-eval, permission `tasaf`; user request 2026-11-24, workbook › PROMO Post Eval Template).
 * The list shows Completed / Closed TASAFs; opening one shows its post evaluation in its own tab (/tasafs/post-eval#id).
 * PROMO- program types: Promo Post Evaluation (actual dates, doors, sales per promoted SKU, cost, conclusion); every other
 * program type: Post Activity form (actual execution dates + cost evaluation). Actual cost within the approved budget =
 * green, over = red flag. Signed by the TASAF's approval chain; the last approval closes the TASAF.
 */
const TAB = '/tasafs/post-eval';
type Sums = { pre: number; planned: number; actual: number; post: number };

@Component({
  selector: 'app-tasaf-post-eval-page',
  standalone: true,
  imports: [DatePipe, DecimalPipe, PercentPipe, FormsModule, TableModule, TagModule, ButtonModule, InputNumberModule, TextareaModule,
            DatePickerModule, DialogModule, TooltipModule],
  template: `
    <div class="page">
    @if (!pe()) {
      <!-- ── List ── -->
      <section class="card">
        <div class="sec-head">
          <h3>Post Evaluation</h3>
          <span class="muted">When a TASAF is Completed (TPARs done), its post evaluation is created here as a Draft and the proponent is notified. Its approval closes the TASAF. Click a row to open it.</span>
          <span class="chk">
            @for (c of counts(); track c.status) { <p-tag [value]="c.status + ' ' + c.n" [severity]="c.sev" /> }
          </span>
        </div>
        <p-table [value]="filtered()" [loading]="loading()" selectionMode="single" (selectionChange)="open($event)"
          dataKey="tasafId" [paginator]="true" [rows]="25" [rowsPerPageOptions]="[25, 50, 100, 200]"
          [showCurrentPageReport]="true" currentPageReportTemplate="{first}–{last} of {totalRecords}" styleClass="p-datatable-sm pe-list" [scrollable]="true">
          <ng-template pTemplate="header">
            <tr>
              <th pSortableColumn="refNo">Document No. <p-sortIcon field="refNo" /></th>
              <th pSortableColumn="title">Title <p-sortIcon field="title" /></th>
              <th pSortableColumn="kind">Form <p-sortIcon field="kind" /></th>
              <th pSortableColumn="salesGroupName">Sales Group <p-sortIcon field="salesGroupName" /></th>
              <th pSortableColumn="proponentName">Proponent <p-sortIcon field="proponentName" /></th>
              <th pSortableColumn="plannedCost" class="num">Approved Budget <p-sortIcon field="plannedCost" /></th>
              <th pSortableColumn="actualCost" class="num">Actual Cost <p-sortIcon field="actualCost" /></th>
              <th>Budget</th>
              <th pSortableColumn="status">Post Eval <p-sortIcon field="status" /></th>
              <th>With</th>
              <th pSortableColumn="tasafStatus">TASAF <p-sortIcon field="tasafStatus" /></th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-r>
            <tr [pSelectableRow]="r" [class.mine]="r.awaitingMe">
              <td class="nowrap"><strong>{{ r.refNo }}</strong></td>
              <td class="wide">{{ r.title }}</td>
              <td class="nowrap">{{ r.kind === 'Promo' ? 'Promo Post Eval' : 'Post Activity' }}</td>
              <td class="nowrap">{{ r.salesGroupName }}</td>
              <td class="nowrap">{{ r.proponentName }}</td>
              <td class="num">{{ r.plannedCost | number:'1.2-2' }}</td>
              <td class="num">{{ r.actualCost === null ? '' : (r.actualCost | number:'1.2-2') }}</td>
              <td class="nowrap">@if (r.overBudget !== null) { <span class="flag" [class.over]="r.overBudget">{{ r.overBudget ? 'Over' : 'Within' }}</span> }</td>
              <td class="nowrap"><p-tag [value]="r.status ?? 'Not started'" [severity]="r.status ? peSev(r.status) : 'info'" /></td>
              <td class="nowrap">@if (r.status === 'For Approval') { {{ r.currentApproverName }} ({{ r.currentRoleCode }})
                @if (r.awaitingMe) { <strong class="me"> — you</strong> } }</td>
              <td class="nowrap"><p-tag [value]="r.tasafStatus" [severity]="tasafSev(r.tasafStatus)" /></td>
            </tr>
          </ng-template>
          <ng-template pTemplate="emptymessage">
            <tr><td colspan="11" class="muted">{{ term() ? 'Nothing matches the search.' : 'No Completed TASAF yet — a TASAF is Completed when its TPARs are done.' }}</td></tr>
          </ng-template>
        </p-table>
      </section>
    } @else {
      <!-- ── The form ── -->
      @let p = pe()!;
      <section class="card">
        <div class="sec-head">
          <h3>{{ p.kind === 'Promo' ? 'Customer Specific Promo Post Evaluation' : 'Post Activity Form' }}</h3>
          <p-tag [value]="p.status" [severity]="peSev(p.status)" />
          <span class="muted">TASAF {{ p.refNo }} · <p-tag [value]="p.tasafStatus" [severity]="tasafSev(p.tasafStatus)" /></span>
          @if (!p.canEdit) { <span class="muted"><i class="pi pi-lock"></i> {{ lockReason() }}</span> }
          <span class="acts"><p-button icon="pi pi-external-link" label="Open TASAF" [text]="true" size="small" (onClick)="openTasaf(p.tasafId)" /></span>
        </div>
        <div class="title">{{ p.title }}</div>
        <div class="grid2">
          <div class="kv"><label>Sales Group</label><span>{{ p.salesGroupName }}</span></div>
          <div class="kv"><label>Area</label><span>{{ p.areaNames }}</span></div>
          <div class="kv"><label>Customer</label><span>{{ p.chainNames }}</span></div>
          <div class="kv"><label>Customer Code</label><span class="small">{{ p.custKeys }}</span></div>
          <div class="kv"><label>Promo Type</label><span>{{ p.programTypeName }}</span></div>
          <div class="kv"><label>Objective</label><span>{{ p.objectiveName }}</span></div>
          <div class="kv"><label>Target Market</label><span>{{ p.targetMarketName }}</span></div>
          <div class="kv"><label>Channel</label><span>{{ p.channelNames }}</span></div>
          <div class="kv"><label>Product Group</label><span>{{ p.productCategoryNames }}</span></div>
          <div class="kv"><label>Category</label><span>{{ p.segmentNames }}</span></div>
          <div class="kv wide"><label>{{ p.kind === 'Promo' ? 'Promo Scheme' : 'Mechanics' }}</label><span class="pre">{{ p.mechanics }}</span></div>
        </div>
      </section>

      <section class="card">
        <h3>Execution</h3>
        <table class="ex">
          <tr><th></th><th>Start Date</th><th>End Date</th></tr>
          <tr><td>Planned</td><td>{{ p.plannedStartDate | date:'MMM d, y' }}</td><td>{{ p.plannedEndDate | date:'MMM d, y' }}</td></tr>
          <tr><td>Actual <span class="req">*</span></td>
            <td><p-datepicker [(ngModel)]="actualStart" dateFormat="M d, yy" [showIcon]="true" [disabled]="!p.canEdit" appendTo="body" (ngModelChange)="touch()" /></td>
            <td><p-datepicker [(ngModel)]="actualEnd" dateFormat="M d, yy" [showIcon]="true" [disabled]="!p.canEdit" appendTo="body" [minDate]="actualStart" (ngModelChange)="touch()" /></td></tr>
          @if (p.kind === 'Promo') {
            <tr><td>Number of Doors</td><td>Planned <strong>{{ p.plannedDoors ?? 0 }}</strong></td>
              <td>Actual <span class="req">*</span> <p-inputNumber [(ngModel)]="actualDoors" [min]="0" [disabled]="!p.canEdit" inputStyleClass="w-num" (ngModelChange)="touch()" /></td></tr>
          }
        </table>
        <label class="blk">{{ p.kind === 'Promo' ? 'Explanation (what worked / what didn\\'t)' : 'Remarks' }} <span class="req">*</span></label>
        <textarea pTextarea [autoResize]="true" rows="3" maxlength="4000" class="w-full" [(ngModel)]="explanation" [disabled]="!p.canEdit" (ngModelChange)="touch()"></textarea>
      </section>

      @if (p.kind === 'Promo') {
        <section class="card">
          <h3>Sales Evaluation</h3>
          @if (!p.lines.length) { <p class="muted">The TASAF lists no promoted SKUs.</p> } @else {
          <div class="scroll">
          <table class="se">
            <thead>
              <tr><th rowspan="3">Promoted SKU</th><th rowspan="3" class="num">LSP / Cs</th>
                <th colspan="2">Pre-Promo</th><th colspan="4">Promo Period</th><th colspan="2">Post-Promo</th><th colspan="4">Incremental</th></tr>
              <tr><th colspan="2">Mo Avg</th><th colspan="2">Planned</th><th colspan="2">Actual</th><th colspan="2">1 Month</th>
                <th colspan="2">Planned</th><th colspan="2">Actual</th></tr>
              <tr><th>Cs</th><th>Pesos</th><th>Cs</th><th>Pesos</th><th>Cs <span class="req">*</span></th><th>Pesos</th><th>Cs</th><th>Pesos</th>
                <th>%</th><th>Pesos</th><th>%</th><th>Pesos</th></tr>
            </thead>
            <tbody>
            @for (l of p.lines; track l.lineNo; let i = $index) {
              <tr>
                <td class="sku">{{ l.cProdNo }} {{ l.sku }}</td>
                <td class="num">{{ l.lspPerCase | number:'1.2-2' }}</td>
                <td class="num">{{ l.prePromoCs | number:'1.0-2' }}</td><td class="num">{{ (l.prePromoCs ?? 0) * l.lspPerCase | number:'1.2-2' }}</td>
                <td class="num">{{ l.plannedCs | number:'1.0-2' }}</td><td class="num">{{ l.plannedCs * l.lspPerCase | number:'1.2-2' }}</td>
                <td class="in"><p-inputNumber [ngModel]="actualCs()[i]" (ngModelChange)="setCs(actualCs, i, $event)" [min]="0" [maxFractionDigits]="2" [disabled]="!p.canEdit" inputStyleClass="w-cs" /></td>
                <td class="num">{{ (actualCs()[i] ?? 0) * l.lspPerCase | number:'1.2-2' }}</td>
                <td class="in"><p-inputNumber [ngModel]="postCs()[i]" (ngModelChange)="setCs(postCs, i, $event)" [min]="0" [maxFractionDigits]="2" [disabled]="!p.canEdit" inputStyleClass="w-cs" /></td>
                <td class="num">{{ (postCs()[i] ?? 0) * l.lspPerCase | number:'1.2-2' }}</td>
                <td class="num">{{ pct(l.plannedCs, l.prePromoCs) }}</td><td class="num">{{ incr(l.plannedCs, l) | number:'1.2-2' }}</td>
                <td class="num">{{ pct(actualCs()[i], l.prePromoCs) }}</td><td class="num">{{ incr(actualCs()[i], l) | number:'1.2-2' }}</td>
              </tr>
            }
            </tbody>
            <tfoot>
              @let s = sums();
              <tr><td>TOTAL</td><td></td><td></td><td class="num">{{ s.pre | number:'1.2-2' }}</td><td></td><td class="num">{{ s.planned | number:'1.2-2' }}</td>
                <td></td><td class="num">{{ s.actual | number:'1.2-2' }}</td><td></td><td class="num">{{ s.post | number:'1.2-2' }}</td>
                <td class="num">{{ pct(s.planned, s.pre) }}</td><td class="num">{{ s.planned - s.pre | number:'1.2-2' }}</td>
                <td class="num">{{ pct(s.actual, s.pre) }}</td><td class="num">{{ s.actual - s.pre | number:'1.2-2' }}</td></tr>
            </tfoot>
          </table>
          </div>
          <small class="muted">Pesos = Cs × LSP / Cs. Incremental = promo period vs the pre-promo monthly average (P3M, else SPLY) from the TASAF.</small>
          }
        </section>
      }

      <div class="pair">
      <section class="card">
        <h3>Cost Evaluation</h3>
        <table class="ce">
          <tr><th></th><th class="num">Planned Cost</th><th class="num">Actual Cost <span class="req">*</span></th><th class="num">Savings</th><th class="num">Variance</th><th></th></tr>
          <tr><td><strong>Total Cost / Budget</strong></td>
            <td class="num">{{ p.plannedCost | number:'1.2-2' }}</td>
            <td class="in"><p-inputNumber [(ngModel)]="actualCost" [min]="0" [minFractionDigits]="2" [maxFractionDigits]="2" [disabled]="!p.canEdit" inputStyleClass="w-amt" (ngModelChange)="touch()" /></td>
            <td class="num" [class.bad]="savings() < 0">{{ actualCost === null ? '' : (savings() | number:'1.2-2') }}</td>
            <td class="num">{{ actualCost === null || !p.plannedCost ? '' : ((actualCost / p.plannedCost - 1) | percent:'1.0-2') }}</td>
            <td>@if (actualCost !== null) {
              <span class="flag big" [class.over]="over()"><i class="pi" [class.pi-check-circle]="!over()" [class.pi-flag-fill]="over()"></i>
                {{ over() ? 'Over the approved budget' : 'Within the approved budget' }}</span> }</td></tr>
          @if (p.kind === 'Promo') {
            <tr><td><strong>CTS (Promoted Value)</strong></td>
              <td class="num">{{ ratio(p.plannedCost, sums().planned) }}</td>
              <td class="num">{{ ratio(actualCost, sums().actual) }}</td><td></td><td></td><td></td></tr>
          }
        </table>
        <small class="muted">Approved TPARs of this TASAF: {{ p.tparApprovedTotal | number:'1.2-2' }} (the starting actual cost).
          @if (p.kind === 'Promo') { CTS = cost ÷ promo-period sales (planned ÷ planned, actual ÷ actual). }</small>
      </section>
        <section class="card">
          <div class="sec-head">
            <h3>Attachments</h3>
            <span class="muted">Photos, proof of execution, receipts — PDF, JPG, PNG, Excel, Word or PowerPoint, up to 10 MB each. Photos are compressed automatically.</span>
            @if (p.canAttach) {
              <span class="acts">
                <input #fileIn type="file" accept=".pdf,.jpg,.jpeg,.png,.xlsx,.docx,.pptx" multiple hidden (change)="upload(fileIn)" />
                <p-button label="Add files" icon="pi pi-paperclip" size="small" [outlined]="true" [loading]="uploading()" (onClick)="fileIn.click()" />
              </span>
            }
          </div>
          @if (p.attachments.length) {
            <ul class="files">
              @for (a of p.attachments; track a.id) {
                <li>
                  <i class="pi" [class]="fileIcon(a.contentType)"></i>
                  <a href="" (click)="$event.preventDefault(); openFile(a)">{{ a.fileName }}</a>
                  <span class="muted">{{ a.size / 1024 | number:'1.0-0' }} KB · {{ a.uploadedBy }} · {{ utc(a.uploadedAt) | date:'MMM d, y h:mm a' }}</span>
                  @if (p.canAttach) {
                    <p-button icon="pi pi-trash" size="small" [text]="true" severity="danger" pTooltip="Remove" (onClick)="removeFile(a)" />
                  }
                </li>
              }
            </ul>
          } @else {
            <p class="muted small">No files attached.</p>
          }
        </section>
      </div>

      @if (p.kind === 'Promo') {
        <section class="card">
          <h3>Conclusion / Recommendation <span class="req">*</span></h3>
          <textarea pTextarea [autoResize]="true" rows="3" maxlength="4000" class="w-full" [(ngModel)]="conclusion" [disabled]="!p.canEdit" (ngModelChange)="touch()"
            placeholder="What worked / what didn't"></textarea>
        </section>
      }

      <section class="card">
        <h3>Approval</h3>
        <div class="sign">
          <div><label>Prepared by</label><strong>{{ p.proponentName }}</strong><small class="muted">Proponent</small></div>
          @for (s of p.steps; track s.seq) {
            <div [class.skip]="s.status === 'Skipped'"><label>{{ s.roleName }}</label><strong>{{ s.userName || '—' }}</strong>
              <small><p-tag [value]="s.status" [severity]="stepSev(s.status)" />
                @if (s.actedAt) { {{ utc(s.actedAt) | date:'MMM d, y h:mm a' }} }</small>
              @if (s.comment) { <small class="muted">"{{ s.comment }}"</small> }</div>
          }
        </div>
        @if (!p.steps.length) { <small class="muted">Signed by the TASAF's approvers ({{ p.approvalTypeName || 'its approval matrix' }}) once submitted.</small> }
        @if (p.createdAt) { <small class="muted block">Created {{ p.createdByName === 'Automatic' ? 'automatically when the TASAF was Completed' : 'by ' + p.createdByName }} {{ utc(p.createdAt) | date:'MMM d, y h:mm a' }}
          @if (p.updatedAt) { · updated by {{ p.updatedBy }} {{ utc(p.updatedAt) | date:'MMM d, y h:mm a' }} }</small> }
      </section>
    }

      <p-dialog [header]="dlgTitle()" [visible]="dlg() !== null" (visibleChange)="!$event && dlg.set(null)" [modal]="true" [style]="{ width: '32rem' }">
        <p class="muted">{{ dlgHelp() }}</p>
        <label>{{ dlg() === 'approve' ? 'Comment' : 'Reason' }} <span class="req">*</span></label>
        <textarea pTextarea rows="3" maxlength="1000" class="w-full" [ngModel]="comment()" (ngModelChange)="comment.set($event)"></textarea>
        <ng-template pTemplate="footer">
          <p-button label="Close" severity="secondary" [text]="true" (onClick)="dlg.set(null)" />
          <p-button [label]="dlgTitle()" [severity]="dlg() === 'approve' ? 'success' : 'warn'" [disabled]="!comment().trim() || acting()" [loading]="acting()" (onClick)="doDialog()" />
        </ng-template>
      </p-dialog>
    </div>
  `,
  styles: [`
    .page { padding: 0.25rem; }
    .card { background: var(--p-surface-card); border-radius: 8px; padding: 1rem 1.25rem; margin-bottom: 0.85rem; }
    .card h3 { margin: 0 0 0.6rem; font-size: 0.95rem; color: #800000; }
    .sec-head { display: flex; align-items: center; gap: 0.75rem; margin-bottom: 0.6rem; flex-wrap: wrap; }
    .sec-head h3 { margin: 0; }
    .sec-head .acts, .sec-head .chk { margin-left: auto; display: flex; align-items: center; gap: 0.35rem; }
    .title { font-weight: 600; font-size: 1.05rem; margin-bottom: 0.6rem; }
    .grid2 { display: grid; grid-template-columns: 1fr 1fr; gap: 0.35rem 1.5rem; }
    .kv { display: flex; gap: 0.75rem; font-size: 0.875rem; }
    .kv label { flex: 0 0 8rem; font-weight: 500; color: var(--p-text-muted-color); }
    .kv.wide { grid-column: 1 / -1; }
    .pre { white-space: pre-wrap; }
    .small { font-size: 0.8rem; }
    .ex, .ce { border-collapse: collapse; margin-bottom: 0.75rem; }
    .ex th, .ex td, .ce th, .ce td { padding: 0.3rem 0.75rem; text-align: left; font-size: 0.875rem; }
    .ce th.num, .ce td.num { text-align: right; }
    .scroll { overflow-x: auto; }
    .se { border-collapse: collapse; width: 100%; font-size: 0.8rem; }
    .se th, .se td { border: 1px solid var(--p-content-border-color); padding: 0.25rem 0.4rem; }
    .se th { background: #800000; color: #fff; text-align: center; font-weight: 600; }
    .se tfoot td { font-weight: 700; background: var(--p-content-hover-background); }
    .se .sku { min-width: 14rem; }
    .num { text-align: right; white-space: nowrap; }
    .in { padding: 0.1rem 0.2rem !important; min-width: 5.5rem; }
    .in p-inputnumber, :host ::ng-deep .in .p-inputnumber { display: flex; width: 100%; }
    :host ::ng-deep .w-cs { width: 100%; min-width: 0; text-align: right; }
    .pair { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); gap: 0 1.5rem; align-items: start; }
    .pair > section { overflow-x: auto; }
    .files { list-style: none; margin: 0; padding: 0; }
    .files li { display: flex; align-items: center; gap: 0.6rem; padding: 0.2rem 0; font-size: 0.875rem; border-bottom: 1px solid var(--p-content-border-color); }
    .files li .muted { font-size: 0.75rem; }
    .files li p-button { margin-left: auto; }
    .files a { color: #800000; text-decoration: none; font-weight: 500; }
    .files a:hover { text-decoration: underline; }
    :host ::ng-deep .w-num { width: 6rem; }
    :host ::ng-deep .w-amt { width: 10rem; text-align: right; }
    .blk { display: block; font-weight: 500; font-size: 0.85rem; margin: 0.25rem 0; }
    .flag { color: #2e7d32; font-weight: 600; }
    .flag.over { color: #c62828; }
    .flag.big { display: inline-flex; gap: 0.35rem; align-items: center; padding: 0.2rem 0.6rem; border-radius: 4px; background: #e8f5e9; }
    .flag.big.over { background: #ffebee; }
    .bad { color: #c62828; }
    .sign { display: flex; flex-wrap: wrap; gap: 1rem 2rem; margin-bottom: 0.5rem; }
    .sign > div { display: flex; flex-direction: column; gap: 0.15rem; min-width: 10rem; }
    .sign label { font-size: 0.75rem; color: var(--p-text-muted-color); }
    .sign .skip { opacity: 0.55; }
    .block { display: block; margin-top: 0.4rem; }
    .muted { color: var(--p-text-muted-color); }
    .req { color: #c62828; }
    .nowrap { white-space: nowrap; }
    .wide { min-width: 14rem; }
    :host ::ng-deep .pe-list thead th { white-space: nowrap; }
    .me { color: #800000; }
    tr.mine td { background: #fff8e1; }
    @media (max-width: 800px) { .grid2, .pair { grid-template-columns: 1fr; } }
  `]
})
export class TasafPostEvalPageComponent implements OnInit, OnDestroy {
  private api     = inject(TasafPostEvalService);
  private toolbar = inject(GlobalToolbarService);
  private msg     = inject(MessageService);
  private confirm = inject(ConfirmationService);
  private router  = inject(Router);
  private tabBar  = inject(TabBarService);

  protected readonly utc = utc;
  protected rows    = signal<PostEvalListItem[]>([]);
  protected loading = signal(false);
  protected saving  = signal(false);
  protected acting  = signal(false);
  protected term    = signal('');
  /**
   * Every status is listed. Still to do on top: For Approval first, then Draft / Returned / not started, then Approved;
   * within a group, the ones waiting for you first, then the newest.
   */
  private static rank(r: PostEvalListItem) {
    return r.status === 'For Approval' ? 0 : r.status === 'Approved' ? 2 : 1;
  }
  protected filtered = computed(() => {
    const q = this.term().trim().toLowerCase();
    const at = (r: PostEvalListItem) => r.decidedAt ?? r.submittedAt ?? '';
    return this.rows()
      .filter(r => !q || [r.refNo, r.title, r.salesGroupName, r.proponentName, r.programTypeName, r.status, r.currentApproverName]
        .some(v => (v ?? '').toLowerCase().includes(q)))
      .sort((a, b) => TasafPostEvalPageComponent.rank(a) - TasafPostEvalPageComponent.rank(b)
        || Number(b.awaitingMe) - Number(a.awaitingMe)
        || at(b).localeCompare(at(a))
        || (b.refNo ?? '').localeCompare(a.refNo ?? ''));
  });
  /** Count per status for the header. */
  protected counts = computed(() => {
    const order = ['For Approval', 'Draft', 'Returned', 'Not started', 'Approved'];
    const n = new Map<string, number>();
    this.rows().forEach(r => { const s = r.status ?? 'Not started'; n.set(s, (n.get(s) ?? 0) + 1); });
    return order.filter(s => n.has(s)).map(s => ({ status: s, n: n.get(s)!, sev: s === 'Not started' ? 'info' as const : this.peSev(s) }));
  });

  // ── The form ──
  protected pe = signal<PostEval | null>(null);
  protected actualStart: Date | null = null;
  protected actualEnd: Date | null = null;
  protected actualDoors: number | null = null;
  protected actualCost: number | null = null;
  protected explanation = '';
  protected conclusion = '';
  protected actualCs = signal<(number | null)[]>([]);
  protected postCs   = signal<(number | null)[]>([]);
  /** Bumped on every edit so computed() values that read plain fields re-run. */
  private rev = signal(0);
  private dirtyFlag = signal(false);
  touch() { this.rev.update(v => v + 1); this.dirtyFlag.set(true); }

  protected sums = computed<Sums>(() => {
    const p = this.pe();
    const s: Sums = { pre: 0, planned: 0, actual: 0, post: 0 };
    p?.lines.forEach((l, i) => {
      s.pre += (l.prePromoCs ?? 0) * l.lspPerCase;
      s.planned += l.plannedCs * l.lspPerCase;
      s.actual += (this.actualCs()[i] ?? 0) * l.lspPerCase;
      s.post += (this.postCs()[i] ?? 0) * l.lspPerCase;
    });
    return s;
  });
  protected savings = computed(() => { this.rev(); return (this.pe()?.plannedCost ?? 0) - (this.actualCost ?? 0); });
  protected over    = computed(() => { this.rev(); return this.actualCost !== null && this.actualCost > (this.pe()?.plannedCost ?? 0); });

  protected lockReason = computed(() => {
    const p = this.pe()!;
    if (p.tasafStatus === 'Closed') return 'Approved — the TASAF is Closed.';
    if (p.status === 'For Approval') return `For approval — with ${p.currentApproverName} (${p.currentRoleCode}).`;
    return `Only ${p.proponentName} (the proponent) or an Admin can fill it in.`;
  });

  // Toolbar
  private saveOff    = computed(() => !this.pe()?.canEdit || this.acting());
  private submitOff  = computed(() => !this.pe()?.canSubmit || this.saving() || this.acting());
  private decideOff  = computed(() => { const p = this.pe(); return !p || !(p.awaitingMe || p.canAdminDecide) || this.acting(); });
  private revertOff  = computed(() => !this.pe()?.canRevert || this.acting());
  protected dlg      = signal<'approve' | 'return' | 'revert' | null>(null);
  protected comment  = signal('');
  protected dlgTitle = computed(() => ({ approve: 'Approve', return: 'Return', revert: 'Revert' }[this.dlg() ?? 'approve']));
  protected dlgHelp  = computed(() => {
    const p = this.pe();
    switch (this.dlg()) {
      case 'approve': return p && !p.awaitingMe ? `You sign the ${p.currentRoleCode} step for ${p.currentApproverName} (Admin); they are notified.`
        : 'Approve the post evaluation. After the last approver, the TASAF is Closed.';
      case 'return':  return 'Back to the proponent to change and resubmit.';
      default:        return p?.status === 'Approved' ? 'Back to the proponent as Returned; the TASAF goes from Closed back to Completed.'
        : 'Withdraw it from approval; it comes back as Returned so it can be changed and resubmitted.';
    }
  });

  private navSub?: Subscription;
  private currentTabKey = TAB;

  ngOnInit() {
    this.setToolbar();
    this.handleNav();
    this.navSub = this.router.events.pipe(filter(e => e instanceof NavigationEnd)).subscribe(() => this.handleNav());
  }

  ngOnDestroy() {
    this.tabBar.unregisterDirtyChecker(this.currentTabKey);
    this.toolbar.clear();
    this.navSub?.unsubscribe();
  }

  private setToolbar() {
    if (!this.pe()) {
      this.toolbar.set({
        title: 'Post Evaluation',
        refresh: { onClick: () => this.load(), loading: this.loading },
        search: t => this.term.set(t),
      });
      return;
    }
    const p = this.pe()!;
    this.toolbar.set({
      title: 'Post Evaluation',
      save:    { onClick: () => this.save(), loading: this.saving, disabled: this.saveOff },
      submit:  { label: 'Submit for approval', onClick: () => this.submit(), disabled: this.submitOff },
      approve: { label: p.awaitingMe || !p.canAdminDecide ? `Approve${p.currentRoleCode ? ' as ' + p.currentRoleCode : ''}`
                                                          : `Approve for ${p.currentApproverName} (Admin)`,
                 onClick: () => this.openDlg('approve'), disabled: this.decideOff },
      return:  { onClick: () => this.openDlg('return'), disabled: this.decideOff },
      revert:  { label: 'Revert post evaluation', onClick: () => this.openDlg('revert'), disabled: this.revertOff },
      refresh: { onClick: () => this.reopen(), loading: this.loading },
      list:    { onClick: () => this.guard(() => { this.tabBar.switchToDefaultTab(TAB); }) },
    });
  }

  peSev(s: string) { return POST_EVAL_STATUS_SEVERITY[s] ?? 'info'; }
  tasafSev(s: string) { return TASAF_STATUS_SEVERITY[s] ?? 'info'; }
  stepSev(s: string) { return s === 'Approved' ? 'success' : s === 'Pending' ? 'warn' : s === 'Returned' ? 'contrast' : 'secondary'; }

  /** Notification { postEvalId: tasafId } / email "?id=" / a tab's state → the form; else the list. */
  private handleNav() {
    const qid = Number(new URLSearchParams(location.search).get('id'));
    if (qid > 0) history.replaceState({ ...history.state, postEvalId: qid }, '', location.pathname);
    const id = history.state?.['postEvalId'] as number | undefined;
    if (id) {
      if (id !== this.pe()?.tasafId) this.fetch(id);
      return;
    }
    if (this.pe()) this.showList();
    else this.load();
  }

  load() {
    this.loading.set(true);
    this.api.list().subscribe({
      next: res => { this.loading.set(false); this.rows.set(res.data ?? []); },
      error: err => { this.loading.set(false); this.fail(err, 'Failed to load the list.'); }
    });
  }

  private showList() {
    this.tabBar.unregisterDirtyChecker(this.currentTabKey);
    this.currentTabKey = TAB;
    this.pe.set(null);
    this.setToolbar();
    this.load();
  }

  open(r: PostEvalListItem | null) {
    if (r) this.router.navigate([TAB], { state: { postEvalId: r.tasafId } });
  }

  openTasaf(id: number) { this.router.navigate(['/tasafs'], { state: { tasafId: id } }); }

  private fetch(tasafId: number) {
    this.loading.set(true);
    this.api.get(tasafId).subscribe({
      next: res => { this.loading.set(false); this.show(res.data!); },
      error: err => { this.loading.set(false); this.fail(err, 'Failed to open the post evaluation.'); if (!this.pe()) this.load(); }
    });
  }

  private reopen() { const p = this.pe(); if (p) this.guard(() => this.fetch(p.tasafId)); }

  // ── Attachments ──
  protected uploading = signal(false);

  /** Only the file list / status change — typed but unsaved form values stay. */
  private takeFiles(p: PostEval) {
    this.pe.update(cur => cur && cur.tasafId === p.tasafId
      ? { ...cur, id: p.id, status: p.status, attachments: p.attachments, canAttach: p.canAttach, createdAt: p.createdAt, createdByName: p.createdByName }
      : p);
  }

  upload(input: HTMLInputElement) {
    const p = this.pe(); const files = Array.from(input.files ?? []); input.value = '';
    if (!p || !files.length) return;
    this.uploading.set(true);
    let before = 0, after = 0;
    const next = async (i: number) => {
      if (i >= files.length) {
        this.uploading.set(false);
        if (after < before) this.ok(`Photos compressed: ${this.kb(before)} → ${this.kb(after)}.`);
        return;
      }
      const file = await this.compress(files[i]);
      if (file !== files[i]) { before += files[i].size; after += file.size; }
      if (file.size > 10 * 1024 * 1024) { this.fail(null, `${file.name} is over 10 MB.`); next(i + 1); return; }
      this.api.addAttachment(p.tasafId, file).subscribe({
        next: r => { if (r.data) this.takeFiles(r.data); next(i + 1); },
        error: err => { this.fail(err, `Failed to upload ${files[i].name}.`); next(i + 1); }
      });
    };
    next(0);
  }

  /**
   * Photos / screenshots (JPG, PNG) are scaled to fit 1920 × 1920 and re-saved (a PNG stays PNG when it is small enough,
   * else JPEG at 85%) — kept only when smaller. PDF and Office files are already compressed formats and go as they are.
   */
  private async compress(file: File): Promise<File> {
    if (!/^image\/(png|jpeg)$/.test(file.type) || file.size < 200 * 1024) return file;
    try {
      const s = await shrinkImage(file, 1920, 1920, 400 * 1024);
      return s.blob.size < file.size ? new File([s.blob], s.fileName, { type: s.type }) : file;
    } catch { return file; }
  }

  private kb(bytes: number) {
    return bytes >= 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : `${Math.round(bytes / 1024)} KB`;
  }

  openFile(a: PostEvalAttachment) {
    const p = this.pe(); if (!p) return;
    this.api.attachment(p.tasafId, a.id).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob.type ? blob : new Blob([blob], { type: a.contentType }));
        const inline = a.contentType === 'application/pdf' || a.contentType.startsWith('image/');
        if (inline) window.open(url, '_blank');
        else { const link = document.createElement('a'); link.href = url; link.download = a.fileName; link.click(); }
        setTimeout(() => URL.revokeObjectURL(url), 60_000);
      },
      error: err => this.fail(err, 'Failed to open the file.')
    });
  }

  removeFile(a: PostEvalAttachment) {
    const p = this.pe(); if (!p) return;
    this.confirm.confirm({
      header: 'Remove file', message: `Remove ${a.fileName}?`, icon: 'pi pi-trash',
      acceptLabel: 'Remove', rejectLabel: 'Keep', acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.api.deleteAttachment(p.tasafId, a.id).subscribe({
        next: r => { if (r.data) this.takeFiles(r.data); },
        error: err => this.fail(err, 'Failed to remove the file.')
      })
    });
  }

  fileIcon(type: string) {
    return type === 'application/pdf' ? 'pi-file-pdf' : type.startsWith('image/') ? 'pi-image'
      : type.includes('spreadsheet') ? 'pi-file-excel' : type.includes('word') ? 'pi-file-word' : 'pi-file';
  }

  private show(p: PostEval) {
    this.pe.set(p);
    this.actualStart = parseIsoDate(p.actualStartDate);
    this.actualEnd = parseIsoDate(p.actualEndDate);
    this.actualDoors = p.actualDoors;
    this.actualCost = p.actualCost;
    this.explanation = p.explanation;
    this.conclusion = p.conclusion;
    this.actualCs.set(p.lines.map(l => l.actualCs));
    this.postCs.set(p.lines.map(l => l.postPromoCs));
    this.dirtyFlag.set(false);
    this.rev.update(v => v + 1);
    const key = `${TAB}#${p.tasafId}`;
    if (key !== this.currentTabKey) {
      this.tabBar.unregisterDirtyChecker(this.currentTabKey);
      this.currentTabKey = key;
    }
    this.tabBar.openTab({ key, label: `Post Eval - ${p.refNo}`, route: TAB, icon: 'pi-check-square', state: { postEvalId: p.tasafId } });
    this.tabBar.registerDirtyChecker(key, () => this.dirtyFlag() && !!this.pe()?.canEdit);
    this.setToolbar();
  }

  setCs(sig: WritableSignal<(number | null)[]>, i: number, v: number | null) {
    sig.update(a => { const c = [...a]; c[i] = v; return c; });
    this.touch();
  }

  pct(n: number | null, base: number | null): string {
    if (n === null || !base) return '';
    return ((n / base - 1) * 100).toFixed(1) + '%';
  }
  incr(n: number | null, l: { prePromoCs: number | null; lspPerCase: number }): number | null {
    if (n === null || l.prePromoCs === null) return null;
    return (n - l.prePromoCs) * l.lspPerCase;
  }
  ratio(cost: number | null, sales: number): string {
    if (cost === null || !sales) return '';
    return (cost / sales * 100).toFixed(2) + '%';
  }

  private body(): SavePostEval {
    const p = this.pe()!;
    return {
      actualStartDate: toIsoDate(this.actualStart), actualEndDate: toIsoDate(this.actualEnd),
      actualDoors: this.actualDoors, explanation: this.explanation.trim(), conclusion: this.conclusion.trim(),
      actualCost: this.actualCost,
      lines: p.lines.map((l, i) => ({ lineNo: l.lineNo, actualCs: this.actualCs()[i] ?? null, postPromoCs: this.postCs()[i] ?? null }))
    };
  }

  save() {
    const p = this.pe();
    if (!p || this.saveOff()) return;
    this.saving.set(true);
    this.api.save(p.tasafId, this.body()).subscribe({
      next: res => { this.saving.set(false); this.show(res.data!); this.ok('Post evaluation saved.'); },
      error: err => { this.saving.set(false); this.fail(err, 'Failed to save.'); }
    });
  }

  submit() {
    const p = this.pe();
    if (!p || this.submitOff()) return;
    const over = this.over();
    this.confirm.confirm({
      header: 'Submit post evaluation?', icon: over ? 'pi pi-flag-fill' : 'pi pi-send',
      message: `Submit the ${p.kind === 'Promo' ? 'promo post evaluation' : 'post activity form'} of ${p.refNo} to its approvers?`
        + (over ? ' The actual cost is OVER the approved budget — it is flagged red for the approvers.' : ''),
      acceptLabel: 'Submit', rejectLabel: 'Cancel',
      accept: () => this.act(this.api.submit(p.tasafId, this.body()), r => `Submitted — now with ${r.currentApproverName} (${r.currentRoleCode}).`)
    });
  }

  openDlg(kind: 'approve' | 'return' | 'revert') { this.comment.set(''); this.dlg.set(kind); }

  doDialog() {
    const p = this.pe();
    const kind = this.dlg();
    const c = this.comment().trim();
    if (!p || !kind || !c) return;
    const call = kind === 'revert' ? this.api.revert(p.tasafId, c) : this.api.decide(p.tasafId, kind, c);
    this.act(call, r => {
      this.dlg.set(null);
      if (kind === 'revert') return 'Post evaluation reverted — back to the proponent as Returned.';
      if (kind === 'return') return 'Returned to the proponent.';
      return r.tasafStatus === 'Closed' ? `Approved — TASAF ${r.refNo} is now Closed.` : `Approved — now with ${r.currentApproverName} (${r.currentRoleCode}).`;
    });
  }

  private act(call: Observable<ApiResponse<PostEval>>, done: (p: PostEval) => string) {
    this.acting.set(true);
    call.subscribe({
      next: res => { this.acting.set(false); this.show(res.data!); this.ok(done(res.data!)); },
      error: err => { this.acting.set(false); this.fail(err, 'Failed.'); }
    });
  }

  private guard(next: () => void) {
    if (!this.dirtyFlag() || !this.pe()?.canEdit) { next(); return; }
    this.confirm.confirm({
      header: 'Discard changes?', message: 'The post evaluation has unsaved changes. Discard them?', icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Discard', rejectLabel: 'Keep editing', accept: next
    });
  }

  private ok(detail: string) { this.msg.add({ severity: 'success', summary: 'Post Evaluation', detail, life: 6000 }); }
  private fail(err: any, fallback: string) {
    this.msg.add({ severity: 'error', summary: 'Post Evaluation', detail: err?.error?.message ?? fallback, life: 8000 });
  }
}
