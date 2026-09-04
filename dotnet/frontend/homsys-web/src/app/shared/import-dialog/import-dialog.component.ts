import { Component, EventEmitter, Input, Output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import * as XLSX from 'xlsx';
import { Observable } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { TableModule } from 'primeng/table';
import { MessageModule } from 'primeng/message';
import { SelectModule } from 'primeng/select';
import { ExportService } from '../../core/services/export.service';

const ALL_SHEETS = '__all__';

export interface ImportColumn {
  header: string;
  field: string;
  required?: boolean;
  /** Set for date columns whose source cell may be an unformatted Excel serial number (no date number format applied) instead of a real date cell. */
  dateField?: boolean;
}

/** A read-only lookup column shown right after `after`, resolved per preview row (e.g. customer name for a customer code). */
export interface ImportEnrichColumn {
  header: string;
  after: string;
  resolve: (row: Record<string, string>) => Observable<string | null>;
}

@Component({
  selector: 'app-import-dialog',
  standalone: true,
  imports: [DialogModule, ButtonModule, TableModule, MessageModule, SelectModule, FormsModule],
  template: `
    <p-dialog
      [(visible)]="visible"
      (visibleChange)="visibleChange.emit($event)"
      [header]="'Import ' + entityName"
      [modal]="true"
      [style]="{ width: '85vw', height: (parsedRows().length || rawHeaders().length) ? '85vh' : 'auto' }"
      [draggable]="false"
      [resizable]="false"
      (onHide)="reset()">

      <div class="import-body">

        <!-- Template download -->
        @if (!fileName()) {
          <div class="import-row">
            <p-button label="Download Template" icon="pi pi-download" [outlined]="true" size="small"
              (onClick)="downloadTemplate()" />
            <span class="hint">Download and fill this template, then upload it below.</span>
          </div>
        }

        <!-- File chooser -->
        <div class="import-row">
          <input #fileInput type="file" accept=".xlsx,.csv" style="display:none"
            (change)="onFileChange($event)" />
          <p-button label="Choose File (.xlsx / .csv)" icon="pi pi-upload" severity="secondary" size="small"
            (onClick)="fileInput.click()" />
          @if (fileName()) {
            <span class="file-name">{{ fileName() }}</span>
          }
        </div>

        <!-- Sheet picker -->
        @if (sheetOptions().length > 1) {
          <div class="import-row">
            <span class="hint">Sheet:</span>
            <p-select [options]="sheetOptions()" [ngModel]="selectedSheet()"
              (ngModelChange)="onSheetChange($event)"
              optionLabel="label" optionValue="value" styleClass="sheet-select" />
          </div>
        }

        <!-- Parse error -->
        @if (parseError()) {
          <p-message severity="error" styleClass="w-full">
            <span>{{ parseError() }}</span>
          </p-message>
        }

        <!-- Raw preview (step 1) — the sheet's actual content, no column mapping applied -->
        @if (step() === 'raw' && rawHeaders().length) {
          <div class="preview-section">
            <div class="preview-header">
              <span class="preview-count">{{ rawDataRows().length }} row(s) in sheet</span>
              @if (rawDataRows().length > previewLimit) {
                <span class="preview-more">Showing first {{ previewLimit }} rows</span>
              }
            </div>
            <p-table [value]="rawPreviewRows()" styleClass="p-datatable-sm"
              [scrollable]="true" scrollHeight="55vh">
              <ng-template pTemplate="header">
                <tr>
                  @for (h of rawHeaders(); track $index) {
                    <th>{{ h || '(column ' + ($index + 1) + ')' }}</th>
                  }
                </tr>
              </ng-template>
              <ng-template pTemplate="body" let-row>
                <tr>
                  @for (cell of row; track $index) {
                    <td>{{ cell || '—' }}</td>
                  }
                </tr>
              </ng-template>
            </p-table>
          </div>
        }

        <!-- Mapped preview (step 2) -->
        @if (step() === 'mapped' && parsedRows().length > 0) {
          <div class="preview-section">
            <div class="preview-header">
              <span class="preview-count">{{ parsedRows().length }} row(s) ready to import</span>
              @if (parsedRows().length > previewLimit) {
                <span class="preview-more">Showing first {{ previewLimit }} rows</span>
              }
            </div>
            <p-table [value]="previewRows()" styleClass="p-datatable-sm"
              [scrollable]="true" scrollHeight="55vh">
              <ng-template pTemplate="header">
                <tr>
                  @for (col of columns; track col.field) {
                    <th [style.width]="colWidth(col.field)">{{ col.header }}@if (col.required) { <span class="req">*</span> }</th>
                    @for (ec of enrichColumnsFor(col.field); track ec.header) {
                      <th>{{ ec.header }}</th>
                    }
                  }
                </tr>
              </ng-template>
              <ng-template pTemplate="body" let-row let-rowIndex="rowIndex">
                <tr>
                  @for (col of columns; track col.field) {
                    <td [class.missing]="col.required && !row[col.field]" [style.width]="colWidth(col.field)">{{ row[col.field] || '—' }}</td>
                    @for (ec of enrichColumnsFor(col.field); track ec.header) {
                      <td>{{ enrichValue(ec, rowIndex) }}</td>
                    }
                  }
                </tr>
              </ng-template>
            </p-table>
          </div>
        }

      </div>

      <ng-template pTemplate="footer">
        <p-button label="Cancel" [text]="true" severity="secondary" (onClick)="close()" />
        @if (step() === 'raw') {
          <p-button label="Next" icon="pi pi-arrow-right" iconPos="right"
            [loading]="validating()"
            [disabled]="rawDataRows().length === 0 || validating()"
            (onClick)="next()" />
        } @else {
          <p-button label="Back" [text]="true" (onClick)="back()" />
          <p-button
            [label]="parsedRows().length ? 'Import ' + parsedRows().length + ' rows' : 'Import'"
            icon="pi pi-check"
            [disabled]="parsedRows().length === 0"
            (onClick)="onImport()" />
        }
      </ng-template>
    </p-dialog>
  `,
  styles: [`
    .import-body { display: flex; flex-direction: column; gap: 1rem; padding: 0.25rem 0; }
    .import-row  { display: flex; align-items: center; gap: 0.75rem; flex-wrap: wrap; }
    .hint        { font-size: 0.8rem; color: var(--p-text-muted-color); }
    .file-name   { font-size: 0.82rem; font-weight: 500; color: var(--p-text-color); }
    .preview-section { display: flex; flex-direction: column; gap: 0.4rem; }
    .preview-header  { display: flex; align-items: center; gap: 1rem; }
    .preview-count   { font-size: 0.82rem; font-weight: 600; }
    .preview-more    { font-size: 0.78rem; color: var(--p-text-muted-color); }
    .req  { color: var(--p-red-500, #ef4444); margin-left: 2px; }
    ::ng-deep .missing { color: var(--p-red-500, #ef4444); }
    ::ng-deep .sheet-select { min-width: 220px; }
  `]
})
export class ImportDialogComponent {
  @Input() visible = false;
  @Input() columns: ImportColumn[] = [];
  @Input() enrichColumns: ImportEnrichColumn[] = [];
  @Input() entityName = 'Records';
  /** Skip the generic column-mapped preview table — go straight from raw preview to emitting importRows. Use when the caller shows its own confirmation UI next (e.g. a customer-mapping dialog). */
  @Input() skipMappedPreview = false;
  /**
   * Optional hard-block gate run right after Next, before column mapping —
   * this dialog stays generic (it has no idea what a "duplicate" means for
   * the caller's entity), so callers pass their own check. Given the
   * just-parsed rows, return `{ blocked: true, message }` to stop the
   * wizard (shown as a parse-style error, staying on the raw step), or
   * `{ blocked: false }` to let it proceed.
   */
  @Input() validateRows?: (rows: Record<string, string>[]) => Observable<{ blocked: boolean; message?: string }>;
  /**
   * Exact headers the sheet must have, in column order (A, B, C, ...).
   * When set, Next hard-blocks if any column's header doesn't match
   * (case/whitespace-insensitive) — used to reject a wrong/legacy template
   * before the name-based column matching in `parseSheet` even runs.
   */
  @Input() expectedHeaders?: string[];
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() importRows    = new EventEmitter<Record<string, string>[]>();
  /** Emitted right before importRows, for callers that want to dedup on the source file's content hash. */
  @Output() fileMeta       = new EventEmitter<{ fileHash: string; fileName: string }>();

  protected fileName    = signal('');
  protected parseError  = signal('');
  protected parsedRows  = signal<Record<string, string>[]>([]);
  protected enrichCache = signal<Map<string, string | null>>(new Map());
  protected sheetOptions = signal<{ label: string; value: string }[]>([]);
  protected selectedSheet = signal<string>('');
  protected step = signal<'raw' | 'mapped'>('raw');
  protected rawHeaders  = signal<string[]>([]);
  protected rawDataRows = signal<string[][]>([]);
  protected validating  = signal(false);
  protected readonly previewLimit = 10;

  private workbook: XLSX.WorkBook | null = null;
  private fileHash = signal('');

  constructor(private exportSvc: ExportService) {}

  protected previewRows() {
    return this.parsedRows().slice(0, this.previewLimit);
  }

  protected rawPreviewRows() {
    return this.rawDataRows().slice(0, this.previewLimit);
  }

  protected enrichColumnsFor(field: string): ImportEnrichColumn[] {
    return this.enrichColumns.filter(ec => ec.after === field);
  }

  /** Narrow the code columns so the enrich columns (name/description) get more room. */
  private static readonly NARROW_FIELDS = new Set(['custKey', 'cProdNo']);
  protected colWidth(field: string): string | null {
    return ImportDialogComponent.NARROW_FIELDS.has(field) ? '75px' : null;
  }

  protected enrichValue(col: ImportEnrichColumn, rowIndex: number): string {
    const v = this.enrichCache().get(`${col.header}|${rowIndex}`);
    return v === undefined ? '…' : (v || '—');
  }

  private resolveEnrichments(rows: Record<string, string>[]): void {
    if (!this.enrichColumns.length) return;
    for (const col of this.enrichColumns) {
      rows.forEach((row, i) => {
        const key = `${col.header}|${i}`;
        col.resolve(row).subscribe(v =>
          this.enrichCache.update(m => new Map(m).set(key, v)));
      });
    }
  }

  downloadTemplate() {
    this.exportSvc.downloadTemplate(this.entityName, this.columns.map(c => c.header));
  }

  onFileChange(event: Event) {
    const input = event.target as HTMLInputElement;
    const file  = input.files?.[0];
    if (!file) return;

    this.fileName.set(file.name);
    this.fileHash.set('');
    this.parseError.set('');
    this.parsedRows.set([]);
    this.sheetOptions.set([]);
    this.step.set('raw');
    this.workbook = null;

    file.arrayBuffer()
      .then(buf => crypto.subtle.digest('SHA-256', buf))
      .then(digest => {
        const hex = Array.from(new Uint8Array(digest)).map(b => b.toString(16).padStart(2, '0')).join('');
        this.fileHash.set(hex);
      })
      .catch(() => this.fileHash.set(''));

    const reader = new FileReader();
    reader.onload = (e) => {
      try {
        const data = e.target?.result;
        // cellDates: true — otherwise date columns come back as raw Excel serial
        // numbers (e.g. 46265) which `new Date('46265')` later misreads as the
        // literal year 46265 instead of the intended calendar date.
        const wb = XLSX.read(data, { type: 'binary', cellDates: true });
        this.workbook = wb;

        const options = wb.SheetNames.map(name => ({ label: name, value: name }));
        if (options.length > 1) {
          options.push({ label: 'All Sheets', value: ALL_SHEETS });
        }
        this.sheetOptions.set(options);
        this.loadRawPreview(wb.SheetNames[0]);
      } catch {
        this.parseError.set('Could not parse the file. Make sure it is a valid .xlsx or .csv file.');
      }
    };
    reader.readAsBinaryString(file);
    input.value = '';
  }

  onSheetChange(sheetName: string) {
    this.parsedRows.set([]);
    this.enrichCache.set(new Map());
    this.step.set('raw');
    this.loadRawPreview(sheetName);
  }

  protected next() {
    const headerError = this.checkExpectedHeaders();
    if (headerError) {
      this.parseError.set(headerError);
      return;
    }
    this.parseSheet(this.selectedSheet());
    if (this.parsedRows().length === 0) return;

    if (!this.validateRows) {
      this.proceedPastRaw();
      return;
    }
    this.validating.set(true);
    this.validateRows(this.parsedRows()).subscribe({
      next: result => {
        this.validating.set(false);
        if (result.blocked) {
          this.parseError.set(result.message || 'This file cannot be imported.');
          return;
        }
        this.proceedPastRaw();
      },
      error: () => {
        this.validating.set(false);
        this.proceedPastRaw();
      }
    });
  }

  /** Column-position header check — returns an error message, or null if the sheet's headers match. */
  private checkExpectedHeaders(): string | null {
    if (!this.expectedHeaders?.length) return null;
    const actual = this.rawHeaders();
    for (let i = 0; i < this.expectedHeaders.length; i++) {
      const expected = this.expectedHeaders[i];
      const got = actual[i] ?? '';
      if (got.trim().toLowerCase() !== expected.trim().toLowerCase()) {
        const col = String.fromCharCode(65 + i);
        return `Invalid file format: column ${col} must be "${expected}" (found "${got || '(blank)'}"). Download the template to see the expected layout.`;
      }
    }
    return null;
  }

  private proceedPastRaw() {
    if (this.skipMappedPreview) {
      this.onImport();
    } else {
      this.step.set('mapped');
    }
  }

  protected back() {
    this.step.set('raw');
  }

  private static cellToString(v: unknown, isDateField = false): string {
    if (v instanceof Date) {
      return `${v.getFullYear()}-${String(v.getMonth() + 1).padStart(2, '0')}-${String(v.getDate()).padStart(2, '0')}`;
    }
    // A date column stored without an Excel date number format comes through as a raw serial
    // number (e.g. 46265) instead of a Date, even with cellDates:true — decode it explicitly.
    if (isDateField && typeof v === 'number' && v > 0) {
      const d = XLSX.SSF.parse_date_code(v);
      if (d) return `${d.y}-${String(d.m).padStart(2, '0')}-${String(d.d).padStart(2, '0')}`;
    }
    return String(v ?? '').trim();
  }

  /** Step 1 — the sheet's real content, no column mapping applied yet. */
  private loadRawPreview(sheetName: string) {
    if (!this.workbook) return;
    this.parseError.set('');
    this.selectedSheet.set(sheetName);

    const sheetNames = sheetName === ALL_SHEETS ? this.workbook.SheetNames : [sheetName];
    const grids = sheetNames.map(name =>
      XLSX.utils.sheet_to_json<unknown[]>(this.workbook!.Sheets[name], { header: 1, defval: '' }));

    const headers = (grids[0]?.[0] ?? []).map(h => ImportDialogComponent.cellToString(h));
    const dataRows = grids.flatMap(g => g.slice(1)).map(r => r.map(v => ImportDialogComponent.cellToString(v)));

    this.rawHeaders.set(headers);
    this.rawDataRows.set(dataRows);
    if (dataRows.length === 0) {
      this.parseError.set('This sheet appears to be empty.');
    }
  }

  /** Step 2 — header-matches the sheet against the configured columns. */
  private parseSheet(sheetName: string) {
    if (!this.workbook) return;
    this.parseError.set('');

    const sheetNames = sheetName === ALL_SHEETS ? this.workbook.SheetNames : [sheetName];
    const raw = sheetNames.flatMap(name => {
      const ws = this.workbook!.Sheets[name];
      return XLSX.utils.sheet_to_json<Record<string, unknown>>(ws, { defval: '' });
    });

    const rows = raw.map(r => {
      const byNormalizedHeader = new Map(
        Object.keys(r).map(k => [k.trim().toLowerCase(), k]));
      const row: Record<string, string> = {};
      for (const col of this.columns) {
        const actualKey = byNormalizedHeader.get(col.header.trim().toLowerCase());
        const v = actualKey !== undefined ? r[actualKey] : undefined;
        row[col.field] = ImportDialogComponent.cellToString(v, col.dateField);
      }
      return row;
    });

    if (rows.length === 0) {
      this.parsedRows.set([]);
      this.parseError.set('No data rows found. Make sure your file has data below the header row.');
      return;
    }
    this.parsedRows.set(rows);
    this.enrichCache.set(new Map());
    this.resolveEnrichments(rows.slice(0, this.previewLimit));
  }

  onImport() {
    this.fileMeta.emit({ fileHash: this.fileHash(), fileName: this.fileName() });
    this.importRows.emit(this.parsedRows());
    this.close();
  }

  close() {
    this.visibleChange.emit(false);
    this.reset();
  }

  reset() {
    this.fileName.set('');
    this.fileHash.set('');
    this.parseError.set('');
    this.parsedRows.set([]);
    this.enrichCache.set(new Map());
    this.sheetOptions.set([]);
    this.selectedSheet.set('');
    this.step.set('raw');
    this.rawHeaders.set([]);
    this.rawDataRows.set([]);
    this.workbook = null;
  }
}
