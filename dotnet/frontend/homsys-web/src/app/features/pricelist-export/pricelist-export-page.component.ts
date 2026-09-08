import { AfterViewInit, Component, ElementRef, Injector, OnDestroy, OnInit, ViewChild, afterNextRender, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AutoCompleteModule, AutoCompleteCompleteEvent } from 'primeng/autocomplete';
import { CheckboxModule } from 'primeng/checkbox';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService, MenuItem } from 'primeng/api';
import { DatePickerModule } from 'primeng/datepicker';
import { InputNumberModule } from 'primeng/inputnumber';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { TableModule } from 'primeng/table';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { MultiSelectModule } from 'primeng/multiselect';
import { Menu, MenuModule } from 'primeng/menu';
import { GlobalToolbarService } from '../../core/services/global-toolbar.service';
import { SalesOrderService } from '../../core/services/sales-order.service';
import { PricelistService, PricelistPreviewResult, ZonePricelistPreviewResult, BranchOptionDto } from '../../core/services/pricelist.service';
import { CustomerSuggestionDto } from '../../core/models/sales-order.model';

type ReportType = 'account' | 'zone' | 'grouped';

@Component({
  selector: 'app-pricelist-export-page',
  standalone: true,
  providers: [ConfirmationService],
  imports: [FormsModule, AutoCompleteModule, CheckboxModule, ConfirmDialogModule, DatePickerModule, InputNumberModule, ButtonModule, MessageModule, TableModule, InputTextModule, SelectModule, MultiSelectModule, MenuModule],
  template: `
    <p-confirmDialog [appendTo]="'body'" />
    <div class="pe-shell">
      @if (apiError()) {
        <p-message severity="error" styleClass="w-full mb-3">
          <span>{{ apiError() }}</span>
        </p-message>
      }

      <div class="pe-body" [hidden]="isFullscreen()">
        @if (sidebarCollapsed()) {
          <div class="sidebar-expand-tab" (click)="sidebarCollapsed.set(false)" title="Show export options">
            <i class="pi pi-chevron-right"></i>
          </div>
        }
        @if (!sidebarCollapsed()) {
        <div class="pe-sidebar">
          <div class="sidebar-heading-row">
            <div class="sidebar-heading no-margin">Export Mode</div>
            <a class="hide-link" (click)="sidebarCollapsed.set(true)">Hide</a>
          </div>
          @for (opt of reportTypeOptions; track opt.value) {
            <label class="mode-radio">
              <input type="radio" name="mode" [checked]="reportType() === opt.value" (change)="onReportTypeChange(opt.value)" />
              {{ opt.label }}
            </label>
          }

          <div class="sidebar-heading">Branch @if (reportType() !== 'account') { <span class="required-star">*</span> }</div>
          <p-select [options]="branches()" optionLabel="label" optionValue="value"
                    [ngModel]="selectedBranch()" (ngModelChange)="selectedBranch.set($event)" [showClear]="reportType() === 'account'"
                    placeholder="All branches" [appendTo]="'body'" (onChange)="onBranchChange()" styleClass="sidebar-input" />

          @if (reportType() === 'zone') {
            <div class="sidebar-heading">Zones <span class="required-star">*</span></div>
            <p-multiselect [options]="zoneOptions()" optionLabel="label" optionValue="value"
                   [(ngModel)]="selectedZones" (onChange)="clearPreview()"
                   [showToggleAll]="true" display="chip" [filter]="true"
                   [disabled]="!selectedBranch()" placeholder="Select zones" [appendTo]="'body'" styleClass="sidebar-input" />
          }

          @if (reportType() === 'grouped') {
            <div class="sidebar-heading">Compare to Customer (optional)</div>
            <p-autoComplete [ngModel]="baselineCustomer()" (ngModelChange)="baselineCustomer.set($event); clearPreview()"
                   [suggestions]="baselineSuggestions()" field="custKey" [minLength]="2" [delay]="500"
                   [disabled]="groupedFieldsDisabled()"
                   [showClear]="true" [appendTo]="'body'" placeholder="Search customer key or name"
                   (completeMethod)="searchBaselineCustomers($event)">
              <ng-template let-item pTemplate="item">
                <div class="autocomplete-item">
                  <span class="autocomplete-item-label">{{ item.custKey }} - {{ item.cusName }}</span>
                </div>
              </ng-template>
            </p-autoComplete>
          }

          <div class="sidebar-heading">Effectivity Date <span class="required-star">*</span></div>
          <p-datepicker [(ngModel)]="effectivityDate" dateFormat="mm/dd/yy" [appendTo]="'body'" (onSelect)="clearPreview()"
                 [disabled]="groupedFieldsDisabled()" styleClass="sidebar-input" />

          <div class="sidebar-heading">SRP Markup %</div>
          <p-inputnumber [(ngModel)]="srpMarkupPercent" mode="decimal" [minFractionDigits]="0" [maxFractionDigits]="2" [min]="0"
                 (onInput)="clearPreview()" [disabled]="groupedFieldsDisabled()" styleClass="sidebar-input srp-input" />

          <p-button label="Generate" icon="pi pi-table" [loading]="generating()"
                 [disabled]="generateDisabled()" styleClass="generate-btn"
                 (onClick)="generate()" />

          @if (reportType() === 'account') {
            <div class="customers-header-row">
              <div class="sidebar-heading no-margin">
                Customers <span class="required-star">*</span>
                @if (selectedCustomers().length > 0) { — {{ selectedCustomers().length }} selected }
              </div>
              @if (selectedCustomers().length > 0) {
                <a class="clear-link" (click)="clearCustomers()">Clear</a>
              }
            </div>
            <div class="all-customers-toggle">
              <p-checkbox [binary]="true" inputId="allCustomers" [ngModel]="allCustomers()" [disabled]="!selectedBranch() || loadingAllCustomers()"
                          (onChange)="onAllCustomersChange($event.checked)" />
              <label for="allCustomers">All Customers</label>
            </div>
            <div class="customers-input-row" [class.collapsed]="customersCollapsed()" #customersRow (click)="onCustomersBoxClick()">
              <p-autoComplete [ngModel]="selectedCustomers()" (ngModelChange)="selectedCustomers.set($event)" [suggestions]="customerSuggestions()"
                     field="cusName" [multiple]="true" [minLength]="2"
                     [delay]="1000" [appendTo]="'body'" placeholder="Search customer key or name"
                     (completeMethod)="searchCustomers($event)" (onChange)="clearPreview()"
                     (onFocus)="onCustomersFocus()" (onBlur)="onCustomersBlur()">
                <ng-template let-item pTemplate="item">
                  <div class="autocomplete-item" (mousedown)="$event.preventDefault()" (click)="toggleCustomer(item); $event.stopPropagation()">
                    <p-checkbox [binary]="true" [ngModel]="isSelected(item)" (click)="$event.stopPropagation()" (onChange)="toggleCustomer(item)" />
                    <span class="autocomplete-item-label">{{ item.custKey }} - {{ item.cusName }}</span>
                  </div>
                </ng-template>
                <ng-template let-item pTemplate="selectedItem">
                  @if (customersCollapsed() && hiddenCustomersCount() > 0 && chipIndex(item) === visibleChipCount() - 1) {
                    <span class="cust-chip cust-chip-more" (click)="expandCustomers(); $event.stopPropagation()">
                      +{{ hiddenCustomersCount() + 1 }} more
                    </span>
                  } @else {
                    <span class="cust-chip" [class.chip-hidden]="customersCollapsed() && chipIndex(item) >= visibleChipCount()"
                          [style.background]="chipColor(item).bg" [style.border-color]="chipColor(item).border"
                          [style.color]="chipColor(item).text" [title]="item.custKey + ' - ' + item.cusName">
                      <span class="cust-chip-label">{{ item.custKey }} - {{ item.cusName }}</span>
                      <i class="pi pi-times-circle cust-chip-remove" (click)="toggleCustomer(item); $event.stopPropagation()"></i>
                    </span>
                  }
                </ng-template>
              </p-autoComplete>
            </div>
          }
        </div>
        }

        <div class="pe-main">
          @if (reportType() !== 'zone') {
            @if (previewResult(); as result) {
              <div class="results-section" [class.fullscreen]="isFullscreen()">
                <div class="pe-toolbar">
                  <div class="pe-summary">{{ filteredRows().length }} items · {{ result.customers.length }} {{ reportType() === 'grouped' ? 'price groups' : 'customers' }}</div>
                  <div class="pe-toolbar-actions">
                    <span class="p-input-icon-left filter-field">
                      <i class="pi pi-search"></i>
                      <input type="text" pInputText placeholder="Filter by SKU or description"
                             [ngModel]="filterText()" (ngModelChange)="filterText.set($event)" />
                    </span>
                    <span class="freeze-field">
                      <i class="pi pi-thumbtack"></i>
                      <p-select [options]="freezeOptions" optionLabel="label" optionValue="value"
                             [ngModel]="freezeColumns()" (ngModelChange)="freezeColumns.set($event)" />
                    </span>
                    <i class="pi fullscreen-toggle" [class.pi-window-maximize]="!isFullscreen()" [class.pi-window-minimize]="isFullscreen()"
                       (click)="toggleFullscreen()" [title]="isFullscreen() ? 'Exit full screen' : 'Full screen'"></i>
                    <button type="button" class="pe-export-btn" [disabled]="exporting()" (click)="onExportClick($event, exportMenu)">
                      <i class="pi pi-download"></i> Export
                    </button>
                    <p-menu #exportMenu [model]="exportMenuItems()" [popup]="true" appendTo="body" />
                  </div>
                </div>

                <div class="pe-results-scroll">
                  <p-table #pricelistTable [value]="filteredRows()" styleClass="p-datatable-sm pricelist-table"
                         [scrollable]="true" scrollHeight="flex">
                    <ng-template pTemplate="header">
                      <tr>
                        <th [class.frozen-col]="isFrozen(0)" [class.frozen-edge]="isLastFrozen(0)" [style.left.px]="isFrozen(0) ? colOffsets()[0] : null"><div class="frozen-th-inner">SKU</div></th>
                        <th [class.frozen-col]="isFrozen(1)" [class.frozen-edge]="isLastFrozen(1)" [style.left.px]="isFrozen(1) ? colOffsets()[1] : null"><div class="frozen-th-inner">Description</div></th>
                        <th [class.frozen-col]="isFrozen(2)" [class.frozen-edge]="isLastFrozen(2)" [style.left.px]="isFrozen(2) ? colOffsets()[2] : null"><div class="frozen-th-inner">Category</div></th>
                        <th [class.frozen-col]="isFrozen(3)" [class.frozen-edge]="isLastFrozen(3)" [style.left.px]="isFrozen(3) ? colOffsets()[3] : null"><div class="frozen-th-inner">Packing</div></th>
                        <th [class.frozen-col]="isFrozen(4)" [class.frozen-edge]="isLastFrozen(4)" [style.left.px]="isFrozen(4) ? colOffsets()[4] : null"><div class="frozen-th-inner">Pieces</div></th>
                        <th>Case Barcode</th>
                        <th>Barcode</th>
                        @for (cust of result.customers; track cust.custKey) {
                          <th colspan="3" class="cust-header">({{ cust.custKey }}) {{ cust.cusName }}</th>
                        }
                      </tr>
                      <tr>
                        <th [class.frozen-col]="isFrozen(0)" [class.frozen-edge]="isLastFrozen(0)" [style.left.px]="isFrozen(0) ? colOffsets()[0] : null"><div class="frozen-th-inner"></div></th>
                        <th [class.frozen-col]="isFrozen(1)" [class.frozen-edge]="isLastFrozen(1)" [style.left.px]="isFrozen(1) ? colOffsets()[1] : null"><div class="frozen-th-inner"></div></th>
                        <th [class.frozen-col]="isFrozen(2)" [class.frozen-edge]="isLastFrozen(2)" [style.left.px]="isFrozen(2) ? colOffsets()[2] : null"><div class="frozen-th-inner"></div></th>
                        <th [class.frozen-col]="isFrozen(3)" [class.frozen-edge]="isLastFrozen(3)" [style.left.px]="isFrozen(3) ? colOffsets()[3] : null"><div class="frozen-th-inner"></div></th>
                        <th [class.frozen-col]="isFrozen(4)" [class.frozen-edge]="isLastFrozen(4)" [style.left.px]="isFrozen(4) ? colOffsets()[4] : null"><div class="frozen-th-inner"></div></th>
                        <th></th>
                        <th></th>
                        @for (cust of result.customers; track cust.custKey) {
                          <th>Case w/ VAT</th>
                          <th>Unit w/ VAT</th>
                          <th>SRP</th>
                        }
                      </tr>
                    </ng-template>
                    <ng-template pTemplate="body" let-row>
                      <tr>
                        <td [class.frozen-col]="isFrozen(0)" [class.frozen-edge]="isLastFrozen(0)" [style.left.px]="isFrozen(0) ? colOffsets()[0] : null">{{ row.cProdNo }}</td>
                        <td [class.frozen-col]="isFrozen(1)" [class.frozen-edge]="isLastFrozen(1)" [style.left.px]="isFrozen(1) ? colOffsets()[1] : null" [title]="row.prodDesc">{{ row.prodDesc }}</td>
                        <td [class.frozen-col]="isFrozen(2)" [class.frozen-edge]="isLastFrozen(2)" [style.left.px]="isFrozen(2) ? colOffsets()[2] : null">{{ row.category }}</td>
                        <td [class.frozen-col]="isFrozen(3)" [class.frozen-edge]="isLastFrozen(3)" [style.left.px]="isFrozen(3) ? colOffsets()[3] : null">{{ row.packSize }}</td>
                        <td [class.frozen-col]="isFrozen(4)" [class.frozen-edge]="isLastFrozen(4)" [style.left.px]="isFrozen(4) ? colOffsets()[4] : null">{{ row.pieces }}</td>
                        <td>{{ row.caseBarcode }}</td>
                        <td>{{ row.barcode }}</td>
                        @for (cust of result.customers; track cust.custKey) {
                          <td>{{ formatValue(row.byCustKey[cust.custKey]?.casePriceWithVat) }}</td>
                          <td>{{ formatValue(row.byCustKey[cust.custKey]?.unitPriceWithVat) }}</td>
                          <td>{{ formatValue(row.byCustKey[cust.custKey]?.srp) }}</td>
                        }
                      </tr>
                    </ng-template>
                    <ng-template pTemplate="emptymessage">
                      <tr><td [attr.colspan]="7 + result.customers.length * 3">No products found.</td></tr>
                    </ng-template>
                  </p-table>
                </div>
              </div>
            }
          } @else {
            @if (zonePreviewResult(); as zresult) {
              <div class="results-section" [class.fullscreen]="isFullscreen()">
                <div class="pe-toolbar">
                  <div class="pe-summary">{{ zoneFilteredRows().length }} items · {{ zresult.zones.length }} zones</div>
                  <div class="pe-toolbar-actions">
                    <span class="p-input-icon-left filter-field">
                      <i class="pi pi-search"></i>
                      <input type="text" pInputText placeholder="Filter by SKU or description"
                             [ngModel]="filterText()" (ngModelChange)="filterText.set($event)" />
                    </span>
                    <span class="freeze-field">
                      <i class="pi pi-thumbtack"></i>
                      <p-select [options]="freezeOptions" optionLabel="label" optionValue="value"
                             [ngModel]="freezeColumns()" (ngModelChange)="freezeColumns.set($event)" />
                    </span>
                    <i class="pi fullscreen-toggle" [class.pi-window-maximize]="!isFullscreen()" [class.pi-window-minimize]="isFullscreen()"
                       (click)="toggleFullscreen()" [title]="isFullscreen() ? 'Exit full screen' : 'Full screen'"></i>
                    <button type="button" class="pe-export-btn" [disabled]="exporting()" (click)="onExportClick($event, exportMenu)">
                      <i class="pi pi-download"></i> Export
                    </button>
                    <p-menu #exportMenu [model]="exportMenuItems()" [popup]="true" appendTo="body" />
                  </div>
                </div>

                <div class="pe-results-scroll">
                  <p-table #pricelistTable [value]="zoneFilteredRows()" styleClass="p-datatable-sm pricelist-table"
                         [scrollable]="true" scrollHeight="flex">
                    <ng-template pTemplate="header">
                      <tr>
                        <th [class.frozen-col]="isFrozen(0)" [class.frozen-edge]="isLastFrozen(0)" [style.left.px]="isFrozen(0) ? colOffsets()[0] : null"><div class="frozen-th-inner">SKU</div></th>
                        <th [class.frozen-col]="isFrozen(1)" [class.frozen-edge]="isLastFrozen(1)" [style.left.px]="isFrozen(1) ? colOffsets()[1] : null"><div class="frozen-th-inner">Description</div></th>
                        <th [class.frozen-col]="isFrozen(2)" [class.frozen-edge]="isLastFrozen(2)" [style.left.px]="isFrozen(2) ? colOffsets()[2] : null"><div class="frozen-th-inner">Category</div></th>
                        <th [class.frozen-col]="isFrozen(3)" [class.frozen-edge]="isLastFrozen(3)" [style.left.px]="isFrozen(3) ? colOffsets()[3] : null"><div class="frozen-th-inner">Packing</div></th>
                        <th [class.frozen-col]="isFrozen(4)" [class.frozen-edge]="isLastFrozen(4)" [style.left.px]="isFrozen(4) ? colOffsets()[4] : null"><div class="frozen-th-inner">Pieces</div></th>
                        <th>Case Barcode</th>
                        <th>Barcode</th>
                        @for (zone of zresult.zones; track zone.zone) {
                          <th colspan="3" class="cust-header">ZONE {{ zone.zone }}</th>
                        }
                      </tr>
                      <tr>
                        <th [class.frozen-col]="isFrozen(0)" [class.frozen-edge]="isLastFrozen(0)" [style.left.px]="isFrozen(0) ? colOffsets()[0] : null"><div class="frozen-th-inner"></div></th>
                        <th [class.frozen-col]="isFrozen(1)" [class.frozen-edge]="isLastFrozen(1)" [style.left.px]="isFrozen(1) ? colOffsets()[1] : null"><div class="frozen-th-inner"></div></th>
                        <th [class.frozen-col]="isFrozen(2)" [class.frozen-edge]="isLastFrozen(2)" [style.left.px]="isFrozen(2) ? colOffsets()[2] : null"><div class="frozen-th-inner"></div></th>
                        <th [class.frozen-col]="isFrozen(3)" [class.frozen-edge]="isLastFrozen(3)" [style.left.px]="isFrozen(3) ? colOffsets()[3] : null"><div class="frozen-th-inner"></div></th>
                        <th [class.frozen-col]="isFrozen(4)" [class.frozen-edge]="isLastFrozen(4)" [style.left.px]="isFrozen(4) ? colOffsets()[4] : null"><div class="frozen-th-inner"></div></th>
                        <th></th>
                        <th></th>
                        @for (zone of zresult.zones; track zone.zone) {
                          <th>Case w/ VAT</th>
                          <th>Unit w/ VAT</th>
                          <th>SRP</th>
                        }
                      </tr>
                    </ng-template>
                    <ng-template pTemplate="body" let-row>
                      <tr>
                        <td [class.frozen-col]="isFrozen(0)" [class.frozen-edge]="isLastFrozen(0)" [style.left.px]="isFrozen(0) ? colOffsets()[0] : null">{{ row.cProdNo }}</td>
                        <td [class.frozen-col]="isFrozen(1)" [class.frozen-edge]="isLastFrozen(1)" [style.left.px]="isFrozen(1) ? colOffsets()[1] : null" [title]="row.prodDesc">{{ row.prodDesc }}</td>
                        <td [class.frozen-col]="isFrozen(2)" [class.frozen-edge]="isLastFrozen(2)" [style.left.px]="isFrozen(2) ? colOffsets()[2] : null">{{ row.category }}</td>
                        <td [class.frozen-col]="isFrozen(3)" [class.frozen-edge]="isLastFrozen(3)" [style.left.px]="isFrozen(3) ? colOffsets()[3] : null">{{ row.packSize }}</td>
                        <td [class.frozen-col]="isFrozen(4)" [class.frozen-edge]="isLastFrozen(4)" [style.left.px]="isFrozen(4) ? colOffsets()[4] : null">{{ row.pieces }}</td>
                        <td>{{ row.caseBarcode }}</td>
                        <td>{{ row.barcode }}</td>
                        @for (zone of zresult.zones; track zone.zone) {
                          <td>{{ formatValue(row.byZone[zone.zone]?.casePriceWithVat) }}</td>
                          <td>{{ formatValue(row.byZone[zone.zone]?.unitPriceWithVat) }}</td>
                          <td>{{ formatValue(row.byZone[zone.zone]?.srp) }}</td>
                        }
                      </tr>
                    </ng-template>
                    <ng-template pTemplate="emptymessage">
                      <tr><td [attr.colspan]="7 + zresult.zones.length * 3">No products found.</td></tr>
                    </ng-template>
                  </p-table>
                </div>
              </div>
            }
          }
        </div>
      </div>
    </div>
  `,
  styles: [`
    :host { display: block; height: 100%; overflow: hidden; }
    .pe-shell { display: flex; flex-direction: column; height: 100%; min-height: 0; font-size: 0.85rem; }
    .required-star { color: var(--p-red-500, #ef4444); margin-left: 2px; }

    .pe-body { display: flex; flex: 1 1 auto; min-height: 0; }

    /* ── Sidebar ── */
    .pe-sidebar {
      flex: 0 0 260px; border-right: 1px solid var(--p-surface-200, #e7e3dd);
      padding: 18px; display: flex; flex-direction: column; overflow-y: auto;
    }
    .sidebar-heading { font-size: 11px; font-weight: 700; color: #6b6b6b; margin: 14px 0 5px; }
    .sidebar-heading:first-child { margin-top: 0; }
    .sidebar-heading.no-margin { margin: 0; }
    .sidebar-heading-row { display: flex; align-items: baseline; justify-content: space-between; gap: 8px; }
    .hide-link { font-size: 11px; color: var(--p-text-muted-color, #888); cursor: pointer; white-space: nowrap; text-decoration: underline; }
    .hide-link:hover { color: #6b6b6b; }
    .sidebar-expand-tab {
      position: fixed; top: 120px; left: 0; z-index: 50;
      display: flex; align-items: center; justify-content: center;
      width: 20px; height: 40px; border-radius: 0 6px 6px 0;
      background: var(--p-surface-100, #f1f5f9); border: 1px solid var(--p-surface-200, #e7e3dd); border-left: none;
      color: var(--p-text-muted-color, #6b6b6b); cursor: pointer;
    }
    .sidebar-expand-tab:hover { background: var(--p-surface-200, #e7e3dd); }
    .mode-radio { display: flex; align-items: center; gap: 8px; font-size: 13px; margin-bottom: 8px; cursor: pointer; }
    .pe-sidebar ::ng-deep .sidebar-input,
    .pe-sidebar ::ng-deep .sidebar-input .p-select,
    .pe-sidebar ::ng-deep .sidebar-input.p-multiselect,
    .pe-sidebar ::ng-deep .sidebar-input .p-datepicker-input,
    .pe-sidebar ::ng-deep .sidebar-input.p-inputnumber .p-inputnumber-input { width: 100%; font-size: 13px; }
    .pe-sidebar ::ng-deep .srp-input.p-inputnumber .p-inputnumber-input { width: 70px; }
    .pe-sidebar ::ng-deep .generate-btn.p-button {
      background: #e2574c; border-color: #e2574c; color: #fff; font-weight: 700;
      width: 100%; margin: 16px 0 20px; justify-content: center;
    }
    .pe-sidebar ::ng-deep .generate-btn.p-button:not(:disabled):hover { background: #c8483e; border-color: #c8483e; }

    .customers-header-row { display: flex; align-items: baseline; justify-content: space-between; gap: 8px; margin-bottom: 8px; }
    .clear-link { font-size: 11px; color: var(--p-red-500, #ef4444); cursor: pointer; white-space: nowrap; }
    .clear-link:hover { color: var(--p-red-700, #b91c1c); }
    .all-customers-toggle { display: flex; align-items: center; gap: 0.4rem; margin-bottom: 10px; }
    .all-customers-toggle label { font-size: 0.78rem; cursor: pointer; }
    .customers-input-row { width: 100%; }
    .customers-input-row ::ng-deep .p-autocomplete { width: 100%; }
    .customers-input-row ::ng-deep .p-autocomplete-input-multiple {
      flex-wrap: wrap; align-items: flex-start; align-content: flex-start;
      padding: 0.25rem; border: 1px solid #d8d4cf; border-radius: 6px; min-height: 80px;
    }
    .customers-input-row ::ng-deep .p-autocomplete-chip-item { display: inline-flex; max-width: 220px; margin: 2px; }
    .cust-chip {
      display: inline-flex; align-items: center; gap: 0.4rem; max-width: 220px;
      padding: 0.2rem 0.6rem; border-radius: 1rem; border: 1px solid; font-size: 0.78rem;
    }
    .cust-chip-label { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .cust-chip-remove { cursor: pointer; font-size: 0.75rem; flex-shrink: 0; }
    .cust-chip.chip-hidden { display: none; }
    /* Hiding the inner span isn't enough — PrimeNG's own <li> wrapper around each
       selected item still carries margin/padding and stays in the flex-wrap flow,
       so hundreds of empty wrappers push extra (invisible) rows below the visible
       two. Collapse the wrapper itself whenever its content is hidden. */
    .customers-input-row.collapsed ::ng-deep .p-autocomplete-chip-item:has(.chip-hidden) { display: none; }
    .customers-input-row.collapsed ::ng-deep .p-autocomplete-input-chip { display: none; }
    .cust-chip-more {
      cursor: pointer; font-weight: 500; border-color: transparent;
      background: var(--p-surface-200, #eceff1); color: var(--p-text-muted-color, #546e7a);
    }
    .cust-chip-more:hover { background: var(--p-surface-300, #cfd8dc); }

    /* ── Main results pane ── */
    .pe-main { flex: 1 1 auto; display: flex; flex-direction: column; min-height: 0; min-width: 0; }
    .results-section { flex: 1 1 auto; display: flex; flex-direction: column; min-height: 0; min-width: 0; }
    .results-section.fullscreen {
      position: fixed; inset: 0; z-index: 1000;
      background: var(--p-content-background, #fff);
      padding: 1rem 1.25rem; display: flex; flex-direction: column; overflow: hidden;
    }

    .pe-toolbar { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 0.75rem; padding: 14px 20px 10px; flex: none; }
    .pe-summary { font-size: 13px; font-weight: 700; white-space: nowrap; }
    .pe-toolbar-actions { display: flex; align-items: center; gap: 0.75rem; flex: none; flex-wrap: wrap; }

    .pe-results-scroll { flex: 1 1 auto; min-height: 0; padding: 0 20px 20px; }
    .pe-results-scroll > p-table { display: flex; flex-direction: column; flex: 1 1 auto; min-height: 0; height: 100%; }

    .pe-export-btn {
      background: #2c7a4b; color: #fff; border: none; border-radius: 6px; padding: 8px 14px;
      font-size: 12px; font-weight: 700; cursor: pointer; white-space: nowrap; flex: none;
      display: inline-flex; align-items: center; gap: 6px;
    }
    .pe-export-btn:hover:not(:disabled) { background: #256640; }
    .pe-export-btn:disabled { opacity: 0.6; cursor: default; }

    ::ng-deep .pricelist-table.p-datatable { display: block; width: 100%; min-width: 0; }
    ::ng-deep .pricelist-table .p-datatable-table-container {
      width: 100%;
      overflow-x: scroll !important;
      scrollbar-width: auto;
    }
    ::ng-deep .pricelist-table .p-datatable-table-container::-webkit-scrollbar {
      height: 10px;
    }
    ::ng-deep .pricelist-table .p-datatable-table-container::-webkit-scrollbar-track {
      background: var(--p-surface-100, #f1f5f9);
    }
    ::ng-deep .pricelist-table .p-datatable-table-container::-webkit-scrollbar-thumb {
      background: var(--p-surface-400, #94a3b8);
      border-radius: 4px;
    }
    ::ng-deep .pricelist-table .p-datatable-table-container::-webkit-scrollbar-thumb:hover {
      background: var(--p-surface-500, #64748b);
    }

    ::ng-deep .p-autocomplete-option { padding: 0 !important; align-items: stretch !important; }
    .autocomplete-item {
      display: flex; align-items: center; gap: 0.5rem;
      width: 100%; box-sizing: border-box; padding: 0.5rem 0.75rem;
      overflow: hidden; cursor: pointer;
    }
    .autocomplete-item-label { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .fullscreen-toggle { cursor: pointer; font-size: 1rem; color: var(--p-text-muted-color, #888); }
    .filter-field { position: relative; display: inline-block; }
    .filter-field .pi-search { position: absolute; left: 0.6rem; top: 50%; transform: translateY(-50%); color: var(--p-text-muted-color, #888); font-size: 0.75rem; }
    .filter-field input { padding-left: 1.9rem; width: 180px; font-size: 0.8rem; }
    .freeze-field { position: relative; display: inline-flex; align-items: center; gap: 0.35rem; }
    .freeze-field .pi-thumbtack { color: var(--p-text-muted-color, #888); font-size: 0.75rem; }
    .freeze-field ::ng-deep .p-select { font-size: 0.78rem; min-width: 11rem; }
    ::ng-deep .pricelist-table .p-datatable-table { min-width: 900px; font-size: 0.78rem; }
    ::ng-deep .pricelist-table .p-datatable-thead > tr > th { padding: 0.4rem 0.5rem; }
    ::ng-deep .pricelist-table .p-datatable-tbody > tr > td { padding: 0.3rem 0.5rem; }
    .cust-header { text-align: center; }

    ::ng-deep .pricelist-table .p-datatable-table th:nth-child(-n+5),
    ::ng-deep .pricelist-table .p-datatable-table td:nth-child(-n+5) {
      box-sizing: border-box; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
    }
    ::ng-deep .pricelist-table .p-datatable-table th:nth-child(1),
    ::ng-deep .pricelist-table .p-datatable-table td:nth-child(1) { width: 70px; max-width: 70px; }
    ::ng-deep .pricelist-table .p-datatable-table th:nth-child(2),
    ::ng-deep .pricelist-table .p-datatable-table td:nth-child(2) { width: 200px; max-width: 200px; }
    ::ng-deep .pricelist-table .p-datatable-table th:nth-child(3),
    ::ng-deep .pricelist-table .p-datatable-table td:nth-child(3) { width: 90px; max-width: 90px; }
    ::ng-deep .pricelist-table .p-datatable-table th:nth-child(4),
    ::ng-deep .pricelist-table .p-datatable-table td:nth-child(4) { width: 80px; max-width: 80px; }
    ::ng-deep .pricelist-table .p-datatable-table th:nth-child(5),
    ::ng-deep .pricelist-table .p-datatable-table td:nth-child(5) { width: 60px; max-width: 60px; }

    ::ng-deep .pricelist-table .frozen-col {
      position: sticky;
      z-index: 2;
      background: var(--p-content-background, #ffffff) !important;
    }
    ::ng-deep .pricelist-table .p-datatable-tbody > tr > td.frozen-col {
      background: var(--p-content-background, #ffffff) !important;
    }
    ::ng-deep .pricelist-table .p-datatable-thead > tr > th.frozen-col {
      z-index: 6;
      background: var(--p-surface-50, #f8fafc) !important;
    }
    ::ng-deep .pricelist-table .frozen-edge { box-shadow: 2px 0 4px rgba(0, 0, 0, 0.12); }

    /* A th that is sticky on BOTH axes (left for freeze + top for header-lock) is unreliable in
       real browsers for table cells — the vertical stick gets dropped. Split the axes across two
       elements: the th itself keeps left (freeze), a nested block owns top (header-lock). */
    ::ng-deep .pricelist-table .p-datatable-thead > tr > th.frozen-col > .frozen-th-inner {
      position: sticky;
      top: 0;
      display: block;
      width: 100%;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
  `]
})
export class PricelistExportPageComponent implements OnInit, AfterViewInit, OnDestroy {
  private api = inject(SalesOrderService);
  private pricelistApi = inject(PricelistService);
  private toolbar = inject(GlobalToolbarService);
  private confirmSvc = inject(ConfirmationService);
  private injector = inject(Injector);
  private resizeHandler = () => this.measureFreezeOffsets();

