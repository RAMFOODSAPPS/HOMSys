import { Component, inject, OnInit, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { environment } from '../../../../environments/environment';

interface EmailWorksheet {
  refNo: string | null;
  title: string;
  worksheetTitle: string;
  html: string;
}

/**
 * "View worksheet ›" from an approval email / the decision page — no login. The link carries the
 * approver's one-time token (?t=) and the worksheet id (?w=); the server checks the worksheet is
 * attached to that TASAF. Full width, so the wide product table fits (scrolls sideways on a phone).
 */
@Component({
  selector: 'app-tasaf-decision-worksheet-page',
  standalone: true,
  template: `
    <div class="tw-wrap">
      <div class="tw-bar">HOMSys <span>· TASAF worksheet</span></div>
      @if (loading()) {
        <p class="tw-note">Loading…</p>
      } @else if (error()) {
        <div class="tw-msg">{{ error() }}</div>
      } @else {
        <div class="tw-doc" [innerHTML]="html()"></div>
        <p class="tw-note">Close this tab to go back to the TASAF.</p>
      }
    </div>
  `,
  styles: [`
    /* html/body never scroll (styles.scss, overflow: clip) — this page scrolls inside its own box. */
    :host { display: block; background: #f6f6f6; height: 100vh; overflow-y: auto; }
    .tw-wrap { max-width: 1400px; margin: 0 auto; padding: 16px; }
    .tw-bar { background: #800000; color: #fff; font-weight: 700; padding: 10px 14px; border-radius: 4px 4px 0 0;
      span { font-weight: 400; opacity: .85; } }
    .tw-doc { background: #fff; padding: 14px 18px; border: 1px solid #e5e5e5; overflow-x: auto; }
    .tw-note { margin: 12px 0; color: #666; font-size: 12px; }
    .tw-msg { padding: 10px 14px; margin: 10px 0; border-radius: 4px; background: #fef2f2; color: #991b1b; }
  `]
})
export class TasafDecisionWorksheetPageComponent implements OnInit {
  private http = inject(HttpClient);
  private sanitizer = inject(DomSanitizer);

  protected loading = signal(true);
  protected error   = signal<string | null>(null);
  protected html    = signal<SafeHtml>('');

  ngOnInit() {
    const q = new URLSearchParams(location.search);
    const token = q.get('t') ?? '';
    const id = Number(q.get('w'));
    if (!token || !id) { this.loading.set(false); this.error.set('This link is not valid.'); return; }
    this.http.get<{ data: EmailWorksheet }>(
      `${environment.apiUrl}/tasaf-email/${encodeURIComponent(token)}/worksheet/${id}`).subscribe({
      next: res => {
        // Server-rendered with every value HTML-encoded (TasafEmailRenderer.RenderWorksheet).
        this.html.set(this.sanitizer.bypassSecurityTrustHtml(res.data.html));
        document.title = `${res.data.worksheetTitle} — ${res.data.refNo ?? 'TASAF'}`;
        this.loading.set(false);
      },
      error: err => { this.loading.set(false); this.error.set(err?.error?.message ?? 'This link is not valid.'); }
    });
  }
}
