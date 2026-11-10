import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TabsModule } from 'primeng/tabs';
import { SelectModule } from 'primeng/select';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { CheckboxModule } from 'primeng/checkbox';
import { MessageModule } from 'primeng/message';
import { TagModule } from 'primeng/tag';
import { ConfirmationService, MessageService } from 'primeng/api';
import { GlobalToolbarService } from '../../core/services/global-toolbar.service';
import { TabBarService } from '../../core/services/tab-bar.service';
import {
  RdpChannels, RdpDistributor, RdpLines, RdpService, RdpSiteOption, RdpUpload, RdpUploadPreview, SaveRdpDistributor
} from '../../core/services/rdp.service';
import { utc } from '../../core/utils/utc-date';

const TAB_KEY = '/rdp-upload';

interface SavedView {
  tab: string; lineUpload: RdpUpload | null; lineRdp: number | null; linePeriod: string | null;
  lineSearch: string; lineFirst: number; lineRows: number;
}

const MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];

/**
 * RDP Sales Upload — distributor sales files laid out like "RAM RDP Sales Raw Data Template.xlsx".
 * Preview first (nothing saved), then import: each distributor's months in the file replace what was there.
 * Also keeps the RDP list (CUSTKEY → distributor, RDM, area, servicing branch) and the channel-label mapping.
 */