  @ViewChild('customersRow') customersRowRef?: ElementRef<HTMLDivElement>;
  @ViewChild('pricelistTable', { read: ElementRef }) pricelistTableRef?: ElementRef<HTMLElement>;

  readonly reportTypeOptions: { label: string; value: ReportType }[] = [
    { label: 'Per Account', value: 'account' },
    { label: 'Per Zone', value: 'zone' },
    { label: 'Grouped by Price', value: 'grouped' },
  ];
  reportType = signal<ReportType>('account');
  sidebarCollapsed = signal(false);

  branches = signal<BranchOptionDto[]>([]);
  selectedBranch = signal<string | null>(null);

  customerSuggestions = signal<CustomerSuggestionDto[]>([]);
  selectedCustomers = signal<CustomerSuggestionDto[]>([]);
  allCustomers = signal(false);
  loadingAllCustomers = signal(false);
  customersCollapsed = signal(false);
  visibleChipCount = signal(1);
  hiddenCustomersCount = computed(() => this.customersCollapsed() ? Math.max(0, this.selectedCustomers().length - this.visibleChipCount()) : 0);

  zoneOptions = signal<{ label: string; value: string }[]>([]);
  selectedZones = signal<string[]>([]);
  zonePreviewResult = signal<ZonePricelistPreviewResult | null>(null);

