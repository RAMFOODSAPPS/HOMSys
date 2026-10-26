import { LodLetterDto } from '../../core/models/collection.model';

const esc = (s: string | null | undefined) =>
  (s ?? '').replace(/[&<>"']/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch]!));

/** 'yyyy-MM-dd' -> 'October 9, 2026' (parsed as a local date, never shifted by the time zone). */
const longDate = (iso: string | null) => {
  if (!iso) return '';
  const [y, m, d] = iso.split('-').map(Number);
  return new Date(y, m - 1, d).toLocaleDateString('en-US', { year: 'numeric', month: 'long', day: 'numeric' });
};

const php = (n: number) => n.toLocaleString('en-PH', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

/**
 * Letter of Dishonor for a bounced PDC, printed from a window the caller opened synchronously on click
 * (opening it after the HTTP call returns would be caught by popup blockers).
 * Wording is a template — have Legal review it before first use.
 */
export function printLodLetter(w: Window, l: LodLetterDto) {
  const company = esc(l.companyName) || 'RAM Foods';
  const head = [l.companyAddress, l.companyPhone && 'Tel. ' + l.companyPhone].filter(Boolean).map(esc).join(' · ');
  const branch = [l.siteName, l.siteAddress].filter(Boolean).map(esc).join(', ');
  const addr = [l.custAddress1, l.custAddress2].filter(x => !!x?.trim()).map(x => `<div>${esc(x)}</div>`).join('');
  const check = `${esc(l.bank)} Check No. <b>${esc(l.checkNo)}</b> dated ${longDate(l.checkDate)}`;

  w.document.open();
  w.document.write(`<!doctype html><html><head><meta charset="utf-8"><title>Notice of Dishonor — ${esc(l.custName)} — Check ${esc(l.checkNo)}</title>
<style>
  @page { size: A4; margin: 22mm 20mm; }
  body { font: 11.5pt/1.55 Georgia, 'Times New Roman', serif; color: #111; max-width: 170mm; margin: 0 auto; }
  .lh { border-bottom: 2px solid #800000; padding-bottom: 6px; margin-bottom: 22px; }
  .lh h1 { margin: 0; font-size: 17pt; color: #800000; letter-spacing: .02em; }
  .lh small { display: block; color: #444; font-size: 9.5pt; }
  .to { margin: 18px 0; } .to b { text-transform: uppercase; }
  .re { margin: 18px 0; font-weight: bold; }
  table { border-collapse: collapse; margin: 10px 0 14px; font-size: 10.5pt; }
  td { padding: 3px 14px 3px 0; vertical-align: top; } td:first-child { color: #444; }
  .amt { font-weight: bold; }
  .sig { margin-top: 46px; } .line { border-top: 1px solid #111; width: 62mm; padding-top: 2px; margin-top: 38px; }
  .rcv { margin-top: 40px; border: 1px dashed #777; padding: 10px 12px; font-size: 10pt; }
  .rcv .row { display: flex; gap: 24px; margin-top: 26px; } .rcv .row div { flex: 1; border-top: 1px solid #111; padding-top: 2px; }
</style></head><body>
<div class="lh"><h1>${company}</h1><small>${head}</small>${branch ? `<small>${branch}</small>` : ''}</div>
<div>${longDate(l.issuedDate)}</div>
<div class="to"><b>${esc(l.custName)}</b>${addr}<div>Customer code: ${esc(l.custKey)}</div></div>
<div class="re">Re: NOTICE OF DISHONOR — ${esc(l.bank)} Check No. ${esc(l.checkNo)} for PHP ${php(l.amount)}</div>
<p>Dear Sir / Madam:</p>
<p>This is to formally notify you that ${check}, in the amount of <b>PHP ${php(l.amount)}</b>, which you issued to ${company}
  in payment of your account (${esc(l.receiptType)} No. ${esc(l.receiptNo)}), was <b>dishonored</b> by the drawee bank upon presentment
  on ${longDate(l.bouncedDate)}${l.bounceReason ? ` for the reason: <b>${esc(l.bounceReason)}</b>` : ''}.</p>
<table>
  <tr><td>Drawee bank</td><td>${esc(l.bank)}</td></tr>
  <tr><td>Check no. / date</td><td>${esc(l.checkNo)} · ${longDate(l.checkDate)}</td></tr>
  <tr><td>Amount</td><td class="amt">PHP ${php(l.amount)}</td></tr>
  ${l.depositDate ? `<tr><td>Deposited</td><td>${longDate(l.depositDate)}</td></tr>` : ''}
  <tr><td>Returned</td><td>${longDate(l.bouncedDate)}${l.bounceReason ? ' — ' + esc(l.bounceReason) : ''}</td></tr>
</table>
<p>We therefore demand that you settle the full amount of <b>PHP ${php(l.amount)}</b> within <b>${l.settleDays} day${l.settleDays === 1 ? '' : 's'}</b>
  from your receipt of this notice. An official Collection Receipt will be issued to you upon payment.</p>
<p>Should you fail to settle within the said period, we shall be constrained to endorse this matter to our legal counsel for the
  appropriate action, including under Batas Pambansa Blg. 22, without further notice.</p>
<p>Please disregard this notice if payment has already been made.</p>
<div class="sig">Very truly yours,<div class="line">${esc(l.issuedBy)}</div><div>${company}</div></div>
<div class="rcv"><b>RECEIVED</b> — customer's copy of this Notice of Dishonor
  <div class="row"><div>Signature over printed name</div><div>Position</div><div>Date received</div></div></div>
</body></html>`);
  w.document.close();
  w.focus();
  setTimeout(() => w.print(), 300);
}
