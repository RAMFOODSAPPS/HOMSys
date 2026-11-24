import { CollectionDto, HomsPostingDto } from '../../core/models/collection.model';

/** A HOMS document a step generated (RPDC/OR at Verified, JV at Bank matched, the RPDC cancel at Voided). */
export interface StepDoc { text: string; state: string; tone: 'ok' | 'wait' | 'hold' | 'bad' | 'off'; tip: string; }

/** One step of the status tracker of a saved collection (collection page header, Collections list preview). */
export interface CollectionStep {
  label: string; state: 'done' | 'bad' | 'todo'; current?: boolean; at?: string | null; by?: string | null; tip?: string | null;
  /** at is a date without a time (check / outcome date). */
  dateOnly?: boolean;
  /** Connector from the previous step is green: both steps reached. */
  linked?: boolean;
  /** HOMS documents generated at this step. */
  docs?: StepDoc[];
}

const KIND_NAME: Record<string, string> = { OR: 'OR', RPDC: 'RPDC', JV: 'JV', DEDUCTIONS: 'Deductions', 'RPDC-CANCEL': 'RPDC cancel' };
const md = (iso: string) => { const [y, m, d] = iso.slice(0, 10).split('-'); return `${m}/${d}/${y.slice(2)}`; };

function stepDoc(p: HomsPostingDto): StepDoc {
  const name = KIND_NAME[p.kind] ?? p.kind;
  const text = p.docNo ? `${name} ${p.docNo}` : name;
  switch (p.status) {
    case 'Done': return { text, state: p.file ? `in ${p.file}` : 'in HOMS', tone: 'ok',
                          tip: `${text} written in HOMS${p.file ? ' (' + p.file + ')' : ''}${p.completedAt ? ' on ' + md(p.completedAt) : ''}${p.message ? ' — ' + p.message : ''}` };
    case 'Held': return { text, state: 'on hold', tone: 'hold', tip: `${text} on hold — ${p.message ?? 'see the HOMS posting panel'}` };
    case 'Failed': return { text, state: 'failed', tone: 'bad', tip: `${text} failed — ${p.message ?? 'press Retry in the HOMS posting panel'}` };
    case 'Cancelled': return { text, state: 'cancelled', tone: 'off', tip: `${text} cancelled${p.message ? ' — ' + p.message : ''}` };
    default: return { text, state: p.status === 'Processing' ? 'writing…' : 'queued', tone: 'wait',
                      tip: `${text} queued for HOMS — the HOMS watcher writes it automatically` };
  }
}

/** Hangs each HOMS document under the step that generated it. */
function attachDocs(c: CollectionDto, steps: CollectionStep[]) {
  const at = (labels: string[], kinds: string[]) => {
    const step = steps.find(x => labels.includes(x.label));
    const docs = c.homsPostings.filter(p => kinds.includes(p.kind)).map(stepDoc);
    if (step && docs.length) step.docs = docs;
  };
  at(['Verified'], ['OR', 'RPDC', 'DEDUCTIONS']);
  at(['Bank matched', 'Bank match found'], ['JV']);
  at(['Voided'], ['RPDC-CANCEL']);
}

/**
 * Status tracker, read as one sequence: every step up to the furthest one reached is highlighted and that one is
 * the current status. Returned, Bounced and Voided show red; Voided ends the sequence.
 *   PDC: Created → For Verification → Verified → Not yet due → Due soon → Matured → Deposit recorded → Bank matched → For AR (= Verified + tagged Succeeded) / Bounced → AR Downloaded (RPDC + JV in the HOMS DBFs)
 *   CR:  Created → Deposit recorded → For Verification → Verified → Bank matched → For AR (= Verified + Bank matched) → AR Downloaded (OR in the HOMS DBFs)
 *   Voided: the steps reached → Voided → Closed
 * Due soon = within the PDC Monitor's default 7-day window before the check date.
 */