  baselineCustomer = signal<CustomerSuggestionDto | null>(null);
  baselineSuggestions = signal<CustomerSuggestionDto[]>([]);

  isFullscreen = signal(false);
  effectivityDate = signal<Date>(new Date());
  srpMarkupPercent = signal<number>(3);
  generating = signal(false);
  exporting = signal(false);
  apiError = signal<string | null>(null);
  previewResult = signal<PricelistPreviewResult | null>(null);
  filterText = signal('');
  freezeColumns = signal(0);
  readonly freezeOptions = [
    { label: 'No Freeze', value: 0 },
    { label: 'Freeze thru SKU', value: 1 },
    { label: 'Freeze thru Description', value: 2 },
    { label: 'Freeze thru Category', value: 3 },
    { label: 'Freeze thru Packing', value: 4 },
    { label: 'Freeze thru Pieces', value: 5 },
  ];
  colOffsets = signal<number[]>([0, 0, 0, 0, 0]);
  private flatRows = computed(() => {
    const result = this.previewResult();
    if (!result) return [];
    return result.groups.flatMap(group =>
      group.rows.map(row => ({ ...row, category: group.header ?? '' })));
  });
  filteredRows = computed(() => {
    const term = this.filterText().trim().toLowerCase();
    const rows = this.flatRows();
    if (!term) return rows;
    return rows.filter(row =>
      row.cProdNo?.toLowerCase().includes(term) ||
      row.prodDesc?.toLowerCase().includes(term) ||
      row.category?.toLowerCase().includes(term));
  });

