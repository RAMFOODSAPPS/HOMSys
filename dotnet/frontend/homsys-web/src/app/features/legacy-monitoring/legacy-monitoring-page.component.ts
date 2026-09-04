import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { GlobalToolbarService } from '../../core/services/global-toolbar.service';
import { SyncStatusService } from '../../core/services/sync-status.service';
import { SyncStatusDto } from '../../core/models/sync-status.model';

@Component({
  selector: 'app-legacy-monitoring-page',
  standalone: true,
  imports: [ButtonModule, MessageModule, TableModule, TagModule, DatePipe, DecimalPipe],
  template: `
    <div class="report-card">
      @if (apiError()) {
        <p-message severity="error" styleClass="w-full mb-3">
          <span>{{ apiError() }}</span>
        </p-message>
      }
      @if (actionMessage()) {
        <p-message severity="success" styleClass="w-full mb-3">
          <span>{{ actionMessage() }}</span>
        </p-message>
      }

      <div class="section">
        <div class="section-header">
          <h3>Reference Data <span class="source">(BMSRAM — Customers / Products)</span></h3>
          <p-button label="Sync Now" icon="pi pi-refresh" size="small"
                    [loading]="referenceSyncing()" (onClick)="syncReference()" />
        </div>

        @if (status(); as s) {
          @if (s.referenceData.lastRunUtc) {
            <div class="status-row">
              <p-tag severity="success" value="Synced" />
              <span>Last run: {{ s.referenceData.lastRunUtc | date: 'MM/dd/yyyy hh:mm a' }}</span>
              <span>Customers: {{ s.referenceData.customers | number }}</span>
              <span>Products: {{ s.referenceData.products | number }}</span>
            </div>
          } @else {
            <div class="status-row">
              <p-tag severity="warn" value="Never synced" />
            </div>
          }
        }
      </div>

      <div class="section">
        <div class="section-header">
          <h3>Pricing Masters <span class="source">(F:\ — zones, customer branches, price history)</span></h3>
          <p-button label="Sync Now" icon="pi pi-refresh" size="small"
                    [loading]="pricingSyncing()" (onClick)="syncPricing()" />
        </div>

        @if (status(); as s) {
          @if (s.pricingMasters.lastRunUtc) {
            <div class="status-row">
              <p-tag severity="success" value="Synced" />
              <span>Last run: {{ s.pricingMasters.lastRunUtc | date: 'MM/dd/yyyy hh:mm a' }}</span>
              <span>Addon branches: {{ s.pricingMasters.addonBranches.join(', ') }}</span>
              <span>Customer branches: {{ s.pricingMasters.customerBranches.join(', ') }}</span>
            </div>
          } @else {
            <div class="status-row">
              <p-tag severity="warn" value="Never synced" />
            </div>
          }

          <div class="table-scroll">
            <p-table [value]="s.pricingMasters.files" [paginator]="true" [rows]="10"
                     dataKey="file" styleClass="p-datatable-sm">
              <ng-template pTemplate="header">
                <tr>
                  <th pSortableColumn="file">File <p-sortIcon field="file" /></th>
                  <th pSortableColumn="lastWriteUtc">Last Write <p-sortIcon field="lastWriteUtc" /></th>
                </tr>
              </ng-template>
              <ng-template pTemplate="body" let-f>
                <tr>
                  <td>{{ f.file }}</td>
                  <td>{{ f.lastWriteUtc | date: 'MM/dd/yyyy hh:mm a' }}</td>
                </tr>
              </ng-template>
              <ng-template pTemplate="emptymessage">
                <tr><td colspan="2">No tracked files yet.</td></tr>
              </ng-template>
            </p-table>
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    .section { margin-bottom: 1.5rem; }
    .section-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 0.5rem; }
    .section-header h3 { margin: 0; }
    .source { font-weight: 400; font-size: 0.85rem; color: #777; }
    .status-row { display: flex; align-items: center; gap: 1rem; flex-wrap: wrap; margin-bottom: 0.75rem; }
    .table-scroll { overflow-x: auto; }
    .table-scroll ::ng-deep .p-datatable-table { min-width: 500px; }
  `]
})
export class LegacyMonitoringPageComponent implements OnInit, OnDestroy {
  private api = inject(SyncStatusService);
  private toolbar = inject(GlobalToolbarService);

  status = signal<SyncStatusDto | null>(null);
  loading = signal(false);
  apiError = signal<string | null>(null);
  actionMessage = signal<string | null>(null);
  referenceSyncing = signal(false);
  pricingSyncing = signal(false);

  ngOnInit(): void {
    this.toolbar.set({
      title: 'Legacy Monitoring',
      refresh: { onClick: () => this.load() }
    });
    this.load();
  }

  ngOnDestroy(): void {
    this.toolbar.clear();
  }

  syncReference(): void {
    this.apiError.set(null);
    this.actionMessage.set(null);
    this.referenceSyncing.set(true);
    this.api.triggerReferenceSync().subscribe({
      next: res => {
        this.actionMessage.set(`Reference data sync complete. ${res.data ?? ''}`);
        this.referenceSyncing.set(false);
        this.load();
      },
      error: err => {
        this.apiError.set(err?.error?.message ?? 'Reference data sync failed.');
        this.referenceSyncing.set(false);
      }
    });
  }

  syncPricing(): void {
    this.apiError.set(null);
    this.actionMessage.set(null);
    this.pricingSyncing.set(true);
    this.api.triggerPricingSync().subscribe({
      next: res => {
        this.actionMessage.set(`Pricing masters sync complete. ${res.data ?? ''}`);
        this.pricingSyncing.set(false);
        this.load();
      },
      error: err => {
        this.apiError.set(err?.error?.message ?? 'Pricing masters sync failed.');
        this.pricingSyncing.set(false);
      }
    });
  }

  private load(): void {
    this.loading.set(true);
    this.api.getStatus().subscribe({
      next: res => {
        this.status.set(res.data ?? null);
        this.loading.set(false);
      },
      error: err => {
        this.apiError.set(err?.error?.message ?? 'Unable to load sync status.');
        this.loading.set(false);
      }
    });
  }
}