export function collectionSteps(c: CollectionDto): CollectionStep[] {
  const last = (action: string) => [...c.history].reverse().find(h => h.action === action);
  const created = last('Created'), submitted = last('Submitted'), matched = last('Bank matched');
  const isPdc = c.paymentMode === 'PostDatedCheck';
  const s: CollectionStep[] = [{ label: 'Created', state: 'done', at: created?.at ?? null, by: created?.by ?? null }];
  const deposit: CollectionStep = { label: 'Deposit recorded', state: c.deposit ? 'done' : 'todo', at: c.deposit?.depositDate, dateOnly: true,
                          tip: c.deposit ? `${c.deposit.bank ?? ''}${c.deposit.slipRefNo ? ' slip ' + c.deposit.slipRefNo : ''}` : null };
  if (!isPdc) s.push(deposit);
  s.push({ label: 'For Verification', state: submitted ? 'done' : 'todo', at: submitted?.at, by: submitted?.by });
  if (c.verificationStatus === 'Returned') {
    const r = last('Returned');
    s.push({ label: 'Returned', state: 'bad', at: r?.at, by: r?.by, tip: c.returnReason });
  } else {
    s.push({ label: 'Verified', state: c.verifiedBy ? 'done' : 'todo', at: c.verifiedAt, by: c.verifiedBy });
  }
  if (isPdc && c.checkDate) {
    const day = 864e5, d = new Date(), today = Date.UTC(d.getFullYear(), d.getMonth(), d.getDate());
    const check = Date.parse(c.checkDate), left = Math.round((check - today) / day);
    const iso = (ms: number) => new Date(ms).toISOString().slice(0, 10);
    s.push({ label: 'Not yet due', state: c.verifiedBy ? 'done' : 'todo', tip: left > 0 ? `${left} day(s) to the check date` : null });
    s.push({ label: 'Due soon', state: left <= 7 ? 'done' : 'todo', at: iso(check - 7 * day), dateOnly: true, tip: 'Within 7 days of the check date' });
    s.push({ label: 'Matured', state: left <= 0 ? 'done' : 'todo', at: c.checkDate, dateOnly: true, tip: 'Check date' });
    s.push(deposit);
  }
  const pending = !!c.bankLine && !c.bankLine.confirmed;
  s.push({ label: pending ? 'Bank match found' : 'Bank matched', state: c.paymentStatus === 'Deposited' ? 'done' : 'todo', at: matched?.at, by: matched?.by,
           tip: pending ? (isPdc ? 'Found on the bank statement — awaiting the CNC Verifier\'s confirmation (posts the JV)' : 'Found on the bank statement — awaiting the Finance Verifier\'s approval')
                        : isPdc ? 'Confirmed by the CNC Verifier — JV posted' : 'Deposit found on the bank statement and approved by the verifier' });
  // For AR = done on the HOMSys side (was "Closed"); AR Downloaded = its HOMS documents are in the DBFs too.
  let forAr = false;
  if (isPdc) {
    const o = c.pdcOutcome;
    forAr = o === 'Succeeded' && !!c.verifiedBy;
    s.push({ label: o === 'Bounced' ? 'Bounced' : 'For AR', state: o === 'Bounced' ? 'bad' : forAr ? 'done' : 'todo',
             at: c.pdcOutcomeDate, dateOnly: true, by: c.pdcOutcomeBy, tip: o === 'Bounced' ? c.pdcOutcomeReason : 'Check cleared (Succeeded)' });
  } else {
    forAr = !!c.verifiedBy && c.paymentStatus === 'Deposited';
    const at = forAr ? [c.verifiedAt, matched?.at].filter((a): a is string => !!a).sort().pop() : null;
    s.push({ label: 'For AR', state: forAr ? 'done' : 'todo', at, tip: 'Verified and found on the bank statement' });
  }
  if (c.pdcOutcome !== 'Bounced') {
    const docs = c.homsPostings.filter(p => p.status !== 'Cancelled');
    const needed = isPdc ? ['RPDC', 'JV'] : ['OR'];
    const synced = needed.every(k => docs.some(p => p.kind === k && p.status === 'Done'));
    const done = forAr && synced;
    s.push({ label: 'AR Downloaded', state: done ? 'done' : 'todo',
             at: done ? docs.map(p => p.completedAt).filter((a): a is string => !!a).sort().pop() : null,
             tip: docs.filter(p => p.status === 'Done').map(p => `${p.kind} ${p.docNo ?? ''}${p.file ? ' in ' + p.file : ''}`).join(' · ')
                  || 'Written into the HOMS DBF files' });
  }
  let cur = s.map(x => x.state !== 'todo').lastIndexOf(true);
  s.forEach((x, i) => { if (i <= cur && x.state === 'todo') x.state = 'done'; });
  let out = s;
  if (c.verificationStatus === 'Voided') {
    const v = last('Voided');
    out = [...s.slice(0, cur + 1).filter(x => x.label !== 'For AR' && x.label !== 'AR Downloaded'), { label: 'Voided', state: 'bad', at: v?.at, by: v?.by, tip: c.voidReason },
           { label: 'Closed', state: 'done', at: v?.at }];
    cur = out.length - 1;
  }
  out[cur].current = true;
  out.forEach((x, i) => x.linked = i > 0 && i <= cur);
  attachDocs(c, out);
  return out;
}