  private zoneFlatRows = computed(() => {
    const result = this.zonePreviewResult();
    if (!result) return [];
    return result.groups.flatMap(group =>
      group.rows.map(row => ({ ...row, category: group.header ?? '' })));
  });
  zoneFilteredRows = computed(() => {
    const term = this.filterText().trim().toLowerCase();
    const rows = this.zoneFlatRows();
    if (!term) return rows;
    return rows.filter(row =>
      row.cProdNo?.toLowerCase().includes(term) ||
      row.prodDesc?.toLowerCase().includes(term) ||
      row.category?.toLowerCase().includes(term));
  });

  generateDisabled = computed(() => {
    if (!this.effectivityDate()) return true;
    switch (this.reportType()) {
      case 'account': return this.selectedCustomers().length === 0;
      case 'zone': return !this.selectedBranch() || this.selectedZones().length === 0;
      case 'grouped': return !this.selectedBranch();
    }
  });

  // Grouped mode's date/SRP/baseline fields are meaningless without a branch —
  // reactive on selectedBranch() now that it's a real signal, unlike the plain
  // field it used to be (which computed()s couldn't react to on its own).
  groupedFieldsDisabled = computed(() => this.reportType() === 'grouped' && !this.selectedBranch());

  // The inline Export button in the results pane reproduces what the app's
  // global toolbar used to do for this page: Per Account has two export
  // variants (opens a popup menu), Per Zone/Grouped each have exactly one
  // (the button fires it directly — see onExportClick).
  exportMenuItems = computed((): MenuItem[] => {
    switch (this.reportType()) {
      case 'account':
        return [
          { label: 'Export Per Comparison', icon: 'pi pi-file-excel', command: () => this.exportToExcel() },
          { label: 'Export Per Account',    icon: 'pi pi-file-excel', command: () => this.exportPerAccount() }
        ];
      case 'zone':
        return [{ label: 'Export', icon: 'pi pi-file-excel', command: () => this.exportZone() }];
      case 'grouped':
        return [{ label: 'Export Grouped Pricelist', icon: 'pi pi-file-excel', command: () => this.exportGrouped() }];
    }
  });

