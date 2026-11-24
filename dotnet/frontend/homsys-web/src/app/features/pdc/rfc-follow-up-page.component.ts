import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { DialogModule } from 'primeng/dialog';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { AuthService } from '../../core/services/auth.service';
import { GlobalToolbarService } from '../../core/services/global-toolbar.service';
import { CollectionService } from '../../core/services/collection.service';
import { RfcCandidateDto, RfcFollowUpDto } from '../../core/models/collection.model';

/**
 * PDC › Returns awaiting RFC (user 2026-11-24): "We cannot download in AR unless the RFC for this has been posted. therefore
 * the CNC verifier is enforced to follow up on the RFC posting." Each "Return from customer" deduction holds its receipt's
 * OR / RPDC until it's linked to the RFC posted in HOMS A/R. Same-amount single RFCs link themselves after every AROF4HO
 * sync; the rest are picked here.
 */
@Component({
  selector: 'app-rfc-follow-up-page',
  standalone: true,
  imports: [DatePipe, DecimalPipe, FormsModule, DialogModule, ToastModule],
  providers: [MessageService],
  template: `
<p-toast />
<div class="pdcm rfu">
  <div class="pdcm-head">
    <div class="pdcm-head-text">
      <div class="pdcm-eyebrow">PDC · CNC follow-up</div>
      <h1>Returns awaiting RFC</h1>
      <div class="pdcm-sub">A receipt with a "Return from customer" is sent to HOMS only when every return on it has its RFC posted in
        HOMS A/R. A single RFC of the same amount is linked automatically after each AROF4HO sync; pick the others here.</div>
    </div>
    <div class="pdcm-head-acts">
      <label class="chk"><input type="checkbox" [ngModel]="showLinked()" (ngModelChange)="showLinked.set($event); load()" /> Linked in the last 30 days</label>
      <button type="button" class="pdcm-btn" [disabled]="busy()" (click)="autoLink()">Re-check RFCs</button>
    </div>
  </div>

  <div class="pdcm-panel">
    <div class="tw">
      <table class="t">
        <thead><tr>
          <th>Branch</th><th>Receipt</th><th>Customer</th><th>Invoice</th><th class="r">Return</th><th>Waiting</th>
          <th>HOMS</th><th>RFCs on the invoice (HOMS A/R)</th><th></th>
        </tr></thead>
        <tbody>
          @for (r of rows(); track r.deductionId) {
            <tr [class.done]="r.rfcNo">
              <td>{{ r.branch }}</td>
              <td>
                @if (canOpen) { <a class="lnk" (click)="open(r)">{{ r.receiptType }} {{ r.receiptNo }}</a> } @else { {{ r.receiptType }} {{ r.receiptNo }} }
                <div class="pdcm-sub">{{ r.receiptDate | date:'MM/dd/yyyy' }} · {{ r.status === 'ForVerification' ? 'For verification' : r.status }}</div>
              </td>
              <td>{{ r.custName }}<div class="pdcm-sub pdcm-mono">{{ r.custKey }}</div></td>
              <td class="pdcm-mono">{{ r.invoiceNo }}<div class="pdcm-sub">{{ r.invoiceDate | date:'MM/dd/yyyy' }}</div></td>
              <td class="r pdcm-mono">{{ r.amount | number:'1.2-2' }}@if (r.reference) {<div class="pdcm-sub">{{ r.reference }}</div>}</td>
              <td>{{ r.daysWaiting }} day{{ r.daysWaiting === 1 ? '' : 's' }}</td>
              <td><span class="st" [class.hold]="r.posting?.includes('hold')">{{ r.posting || '—' }}</span></td>
              <td>
                @if (r.rfcNo) {
                  <span class="ok">RFC {{ r.rfcNo }}</span>
                  <div class="pdcm-sub">linked {{ r.rfcLinkedAt | date:'MM/dd HH:mm' }} by {{ r.rfcLinkedBy === 'auto' ? 'auto-match' : r.rfcLinkedBy }}</div>
                } @else if (r.rfcs.length === 0) {
                  <span class="pdcm-dim">None posted yet</span>
                } @else {
                  @for (c of r.rfcs; track c.rfcNo) {
                    <div class="cand" [class.taken]="c.linkedTo">
                      <span class="pdcm-mono">{{ c.rfcNo }}</span> · {{ c.rfcDate | date:'MM/dd/yyyy' }} ·
                      <span class="pdcm-mono" [class.diff]="c.amount !== r.amount">{{ c.amount | number:'1.2-2' }}</span>
                      @if (c.linkedTo) { <span class="pdcm-sub"> — {{ c.linkedTo }}</span> }
                    </div>
                  }
                }
              </td>
              <td class="r">
                @if (!r.rfcNo && free(r).length > 0) { <button type="button" class="pdcm-btn primary" (click)="pick(r)">Link RFC</button> }
              </td>
            </tr>
          } @empty {
            <tr><td colspan="9" class="empty">{{ loading() ? 'Loading…' : 'No returns waiting for their RFC.' }}</td></tr>
          }
        </tbody>
      </table>
    </div>
  </div>
</div>

<p-dialog header="Link the RFC" [visible]="!!linking()" (visibleChange)="!$event && linking.set(null)" [modal]="true" [style]="{ width: '440px' }">
  @if (linking(); as r) {
    <div class="dlg">
      <div>{{ r.receiptType }} {{ r.receiptNo }} · {{ r.branch }} — invoice <b class="pdcm-mono">{{ r.invoiceNo }}</b>,
        return <b class="pdcm-mono">{{ r.amount | number:'1.2-2' }}</b></div>
      @for (c of free(r); track c.rfcNo) {
        <label class="opt"><input type="radio" name="rfc" [value]="c.rfcNo" [(ngModel)]="chosen" />
          RFC <b class="pdcm-mono">{{ c.rfcNo }}</b> · {{ c.rfcDate | date:'MM/dd/yyyy' }} · <span class="pdcm-mono">{{ c.amount | number:'1.2-2' }}</span>
          @if (c.amount !== r.amount) { <span class="diff"> (differs by {{ c.amount - r.amount | number:'1.2-2' }})</span> }
        </label>
      }
      <div class="pdcm-sub">Once every return on the receipt has its RFC, its {{ r.posting?.startsWith('RPDC') ? 'RPDC' : 'OR' }} goes to HOMS,
        with the RFC no. on its BO RETURNS row. A difference in amount stays as it is — encode it as "RFC amount difference" if needed.</div>
    </div>
  }
  <ng-template #footer>
    <button type="button" class="pdcm-btn" (click)="linking.set(null)">Cancel</button>
    <button type="button" class="pdcm-btn primary" [disabled]="!chosen || busy()" (click)="link()">Link</button>
  </ng-template>
</p-dialog>
`,
  styles: [`
.rfu .chk { display: inline-flex; align-items: center; gap: 6px; font-size: 11px; color: var(--pdcm-dim); margin-right: 8px; }
.rfu .tw { overflow-x: auto; }
.rfu .t { width: 100%; border-collapse: collapse; font-size: 11.5px; }
.rfu .t th { text-align: left; font-size: 10px; font-weight: 600; color: var(--pdcm-muted); text-transform: uppercase; letter-spacing: .03em;
  padding: 8px 10px; border-bottom: 1px solid var(--pdcm-line); white-space: nowrap; }
.rfu .t td { padding: 8px 10px; border-bottom: 1px solid var(--pdcm-line2); vertical-align: top; }
.rfu .t tr.done td { background: #FAFAF8; }
.rfu .r { text-align: right; }
.rfu .lnk { color: var(--pdcm-brand); cursor: pointer; font-weight: 600; }
.rfu .st { font-size: 10.5px; padding: 2px 8px; border-radius: 999px; background: #F1EFEC; white-space: nowrap; }
.rfu .st.hold { background: #FEF3C7; color: #92400E; }
.rfu .ok { color: #1F6B3E; font-weight: 600; }
.rfu .cand { white-space: nowrap; }
.rfu .cand.taken { color: var(--pdcm-muted); text-decoration: line-through; }
.rfu .diff { color: #B45309; }
.rfu .empty { text-align: center; color: var(--pdcm-muted); padding: 24px; }
.dlg { display: flex; flex-direction: column; gap: 10px; font-size: 12px; }
.dlg .opt { display: flex; align-items: center; gap: 8px; padding: 6px 8px; border: 1px solid #E6E2DD; border-radius: 6px; cursor: pointer; }
.dlg .diff { color: #B45309; }
`],
})
export class RfcFollowUpPageComponent implements OnInit, OnDestroy {
  private api = inject(CollectionService);
  private auth = inject(AuthService);
  private router = inject(Router);
  private toolbar = inject(GlobalToolbarService);
  private toast = inject(MessageService);

