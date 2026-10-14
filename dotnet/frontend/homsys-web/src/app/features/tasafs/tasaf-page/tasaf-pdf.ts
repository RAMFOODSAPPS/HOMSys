import { jsPDF } from 'jspdf';
import autoTable, { RowInput } from 'jspdf-autotable';
import { TasafDto, utc } from '../../../core/models/tasaf.model';

/**
 * The TASAF as a PDF (toolbar Export › PDF) — same sections and order as the approval email /
 * print layout (server TasafEmailRenderer): header, details, products, costs, computation,
 * payment, approval chain. A4 portrait, text (not an image), so it can be searched and copied.
 * Standard PDF fonts have no peso sign, so amounts are plain numbers.
 */
const BRAND: [number, number, number] = [128, 0, 0];
const M = 14; // page margin (mm)

const n2 = (v: number | null | undefined) =>
  v == null ? '-' : v.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const pct = (v: number | null | undefined) =>
  v == null ? '-' : (v * 100).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + '%';
const day = (iso: string | null | undefined) =>
  iso ? new Date(iso + 'T00:00:00').toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' }) : '-';
const when = (s: string | null | undefined) =>
  s ? new Date(utc(s)!).toLocaleString('en-US', { timeZone: 'Asia/Manila', month: 'short', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit' }) : '';
const other = (name: string | null | undefined, o: string) => (o?.trim() ? `${name ?? ''} - ${o.trim()}` : name ?? '');
const join = (...xs: (string | null | undefined)[]) => xs.filter(x => x && x.trim()).join(' / ');

export function isSupportProgram(name: string | null | undefined): boolean {
  const n = (name ?? '').trim().toUpperCase();
  return n.startsWith('ACCOUNT SUPPORT') || n === 'CONVENTION';
}

/** @param logo Company logo (data: URI, PNG / JPEG) from GET tasafs/{id}/document; null = "HOMSys" text. */
export function buildTasafPdf(t: TasafDto, logo?: string | null): jsPDF {
  const doc = new jsPDF({ orientation: 'portrait', unit: 'mm', format: 'a4' });
  const W = doc.internal.pageSize.getWidth();
  const H = doc.internal.pageSize.getHeight();
  const support = isSupportProgram(t.programTypeName);
  let y = M;
  const lastY = () => (doc as unknown as { lastAutoTable: { finalY: number } }).lastAutoTable.finalY;

  // Title block. With the company logo (max 16 mm high / 55 mm wide, aspect kept): logo, then the
  // form name and activity title on the same line to its right. Without: "HOMSys" above the name.
  doc.setDrawColor(...BRAND).setLineWidth(0.8).line(M, y - 3, W - M, y - 3);
  let logoBox: { w: number; h: number } | null = null;
  if (logo) {
    try {
      const p = doc.getImageProperties(logo);
      const s = Math.min(16 / p.height, 55 / p.width);
      logoBox = { w: p.width * s, h: p.height * s };
      doc.addImage(logo, p.fileType, M, y, logoBox.w, logoBox.h);
    } catch { logoBox = null; /* unreadable image: fall back to the text header */ }
  }
  if (logoBox) {
    // Form name beside the logo (vertically centred).
    doc.setTextColor(20).setFont('helvetica', 'bold').setFontSize(14)
      .text('TRADE ACTIVITY SPEND APPROVAL FORM', M + logoBox.w + 6, y + logoBox.h / 2 + 1.8);
    y += logoBox.h + 3;
  } else {
    doc.setTextColor(...BRAND).setFont('helvetica', 'bold').setFontSize(10).text('HOMSys', M, y + 2);
    doc.setTextColor(20).setFontSize(14).text('TRADE ACTIVITY SPEND APPROVAL FORM', M, y + 10);
    y += 14;
  }

  // Info block, 3 columns: Title / Proponent · Document No. / Date Created · Status
  autoTable(doc, {
    startY: y, theme: 'plain', margin: { left: M, right: M },
    styles: { fontSize: 9, cellPadding: 1.8, fillColor: [250, 243, 243], valign: 'top' },
    columnStyles: {
      0: { textColor: 90, cellWidth: 19 }, 1: { fontStyle: 'bold', cellWidth: 58 },
      2: { textColor: 90, cellWidth: 25 }, 3: { fontStyle: 'bold', cellWidth: 38 },
      4: { textColor: 90, cellWidth: 13 }, 5: { fontStyle: 'bold' },
    },
    body: [
      ['Title', t.title, 'Document No.', t.refNo ?? '-', 'Status', t.status],
      ['Proponent', t.proponentName, 'Date Created', when(t.createdAt), '', ''],
    ],
  });
  y = lastY();

  const section = (title: string) => {
    if (y > H - 30) { doc.addPage(); y = M; }
    y += 7;
    doc.setTextColor(...BRAND).setFont('helvetica', 'bold').setFontSize(11).text(title, M, y);
    doc.setDrawColor(...BRAND).setLineWidth(0.5).line(M, y + 1.5, W - M, y + 1.5);
    doc.setTextColor(20).setFont('helvetica', 'normal');
    y += 3;
  };
  /** Label / value rows; `hl` shows one row's value bold in a colour (CTS over / within the limit). */
  const rows = (body: [string, string | null | undefined][], hl?: { label: string; rgb: [number, number, number] }) => {
    autoTable(doc, {
      startY: y, theme: 'plain', margin: { left: M, right: M },
      styles: { fontSize: 9, cellPadding: 1.6, lineColor: 235, lineWidth: { bottom: 0.2 } },
      columnStyles: { 0: { textColor: 90, cellWidth: 45 } },
      body: body.map(([l, v]) => [l, v?.toString().trim() ? v : '-']),
      didParseCell: d => {
        if (hl && d.section === 'body' && d.column.index === 1 && body[d.row.index]?.[0] === hl.label) {
          d.cell.styles.textColor = hl.rgb;
          d.cell.styles.fontStyle = 'bold';
        }
      },
    });
    y = lastY();
  };
  const table = (head: string[], body: RowInput[], right: number[], foot?: string[]) => {
    autoTable(doc, {
      startY: y, theme: 'grid', margin: { left: M, right: M },
      styles: { fontSize: 8, cellPadding: 1.5, lineColor: 220 },
      headStyles: { fillColor: BRAND, textColor: 255, fontStyle: 'bold' },
      footStyles: { fillColor: [250, 243, 243], textColor: 20, fontStyle: 'bold' },
      columnStyles: Object.fromEntries(right.map(i => [i, { halign: 'right' as const }])),
      head: [head], body: body.length ? body : [[{ content: '-', colSpan: head.length }]],
      foot: foot ? [foot] : undefined, showFoot: 'lastPage',
    });
    y = lastY();
  };

  // Details
  section('Details');
  const stores = t.customers.length === 0 ? '-'
    : t.customers.slice(0, 30).map(c => `${c.custKey} ${c.cusName}`.trim()).join(', ')
      + (t.customers.length > 30 ? ` ... (+${t.customers.length - 30} more, ${t.customers.length} stores)` : '');
  rows([
    ['Activity Title', t.title],
    ['Program Duration', `${day(t.startDate)} - ${day(t.endDate)}`],
    ['Sales Group', t.salesGroupName],
    ['Area', t.areaNames],
    ['Branch(es)', t.branches.map(b => b.name).join(', ')],
    ['Customer / Chain', t.chainName],
    ['Cust Key', stores],
    ['Program Type', other(t.programTypeName, t.programTypeOther)],
    ['Objective', other(t.objectiveName, t.objectiveOther)],
    ['Target Market', other(t.targetMarketName, t.targetMarketOther)],
    ['Channel', t.channelNames],
    ['Product Group', join(t.productGroupName, t.productGroup2Name)],
    ['Category', join(t.categoryName, t.category2Name)],
    ['Background', t.background],
    ['Concessions', t.concessions],
    ['Program Mechanics', t.mechanics],
  ]);

  // Participating Products
  if (!support && t.products.length) {
    section('Participating Products');
    table(
      ['ProdNo', 'SKU', 'P3 Mo Avg (Cs)', 'SPLY (Cs)', 'Promoted Vol (Cs)', 'Incr Vol (Cs)', 'LSP / Case', 'Promoted Value'],
      t.products.map(p => [p.cProdNo, p.sku, n2(p.p3MoAvgCs), n2(p.splyCs), n2(p.promotedVolCs), n2(p.incrVolCs),
        n2(p.lspPerCase) + (p.lspComputed != null && p.lspComputed !== p.lspPerCase ? `\n(computed ${n2(p.lspComputed)})` : ''),
        n2(p.promotedValue)]),
      [2, 3, 4, 5, 6, 7],
      ['Total', '', '', '', n2(t.products.reduce((s, p) => s + (p.promotedVolCs || 0), 0)),
        n2(t.products.reduce((s, p) => s + (p.incrVolCs || 0), 0)), '', n2(t.products.reduce((s, p) => s + (p.promotedValue || 0), 0))]);
  }

  // Cost Details
  section('Cost Details');
  table(['Description', 'Amount', 'Remarks'], t.costs.map(c => [c.costTypeName ?? '-', n2(c.amount), c.remarks]), [1],
    ['Total Cost', n2(t.totalCost), '']);

  // Computation
  section(support ? 'Account Support & Convention Computation' : 'Computation');
  const comp: [string, string][] = [[support ? 'Annual Projected Sales' : 'Promoted Value', n2(t.promotedValue)]];
  if (!support) {
    comp.push(['Pre-Promo Sales', n2(t.prePromoSales) + (t.promotedVsPrePromo != null ? `  (promoted vs pre-promo ${pct(t.promotedVsPrePromo)})` : '')]);
    comp.push(['Incremental Sales', n2(t.incrementalSales) + (t.incrementalVsPrePromo != null ? `  (vs pre-promo ${pct(t.incrementalVsPrePromo)})` : '')]);
  }
  comp.push(['Total Cost', n2(t.totalCost)]);
  const ctsLabel = `CTS (${support ? 'Annual Projected Sales' : 'Promoted Value'})`;
  comp.push([ctsLabel, pct(t.cts)]);
  if (t.ctsJustification?.trim()) comp.push(['CTS Justification', t.ctsJustification]);
  // Like the form: red over the program type's Max CTS % (8% guide when it has none), else green.
  const over = t.cts != null && t.cts * 100 > (t.ctsLimitPercent ?? 8);
  rows(comp, t.cts == null ? undefined : { label: ctsLabel, rgb: over ? [220, 38, 38] : [22, 163, 74] });

  // Mode of Payment
  section('Mode of Payment');
  const pay = ([
    ['Invoice Deduction', t.payInvDeductionAp + t.payInvDeductionSalesman],
    ['Check Payment', t.payCheckAp + t.payCheckSalesman],
    ['Free Goods', t.payFreeGoodsAp + t.payFreeGoodsSalesman],
    ['Off-Invoice', t.payOffInvoiceAp + t.payOffInvoiceSalesman],
  ] as [string, number][]).filter(([, a]) => a);
  table(['Payment Type', 'Amount'], pay.map(([l, a]) => [l, n2(a)]), [1]);

  // Approval chain
  if (t.steps.length) {
    section('Approval' + (t.approvalTypeName ? ` - ${t.approvalTypeName}` : ''));
    table(['#', 'Role', 'Approver', 'Status', 'Date', 'Comment'],
      t.steps.map(s => [String(s.seq), s.roleCode, s.userName || '(vacant)',
        s.status === 'Approved' ? 'Approved\nElectronically approved' : s.status, when(s.actedAt), s.comment]), []);
  }

  // Footer on every page
  const pages = doc.getNumberOfPages();
  for (let i = 1; i <= pages; i++) {
    doc.setPage(i);
    doc.setFont('helvetica', 'normal').setFontSize(8).setTextColor(130);
    doc.text(`${t.refNo ?? 'TASAF (draft)'}  -  printed ${new Date().toLocaleString('en-US', { timeZone: 'Asia/Manila' })}`, M, H - 7);
    doc.text(`Page ${i} of ${pages}`, W - M, H - 7, { align: 'right' });
  }
  return doc;
}