  onExportClick(event: Event, menu: Menu): void {
    const items = this.exportMenuItems();
    if (items.length === 1) { items[0].command?.({ originalEvent: event, item: items[0] }); return; }
    menu.toggle(event);
  }

  ngOnInit(): void {
    this.toolbar.set({ title: 'Pricelist Export' });
    this.pricelistApi.getBranches().subscribe({
      next: res => this.branches.set(res.data ?? []),
      error: () => this.branches.set([])
    });
  }

  onReportTypeChange(type: ReportType): void {
    this.reportType.set(type);
    this.clearPreview();
    this.apiError.set(null);
    if (type === 'zone' && this.selectedBranch()) this.loadZonesForBranch(this.selectedBranch()!);
  }

  onBranchChange(): void {
    this.customerSearchCache.clear();
    this.selectedCustomers.set([]);
    this.allCustomers.set(false);
    this.customersCollapsed.set(false);
    this.visibleChipCount.set(1);
    this.selectedZones.set([]);
    this.zoneOptions.set([]);
    this.clearPreview();

    if (this.reportType() === 'zone' && this.selectedBranch()) this.loadZonesForBranch(this.selectedBranch()!);
  }

  private loadZonesForBranch(branch: string): void {
    this.pricelistApi.getZonesForBranch(branch).subscribe({
      next: res => {
        const zones = res.data ?? [];
        this.zoneOptions.set(zones.map(z => ({ label: z.label, value: z.zone })));
      },
      error: () => this.apiError.set('Failed to load zones for the selected branch.')
    });
  }