@Component({
  selector: 'app-rdp-upload-page',
  standalone: true,
  imports: [FormsModule, DatePipe, DecimalPipe, TableModule, TabsModule, SelectModule, ButtonModule, DialogModule,
            InputTextModule, InputNumberModule, CheckboxModule, MessageModule, TagModule],
  template: `
    <p-tabs [(value)]="tab">
      <p-tablist>
        <p-tab value="upload"><i class="pi pi-upload"></i> Upload</p-tab>
        <p-tab value="history"><i class="pi pi-history"></i> Uploaded files</p-tab>
        <p-tab value="lines"><i class="pi pi-table"></i> Sales lines</p-tab>
        <p-tab value="rdps"><i class="pi pi-truck"></i> RDP list</p-tab>
        <p-tab value="channels"><i class="pi pi-sitemap"></i> Channel mapping
          @if (channels()?.unmapped?.length) { <p-tag severity="warn" [value]="'' + channels()!.unmapped.length" /> }
        </p-tab>
      </p-tablist>
      <p-tabpanels>
        <!-- ── Upload ── -->
        <p-tabpanel value="upload">
          <div class="row">
            <label>RDP
              <p-select [options]="rdpOptions()" [(ngModel)]="pickedRdp" optionLabel="label" optionValue="id" [filter]="true"
                filterBy="label" [showClear]="true" placeholder="From the file's RDP CUSTKEY column" styleClass="rdp-sel"
                (onChange)="preview.set(null)" />
            </label>
            <input #fileInput type="file" accept=".xlsx,.xlsb,.xls,.csv" hidden (change)="choose(fileInput)" />
            <p-button icon="pi pi-file-excel" [label]="file() ? file()!.name : 'Choose file…'" severity="secondary" (onClick)="fileInput.click()" />
            <p-button icon="pi pi-search" label="Preview" [disabled]="!file() || busy()" [loading]="busy() && !preview()" (onClick)="runPreview()" />
          </div>
          <p class="hint">Use the TMG template (sheet “Template- Raw Data RDP STT”), or the RDP's own export — the columns are found by
            their headers. Pick the RDP when the file has no RDP CUSTKEY or distributor column. Uploading a month again replaces that distributor's month —
            always send the <b>complete</b> month.</p>

          @if (preview(); as p) {
            <div class="kpis">
              <div><span>Lines</span><b>{{ p.lines | number }}</b></div>
              <div><span>Accounts</span><b>{{ p.accounts | number }}</b></div>
              <div><span>Net amount</span><b>₱{{ p.netAmount | number:'1.2-2' }}</b></div>
              <div><span>Replaces</span><b>{{ p.replacesLines | number }} lines</b></div>
            </div>
            @if (p.errorCount) {
              <p-message severity="error" styleClass="w-full mb-2">
                <div><b>{{ p.errorCount | number }} error(s) — nothing can be imported until they are fixed.</b>
                  <ul class="errs">@for (e of p.errors; track $index) { <li>{{ e }}</li> }</ul>
                  @if (p.errorCount > p.errors.length) { <div>… and {{ p.errorCount - p.errors.length | number }} more.</div> }
                </div>
              </p-message>
            }
            <p-table [value]="p.distributors" styleClass="p-datatable-sm mb-2">
              <ng-template pTemplate="header">
                <tr><th>RDP</th><th>Months</th><th class="num">Lines</th><th class="num">Accounts</th>
                    <th class="num">Net amount</th><th class="num">Replaces</th></tr>
              </ng-template>
              <ng-template pTemplate="body" let-d>
                <tr><td>{{ d.custKey }} — {{ d.name }}</td><td>{{ d.periods.join(', ') }}</td><td class="num">{{ d.lines | number }}</td>
                    <td class="num">{{ d.accounts | number }}</td><td class="num">{{ d.netAmount | number:'1.2-2' }}</td>
                    <td class="num">{{ d.existingLines ? (d.existingLines | number) + ' lines' : '—' }}</td></tr>
              </ng-template>
            </p-table>
            <ul class="warns">
              @if (p.unknownSkus.length) {
                <li><i class="pi pi-exclamation-triangle"></i> {{ p.unknownSkus.length }} SKU code(s) are not in the RAM SKU list
                  (shown as UNTAGGED): {{ codes(p.unknownSkus) }}</li>
              }
              @if (p.unmappedChannels.length) {
                <li><i class="pi pi-exclamation-triangle"></i> {{ p.unmappedChannels.length }} channel label(s) have no mapping yet
                  (map them on the Channel mapping tab, before or after importing): {{ codes(p.unmappedChannels) }}</li>
              }
              @if (p.noChannelLines) { <li><i class="pi pi-info-circle"></i> {{ p.noChannelLines | number }} line(s) have no channel (“No channel in source”).</li> }
              @if (p.noAccountCodeLines) { <li><i class="pi pi-info-circle"></i> {{ p.noAccountCodeLines | number }} line(s) have no account code; the account is keyed on name + city.</li> }
              @if (p.noCasesLines) { <li><i class="pi pi-info-circle"></i> {{ p.noCasesLines | number }} line(s) have a unit that can't be converted to cases.</li> }
              @if (p.blankRowsSkipped) { <li><i class="pi pi-info-circle"></i> {{ p.blankRowsSkipped | number }} empty row(s) skipped.</li> }
              @for (n of p.notes; track $index) { <li><i class="pi pi-info-circle"></i> {{ n }}</li> }
            </ul>
            <p-button icon="pi pi-check" label="Import" [disabled]="p.errorCount > 0 || !p.lines || busy()" [loading]="busy()" (onClick)="runImport()" />
          }
        </p-tabpanel>

        <!-- ── History ── -->
        <p-tabpanel value="history">
          <p-table [value]="uploads()" [loading]="loading()" styleClass="p-datatable-sm" [paginator]="true" [rows]="25">
            <ng-template pTemplate="header">
              <tr><th>Uploaded</th><th>By</th><th>File</th><th>RDP(s)</th><th>Months</th><th class="num">Lines</th>
                  <th class="num">Still current</th><th class="num">Replaced</th><th class="num">Net amount</th><th></th></tr>
            </ng-template>
            <ng-template pTemplate="body" let-u>
              <tr><td>{{ utc(u.uploadedAt) | date:'MMM d, yyyy h:mm a' }}</td><td>{{ u.uploadedBy }}</td><td>{{ u.fileName }}</td>
                  <td>{{ u.distributors }}</td><td>{{ u.periods }}</td><td class="num">{{ u.lineCount | number }}</td>
                  <td class="num">{{ u.currentLines | number }}</td><td class="num">{{ u.replacedLines | number }}</td>
                  <td class="num">{{ u.netAmount | number:'1.2-2' }}</td>
                  <td class="nowrap"><p-button icon="pi pi-table" [text]="true" size="small" ariaLabel="View lines" [disabled]="!u.currentLines" (onClick)="viewUpload(u)" />
                    <p-button icon="pi pi-trash" severity="danger" [text]="true" size="small" ariaLabel="Delete" (onClick)="deleteUpload(u)" /></td></tr>
            </ng-template>
            <ng-template pTemplate="emptymessage"><tr><td colspan="10">No files uploaded yet.</td></tr></ng-template>
          </p-table>
        </p-tabpanel>

        <!-- ── Sales lines (template columns) ── -->
        <p-tabpanel value="lines">
          <div class="row">
            @if (lineUpload(); as u) {
              <span class="file-chip"><i class="pi pi-file-excel"></i> {{ u.fileName }}
                <button type="button" aria-label="Show all files" (click)="lineUpload.set(null); reloadLines()"><i class="pi pi-times"></i></button></span>
            }
            <label>RDP
              <p-select [options]="rdpOptions()" [(ngModel)]="lineRdp" optionLabel="label" optionValue="id" [filter]="true" filterBy="label"
                [showClear]="true" placeholder="All RDPs" styleClass="rdp-sel" (onChange)="reloadLines()" />
            </label>
            <label>Month
              <p-select [options]="periodOptions()" [(ngModel)]="linePeriod" optionLabel="label" optionValue="value" [showClear]="true"
                placeholder="All months" (onChange)="reloadLines()" />
            </label>
            <label>Search
              <input pInputText [(ngModel)]="lineSearch" (keyup.enter)="reloadLines()" (blur)="reloadLines()" placeholder="Invoice, account, SKU, salesman…" />
            </label>
          </div>
          <p-table [value]="lines()?.rows ?? []" [lazy]="true" (onLazyLoad)="loadLines($event)" [loading]="linesLoading()"
            [paginator]="true" [rows]="lineRows" [first]="lineFirst" [totalRecords]="lines()?.total ?? 0" [rowsPerPageOptions]="[25, 50, 100, 200]"
            [scrollable]="true" styleClass="p-datatable-sm p-datatable-gridlines raw-grid">
            <ng-template pTemplate="header">
              <tr>
                <th>RDP CUSTKEY</th><th>MONTH</th><th>YEAR</th><th>INVOICE DATE</th><th>INVOICE NO.</th>
                <th class="pick">TRANSACTION TYPE</th><th>ACCOUNT CODE</th><th>ACCOUNT NAME</th><th class="pick">CHANNEL TYPE</th>
                <th>PROVINCE</th><th class="pick">CITY / MUNICIPALITY</th><th class="pick">SALESMAN</th><th>RAM SKU CODE</th>
                <th>ITEM DESCRIPTION</th><th class="num">QUANTITY</th><th class="pick">UNIT OF MEASURE</th><th class="num">NET AMOUNT</th>
              </tr>
            </ng-template>
            <ng-template pTemplate="body" let-l>
              <tr>
                <td class="num" [title]="l.distributorName">{{ l.custKey }}</td><td>{{ monthName(l.period) }}</td><td>{{ l.period.slice(0, 4) }}</td>
                <td class="nowrap">{{ l.invoiceDate ?? '' }}</td><td class="nowrap">{{ l.invoiceNo }}</td><td>{{ l.txnType }}</td>
                <td>{{ l.accountCode }}</td><td>{{ l.accountName }}</td><td>{{ l.channelLabel }}</td><td>{{ l.province }}</td>
                <td>{{ l.city }}</td><td>{{ l.salesman }}</td><td>{{ l.skuCode }}</td><td>{{ l.itemDesc }}</td>
                <td class="num">{{ l.quantity | number:'1.0-2' }}</td><td>{{ l.uom }}</td>
                <td class="num" [class.neg]="l.netAmount < 0">{{ l.netAmount | number:'1.2-2' }}</td>
              </tr>
            </ng-template>
            <ng-template pTemplate="footer">
              @if (lines()?.total) {
                <tr><td colspan="14">Total — {{ lines()!.total | number }} line(s)</td>
                    <td class="num">{{ lines()!.quantity | number:'1.0-2' }}</td><td></td>
                    <td class="num">{{ lines()!.netAmount | number:'1.2-2' }}</td></tr>
              }
            </ng-template>
            <ng-template pTemplate="emptymessage"><tr><td colspan="17">No lines match.</td></tr></ng-template>
          </p-table>
        </p-tabpanel>

        <!-- ── RDP list ── -->
        <p-tabpanel value="rdps">
          <div class="row"><p-button icon="pi pi-plus" label="Add RDP" (onClick)="editRdp(null)" />
            <span class="hint">From the template's “RDP List” sheet. One CUSTKEY per distributor and servicing branch.</span></div>
          <p-table [value]="filteredRdps()" [loading]="loading()" styleClass="p-datatable-sm" sortField="name" [sortOrder]="1">
            <ng-template pTemplate="header">
              <tr><th pSortableColumn="custKey">CUSTKEY</th><th pSortableColumn="name">Distributor</th><th pSortableColumn="rdm">RDM</th>
                  <th pSortableColumn="area">Area</th><th pSortableColumn="territory">Territory</th><th pSortableColumn="siteName">Servicing branch</th>
                  <th pSortableColumn="lineCount" class="num">Lines</th><th>Months on file</th><th>Status</th></tr>
            </ng-template>
            <ng-template pTemplate="body" let-d>
              <tr class="clickable" (click)="editRdp(d)"><td>{{ d.custKey }}</td><td>{{ d.name }}</td><td>{{ d.rdm }}</td><td>{{ d.area }}</td>
                  <td>{{ d.territory }}</td><td>{{ d.siteName || '—' }}</td><td class="num">{{ d.lineCount | number }}</td>
                  <td>{{ d.firstPeriod ? d.firstPeriod + ' – ' + d.lastPeriod : '—' }}</td>
                  <td><p-tag [value]="d.isActive ? 'Active' : 'Inactive'" [severity]="d.isActive ? 'success' : 'secondary'" /></td></tr>
            </ng-template>
          </p-table>
        </p-tabpanel>

        <!-- ── Channel mapping ── -->
        <p-tabpanel value="channels">
          @if (channels(); as c) {
            @if (c.unmapped.length) {
              <h4>Labels without a mapping</h4>
              <p-table [value]="c.unmapped" styleClass="p-datatable-sm mb-3">
                <ng-template pTemplate="header"><tr><th>Label in the files</th><th class="num">Lines</th><th class="num">RDPs</th><th>Map to</th></tr></ng-template>
                <ng-template pTemplate="body" let-u>
                  <tr><td>{{ u.label }}</td><td class="num">{{ u.lineCount | number }}</td><td class="num">{{ u.distributorCount }}</td>
                      <td><p-select [options]="c.channels" placeholder="Pick channel type" appendTo="body" styleClass="ch-sel"
                            (onChange)="mapChannel(u.label, $event.value)" /></td></tr>
                </ng-template>
              </p-table>
            } @else {
              <p-message severity="success" styleClass="w-full mb-3" text="Every channel label in the uploaded files is mapped." />
            }
            <h4>Mappings</h4>
            <div class="row">
              <input pInputText [(ngModel)]="newLabel" placeholder="Label as sent, e.g. SARI SARI" />
              <p-select [options]="c.channels" [(ngModel)]="newChannel" placeholder="Channel type" styleClass="ch-sel" />
              <p-button icon="pi pi-plus" label="Add" [disabled]="!newLabel.trim() || !newChannel" (onClick)="mapChannel(newLabel, newChannel!)" />
            </div>
            <p-table [value]="c.maps" styleClass="p-datatable-sm" [paginator]="true" [rows]="25" sortField="channel" [sortOrder]="1">
              <ng-template pTemplate="header"><tr><th pSortableColumn="label">Label</th><th pSortableColumn="channel">Channel type</th>
                <th pSortableColumn="lineCount" class="num">Lines</th><th></th></tr></ng-template>
              <ng-template pTemplate="body" let-m>
                <tr><td>{{ m.label }}</td>
                    <td><p-select [options]="c.channels" [ngModel]="m.channel" appendTo="body" styleClass="ch-sel" (onChange)="mapChannel(m.label, $event.value)" /></td>
                    <td class="num">{{ m.lineCount | number }}</td>
                    <td><p-button icon="pi pi-trash" severity="danger" [text]="true" size="small" (onClick)="deleteMap(m.id, m.label)" /></td></tr>
              </ng-template>
            </p-table>
          }
        </p-tabpanel>
      </p-tabpanels>
    </p-tabs>

    <p-dialog [header]="rdpForm.id ? 'Edit RDP' : 'Add RDP'" [(visible)]="rdpVisible" [modal]="true" [style]="{ width: '460px' }">
      <div class="form">
        <label>RDP CUSTKEY<p-inputNumber [(ngModel)]="rdpForm.custKey" [useGrouping]="false" /></label>
        <label>Distributor name<input pInputText [(ngModel)]="rdpForm.name" /></label>
        <label>RDM<input pInputText [(ngModel)]="rdpForm.rdm" /></label>
        <label>Area tagging<p-select [options]="areaOptions()" [(ngModel)]="rdpForm.area" [editable]="true" placeholder="e.g. MIN GT" /></label>
        <label>Territory<p-select [options]="territories" [(ngModel)]="rdpForm.territory" placeholder="Luzon / Visayas / Mindanao" /></label>
        <label>Servicing branch<p-select [options]="sites()" [(ngModel)]="rdpForm.siteId" optionLabel="name" optionValue="id"
          [filter]="true" [showClear]="true" placeholder="Site" appendTo="body" /></label>
        <label>Names in files<input pInputText [(ngModel)]="rdpForm.fileNames" placeholder="e.g. DSDI; Davao Squire" />
          <small>What the RDP's own files call it in a distributor column, separated by “;”.</small></label>
        <label class="chk"><p-checkbox [(ngModel)]="rdpForm.isActive" [binary]="true" /> Active</label>
      </div>
      <ng-template pTemplate="footer">
        <p-button label="Cancel" severity="secondary" (onClick)="rdpVisible = false" />
        <p-button label="Save" icon="pi pi-check" [loading]="busy()" (onClick)="saveRdp()" />
      </ng-template>
    </p-dialog>
  `,
  styles: [`
    :host { display: block; }
    .row { display: flex; align-items: flex-end; gap: .6rem; flex-wrap: wrap; margin-bottom: .6rem; }
    .row label, .form label { display: flex; flex-direction: column; gap: .25rem; font-size: .8rem; color: var(--p-text-muted-color); }
    :host ::ng-deep .rdp-sel { min-width: 380px; }
    :host ::ng-deep .ch-sel { min-width: 240px; }
    .hint { font-size: .8rem; color: var(--p-text-muted-color); margin: .2rem 0 .8rem; }
    .kpis { display: flex; gap: .75rem; flex-wrap: wrap; margin-bottom: .6rem; }
    .kpis div { border: 1px solid var(--p-surface-border); border-radius: 6px; padding: .45rem .8rem; display: flex; flex-direction: column; min-width: 130px; }
    .kpis span { font-size: .72rem; color: var(--p-text-muted-color); }
    .errs { margin: .3rem 0 0; padding-left: 1.2rem; max-height: 220px; overflow: auto; font-size: .8rem; }
    .warns { list-style: none; padding: 0; margin: 0 0 .8rem; font-size: .82rem; display: grid; gap: .25rem; }
    .warns i { color: #b45309; margin-right: .3rem; }
    .num { text-align: right; }
    .clickable { cursor: pointer; }
    h4 { margin: .4rem 0; font-size: .9rem; color: #800000; }
    .form { display: grid; gap: .6rem; }
    .form .chk { flex-direction: row; align-items: center; color: inherit; }
    .nowrap { white-space: nowrap; }
    .neg { color: #b91c1c; }
    .file-chip { display: inline-flex; align-items: center; gap: .4rem; padding: .35rem .6rem; border-radius: 6px;
                 background: #fdf2f2; border: 1px solid #e8c4c4; font-size: .82rem; }
    .file-chip button { border: 0; background: none; cursor: pointer; color: #800000; padding: 0; }
    /* Same look as the TMG template: red headers, gold for the columns with a pick list. */
    :host ::ng-deep .raw-grid .p-datatable-thead > tr > th { background: #b03a2e; color: #fff; font-size: .72rem;
      font-weight: 700; white-space: nowrap; }
    :host ::ng-deep .raw-grid .p-datatable-thead > tr > th.pick { background: #b8862b; }
    :host ::ng-deep .raw-grid td { font-size: .8rem; }
    :host ::ng-deep .raw-grid .p-datatable-tfoot td { font-weight: 700; }
  `]
})
export class RdpUploadPageComponent implements OnInit, OnDestroy {
  private api = inject(RdpService);
  private toolbar = inject(GlobalToolbarService);
  private tabBar = inject(TabBarService);
  private toast = inject(MessageService);
  private confirm = inject(ConfirmationService);

