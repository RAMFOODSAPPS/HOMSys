import { Component, effect, input, model, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DialogModule } from 'primeng/dialog';
import { MultiSelectModule } from 'primeng/multiselect';
import { CheckboxModule } from 'primeng/checkbox';
import { ButtonModule } from 'primeng/button';

export interface ItemProps { name: string; description: string; sharedRoleIds: number[]; isSystem: boolean }

/** Save / Save As / Share dialog shared by reports and dashboards. Shares are view-only. */
@Component({
  selector: 'app-item-props-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, DialogModule, MultiSelectModule, CheckboxModule, ButtonModule],
  template: `
    <p-dialog [header]="header()" [visible]="visible()" (visibleChange)="visible.set($event)" [modal]="true"
              [style]="{ width: '440px' }" [draggable]="false" appendTo="body">
      <div class="an-form">
        <label for="an-name">Name</label>
        <input id="an-name" class="p-inputtext p-inputtext-sm" maxlength="100" [(ngModel)]="name" />
        <label for="an-desc">Description</label>
        <textarea id="an-desc" class="p-inputtext p-inputtext-sm" rows="2" maxlength="500" [(ngModel)]="description"></textarea>
        <label>Share with roles <span class="an-muted">(view only; each viewer sees only their own branch)</span></label>
        <p-multiSelect [options]="roles()" optionLabel="name" optionValue="id" [(ngModel)]="roleIds"
                       placeholder="Private" display="chip" size="small" appendTo="body" styleClass="w-full" />
        @if (canPublish()) {
          <div class="an-opt">
            <p-checkbox [binary]="true" inputId="an-system" [(ngModel)]="isSystem" />
            <label for="an-system">Publish as system template (visible to every analytics user)</label>
          </div>
        }
      </div>
      <ng-template pTemplate="footer">
        <p-button label="Cancel" severity="secondary" [text]="true" (onClick)="visible.set(false)" />
        <p-button [label]="saveLabel()" icon="pi pi-check" [disabled]="!name.trim()" (onClick)="submit()" />
      </ng-template>
    </p-dialog>
  `,
})
export class ItemPropsDialogComponent {
  readonly visible = model(false);
  readonly header = input('Save');
  readonly saveLabel = input('Save');
  readonly initial = input<ItemProps>({ name: '', description: '', sharedRoleIds: [], isSystem: false });
  readonly roles = input<{ id: number; name: string }[]>([]);
  readonly canPublish = input(false);
  readonly save = output<ItemProps>();

  name = '';
  description = '';
  roleIds: number[] = [];
  isSystem = false;
  private readonly opened = signal(false);

  constructor() {
    // Reset the form from `initial` each time the dialog opens.
    effect(() => {
      if (this.visible() && !this.opened()) {
        const i = this.initial();
        this.name = i.name;
        this.description = i.description;
        this.roleIds = [...i.sharedRoleIds];
        this.isSystem = i.isSystem;
      }
      this.opened.set(this.visible());
    }, { allowSignalWrites: true });
  }

  submit() {
    this.save.emit({ name: this.name.trim(), description: this.description.trim(), sharedRoleIds: this.roleIds, isSystem: this.isSystem });
    this.visible.set(false);
  }
}
