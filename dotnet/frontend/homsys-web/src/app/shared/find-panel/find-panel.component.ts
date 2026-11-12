import { Component, input, model, output } from '@angular/core';
import { DrawerModule } from 'primeng/drawer';
import { ButtonModule } from 'primeng/button';

/**
 * "Find" side panel for list pages (toolbar `filters` button): the page projects its own filter fields,
 * each wrapped in `<div class="ff"><label>…</label> control </div>`. Filters apply as they change.
 */
@Component({
  selector: 'app-find-panel',
  standalone: true,
  imports: [DrawerModule, ButtonModule],
  template: `
    <p-drawer [visible]="visible()" (visibleChange)="visible.set($event)" position="right"
      [style]="{ width: '340px' }" header="Find" styleClass="find-panel">
      <div class="find-body">
        <ng-content />
        <div class="find-foot">
          @if (active() > 0) { <span class="n">{{ active() }} filter{{ active() === 1 ? '' : 's' }} on</span> }
          <p-button label="Clear All" [text]="true" size="small" [disabled]="active() === 0" (onClick)="clear.emit()" />
        </div>
      </div>
    </p-drawer>
  `,
  styles: [`
    /* Field layout (.ff / .pair) lives in styles.scss under .find-panel: the drawer is moved to <body>,
       so scoped rules can't reach the fields the page projects. */
    .find-body { display: flex; flex-direction: column; font-size: 0.85rem; }
    .find-foot { display: flex; align-items: center; justify-content: flex-end; gap: 0.5rem;
      border-top: 1px solid var(--p-surface-border); padding-top: 0.6rem; margin-top: 0.3rem; }
    .find-foot .n { margin-right: auto; color: var(--p-text-muted-color); font-size: 0.78rem; }
  `]
})
export class FindPanelComponent {
  readonly visible = model(false);
  /** Number of filters set (shown + enables Clear All). */
  readonly active = input(0);
  readonly clear = output<void>();
}