  protected readonly utc = utc;
  protected readonly territories = ['Luzon', 'Visayas', 'Mindanao'];
  protected tab = 'upload';
  protected loading = signal(true);
  protected busy = signal(false);
  protected searchTerm = signal('');

  protected rdps = signal<RdpDistributor[]>([]);
  protected sites = signal<RdpSiteOption[]>([]);
  protected uploads = signal<RdpUpload[]>([]);
  protected channels = signal<RdpChannels | null>(null);

  protected pickedRdp: number | null = null;
  protected file = signal<File | null>(null);
  protected preview = signal<RdpUploadPreview | null>(null);

  // sales lines tab
  protected lines = signal<RdpLines | null>(null);
  protected linesLoading = signal(false);
  protected lineUpload = signal<RdpUpload | null>(null);
  protected lineRdp: number | null = null;
  protected linePeriod: string | null = null;
  protected lineSearch = '';
  protected lineFirst = 0;
  protected lineRows = 50;
  private lastLineQuery = '';

  protected rdpVisible = false;
  protected rdpForm: SaveRdpDistributor & { id: number | null } = this.blankRdp();
  protected newLabel = '';
  protected newChannel: string | null = null;

  protected rdpOptions = computed(() => this.rdps().filter(d => d.isActive)
    .map(d => ({ id: d.id, label: `${d.custKey} — ${d.name}${d.siteName ? ' (' + d.siteName + ')' : ''}` })));
  protected areaOptions = computed(() => [...new Set(this.rdps().map(d => d.area).filter(a => !!a))].sort());
  /** Months with data, newest first, from the upload history. */
  protected periodOptions = computed(() => [...new Set(this.uploads().flatMap(u => u.periods.split(',').map(p => p.trim()).filter(p => !!p)))]
    .sort().reverse().map(p => ({ value: p, label: `${this.monthName(p)} ${p.slice(0, 4)}` })));
  protected filteredRdps = computed(() => {
    const t = this.searchTerm().toLowerCase().trim();
    return !t ? this.rdps() : this.rdps().filter(d =>
      [d.custKey, d.name, d.rdm, d.area, d.siteName ?? ''].join(' ').toLowerCase().includes(t));
  });

