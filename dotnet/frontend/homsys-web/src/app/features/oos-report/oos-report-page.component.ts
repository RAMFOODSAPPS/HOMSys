import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { DatePipe, CurrencyPipe } from '@angular/common';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { MessageModule } from 'primeng/message';
import { DialogModule } from 'primeng/dialog';
import { TooltipModule } from 'primeng/tooltip';
import { GlobalToolbarService } from '../../core/services/global-toolbar.service';
import { SalesOrderService } from '../../core/services/sales-order.service';
import { SalesOrderDto, SalesOrderLineDto } from '../../core/models/sales-order.model';

/** Original vs Allocated vs OOS for one SKU line, computed client-side — never stored. */
interface OosLineRow {
  cProdNo: string;
  prodDesc: string;
  origCs: number;
  origPc: number;
  allocCs: number;
  allocPc: number;
  oosCs: number;
  oosPc: number;
  amt: number | null;
}

@Component({
  selector: 'app-oos-report-page',
  standalone: true,
  imports: [TableModule, TagModule, MessageModule, DialogModule, TooltipModule, DatePipe, CurrencyPipe],
  template: `
    <div class="report-card">
      @if (apiError()) {
        <p-message severity="error" styleClass="w-full mb-3">
          <span>{{ apiError() }}</span>
        </p-message>
      }

      <div class="table-scroll">
        <p-table [value]="oosOrders()" [loading]="loading()" [paginator]="true" [rows]="25"
                 [rowsPerPageOptions]="[25, 50, 100]" dataKey="soId" styleClass="p-datatable-sm">
          <ng-template pTemplate="header">
            <tr>
              <th pSortableColumn="soId">SOID <p-sortIcon field="soId" /></th>
              <th pSortableColumn="soNo">BMS SO# <p-sortIcon field="soNo" /></th>
              <th pSortableColumn="orderDate">Order Date <p-sortIcon field="orderDate" /></th>
              <th pSortableColumn="custKey">CustKey <p-sortIcon field="custKey" /></th>
              <th pSortableColumn="cusName">Customer <p-sortIcon field="cusName" /></th>
              <th pSortableColumn="poNum">PO No. <p-sortIcon field="poNum" /></th>
              <th>OOS Lines</th>
            </tr>
          </ng-template>
          <ng-template pTemplate="body" let-o>
            <tr>
              <td>{{ o.soId }}</td>
              <td>
                @if (o.soNo) {
                  <a href="javascript:void(0)" class="so-link" (click)="viewOosDetails(o)" pTooltip="View OOS details">
                    <p-tag severity="success" [value]="o.soNo" />
                  </a>
                } @else {
                  <p-tag severity="warn" value="not pushed" />
                }
              </td>
              <td>{{ o.orderDate | date: 'MM/dd/yyyy' }}</td>
              <td>{{ o.custKey }}</td>
              <td>{{ o.cusName }}</td>
              <td>{{ o.poNum }}</td>
              <td>{{ oosLineCount(o) }}</td>
            </tr>
          </ng-template>
          <ng-template pTemplate="emptymessage">
            <tr><td colspan="7">No out-of-stock lines found.</td></tr>
          </ng-template>
        </p-table>

        <p-dialog [(visible)]="detailsVisible" [modal]="true" [style]="{ width: '55rem' }"
                  [header]="'OOS Details — SO# ' + (selectedOrder()?.soNo ?? selectedOrder()?.soId)">
          @if (selectedOrder(); as o) {
            <table class="detail-table">
              <thead>
                <tr>
                  <th>Prodno</th>
                  <th>Description</th>
                  <th>Original (Cs/Pc)</th>
                  <th>Allocated (Cs/Pc)</th>
                  <th>OOS (Cs/Pc)</th>
                  <th>Amt</th>
                </tr>
              </thead>
              <tbody>
                @for (l of oosLines(o); track l.cProdNo) {
                  <tr>
                    <td>{{ l.cProdNo }}</td>
                    <td>{{ l.prodDesc }}</td>
                    <td>{{ l.origCs }} / {{ l.origPc }}</td>
                    <td>{{ l.allocCs }} / {{ l.allocPc }}</td>
                    <td>{{ l.oosCs }} / {{ l.oosPc }}</td>
                    <td>{{ l.amt == null ? '—' : (l.amt | currency: 'PHP') }}</td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </p-dialog>
      </div>
    </div>
  `,
  styles: [`
    .table-scroll { overflow-x: auto; }
    .table-scroll ::ng-deep .p-datatable-table { min-width: 900px; }
    .detail-table { width: 100%; border-collapse: collapse; font-size: 0.85rem; }
    .detail-table th, .detail-table td { padding: 0.4rem 0.6rem; border-bottom: 1px solid #eee; text-align: left; }
    .detail-table thead th { background: #fafafa; font-weight: 600; }
    .so-link { cursor: pointer; }
  `]
})
export class OosReportPageComponent implements OnInit, OnDestroy {
  private api = inject(SalesOrderService);
  private toolbar = inject(GlobalToolbarService);

