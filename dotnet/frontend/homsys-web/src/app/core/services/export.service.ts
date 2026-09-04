import { Injectable } from '@angular/core';
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

  downloadTemplate(filename: string, headers: string[]): void {
    const ws = XLSX.utils.aoa_to_sheet([headers]);
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, 'Template');
    XLSX.writeFile(wb, `${filename}_import_template.xlsx`);
  }
}