  ngOnInit() {
    this.restoreView();
    this.toolbar.set({
      title: 'RDP Sales Upload',
      refresh: { onClick: () => this.load(), loading: this.loading },
      search: term => this.searchTerm.set(term),
    });
    this.load();
    this.api.sites().subscribe(r => this.sites.set(r.data ?? []));
  }

  ngOnDestroy() {
    this.saveView();
    this.toolbar.clear();
  }

  /** Switching to another app tab destroys this page; keep the inner tab and the Sales lines filters on our app tab. */
  private saveView() {
    const view: SavedView = { tab: this.tab, lineUpload: this.lineUpload(), lineRdp: this.lineRdp, linePeriod: this.linePeriod,
                              lineSearch: this.lineSearch, lineFirst: this.lineFirst, lineRows: this.lineRows };
    this.tabBar.updateTabState(TAB_KEY, { view });
  }

  private restoreView() {
    const v = this.tabBar.tabs().find(t => t.key === TAB_KEY)?.state?.['view'] as SavedView | undefined;
    if (!v) return;
    this.tab = v.tab;
    this.lineUpload.set(v.lineUpload);
    this.lineRdp = v.lineRdp;
    this.linePeriod = v.linePeriod;
    this.lineSearch = v.lineSearch;
    this.lineFirst = v.lineFirst;
    this.lineRows = v.lineRows;
  }