  protected canOpen = this.auth.isAdmin() || ['collections-encode', 'collections-verify', 'collections-manage'].some(k => this.auth.hasPermission(k));
  rows = signal<RfcFollowUpDto[]>([]);
  loading = signal(false);
  busy = signal(false);
  showLinked = signal(false);
  linking = signal<RfcFollowUpDto | null>(null);
  chosen: number | null = null;
  waiting = computed(() => this.rows().filter(r => !r.rfcNo).length);

  ngOnInit() {
    this.load();
    this.toolbar.set({ title: 'Returns awaiting RFC', refresh: { onClick: () => this.load(), loading: this.loading } });
  }
  ngOnDestroy() { this.toolbar.clear(); }

  load() {
    this.loading.set(true);
    this.api.rfcFollowUp(this.showLinked() ? 30 : 0).subscribe({
      next: res => { this.rows.set(res.data ?? []); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  /** RFCs on the invoice no other return has taken. */
  free(r: RfcFollowUpDto): RfcCandidateDto[] { return r.rfcs.filter(c => !c.linkedTo); }

  pick(r: RfcFollowUpDto) {
    const same = this.free(r).filter(c => c.amount === r.amount);
    this.chosen = same.length === 1 ? same[0].rfcNo : null;
    this.linking.set(r);
  }

  link() {
    const r = this.linking();
    if (!r || !this.chosen) return;
    this.busy.set(true);
    this.api.linkRfc(r.deductionId, this.chosen).subscribe({
      next: () => {
        this.busy.set(false);
        this.linking.set(null);
        this.toast.add({ severity: 'success', summary: 'RFC linked', detail: `${r.receiptType} ${r.receiptNo} — inv ${r.invoiceNo} → RFC ${this.chosen}` });
        this.load();
      },
      error: e => { this.busy.set(false); this.toast.add({ severity: 'error', summary: 'Not linked', detail: e?.error?.message ?? 'Failed.' }); },
    });
  }

  autoLink() {
    this.busy.set(true);
    this.api.autoLinkRfcs().subscribe({
      next: res => {
        this.busy.set(false);
        const n = res.data ?? 0;
        this.toast.add({ severity: n ? 'success' : 'info', summary: n ? `${n} return(s) linked` : 'Nothing new',
          detail: n ? 'Their receipts go to HOMS once every return has its RFC.' : 'No return has a single same-amount RFC yet.' });
        this.load();
      },
      error: () => this.busy.set(false),
    });
  }

  open(r: RfcFollowUpDto) { this.router.navigate(['/collections'], { state: { collectionId: r.collectionId } }); }
}
