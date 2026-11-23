import { Component, inject, model, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { CollectionDocumentKindDto } from '../../core/models/collection.model';
import { CollectionService } from '../../core/services/collection.service';

interface KindForm { id: number | null; label: string; receiptTypes: string[]; sortOrder: number; isActive: boolean; isSystem: boolean; inUse: number }

/**
 * Maintenance of the supporting-document types ("What is it?" on a collection's files), Finance Manager / Admin
 * (user 2026-11-23). A type limited to receipt types is offered only for those: the receipt's own copy (CR / PR / AR)
 * shows only on that receipt type. A type in use can be renamed or deactivated, not deleted.
 */
@Component({
  selector: 'app-document-kinds-dialog',
  standalone: true,
  imports: [FormsModule, DialogModule, ButtonModule, CheckboxModule, InputNumberModule, InputTextModule, ToggleSwitchModule],
  template: `
    <p-dialog header="Supporting document types" [(visible)]="visible" [modal]="true" [style]="{ width: '760px' }" (onShow)="load()">
      <p class="dk-hint">What a collection's uploaded file can be. Pick receipt types to offer a type only on those receipts
        (e.g. Collection Receipt on CR only); none = every receipt. Files keep their type when it is renamed or deactivated.</p>
      <table class="dk-table">
        <thead><tr><th>Name</th><th>Receipt types</th><th class="num">Order</th><th>Status</th><th class="num">Files</th><th></th></tr></thead>
        <tbody>
          @for (k of kinds(); track k.id) {
            <tr [class.off]="!k.isActive" [class.sel]="form()?.id === k.id">
              <td>{{ k.label }} @if (k.isSystem) { <span class="dk-sys" title="Used by the system">system</span> }</td>
              <td>{{ k.receiptTypes.length ? k.receiptTypes.join(', ') : 'All' }}</td>
              <td class="num">{{ k.sortOrder }}</td>
              <td>{{ k.isActive ? 'Active' : 'Inactive' }}</td>
              <td class="num">{{ k.inUse }}</td>
              <td class="act"><p-button icon="pi pi-pencil" [text]="true" size="small" (onClick)="edit(k)" /></td>
            </tr>
          } @empty { <tr><td colspan="6" class="muted">{{ loading() ? 'Loading…' : 'No types yet.' }}</td></tr> }
        </tbody>
      </table>

      @if (form(); as f) {
        <div class="dk-form">
          <div class="dk-title">{{ f.id ? 'Edit type' : 'New type' }}</div>
          <div class="dk-grid">
            <label class="dk-field wide"><span>Name</span><input pInputText [(ngModel)]="f.label" maxlength="60" /></label>
            <label class="dk-field"><span>Order</span><p-inputnumber [(ngModel)]="f.sortOrder" [min]="0" [max]="9999" [useGrouping]="false" inputStyleClass="dk-num" /></label>
            <div class="dk-field"><span>Offered for</span>
              <div class="dk-checks">
                @for (rt of receiptCodes; track rt) {
                  <label><p-checkbox [binary]="true" [disabled]="f.isSystem" [ngModel]="f.receiptTypes.includes(rt)" (ngModelChange)="toggleRt(f, rt, $event)" /> {{ rt }}</label>
                }
                <span class="muted">{{ f.receiptTypes.length ? '' : '(all receipts)' }}</span>
              </div>
            </div>
            <label class="dk-field"><span>Active</span><p-toggleswitch [(ngModel)]="f.isActive" [disabled]="f.isSystem" /></label>
          </div>
          <div class="dk-acts">
            @if (f.id && !f.isSystem && f.inUse === 0) {
              <p-button label="Delete" severity="danger" [text]="true" [loading]="busy()" (onClick)="remove(f)" />
            }
            <span class="grow"></span>
            <p-button label="Cancel" [text]="true" (onClick)="form.set(null)" />
            <p-button label="Save" [disabled]="!f.label.trim()" [loading]="busy()" (onClick)="save(f)" />
          </div>
        </div>
      } @else {
        <div class="dk-acts"><span class="grow"></span><p-button label="+ New type" (onClick)="add()" /></div>
      }
    </p-dialog>
  `,
  styles: [`
    .dk-hint { margin: 0 0 10px; font-size: 12px; color: #6b6b6b; }
    .dk-table { width: 100%; border-collapse: collapse; font-size: 13px; }
    .dk-table th { text-align: left; font-weight: 600; font-size: 11px; text-transform: uppercase; color: #6b6b6b; padding: 6px 8px; border-bottom: 1px solid #e5e5e5; }
    .dk-table td { padding: 4px 8px; border-bottom: 1px solid #f0f0f0; }
    .dk-table .num { text-align: right; }
    .dk-table .act { width: 40px; text-align: right; }
    .dk-table tr.off td { color: #9a9a9a; }
    .dk-table tr.sel td { background: #fbf3f3; }
    .dk-sys { font-size: 10px; color: #800000; border: 1px solid #e3c6c6; border-radius: 8px; padding: 0 6px; margin-left: 4px; }
    .dk-form { margin-top: 12px; padding: 12px; border: 1px solid #e5e5e5; border-radius: 8px; }
    .dk-title { font-weight: 600; margin-bottom: 8px; }
    .dk-grid { display: grid; grid-template-columns: 1fr 110px; gap: 10px 14px; }
    .dk-field { display: flex; flex-direction: column; gap: 4px; font-size: 12px; color: #6b6b6b; }
    .dk-field input { width: 100%; }
    .dk-checks { display: flex; gap: 14px; align-items: center; color: #1c1b1a; font-size: 13px; min-height: 32px; }
    .dk-checks label { display: flex; gap: 6px; align-items: center; }
    .dk-acts { display: flex; gap: 8px; align-items: center; margin-top: 12px; }
    .grow { flex: 1; }
    .muted { color: #9a9a9a; }
    :host ::ng-deep .dk-num { width: 100%; }
  `],
})
export class DocumentKindsDialogComponent {
  private api = inject(CollectionService);
  private toast = inject(MessageService);

  visible = model(false);
  protected readonly receiptCodes = ['CR', 'PR', 'AR'];
  kinds = signal<CollectionDocumentKindDto[]>([]);
  form = signal<KindForm | null>(null);
  loading = signal(false);
  busy = signal(false);

  load() {
    this.form.set(null);
    this.loading.set(true);
    this.api.documentKinds(true).subscribe({
      next: res => { this.kinds.set(res.data ?? []); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  add() {
    const last = Math.max(0, ...this.kinds().filter(k => k.sortOrder < 990).map(k => k.sortOrder));
    this.form.set({ id: null, label: '', receiptTypes: [], sortOrder: last + 10, isActive: true, isSystem: false, inUse: 0 });
  }

  edit(k: CollectionDocumentKindDto) {
    this.form.set({ id: k.id, label: k.label, receiptTypes: [...k.receiptTypes], sortOrder: k.sortOrder, isActive: k.isActive, isSystem: k.isSystem, inUse: k.inUse });
  }

  toggleRt(f: KindForm, rt: string, on: boolean) {
    f.receiptTypes = on ? this.receiptCodes.filter(x => x === rt || f.receiptTypes.includes(x)) : f.receiptTypes.filter(x => x !== rt);
  }

  save(f: KindForm) {
    this.busy.set(true);
    this.api.saveDocumentKind(f.id, { label: f.label.trim(), receiptTypes: f.receiptTypes, sortOrder: f.sortOrder ?? 0, isActive: f.isActive }).subscribe({
      next: () => { this.busy.set(false); this.toast.add({ severity: 'success', summary: 'Document types', detail: `${f.label.trim()} saved.` }); this.load(); },
      error: err => this.fail(err),
    });
  }

  remove(f: KindForm) {
    this.busy.set(true);
    this.api.deleteDocumentKind(f.id!).subscribe({
      next: () => { this.busy.set(false); this.toast.add({ severity: 'success', summary: 'Document types', detail: `${f.label} deleted.` }); this.load(); },
      error: err => this.fail(err),
    });
  }

  private fail(err: { error?: { message?: string } }) {
    this.busy.set(false);
    this.toast.add({ severity: 'error', summary: 'Document types', detail: err?.error?.message ?? 'Could not save.' });
  }
}