  private load() {
    this.loading.set(true);
    let pending = 3;
    const done = () => { if (--pending === 0) this.loading.set(false); };
    this.api.distributors().subscribe({ next: r => { this.rdps.set(r.data ?? []); done(); }, error: done });
    this.api.uploads().subscribe({ next: r => { this.uploads.set(r.data ?? []); done(); }, error: done });
    this.api.channels().subscribe({ next: r => { this.channels.set(r.data ?? null); done(); }, error: done });
    if (this.lastLineQuery) this.fetchLines(this.lineFirst, this.lineRows, true);
  }

  protected codes(list: { code: string; lines: number }[]) {
    const shown = list.slice(0, 12).map(x => `${x.code} (${x.lines})`).join(', ');
    return list.length > 12 ? `${shown}, …` : shown;
  }

  // ── upload ──
  protected choose(input: HTMLInputElement) {
    this.file.set(input.files?.[0] ?? null);
    this.preview.set(null);
    input.value = '';
    if (this.file()) this.runPreview();
  }

  protected runPreview() {
    const f = this.file();
    if (!f) return;
    this.busy.set(true);
    this.preview.set(null);
    this.api.preview(this.pickedRdp, f).subscribe({
      next: r => { this.busy.set(false); this.preview.set(r.data ?? null); },
      error: e => this.fail(e),
    });
  }

