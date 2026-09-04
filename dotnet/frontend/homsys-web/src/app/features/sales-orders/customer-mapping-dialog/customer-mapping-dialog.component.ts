import { Component, EventEmitter, Input, Output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { AutoCompleteModule, AutoCompleteCompleteEvent, AutoCompleteSelectEvent } from 'primeng/autocomplete';
import { SalesOrderService } from '../../../core/services/sales-order.service';
import { CustomerSuggestionDto } from '../../../core/models/sales-order.model';

export interface MappingRow {
  identifier: string;
  poNums: string[];
  custKey: string;
  cusName: string;
}

/**
 * Resolves each free-text Customer Identifier found in an "Import by Customer
 * Name" Excel batch to a CustKey, pre-filled from previously-saved mappings.
 */
@Component({
  selector: 'app-customer-mapping-dialog',
  standalone: true,
  imports: [DialogModule, TableModule, ButtonModule, MessageModule, AutoCompleteModule, FormsModule],
  template: `
    <p-dialog
      [(visible)]="visible"
      (visibleChange)="visibleChange.emit($event)"
      header="Map Customer Identifiers"
      [modal]="true"
      [style]="{ width: '78vw' }"
      [draggable]="false"
      [resizable]="false"
      (onHide)="close()">

      @if (error()) {
        <p-message severity="error" styleClass="w-full mb-3">
          <span>{{ error() }}</span>
        </p-message>
      }

      <p-table [value]="rows()" styleClass="p-datatable-sm">
        <ng-template pTemplate="header">
          <tr>
            <th>Customer Identifier</th>
            <th>PO Number(s)</th>
            <th style="width: 140px">Customer Key</th>
            <th style="width: 260px">Customer Name</th>
          </tr>
        </ng-template>
        <ng-template pTemplate="body" let-row let-rowIndex="rowIndex">
          <tr>
            <td>{{ row.identifier }}</td>
            <td>{{ row.poNums.join(', ') }}</td>
            <td>
              <p-autoComplete [ngModel]="row.custKey" [ngModelOptions]="{ standalone: true }"
                     styleClass="w-full" [inputStyle]="{ width: '100%' }"
                     [suggestions]="suggestions()" field="custKey" [dropdown]="true" [minLength]="2"
                     [appendTo]="'body'" placeholder="e.g. 0100123"
                     (completeMethod)="search($event)"
                     (onSelect)="onSelect(rowIndex, $event)">
                <ng-template let-item pTemplate="item">
                  <div>{{ item.custKey }} - {{ item.cusName }}</div>
                </ng-template>
              </p-autoComplete>
            </td>
            <td>
              <p-autoComplete [ngModel]="row.cusName" [ngModelOptions]="{ standalone: true }"
                     styleClass="w-full" [inputStyle]="{ width: '100%' }"
                     [suggestions]="suggestions()" field="cusName" [dropdown]="true" [minLength]="2"
                     [appendTo]="'body'" placeholder="Search by name"
                     (completeMethod)="search($event)"
                     (onSelect)="onSelect(rowIndex, $event)">
                <ng-template let-item pTemplate="item">
                  <div>{{ item.custKey }} - {{ item.cusName }}</div>
                </ng-template>
              </p-autoComplete>
            </td>
          </tr>
        </ng-template>
      </p-table>

      <ng-template pTemplate="footer">
        <p-button label="Cancel" [text]="true" severity="secondary" (onClick)="close()" />
        <p-button label="Save" icon="pi pi-check" [disabled]="!allMapped()" [loading]="saving()"
          (onClick)="save()" />
      </ng-template>
    </p-dialog>
  `
})
export class CustomerMappingDialogComponent {
  @Input() visible = false;
  @Input() set identifiers(value: { identifier: string; poNums: string[] }[]) {
    this.rows.set(value.map(v => ({ identifier: v.identifier, poNums: v.poNums, custKey: '', cusName: '' })));
    if (value.length) this.loadExistingMaps(value.map(v => v.identifier));
  }
  @Output() visibleChange = new EventEmitter<boolean>();
  @Output() mapped = new EventEmitter<Map<string, string>>();

  protected rows = signal<MappingRow[]>([]);
  protected suggestions = signal<CustomerSuggestionDto[]>([]);
  protected saving = signal(false);
  protected error = signal('');

  constructor(private api: SalesOrderService) {}

  private loadExistingMaps(identifiers: string[]): void {
    this.api.getCustomerIdentifierMaps(identifiers).subscribe({
      next: res => {
        const byIdentifier = new Map((res.data ?? []).map(m => [m.identifier, m]));
        this.rows.update(rows => rows.map(r => {
          const m = byIdentifier.get(r.identifier);
          return m ? { ...r, custKey: m.custKey, cusName: m.cusName } : r;
        }));
      },
      error: () => {}
    });
  }

  protected search(event: AutoCompleteCompleteEvent): void {
    const term = (event.query ?? '').trim();
    if (!term) { this.suggestions.set([]); return; }
    this.api.searchCustomers(term).subscribe({
      next: res => this.suggestions.set(res.data ?? []),
      error: () => this.suggestions.set([])
    });
  }

  protected onSelect(rowIndex: number, event: AutoCompleteSelectEvent): void {
    const c = event.value as CustomerSuggestionDto;
    this.rows.update(rows => rows.map((r, i) =>
      i === rowIndex ? { ...r, custKey: c.custKey, cusName: c.cusName } : r));
  }

  protected allMapped(): boolean {
    const rows = this.rows();
    return rows.length > 0 && rows.every(r => !!r.custKey);
  }

  protected save(): void {
    if (!this.allMapped()) return;
    this.error.set('');
    this.saving.set(true);
    const mappings = this.rows().map(r => ({ identifier: r.identifier, custKey: r.custKey }));
    this.api.saveCustomerIdentifierMaps(mappings).subscribe({
      next: () => {
        this.saving.set(false);
        const result = new Map(this.rows().map(r => [r.identifier, r.custKey]));
        this.mapped.emit(result);
        this.close();
      },
      error: err => {
        this.saving.set(false);
        this.error.set(err?.error?.message ?? 'Unable to save the mapping.');
      }
    });
  }

  close(): void {
    this.visibleChange.emit(false);
  }
}
