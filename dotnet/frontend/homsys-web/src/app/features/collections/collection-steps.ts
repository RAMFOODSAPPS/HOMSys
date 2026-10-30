import { CollectionDto } from '../../core/models/collection.model';

/** One step of the status tracker of a saved collection (collection page header, Collections list preview). */
export interface CollectionStep {
  label: string; state: 'done' | 'bad' | 'todo'; current?: boolean; at?: string | null; by?: string | null; tip?: string | null;
  /** at is a date without a time (check / outcome date). */
  dateOnly?: boolean;
  /** Connector from the previous step is green: both steps reached. */
  linked?: boolean;
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
  return out;
}