  protected runImport() {
    const f = this.file(), p = this.preview();
    if (!f || !p) return;
    const go = () => {
      this.busy.set(true);
      this.api.upload(this.pickedRdp, f).subscribe({
        next: r => {
          this.busy.set(false);
          this.toast.add({ severity: 'success', summary: 'Imported',
            detail: `${r.data!.lineCount.toLocaleString()} lines (${r.data!.periods})${r.data!.replacedLines ? `, replaced ${r.data!.replacedLines.toLocaleString()} earlier lines` : ''}.` });
          this.file.set(null);
          this.preview.set(null);
          this.load();
          this.viewUpload(r.data!);
        },
        error: e => this.fail(e),
      });
    };
    if (!p.replacesLines) return go();
    this.confirm.confirm({
      header: 'Replace months?',
      message: `This replaces ${p.replacesLines.toLocaleString()} line(s) already uploaded for the same RDP and month(s). Continue?`,
      icon: 'pi pi-exclamation-triangle', accept: go,
    });
  }

  protected deleteUpload(u: RdpUpload) {
    this.confirm.confirm({
      header: 'Delete upload?',
      message: `Removes the ${u.currentLines.toLocaleString()} line(s) of “${u.fileName}” still on file. Months a later file replaced are not affected, and months this file replaced don't come back.`,
      icon: 'pi pi-trash', acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.api.deleteUpload(u.id).subscribe({
        next: () => {
          this.toast.add({ severity: 'info', summary: 'Deleted', detail: u.fileName });
          if (this.lineUpload()?.id === u.id) { this.lineUpload.set(null); this.lineFirst = 0; }
          this.load();
        },
        error: e => this.fail(e),
      }),
    });
  }

