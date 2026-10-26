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
          @if (cfg().submit) {
            <p-button icon="pi pi-send" [pTooltip]="cfg().submit!.label" tooltipPosition="bottom"
              [text]="true"
              [loading]="cfg().submit!.loading ? cfg().submit!.loading!() : false"
              [disabled]="cfg().submit!.disabled ? cfg().submit!.disabled!() : false"
              (onClick)="cfg().submit!.onClick()" />
          }
          @if (cfg().cancel) {
            <p-button icon="pi pi-times" pTooltip="Cancel" tooltipPosition="bottom"
              [text]="true"
              [loading]="cfg().cancel!.loading ? cfg().cancel!.loading!() : false"
              [disabled]="cfg().cancel!.disabled ? cfg().cancel!.disabled!() : false"
              (onClick)="cfg().cancel!.onClick()" />
          }
          @if (cfg().clearForm) {
            <p-button icon="pi pi-eraser" pTooltip="Clear" tooltipPosition="bottom"
              [text]="true"
              [disabled]="cfg().clearForm!.disabled ? cfg().clearForm!.disabled!() : false"
              (onClick)="cfg().clearForm!.onClick()" />
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
          @if (cfg().history) {
            <p-button icon="pi pi-history" pTooltip="History" tooltipPosition="bottom"
              [text]="true"
              [disabled]="cfg().history!.disabled ? cfg().history!.disabled!() : false"
              (onClick)="cfg().history!.onClick()" />
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

        <!-- Workflow decisions (current approver only) -->
        @if (cfg().approve || cfg().return || cfg().reject || cfg().revert || cfg().cancelDoc) {
          <p-divider layout="vertical" />
          <div class="action-group">
            @if (cfg().approve) {
              <p-button icon="pi pi-check" [pTooltip]="cfg().approve!.label" tooltipPosition="bottom"
                [text]="true" severity="success"
                [disabled]="cfg().approve!.disabled ? cfg().approve!.disabled!() : false"
                (onClick)="cfg().approve!.onClick()" />
            }
            @if (cfg().return) {
              <p-button icon="pi pi-undo" pTooltip="Return to proponent" tooltipPosition="bottom"
                [text]="true" severity="secondary"
                [disabled]="cfg().return!.disabled ? cfg().return!.disabled!() : false"
                (onClick)="cfg().return!.onClick()" />
            }
            @if (cfg().reject) {
              <p-button icon="pi pi-ban" pTooltip="Reject" tooltipPosition="bottom"
                [text]="true" severity="danger"
                [disabled]="cfg().reject!.disabled ? cfg().reject!.disabled!() : false"
                (onClick)="cfg().reject!.onClick()" />
            }
            @if (cfg().revert) {
              <p-button icon="pi pi-replay" pTooltip="Revert (Admin) — back to the proponent" tooltipPosition="bottom"
                [text]="true" severity="warn"
                [disabled]="cfg().revert!.disabled ? cfg().revert!.disabled!() : false"
                (onClick)="cfg().revert!.onClick()" />
            }
            @if (cfg().cancelDoc) {
              <p-button icon="pi pi-times-circle" [pTooltip]="cfg().cancelDoc!.label" tooltipPosition="bottom"
                [text]="true" severity="danger"
                [disabled]="cfg().cancelDoc!.disabled ? cfg().cancelDoc!.disabled!() : false"
                (onClick)="cfg().cancelDoc!.onClick()" />
            }
          </div>
        }

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
              <span class="tooltip-wrap" [pTooltip]="cfg().export!.label ? '' : 'Export'" tooltipPosition="bottom">
                <p-button [icon]="cfg().export!.label ? '' : 'pi pi-file-export'" [label]="cfg().export!.label"
                  [text]="!cfg().export!.label" [outlined]="!!cfg().export!.label" [size]="cfg().export!.label ? 'small' : undefined"
                  [disabled]="cfg().export!.disabled ? cfg().export!.disabled!() : false"
                  (onClick)="exportMenu.toggle($event)" />
              </span>
            }
            @if (cfg().import) {
              <span class="tooltip-wrap" [class.word-first]="!!cfg().import!.label" [pTooltip]="cfg().import!.label ? '' : 'Import by Custkey'" tooltipPosition="bottom">
                <p-button [icon]="cfg().import!.label ? '' : 'pi pi-file-import'" [label]="cfg().import!.label"
                  [text]="!cfg().import!.label" [outlined]="!!cfg().import!.label" [size]="cfg().import!.label ? 'small' : undefined"
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

        <!-- Form Designer (Admin) -->
        @if (cfg().design) {
          <p-divider layout="vertical" />
          <div class="action-group">
            <span class="tooltip-wrap" pTooltip="Design Layout" tooltipPosition="bottom">
              <p-button icon="pi pi-objects-column"
                [text]="true"
                [disabled]="cfg().design!.disabled ? cfg().design!.disabled!() : false"
                (onClick)="cfg().design!.onClick()" />
            </span>
          </div>
        }

        <!-- View switch (right end) -->
        @if (cfg().views; as v) {
          <div class="action-group view-switch" role="group" aria-label="View">
            @for (o of v.options; track o.value) {
              <p-button [icon]="o.icon" [text]="true" [pTooltip]="o.label" tooltipPosition="bottom" [ariaLabel]="o.label"
                [styleClass]="v.value() === o.value ? 'view-on' : ''" (onClick)="v.onChange(o.value)" />
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
      background: #faf3f3;
      /* 1px rule + soft shadow onto the page; lives here so it disappears with the toolbar (e.g. Home) */
      border-bottom: 1px solid #ecdcdc;
      box-shadow: 0 2px 6px rgba(0, 0, 0, 0.12);
      position: relative;
      z-index: 1;
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

    /* Word buttons (e.g. Bank Statements' Export / Import statement) need breathing room; icon buttons stay tight. */
    .action-group:has(.p-button-label) { gap: 0.5rem; }
    /* A worded Import (the page's main action) goes before Export. */
    .word-first { order: -1; }

    .view-switch { margin-left: auto; }

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

    ::ng-deep .page-toolbar .p-button.p-button-text:not(:disabled):hover {
      background: var(--p-primary-100);
    }

    /* Disabled = grey whatever the button's colour. PrimeNG 19 marks it with the native disabled attribute (no p-disabled class). */
    ::ng-deep .page-toolbar .p-button.p-button-text:disabled {
      opacity: 1;
      color: #b8bcc4 !important;
      filter: grayscale(1);
    }

    ::ng-deep .page-toolbar .p-button .p-button-icon {
      font-size: 1rem;
    }

    /* The active view: maroon tint, like a pressed toggle. */
    ::ng-deep .page-toolbar .p-button.p-button-text.view-on,
    ::ng-deep .page-toolbar .p-button.p-button-text.view-on:not(.p-disabled):hover {
      background: rgba(128, 0, 0, 0.1);
      color: #800000;
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
    return c.save || c.submit || c.approve || c.return || c.reject || c.revert || c.cancelDoc || c.cancel || c.clearForm || c.add || c.edit || c.history || c.delete || c.refresh || c.find || c.search || c.print || c.export || c.import || c.importByName || c.design || c.views;
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