  clearCustomers(): void {
    this.allCustomers.set(false);
    this.selectedCustomers.set([]);
    this.customersCollapsed.set(false);
    this.visibleChipCount.set(1);
    this.clearPreview();
  }

  onAllCustomersChange(checked: boolean): void {
    this.allCustomers.set(checked);
    this.clearPreview();

    if (!checked) {
      this.selectedCustomers.set([]);
      this.customersCollapsed.set(false);
      this.visibleChipCount.set(1);
      return;
    }

    const branch = this.selectedBranch();
    if (!branch) return;

    this.loadingAllCustomers.set(true);
    this.api.searchCustomers('', branch, 20000).subscribe({
      next: res => {
        this.selectedCustomers.set(res.data ?? []);
        this.loadingAllCustomers.set(false);
        afterNextRender(() => this.waitForChipsThenCollapse(), { injector: this.injector });
      },
      error: () => {
        this.apiError.set('Failed to load all customers for the selected branch.');
        this.allCustomers.set(false);
        this.loadingAllCustomers.set(false);
      }
    });
  }

  ngAfterViewInit(): void {
    setTimeout(() => this.measureFreezeOffsets(), 0);
    window.addEventListener('resize', this.resizeHandler);
  }

  ngOnDestroy(): void {
    this.toolbar.clear();
    window.removeEventListener('resize', this.resizeHandler);
  }

  // Keyed by sorted lowercase keywords so "Puregold Isabela" and "Isabela Puregold"
  // share a cache entry, and repeated/backspaced-then-retyped terms skip the API call.
  private customerSearchCache = new Map<string, CustomerSuggestionDto[]>();

  private cacheKey(term: string): string {
    const branch = this.selectedBranch() ?? '';
    return `${branch}|${term.toLowerCase().split(' ').filter(Boolean).sort().join(' ')}`;
  }