  // ── sales lines ──
  protected monthName(period: string) {
    const m = Number(period.slice(5, 7));
    return m >= 1 && m <= 12 ? MONTHS[m - 1] : period;
  }

  /** Opens the Sales lines tab showing just this file's lines. */
  protected viewUpload(u: RdpUpload) {
    this.lineUpload.set(u);
    this.lineRdp = null;
    this.linePeriod = null;
    this.lineSearch = '';
    this.tab = 'lines';
    this.reloadLines(true);
  }

  protected reloadLines(force = false) {
    this.lineFirst = 0;
    this.fetchLines(0, this.lineRows, force);
  }

  protected loadLines(e: TableLazyLoadEvent) {
    this.lineRows = e.rows ?? this.lineRows;
    this.lineFirst = e.first ?? 0;
    this.fetchLines(this.lineFirst, this.lineRows, false);
  }

  private fetchLines(skip: number, take: number, force: boolean) {
    const q = { uploadId: this.lineUpload()?.id ?? null, distributorId: this.lineRdp, period: this.linePeriod,
                search: this.lineSearch, skip, take };
    const key = JSON.stringify(q);
    if (!force && key === this.lastLineQuery) return;   // blur after Enter, or the same page again
    this.lastLineQuery = key;
    this.linesLoading.set(true);
    this.api.lines(q).subscribe({
      next: r => { this.lines.set(r.data ?? null); this.linesLoading.set(false); },
      error: e => { this.linesLoading.set(false); this.lastLineQuery = ''; this.fail(e); },
    });
  }

