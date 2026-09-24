import { Injectable } from '@angular/core';
import { HttpResponse } from '@angular/common/http';
import * as XLSX from 'xlsx';
import { jsPDF } from 'jspdf';
import autoTable from 'jspdf-autotable';

export interface ExportColumn {
  header: string;
  field: string;
  formatter?: (val: unknown) => string;
}

@Injectable({ providedIn: 'root' })
export class ExportService {

  private cell(row: Record<string, unknown>, col: ExportColumn): string {
    const val = row[col.field];
    if (col.formatter) return col.formatter(val);
    if (val === null || val === undefined) return '';
    if (Array.isArray(val)) return (val as unknown[]).join(', ');
    return String(val);
  }

  exportPdf(title: string, columns: ExportColumn[], data: Record<string, unknown>[]): void {
    const doc = new jsPDF({ orientation: 'landscape' });
    doc.setFontSize(13);
    doc.text(title, 14, 15);
    autoTable(doc, {
      startY: 22,
      head:       [columns.map(c => c.header)],
      body:       data.map(row => columns.map(col => this.cell(row, col))),
      styles:     { fontSize: 8 },
      headStyles: { fillColor: [128, 0, 0] },
    });
    const blob = doc.output('blob');
    const url  = URL.createObjectURL(blob);
    const a    = document.createElement('a');
    a.href     = url;
    a.download = `${title}.pdf`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  }

  exportExcel(filename: string, columns: ExportColumn[], data: Record<string, unknown>[]): void {
    const rows = data.map(row =>
      Object.fromEntries(columns.map(col => [col.header, this.cell(row, col)]))
    );
    const ws = XLSX.utils.json_to_sheet(rows);
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, 'Data');
    XLSX.writeFile(wb, `${filename}.xlsx`);
  }

  exportCsv(filename: string, columns: ExportColumn[], data: Record<string, unknown>[]): void {
    const rows = data.map(row =>
      Object.fromEntries(columns.map(col => [col.header, this.cell(row, col)]))
    );
    const ws  = XLSX.utils.json_to_sheet(rows);
    const csv = XLSX.utils.sheet_to_csv(ws);
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
    const url  = URL.createObjectURL(blob);
    const a    = document.createElement('a');
    a.href     = url;
    a.download = `${filename}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }

  /** Saves a server-built file (blob response), using its Content-Disposition filename when present. */
  saveResponse(response: HttpResponse<Blob>, fallbackName: string): void {
    const disposition = response.headers.get('Content-Disposition') ?? '';
    const match = /filename="?([^";]+)"?/.exec(disposition);
    const url = URL.createObjectURL(response.body!);
    const a = document.createElement('a');
    a.href = url;
    a.download = match?.[1] ?? fallbackName;
    a.click();
    URL.revokeObjectURL(url);
  }

  /** Multi-section PDF: per section a heading, an optional chart image and a table (Data Analytics dashboards/reports). */
  exportPdfSections(title: string, subtitle: string, sections: { title: string; image: string | null; columns: string[]; rows: string[][] }[]): void {
    const doc = new jsPDF({ orientation: 'landscape' });
    const pageW = doc.internal.pageSize.getWidth();
    const pageH = doc.internal.pageSize.getHeight();
    doc.setFontSize(14);
    doc.text(title, 14, 15);
    doc.setFontSize(8);
    doc.setTextColor(110);
    doc.text(subtitle, 14, 21);
    doc.setTextColor(0);
    let y = 28;
    for (const s of sections) {
      if (y > pageH - 40) { doc.addPage(); y = 15; }
      doc.setFontSize(11);
      doc.text(s.title || ' ', 14, y);
      y += 4;
      if (s.image) {
        const w = pageW - 28, h = Math.min(80, w * 0.4);
        if (y + h > pageH - 10) { doc.addPage(); y = 15; }
        doc.addImage(s.image, 'PNG', 14, y, w, h);
        y += h + 4;
      }
      if (s.columns.length && s.rows.length) {
        autoTable(doc, {
          startY: y, head: [s.columns], body: s.rows,
          styles: { fontSize: 7 }, headStyles: { fillColor: [128, 0, 0] }, margin: { left: 14, right: 14 },
        });
        y = (doc as unknown as { lastAutoTable: { finalY: number } }).lastAutoTable.finalY + 8;
      } else {
        y += 4;
      }
    }
    doc.save(`${title}.pdf`);
  }

  downloadTemplate(filename: string, headers: string[]): void {
    const ws = XLSX.utils.aoa_to_sheet([headers]);
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, 'Template');
    XLSX.writeFile(wb, `${filename}_import_template.xlsx`);
  }
}