  // Chips are colored by which search term found the customer: the first search
  // ("puregold") gets a color, and re-searching that same term later reuses it.
  private lastSearchTerm = '';
  private custKeyTerm = new Map<string, string>();
  private termColors = new Map<string, { bg: string; border: string; text: string }>();
  private static readonly CHIP_PALETTE = [
    { bg: '#e8f5e9', border: '#81c784', text: '#1b5e20' },
    { bg: '#e3f2fd', border: '#64b5f6', text: '#0d47a1' },
    { bg: '#fff3e0', border: '#ffb74d', text: '#e65100' },
    { bg: '#f3e5f5', border: '#ba68c8', text: '#4a148c' },
    { bg: '#fce4ec', border: '#f06292', text: '#880e4f' },
    { bg: '#e0f7fa', border: '#4dd0e1', text: '#006064' },
    { bg: '#fff9c4', border: '#dce775', text: '#827717' },
  ];
  private readonly defaultChipColor = { bg: '#eceff1', border: '#b0bec5', text: '#37474f' };

  chipColor(item: CustomerSuggestionDto): { bg: string; border: string; text: string } {
    const term = this.custKeyTerm.get(item.custKey);
    if (!term) return this.defaultChipColor;
    return this.termColors.get(term) ?? this.defaultChipColor;
  }

  searchCustomers(event: AutoCompleteCompleteEvent): void {
    const term = (event.query ?? '').trim();
    if (!term) { this.customerSuggestions.set([]); return; }
    const key = this.cacheKey(term);
    this.lastSearchTerm = key;
    if (!this.termColors.has(key)) {
      this.termColors.set(key, PricelistExportPageComponent.CHIP_PALETTE[this.termColors.size % PricelistExportPageComponent.CHIP_PALETTE.length]);
    }
    const cached = this.customerSearchCache.get(key);
    if (cached) { this.customerSuggestions.set(cached); return; }
    this.api.searchCustomers(term, this.selectedBranch() ?? undefined).subscribe({
      next: res => {
        const data = res.data ?? [];
        this.customerSearchCache.set(key, data);
        this.customerSuggestions.set(data);
      },
      error: () => this.customerSuggestions.set([])
    });
  }

  // Unscoped by branch — the baseline is an arbitrary comparison customer,
  // not necessarily one of the selected branch's own accounts.
  searchBaselineCustomers(event: AutoCompleteCompleteEvent): void {
    const term = (event.query ?? '').trim();
    if (!term) { this.baselineSuggestions.set([]); return; }
    this.api.searchCustomers(term).subscribe({
      next: res => this.baselineSuggestions.set(res.data ?? []),
      error: () => this.baselineSuggestions.set([])
    });
  }

  isSelected(item: CustomerSuggestionDto): boolean {
    return this.selectedCustomers().some(c => c.custKey === item.custKey);
  }

  chipIndex(item: CustomerSuggestionDto): number {
    return this.selectedCustomers().findIndex(c => c.custKey === item.custKey);
  }

  toggleCustomersCollapsed(): void {
    if (!this.customersCollapsed()) {
      this.visibleChipCount.set(this.computeVisibleChipCount());
    }
    this.customersCollapsed.update(v => !v);
  }

  onCustomersBlur(): void {
    if (this.selectedCustomers().length > 0) {
      this.visibleChipCount.set(this.computeVisibleChipCount());
      this.customersCollapsed.set(true);
    }
  }

  private confirmingExpand = false;

  expandCustomers(): void {
    const count = this.selectedCustomers().length;
    if (count > 20) {
      if (this.confirmingExpand) return;
      this.confirmingExpand = true;
      this.confirmSvc.confirm({
        message: `This will show all ${count} customers. Continue?`,
        header: 'Show All Customers',
        icon: 'pi pi-exclamation-triangle',
        acceptLabel: 'Yes',
        rejectLabel: 'Cancel',
        accept: () => {
          this.confirmingExpand = false;
          this.customersCollapsed.set(false);
          setTimeout(() => this.focusCustomersInput(), 0);
        },
        reject: () => {
          this.confirmingExpand = false;
        }
      });
      return;
    }

    this.customersCollapsed.set(false);
    setTimeout(() => this.focusCustomersInput(), 0);
  }

  onCustomersFocus(): void {
    this.customersCollapsed.set(false);
  }

  onCustomersBoxClick(): void {
    this.customersCollapsed.set(false);
    setTimeout(() => this.focusCustomersInput(), 0);
  }

  private focusCustomersInput(): void {
    this.customersRowRef?.nativeElement.querySelector<HTMLInputElement>('input[role="combobox"]')?.focus();
  }

  // Bulk-loading thousands of chips can render across more frames than a single
  // afterNextRender callback waits for (the chip list library can flush its own
  // DOM update outside Angular's render commit) — poll a few frames until the
  // chips actually exist before measuring, instead of trusting one callback.
  private waitForChipsThenCollapse(attempt = 0): void {
    const row = this.customersRowRef?.nativeElement;
    const hasChips = !!row && row.querySelectorAll('.cust-chip').length > 0;
    if (!hasChips && attempt < 20) {
      requestAnimationFrame(() => this.waitForChipsThenCollapse(attempt + 1));
      return;
    }
    this.waitForStableWidthThenCollapse();
  }

  // The sidebar's customer field has a fixed width (unlike the old shared-row
  // layout that animated between 300px and 100%), but a browser reflow right
  // after bulk-loading thousands of chips can still momentarily report a
  // stale width before settling. Wait for the row's width to stop changing
  // between frames before measuring chips-per-row.
  private waitForStableWidthThenCollapse(lastWidth = -1, attempt = 0): void {
    const row = this.customersRowRef?.nativeElement;
    const width = row?.getBoundingClientRect().width ?? 0;
    if (Math.abs(width - lastWidth) > 0.5 && attempt < 20) {
      requestAnimationFrame(() => this.waitForStableWidthThenCollapse(width, attempt + 1));
      return;
    }
    this.visibleChipCount.set(this.computeVisibleChipCount());
    this.customersCollapsed.set(true);
  }

  private computeVisibleChipCount(): number {
    const row = this.customersRowRef?.nativeElement;
    if (!row) return 1;
    const chips = Array.from(row.querySelectorAll<HTMLElement>('.cust-chip'));
    if (chips.length === 0) return 1;
    const maxRows = 2;
    const rowTops: number[] = [];
    let count = 0;
    for (const chip of chips) {
      const top = chip.getBoundingClientRect().top;
      if (!rowTops.some(t => Math.abs(t - top) < 2)) {
        if (rowTops.length >= maxRows) break;
        rowTops.push(top);
      }
      count++;
    }
    return Math.max(1, count);
  }

  toggleFullscreen(): void {
    this.isFullscreen.update(v => !v);
    setTimeout(() => this.measureFreezeOffsets(), 0);
  }

  isFrozen(colIndex: number): boolean {
    return colIndex < this.freezeColumns();
  }

  isLastFrozen(colIndex: number): boolean {
    return colIndex === this.freezeColumns() - 1;
  }

  measureFreezeOffsets(): void {
    const table = this.pricelistTableRef?.nativeElement;
    if (!table) return;
    const headerCells = table.querySelectorAll<HTMLElement>('.p-datatable-thead > tr:first-child > th');
    if (headerCells.length < 5) return;
    const offsets: number[] = [];
    let left = 0;
    for (let i = 0; i < 5; i++) {
      offsets.push(left);
      left += headerCells[i].getBoundingClientRect().width;
    }
    this.colOffsets.set(offsets);
  }