  // ── RDP list ──
  private blankRdp() {
    return { id: null, custKey: 0, name: '', rdm: '', area: '', territory: '', siteId: null, isActive: true, fileNames: '' };
  }

  protected editRdp(d: RdpDistributor | null) {
    this.rdpForm = d
      ? { id: d.id, custKey: d.custKey, name: d.name, rdm: d.rdm, area: d.area, territory: d.territory, siteId: d.siteId,
          isActive: d.isActive, fileNames: d.fileNames }
      : this.blankRdp();
    this.rdpVisible = true;
  }

  protected saveRdp() {
    const { id, ...dto } = this.rdpForm;
    this.busy.set(true);
    this.api.saveDistributor(id, dto).subscribe({
      next: () => { this.busy.set(false); this.rdpVisible = false; this.toast.add({ severity: 'success', summary: 'RDP', detail: 'Saved.' }); this.load(); },
      error: e => this.fail(e),
    });
  }

  // ── channel mapping ──
  protected mapChannel(label: string, channel: string) {
    if (!channel) return;
    this.api.saveChannel(label, channel).subscribe({
      next: r => {
        this.toast.add({ severity: 'success', summary: 'Channel mapping', detail: `${r.data!.label} → ${r.data!.channel}` });
        this.newLabel = '';
        this.newChannel = null;
        this.api.channels().subscribe(c => this.channels.set(c.data ?? null));
      },
      error: e => this.fail(e),
    });
  }

  protected deleteMap(id: number, label: string) {
    this.confirm.confirm({
      header: 'Delete mapping?', message: `Lines labelled “${label}” will show as unmapped.`, icon: 'pi pi-trash',
      accept: () => this.api.deleteChannel(id).subscribe({
        next: () => this.api.channels().subscribe(c => this.channels.set(c.data ?? null)),
        error: e => this.fail(e),
      }),
    });
  }

  private fail(err: any) {
    this.busy.set(false);
    this.toast.add({ severity: 'error', summary: 'Error', detail: err?.error?.message ?? 'Please try again.' });
  }
}