  private allOrders = signal<SalesOrderDto[]>([]);
  loading = signal(false);
  apiError = signal<string | null>(null);
  selectedOrder = signal<SalesOrderDto | null>(null);
  detailsVisible = signal(false);

  /** cProdNo|custKey -> price per case, ex-VAT. Fetched lazily on row expand. */
  private priceCache = new Map<string, number | null>();
  private priceVersion = signal(0);

  oosOrders = computed(() => this.allOrders().filter(o => o.lines.some(l => this.lineOosTotal(l) > 0)));

  ngOnInit(): void {
    this.toolbar.set({
      title: 'OOS Report',
      refresh: { onClick: () => this.load() }
    });
    this.load();
  }

  ngOnDestroy(): void {
    this.toolbar.clear();
  }

  oosLineCount(o: SalesOrderDto): number {
    return o.lines.filter(l => this.lineOosTotal(l) > 0).length;
  }

  oosLines(o: SalesOrderDto): OosLineRow[] {
    this.priceVersion();
    return o.lines
      .filter(l => this.lineOosTotal(l) > 0)
      .map(l => this.toRow(o, l));
  }

  viewOosDetails(o: SalesOrderDto): void {
    this.selectedOrder.set(o);
    this.detailsVisible.set(true);
    for (const l of o.lines) {
      if (this.lineOosTotal(l) <= 0) continue;
      const key = `${l.cProdNo}|${o.custKey}`;
      if (this.priceCache.has(key)) continue;
      this.priceCache.set(key, null);
      this.api.getQuote(l.cProdNo, o.custKey).subscribe({
        next: res => {
          this.priceCache.set(key, res.data?.hasPrice ? (res.data.pricePerCase ?? null) : null);
          this.priceVersion.update(v => v + 1);
        },
        error: () => this.priceVersion.update(v => v + 1)
      });
    }
  }

  private lineOosTotal(l: SalesOrderLineDto): number {
    if (l.allocatedQtyCs == null || l.allocatedQtyPc == null) return 0;
    const pieces = l.pieces || 0;
    const orig = l.qtyCs * pieces + l.qtyPc;
    const alloc = l.allocatedQtyCs * pieces + l.allocatedQtyPc;
    return orig - alloc;
  }

  private toRow(o: SalesOrderDto, l: SalesOrderLineDto): OosLineRow {
    const pieces = l.pieces || 0;
    const oosTotal = this.lineOosTotal(l);
    const oosCs = pieces > 0 ? Math.floor(oosTotal / pieces) : 0;
    const oosPc = pieces > 0 ? oosTotal % pieces : oosTotal;

    const pricePerCase = this.priceCache.get(`${l.cProdNo}|${o.custKey}`) ?? null;
    const amt = pricePerCase != null && pieces > 0 ? (oosTotal / pieces) * pricePerCase : null;

    return {
      cProdNo: l.cProdNo,
      prodDesc: l.prodDesc,
      origCs: l.qtyCs,
      origPc: l.qtyPc,
      allocCs: l.allocatedQtyCs ?? 0,
      allocPc: l.allocatedQtyPc ?? 0,
      oosCs,
      oosPc,
      amt
    };
  }

  private load(): void {
    this.loading.set(true);
    this.api.getAll().subscribe({
      next: res => {
        this.allOrders.set(res.data ?? []);
        this.loading.set(false);
      },
      error: err => {
        this.apiError.set(err?.error?.message ?? 'Unable to load sales orders.');
        this.loading.set(false);
      }
    });
  }
}