  toggleCustomer(item: CustomerSuggestionDto): void {
    const current = this.selectedCustomers();
    if (this.isSelected(item)) {
      this.selectedCustomers.set(current.filter(c => c.custKey !== item.custKey));
    } else {
      if (this.lastSearchTerm) this.custKeyTerm.set(item.custKey, this.lastSearchTerm);
      this.selectedCustomers.set([...current, item]);
    }
    this.clearPreview();
    this.focusCustomersInput();
  }

  clearPreview(): void {
    this.previewResult.set(null);
    this.zonePreviewResult.set(null);
    this.filterText.set('');
  }

  formatValue(value: number | null | undefined): string {
    return value == null ? '—' : value.toFixed(2);
  }

  generate(): void {
    switch (this.reportType()) {
      case 'account': this.generateAccount(); break;
      case 'zone': this.generateZone(); break;
      case 'grouped': this.generateGrouped(); break;
    }
  }

  private generateAccount(): void {
    const customers = this.selectedCustomers();
    const date = this.effectivityDate();
    if (customers.length === 0 || !date) return;

    this.apiError.set(null);
    this.previewResult.set(null);
    this.generating.set(true);

    this.pricelistApi.preview(this.buildRequest(customers, date)).subscribe({
      next: res => {
        this.previewResult.set(res.data);
        this.generating.set(false);
        setTimeout(() => this.measureFreezeOffsets(), 0);
      },
      error: () => {
        this.apiError.set('Failed to generate the pricelist preview.');
        this.generating.set(false);
      }
    });
  }

  private generateZone(): void {
    const branch = this.selectedBranch();
    const zones = this.selectedZones();
    const date = this.effectivityDate();
    if (!branch || zones.length === 0 || !date) return;

    this.apiError.set(null);
    this.zonePreviewResult.set(null);
    this.generating.set(true);

    this.pricelistApi.zonePreview({
      branch, zones, effectivityDate: this.toIsoDate(date), srpMarkupPercent: this.srpMarkupPercent() ?? 3
    }).subscribe({
      next: res => {
        this.zonePreviewResult.set(res.data);
        this.generating.set(false);
        setTimeout(() => this.measureFreezeOffsets(), 0);
      },
      error: () => {
        this.apiError.set('Failed to generate the pricelist preview.');
        this.generating.set(false);
      }
    });
  }

  private generateGrouped(): void {
    const branch = this.selectedBranch();
    const date = this.effectivityDate();
    if (!branch || !date) return;

    this.apiError.set(null);
    this.previewResult.set(null);
    this.generating.set(true);

    this.pricelistApi.groupedPreview(this.buildGroupedRequest(branch, date)).subscribe({
      next: res => {
        this.previewResult.set(res.data);
        this.generating.set(false);
        setTimeout(() => this.measureFreezeOffsets(), 0);
      },
      error: err => {
        this.apiError.set(err?.error?.message ?? 'Failed to generate the grouped pricelist preview.');
        this.generating.set(false);
      }
    });
  }

  exportToExcel(): void {
    const customers = this.selectedCustomers();
    const date = this.effectivityDate();
    if (customers.length === 0 || !date) return;

    this.apiError.set(null);
    this.exporting.set(true);

    const effectivityDate = this.toIsoDate(date);
    this.pricelistApi.export(this.buildRequest(customers, date)).subscribe({
      next: res => {
        this.pricelistApi.download(res, `Pricelist_${effectivityDate.replace(/-/g, '')}.xlsx`);
        this.exporting.set(false);
      },
      error: () => {
        this.apiError.set('Failed to generate the pricelist export.');
        this.exporting.set(false);
      }
    });
  }

  exportPerAccount(): void {
    const customers = this.selectedCustomers();
    const date = this.effectivityDate();
    if (customers.length === 0 || !date) return;

    this.apiError.set(null);
    this.exporting.set(true);

    const effectivityDate = this.toIsoDate(date);
    this.downloadNextAccount(customers, 0, effectivityDate);
  }

  private downloadNextAccount(customers: CustomerSuggestionDto[], index: number, effectivityDate: string): void {
    if (index >= customers.length) {
      this.exporting.set(false);
      return;
    }

    const customer = customers[index];
    this.pricelistApi.export({
      custKeys: [customer.custKey],
      effectivityDate,
      srpMarkupPercent: this.srpMarkupPercent() ?? 3
    }).subscribe({
      next: res => {
        this.pricelistApi.download(res, `Pricelist_${customer.custKey}_${effectivityDate.replace(/-/g, '')}.xlsx`);
        this.downloadNextAccount(customers, index + 1, effectivityDate);
      },
      error: () => {
        this.apiError.set(`Failed to generate the export for ${customer.custKey}.`);
        this.downloadNextAccount(customers, index + 1, effectivityDate);
      }
    });
  }

  exportZone(): void {
    const branch = this.selectedBranch();
    const zones = this.selectedZones();
    const date = this.effectivityDate();
    if (!branch || zones.length === 0 || !date) return;

    this.apiError.set(null);
    this.exporting.set(true);

    const effectivityDate = this.toIsoDate(date);
    this.pricelistApi.zoneExport({
      branch, zones, effectivityDate, srpMarkupPercent: this.srpMarkupPercent() ?? 3
    }).subscribe({
      next: res => {
        this.pricelistApi.download(res, `ZonePricelist_${branch}_${effectivityDate.replace(/-/g, '')}.xlsx`);
        this.exporting.set(false);
      },
      error: () => {
        this.apiError.set('Failed to generate the pricelist export.');
        this.exporting.set(false);
      }
    });
  }

  exportGrouped(): void {
    const branch = this.selectedBranch();
    const date = this.effectivityDate();
    if (!branch || !date) return;

    this.apiError.set(null);
    this.exporting.set(true);

    const effectivityDate = this.toIsoDate(date);
    this.pricelistApi.groupedExport(this.buildGroupedRequest(branch, date)).subscribe({
      next: res => {
        this.pricelistApi.download(res, `GroupedPricelist_${branch}_${effectivityDate.replace(/-/g, '')}.xlsx`);
        this.exporting.set(false);
      },
      error: () => {
        this.apiError.set('Failed to generate the grouped pricelist export.');
        this.exporting.set(false);
      }
    });
  }

  private buildRequest(customers: CustomerSuggestionDto[], date: Date) {
    return {
      custKeys: customers.map(c => c.custKey),
      effectivityDate: this.toIsoDate(date),
      srpMarkupPercent: this.srpMarkupPercent() ?? 3
    };
  }

  private buildGroupedRequest(branch: string, date: Date) {
    return {
      branch,
      effectivityDate: this.toIsoDate(date),
      srpMarkupPercent: this.srpMarkupPercent() ?? 3,
      baselineCustKey: this.baselineCustomer()?.custKey
    };
  }

  private toIsoDate(d: Date): string {
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
  }
}