/** Where the collection stands and what happens next (who does it, where). */
export interface CollectionGuide { status: string; next: string[]; tone: 'ok' | 'wait' | 'warn' | 'bad'; }

export function collectionGuide(c: CollectionDto): CollectionGuide {
  const isPdc = c.paymentMode === 'PostDatedCheck';
  const live = c.homsPostings.filter(p => p.status !== 'Cancelled');
  const name = (p: HomsPostingDto) => `${KIND_NAME[p.kind] ?? p.kind}${p.docNo ? ' ' + p.docNo : ''}`;
  const queued = live.filter(p => p.status === 'Pending' || p.status === 'Processing');
  const auto = queued.length ? [`Automatic: the HOMS watcher writes the ${queued.map(name).join(', ')} into HOMS (no action needed).`] : [];
  const siDocs = c.missingSiDocs > 0
    ? [`Encoder: upload the scanned Service Invoice (Update supporting documents) — until then ${c.missingSiDocs === 1 ? 'that deduction stays' : 'those deductions stay'} out of HOMS ordeduc2.`] : [];

  switch (c.verificationStatus) {
    case 'Voided': {
      const cancel = live.find(p => p.kind === 'RPDC-CANCEL' && p.status !== 'Done');
      return { status: `Voided${c.voidReason ? ' — ' + c.voidReason : ''}`, tone: 'bad',
               next: cancel ? ['Automatic: the HOMS watcher cancels its RPDC in HOMS.'] : ['Nothing more to do on this receipt.'] };
    }
    case 'Draft':
      return { status: 'Draft — not yet submitted', tone: 'wait',
               next: [...(c.missing.length ? [`Encoder: complete ${c.missing.join(', ')}.`] : []), 'Encoder: Submit for verification.'] };
    case 'Returned':
      return { status: `Returned by the verifier${c.returnReason ? ' — ' + c.returnReason : ''}`, tone: 'bad',
               next: ['Encoder: correct the receipt as noted, then Submit it again.'] };
    case 'ForVerification':
      return { status: 'Submitted — waiting for the Finance Verifier', tone: 'wait',
               next: ['Finance Verifier: check the receipt against its soft copy, then Verify (or Return it with a reason).',
                      `On Verify the ${isPdc ? 'RPDC' : 'OR'} is queued for HOMS.`] };
  }

  // Verified
  const awaitingRfc = c.deductions.filter(d => d.isReturn && !d.rfcNo && d.amount > 0);
  const held = live.find(p => p.status === 'Held'), failed = live.find(p => p.status === 'Failed');
  if (awaitingRfc.length)
    return { status: `Verified — the ${isPdc ? 'RPDC' : 'OR'} is on hold until the RFC of the return is posted in HOMS A/R`, tone: 'warn',
             next: [`CNC Verifier: once the RFC is posted in HOMS A/R, link it in PDC › Returns awaiting RFC (it links by itself when the amount matches).`,
                    `Then the ${isPdc ? 'RPDC' : 'OR'} is released to HOMS automatically.`] };
  if (held) return { status: `Verified — HOMS ${name(held)} on hold`, tone: 'warn',
                     next: held.message ? [held.message, 'Once fixed, press Retry in the HOMS posting panel.'] : ['Fix the problem, then press Retry in the HOMS posting panel.'] };
  if (failed) return { status: `HOMS ${name(failed)} could not be written`, tone: 'bad',
                       next: [`Press Retry HOMS posting${failed.message ? ' (' + failed.message + ')' : ''}.`] };

  if (isPdc) {
    if (c.pdcOutcome === 'Bounced')
      return { status: `Bounced${c.pdcOutcomeReason ? ' — ' + c.pdcOutcomeReason : ''}`, tone: 'bad',
               next: ['Collections: follow up in the PDC Monitor › Exceptions — issue the LOD, then settle with a new receipt (or endorse to legal when overdue).'] };
    const d = new Date(), today = Date.UTC(d.getFullYear(), d.getMonth(), d.getDate());
    const left = c.checkDate ? Math.round((Date.parse(c.checkDate) - today) / 864e5) : 0;
    const due = c.checkDate ? md(c.checkDate) : '';
    if (!c.deposit && left > 0)
      return { status: `Verified — check not yet due (matures ${due}, ${left} day${left === 1 ? '' : 's'} left)`, tone: 'wait',
               next: [...auto, `Custodian: on the check date (${due}) deposit the check, then record the deposit in the PDC Monitor (Deposit).`, ...siDocs] };
    if (!c.deposit)
      return { status: `Check matured on ${due} — not yet deposited`, tone: 'warn',
               next: [...auto, 'Custodian: deposit the check, then record the deposit in the PDC Monitor (Deposit).', ...siDocs] };
    if (!c.bankLine)
      return { status: 'Deposited — waiting for the credit on the bank statement', tone: 'wait',
               next: [...auto, 'Finance: upload the bank statement; the credit is matched automatically (or match it on Bank Statements).', ...siDocs] };
    if (!c.bankLine.confirmed)
      return { status: 'Bank credit found — waiting for the CNC Verifier', tone: 'wait',
               next: [...auto, 'CNC Verifier: Confirm the bank match in the PDC Monitor — this posts the JV to HOMS.', ...siDocs] };
    if (c.pdcOutcome !== 'Succeeded')
      return { status: 'Deposited and bank-matched — JV posted, waiting for the outcome tag', tone: 'wait',
               next: [...auto, 'PDC Monitor › Tag outcome: Succeeded once the check clears, or Bounced if it comes back.', ...siDocs] };
    const done = ['RPDC', 'JV'].every(k => live.some(p => p.kind === k && p.status === 'Done'));
    return done && !auto.length
      ? { status: 'Complete — the check cleared; RPDC and JV are in HOMS A/R', tone: 'ok', next: siDocs }
      : { status: 'Cleared — finishing the HOMS documents', tone: 'wait', next: [...auto, ...siDocs] };
  }

  if (!c.bankLine)
    return { status: 'Verified — waiting for the deposit on the bank statement', tone: 'wait',
             next: [...auto, 'Finance: upload the bank statement; the deposit is matched automatically (or match it on Bank Statements).', ...siDocs] };
  if (!c.bankLine.confirmed)
    return { status: 'Bank credit found — waiting for approval', tone: 'wait',
             next: [...auto, 'Finance Verifier: approve the bank match.', ...siDocs] };
  const orDone = live.some(p => p.kind === 'OR' && p.status === 'Done');
  return orDone && !auto.length
    ? { status: 'Complete — deposit matched; the OR is in HOMS A/R', tone: 'ok', next: siDocs }
    : { status: 'Deposit matched — finishing the HOMS documents', tone: 'wait', next: [...auto, ...siDocs] };
}
