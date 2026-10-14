import { Component, inject, OnInit, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { ButtonModule } from 'primeng/button';
import { TextareaModule } from 'primeng/textarea';
import { environment } from '../../../../environments/environment';

interface EmailDecision {
  refNo: string | null;
  title: string;
  approverName: string;
  roleCode: string;
  canDecide: boolean;
  message: string | null;
  html: string;
}

/**
 * Opened from an approval email's "Review & Decide" button — no login. The token in the link
 * (?t=) stands in for the approver; the server checks it's still their turn. Shows the whole
 * TASAF (same layout as the email) and asks for the comment before confirming.
 */
@Component({
  selector: 'app-tasaf-decision-page',
  standalone: true,
  imports: [FormsModule, ButtonModule, TextareaModule],
  template: `
    <div class="td-wrap">
      <div class="td-bar">HOMSys <span>· TASAF approval</span></div>
      @if (loading()) {
        <p class="td-note">Loading…</p>
      } @else if (error() && !data()) {
        <div class="td-msg bad">{{ error() }}</div>
      } @else if (data()) {
        @let d = data()!;
        <p class="td-hi">Hi <strong>{{ d.approverName || 'approver' }}</strong>@if (d.roleCode) { ({{ d.roleCode }}) }.</p>
        @if (done()) { <div class="td-msg ok">{{ done() }}</div> }
        @if (!d.canDecide && d.message) { <div class="td-msg">{{ d.message }}</div> }

        <div class="td-doc" [innerHTML]="html()"></div>

        @if (d.canDecide) {
          <div class="td-panel">
            <label>Your decision <span class="req">*</span></label>
            <div class="td-choice">
              <p-button label="Approve" icon="pi pi-check" [outlined]="decision() !== 'approve'" severity="success" (onClick)="decision.set('approve')" />
              <p-button label="Return" icon="pi pi-replay" [outlined]="decision() !== 'return'" severity="warn" (onClick)="decision.set('return')" />
              <p-button label="Reject" icon="pi pi-times" [outlined]="decision() !== 'reject'" severity="danger" (onClick)="decision.set('reject')" />
            </div>
            <label for="td-comment">Comment <span class="req">*</span></label>
            <textarea id="td-comment" pTextarea [(ngModel)]="comment" rows="4" [autoResize]="true" maxlength="1000"
              [placeholder]="placeholder()"></textarea>
            @if (error()) { <div class="td-msg bad">{{ error() }}</div> }
            <p-button [label]="('Confirm ' + label()).trim()"
              [severity]="severity()" [loading]="saving()"
              [disabled]="!decision() || !comment.trim()" (onClick)="confirm()" />
          </div>
        }
      }
    </div>
  `,
  styles: [`
    /* html/body never scroll (styles.scss, overflow: clip) — this page scrolls inside its own box. */
    :host { display: block; background: #f6f6f6; height: 100vh; overflow-y: auto; }
    .td-wrap { max-width: 820px; margin: 0 auto; padding: 16px; }
    .td-bar { background: #800000; color: #fff; font-weight: 700; padding: 10px 14px; border-radius: 4px 4px 0 0;
      span { font-weight: 400; opacity: .85; } }
    .td-hi, .td-note { margin: 14px 0 8px; }
    .td-doc { background: #fff; padding: 14px 18px; border: 1px solid #e5e5e5; }
    .td-msg { padding: 10px 14px; margin: 10px 0; border-radius: 4px; background: #eef2ff; color: #1e3a8a;
      &.ok { background: #ecfdf5; color: #065f46; } &.bad { background: #fef2f2; color: #991b1b; } }
    .td-panel { background: #fff; border: 1px solid #e5e5e5; border-top: 3px solid #800000; padding: 14px 18px; margin: 12px 0 30px;
      display: flex; flex-direction: column; gap: 10px;
      textarea { width: 100%; } }
    .td-choice { display: flex; gap: 10px; }
    .req { color: #dc2626; }
  `]
})
export class TasafDecisionPageComponent implements OnInit {
  private http = inject(HttpClient);
  private sanitizer = inject(DomSanitizer);
  private readonly base = `${environment.apiUrl}/tasaf-email`;
  private token = '';

  protected loading  = signal(true);
  protected saving   = signal(false);
  protected data     = signal<EmailDecision | null>(null);
  protected html     = signal<SafeHtml>('');
  protected error    = signal<string | null>(null);
  protected done     = signal<string | null>(null);
  /** Nothing pre-selected: the approver picks Approve / Return / Reject on purpose. */
  protected decision = signal<'approve' | 'return' | 'reject' | null>(null);
  protected comment  = '';

  ngOnInit() {
    const q = new URLSearchParams(location.search);
    this.token = q.get('t') ?? '';
    if (!this.token) { this.loading.set(false); this.error.set('This link is not valid.'); return; }
    this.http.get<{ data: EmailDecision }>(`${this.base}/${encodeURIComponent(this.token)}`).subscribe({
      next: res => { this.show(res.data); this.loading.set(false); },
      error: err => { this.loading.set(false); this.error.set(err?.error?.message ?? 'This link is not valid.'); }
    });
  }

  protected confirm() {
    const comment = this.comment.trim();
    const d = this.decision();
    if (!d) { this.error.set('Choose Approve, Return or Reject.'); return; }
    if (!comment) { this.error.set('Please enter a comment.'); return; }
    this.saving.set(true);
    this.error.set(null);
    this.http.post<{ data: EmailDecision }>(`${this.base}/${encodeURIComponent(this.token)}/decide`, { decision: d, comment }).subscribe({
      next: res => {
        this.saving.set(false);
        this.done.set(d === 'approve' ? 'Approved — thank you. The next approver / the proponent has been notified.'
          : d === 'return' ? 'Returned to the proponent to correct and resubmit — they have been notified.'
          : 'Rejected — the proponent has been notified.');
        if (res.data) this.show(res.data);
      },
      error: err => { this.saving.set(false); this.error.set(err?.error?.message ?? 'Failed to save your decision.'); }
    });
  }

  protected label() { const d = this.decision(); return d ? { approve: 'Approve', return: 'Return', reject: 'Reject' }[d] : ''; }
  protected severity() { const d = this.decision(); return d ? ({ approve: 'success', return: 'warn', reject: 'danger' } as const)[d] : 'secondary'; }
  protected placeholder() {
    const d = this.decision();
    return d ? { approve: 'Your comment for the approval record', return: 'What should the proponent correct?', reject: 'Why is this TASAF rejected?' }[d]
      : 'Choose a decision above, then enter your comment';
  }

  /** The TASAF HTML is built by the server with every value HTML-encoded (TasafEmailRenderer). */
  private show(d: EmailDecision) {
    this.data.set(d);
    this.html.set(this.sanitizer.bypassSecurityTrustHtml(d.html));
  }
}
