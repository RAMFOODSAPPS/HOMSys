import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { AppTab, TabBarService } from '../../core/services/tab-bar.service';

@Component({
  selector: 'app-tab-bar',
  standalone: true,
  providers: [ConfirmationService],
  imports: [ConfirmDialogModule],
  template: `
    <p-confirmDialog key="tab-close" />

    @if (tabBar.tabs().length) {
      <div class="tab-bar">
        @for (tab of tabBar.tabs(); track tab.key) {
          <div class="tab" [class.active]="tabBar.activeKey() === tab.key"
               (click)="navigate(tab)">
            <i class="pi {{ tab.icon }}"></i>
            <span>{{ tab.label }}</span>
            <button class="tab-close" (click)="close($event, tab.key)" title="Close">
              <i class="pi pi-times"></i>
            </button>
          </div>
        }
      </div>
    }
  `,
  styles: [`
    .tab-bar {
      display: flex;
      align-items: stretch;
      background: var(--p-surface-card);
      border-bottom: 1px solid #d1d5db;
      height: 34px;
      overflow-x: auto;
      overflow-y: hidden;
      flex-shrink: 0;
      scrollbar-width: none;
    }
    .tab-bar::-webkit-scrollbar { display: none; }

    .tab {
      display: flex;
      align-items: center;
      gap: 0.35rem;
      padding: 0 0.65rem;
      font-size: 0.775rem;
      font-weight: 500;
      color: var(--p-text-muted-color);
      cursor: pointer;
      border-right: 1px solid #e5e7eb;
      border-bottom: 2px solid transparent;
      white-space: nowrap;
      transition: background 0.13s, color 0.13s;
      user-select: none;
      min-width: 0;
    }

    .tab:hover { background: var(--p-surface-hover); color: var(--p-text-color); }

    .tab.active {
      color: #800000;
      border-bottom-color: #800000;
      background: rgba(128, 0, 0, 0.05);
      font-weight: 600;
    }

    .tab .pi:first-child { font-size: 0.68rem; opacity: 0.75; }

    .tab-close {
      display: flex;
      align-items: center;
      justify-content: center;
      width: 16px;
      height: 16px;
      margin-left: 0.15rem;
      background: transparent;
      border: none;
      border-radius: 3px;
      color: inherit;
      cursor: pointer;
      opacity: 0;
      padding: 0;
      transition: opacity 0.12s, background 0.12s;
      flex-shrink: 0;
    }

    .tab-close .pi { font-size: 0.6rem; }

    .tab:hover .tab-close,
    .tab.active .tab-close { opacity: 0.6; }

    .tab-close:hover { background: rgba(128, 0, 0, 0.12); opacity: 1 !important; }
  `]
})
export class AppTabBarComponent {
  protected tabBar   = inject(TabBarService);
  private router     = inject(Router);
  private confirmSvc = inject(ConfirmationService);

  navigate(tab: AppTab) {
    this.router.navigate([tab.route], tab.state ? { state: tab.state } : {});
  }

  close(event: MouseEvent, key: string) {
    event.stopPropagation();
    if (this.tabBar.isTabDirty(key)) {
      this.confirmSvc.confirm({
        key: 'tab-close',
        message: 'You have unsaved changes. Close this tab anyway?',
        header: 'Unsaved Changes',
        icon: 'pi pi-exclamation-triangle',
        acceptLabel: 'Close Tab',
        rejectLabel: 'Stay',
        acceptButtonStyleClass: 'p-button-danger',
        accept: () => this.tabBar.closeTab(key)
      });
    } else {
      this.tabBar.closeTab(key);
    }
  }
}
