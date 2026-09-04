import { Component, effect, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { TooltipModule } from 'primeng/tooltip';
import { DividerModule } from 'primeng/divider';
import { MenuModule } from 'primeng/menu';
import { MenuItem } from 'primeng/api';
import { GlobalToolbarService } from '../../core/services/global-toolbar.service';

@Component({
  selector: 'app-page-toolbar',
  standalone: true,
  imports: [FormsModule, ButtonModule, InputTextModule, IconFieldModule, InputIconModule, TooltipModule, DividerModule, MenuModule],
  template: `
    @if (hasAnyAction()) {
      <div class="page-toolbar">

        <!-- CRUD actions -->
        <div class="action-group">
          @if (cfg().save) {
            <p-button icon="pi pi-save" pTooltip="Save" tooltipPosition="bottom"
              [text]="true"
              [loading]="cfg().save!.loading ? cfg().save!.loading!() : false"
              [disabled]="cfg().save!.disabled ? cfg().save!.disabled!() : false"
              (onClick)="cfg().save!.onClick()" />
          }
          @if (cfg().cancel) {
            <p-button icon="pi pi-times" pTooltip="Cancel" tooltipPosition="bottom"
              [text]="true" severity="secondary"
              [loading]="cfg().cancel!.loading ? cfg().cancel!.loading!() : false"
              [disabled]="cfg().cancel!.disabled ? cfg().cancel!.disabled!() : false"
              (onClick)="cfg().cancel!.onClick()" />
          }
          @if (cfg().add) {
            <p-button icon="pi pi-plus" pTooltip="Add" tooltipPosition="bottom"
              [text]="true"
              [loading]="cfg().add!.loading ? cfg().add!.loading!() : false"
              [disabled]="cfg().add!.disabled ? cfg().add!.disabled!() : false"
              (onClick)="cfg().add!.onClick()" />
          }
          @if (cfg().edit) {
            <p-button icon="pi pi-pencil" pTooltip="Edit" tooltipPosition="bottom"
              [text]="true"
              [loading]="cfg().edit!.loading ? cfg().edit!.loading!() : false"
              [disabled]="cfg().edit!.disabled ? cfg().edit!.disabled!() : false"
              (onClick)="cfg().edit!.onClick()" />
          }
          @if (cfg().delete) {
            <p-button icon="pi pi-trash" pTooltip="Delete" tooltipPosition="bottom"
              [text]="true" severity="danger"
              [loading]="cfg().delete!.loading ? cfg().delete!.loading!() : false"
              [disabled]="cfg().delete!.disabled ? cfg().delete!.disabled!() : false"
              (onClick)="cfg().delete!.onClick()" />
          }
          @if (cfg().refresh) {
            <p-button icon="pi pi-refresh" pTooltip="Refresh" tooltipPosition="bottom"
              [text]="true"
              [loading]="cfg().refresh!.loading ? cfg().refresh!.loading!() : false"
              [disabled]="cfg().refresh!.disabled ? cfg().refresh!.disabled!() : false"
              (onClick)="cfg().refresh!.onClick()" />
          }
          @if (cfg().list) {
            <p-button icon="pi pi-list" pTooltip="List" tooltipPosition="bottom"
              [text]="true"
              [disabled]="cfg().list!.disabled ? cfg().list!.disabled!() : false"
              (onClick)="cfg().list!.onClick()" />
          }
          @if (cfg().find) {
            <p-button icon="pi pi-search" pTooltip="Find" tooltipPosition="bottom"
              [text]="true"
              [loading]="cfg().find!.loading ? cfg().find!.loading!() : false"
              [disabled]="cfg().find!.disabled ? cfg().find!.disabled!() : false"
              (onClick)="cfg().find!.onClick()" />
          }
        </div>

        <!-- Search box (live filter — list pages only) -->
        @if (cfg().search) {
          <p-divider layout="vertical" />
          <div class="search-group">
            <p-iconfield>
              <p-inputicon class="pi pi-search" />
              <input pInputText type="text" [(ngModel)]="searchTerm"
                (ngModelChange)="onSearch($event)"
                placeholder="Find..." class="search-input" />
            </p-iconfield>
            @if (searchTerm) {
              <p-button icon="pi pi-times" [text]="true" severity="secondary"
                pTooltip="Clear" tooltipPosition="bottom"
                (onClick)="clearSearch()" />
            }
          </div>
        }

        <!-- IO actions -->
        @if (cfg().print || cfg().export || cfg().import || cfg().importByName) {
          <p-divider layout="vertical" />
          <div class="action-group">
            @if (cfg().print) {
              <span class="tooltip-wrap" pTooltip="Print" tooltipPosition="bottom">
                <p-button icon="pi pi-print"
                  [text]="true"
                  [disabled]="cfg().print!.disabled ? cfg().print!.disabled!() : false"
                  (onClick)="cfg().print!.onClick()" />
              </span>
            }
            @if (cfg().export) {
              <p-menu #exportMenu [model]="exportMenuItems()" [popup]="true" appendTo="body" />
              <span class="tooltip-wrap" pTooltip="Export" tooltipPosition="bottom">
                <p-button icon="pi pi-file-export"
                  [text]="true"
                  [disabled]="cfg().export!.disabled ? cfg().export!.disabled!() : false"
                  (onClick)="exportMenu.toggle($event)" />
              </span>
            }
            @if (cfg().import) {
              <span class="tooltip-wrap" pTooltip="Import by Custkey" tooltipPosition="bottom">
                <p-button icon="pi pi-file-import"
                  [text]="true"
                  [disabled]="cfg().import!.disabled ? cfg().import!.disabled!() : false"
                  (onClick)="cfg().import!.onClick()" />
              </span>
            }
            @if (cfg().importByName) {
              <span class="tooltip-wrap" pTooltip="Import by Customer Name" tooltipPosition="bottom">
                <p-button icon="pi pi-user-plus"
                  [text]="true"
                  [disabled]="cfg().importByName!.disabled ? cfg().importByName!.disabled!() : false"
                  (onClick)="cfg().importByName!.onClick()" />
              </span>
            }
          </div>
        }

      </div>
    }
  `,
  styles: [`
    .page-toolbar {
      display: flex;
      align-items: center;
      padding: 0 0.75rem;
      background: var(--p-surface-card);
      border-bottom: 1px solid var(--p-surface-border);
      flex-shrink: 0;
      min-height: 36px;
      gap: 0.15rem;
      position: relative;
      z-index: 200;
    }

    .action-group {
      display: flex;
      align-items: center;
      gap: 0;
    }

    .search-group {
      display: flex;
      align-items: center;
      gap: 0.25rem;
    }

    .search-input {
      height: 26px;
      font-size: 0.78rem;
      width: 180px;
      padding: 0 0.5rem 0 2rem;
    }

    ::ng-deep .page-toolbar .p-button.p-button-text {
      width: 32px;
      height: 32px;
      padding: 0;
      display: flex;
      align-items: center;
      justify-content: center;
      opacity: 1;
    }

    ::ng-deep .page-toolbar .p-button.p-button-text:not(.p-disabled):hover {
      background: var(--p-primary-100);
    }

    ::ng-deep .page-toolbar .p-button.p-button-text.p-disabled {
      opacity: 0.35;
    }

    ::ng-deep .page-toolbar .p-button .p-button-icon {
      font-size: 1rem;
    }

    ::ng-deep .page-toolbar .p-divider.p-divider-vertical {
      margin: 0 0.25rem;
      height: 18px;
      align-self: center;
    }
  `]
})
export class PageToolbarComponent {
  protected toolbar = inject(GlobalToolbarService);

  protected searchTerm = '';

  constructor() {
    effect(() => {
      const initial = this.toolbar.config().initialSearch;
      this.searchTerm = initial ?? '';
    });
  }

  protected cfg() { return this.toolbar.config(); }

  protected hasAnyAction() {
    const c = this.cfg();
    return c.save || c.cancel || c.add || c.edit || c.delete || c.refresh || c.find || c.search || c.print || c.export || c.import || c.importByName;
  }

  protected exportMenuItems(): MenuItem[] {
    const exp = this.cfg().export;
    if (!exp) return [];
    const disabled = exp.disabled ? exp.disabled() : false;
    const items: MenuItem[] = [];
    if (exp.pdf)   items.push({ label: 'PDF',   icon: 'pi pi-file-pdf',   disabled, command: () => exp.pdf!() });
    if (exp.excel) items.push({ label: 'Excel', icon: 'pi pi-file-excel', disabled, command: () => exp.excel!() });
    if (exp.csv)   items.push({ label: 'CSV',   icon: 'pi pi-file',       disabled, command: () => exp.csv!() });
    for (const item of exp.items ?? []) {
      items.push({ label: item.label, icon: item.icon ?? 'pi pi-file-excel', disabled, command: item.command });
    }
    return items;
  }

  protected clearSearch() {
    this.searchTerm = '';
    this.cfg().search?.('');
  }

  protected onSearch(term: string) {
    this.cfg().search?.(term);
  }
}
