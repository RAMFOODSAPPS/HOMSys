import { Component, computed, effect, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { DatePipe, DecimalPipe, PercentPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NavigationEnd, Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { filter } from 'rxjs/operators';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { SelectModule } from 'primeng/select';
import { MultiSelectModule } from 'primeng/multiselect';
import { AutoCompleteModule } from 'primeng/autocomplete';
import { TextareaModule } from 'primeng/textarea';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { MessageModule } from 'primeng/message';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { ConfirmationService, MessageService } from 'primeng/api';
import { GlobalToolbarService } from '../../../core/services/global-toolbar.service';
import { TabBarService } from '../../../core/services/tab-bar.service';
import { TasafChainCustomer, TasafCustomerSuggestion, TasafProductOption, TasafService } from '../../../core/services/tasaf.service';
import { DialogModule } from 'primeng/dialog';
import { DatePickerModule } from 'primeng/datepicker';
import { AuthService } from '../../../core/services/auth.service';
import { FormLayoutService } from '../../../core/services/form-layout.service';
import { FormLayoutItem, cellStyle, defaultItems, mergeLayout } from '../../../core/models/form-layout.model';
import { FormDesignerComponent } from '../../../shared/form-designer/form-designer.component';
import { ResizableColumnsDirective } from '../../../shared/resizable-columns.directive';
import { TASAF_FIELDS, TASAF_FORM_KEY } from './tasaf-layout';
import { buildTasafPdf } from './tasaf-pdf';
import {
  SaveTasafDto, TASAF_STATUS_SEVERITY, utc, parseIsoDate, programDuration, toIsoDate, TasafBranch, TasafDialogAction, TasafDto, TasafFormOptions, TasafCustomerRef, TasafOption, TasafProduct, TasafSkuPrice
} from '../../../core/models/tasaf.model';

type PayKey = 'payInvDeductionAp' | 'payInvDeductionSalesman' | 'payCheckAp' | 'payCheckSalesman'
  | 'payFreeGoodsAp' | 'payFreeGoodsSalesman' | 'payOffInvoiceAp' | 'payOffInvoiceSalesman';

const PAY_ROWS: { label: string; ap: PayKey; sm: PayKey }[] = [
  { label: 'Invoice Deduction', ap: 'payInvDeductionAp', sm: 'payInvDeductionSalesman' },
  { label: 'Check Payment',     ap: 'payCheckAp',        sm: 'payCheckSalesman' },
  { label: 'Free Goods',        ap: 'payFreeGoodsAp',    sm: 'payFreeGoodsSalesman' },
  { label: 'Off-Invoice',       ap: 'payOffInvoiceAp',   sm: 'payOffInvoiceSalesman' },
];

const TAB = '/tasafs';

function blankModel(): SaveTasafDto {
  return {
    title: '', startDate: null, endDate: null,
    salesGroupId: null, areaIds: [], chainId: null,
    programTypeId: null, programTypeOther: '', objectiveId: null, objectiveOther: '',
    targetMarketId: null, targetMarketOther: '', channelIds: [],
    productGroupId: null, productGroup2Id: null, categoryId: null, category2Id: null,
    background: '', concessions: '', mechanics: '', ctsJustification: '',
    promotedValue: null, prePromoSales: null, incrementalSales: null,
    payInvDeductionAp: 0, payInvDeductionSalesman: 0, payCheckAp: 0, payCheckSalesman: 0,
    payFreeGoodsAp: 0, payFreeGoodsSalesman: 0, payOffInvoiceAp: 0, payOffInvoiceSalesman: 0,
    branches: [], customers: [], products: [], costs: [{ costTypeId: null, amount: 0, remarks: '' }]
  };
}

function blankProduct(): TasafProduct {
  return { cProdNo: '', sku: '', p3MoAvgCs: null, splyCs: null, promotedVolCs: 0, incrVolCs: 0, lspPerCase: 0, promotedValue: 0 };
}

/** TASAF form — mirrors the "TASAF Template" sheet section by section. */
@Component({
  selector: 'app-tasaf-page',
  standalone: true,
  providers: [MessageService, ConfirmationService],
  imports: [
    FormsModule, DatePipe, DecimalPipe, PercentPipe, InputTextModule, InputNumberModule, SelectModule,
    MultiSelectModule, AutoCompleteModule, TextareaModule, ButtonModule, TagModule, MessageModule,
    ToastModule, TooltipModule, ConfirmDialogModule, DialogModule, DatePickerModule, FormDesignerComponent, ResizableColumnsDirective
  ],
  template: `
    <p-toast position="top-right" />
    <p-confirmDialog />

    <div class="tasaf">
      <div class="doc-head">
        <div>
          <div class="doc-title">TRADE ACTIVITY SPEND APPROVAL FORM</div>
        </div>
        <div class="doc-meta">
          <span>Document No.</span>
          @if (current()?.refNo) { <strong>{{ current()!.refNo }}</strong> }
          @else if (refPreview()) {
            <strong class="ref-preview" pTooltip="YEAR - Sales Group - Proponent Initials - Proposal No. The proposal number is one continuous series across all TASAFs, assigned when you first save (with a Sales Group).">{{ refPreview() }}</strong>
          }
          @else { <strong class="ref-preview">assigned on save</strong> }
          <span>Date</span><strong>{{ current() ? (utc(current()!.createdAt) | date:'dd MMM yyyy h:mm a') : 'today' }}</strong>
          <span>Status</span><p-tag [value]="current()?.status ?? 'New'" [severity]="severity()" />
        </div>
      </div>

      @if (apiError()) { <p-message severity="error" styleClass="w-full mb-3" [text]="apiError()!" /> }

      <!-- ── Workflow action bar ── -->
      @if (designing()) {
      } @else if (current()?.myAction) {
        <div class="actbar mine">
          <span><i class="pi pi-bell"></i>
            This TASAF is waiting for your approval as {{ current()!.currentRoleCode }} —
            use <i class="pi pi-check"></i> Approve, <i class="pi pi-undo"></i> Return or <i class="pi pi-ban"></i> Reject on the toolbar.
          </span>
        </div>
      } @else if (readonly()) {
        <p-message severity="info" styleClass="w-full mb-3"
          [text]="'Read only — ' + readonlyReason()" />
      }

      @if (designing()) {
        <app-form-designer [formKey]="formKey" title="TASAF" [fields]="fields" [layout]="layout()"
          (saved)="layout.set($event); designing.set(false)" (closed)="designing.set(false)" />
      } @else {
      <fieldset [disabled]="readonly()">
      <!-- Field arrangement comes from the Form Designer layout (defaults in tasaf-layout.ts). -->
      <div class="card fl-grid">
      @for (it of visibleItems(); track it.key) {
        <div class="fl-cell" [class.blk]="isBlock(it.key)" [style]="cellStyle(it)">
        @switch (it.key) {
          @case ('title') {
            <div class="field">
              <label>{{ cap(it) }} <span class="req">*</span></label>
              <input pInputText [(ngModel)]="m.title" maxlength="200" class="w-full"
                placeholder="One line that best describes the program — promo title, activity title, etc." />
            </div>
          }
          @case ('start') {
            <div class="field">
              <label>{{ cap(it) }}@if (isReq(it)) { <span class="req">*</span> }</label>
              <p-datepicker [ngModel]="startDate" (ngModelChange)="startDate = $event; schedulePrices()" dateFormat="M d, yy" placeholder="Select start date" [showIcon]="true"
                [showButtonBar]="true" [maxDate]="endDate" appendTo="body" styleClass="w-full" inputStyleClass="w-full"
                [disabled]="readonly()" />
            </div>
          }
          @case ('end') {
            <div class="field">
              <label>{{ cap(it) }}@if (isReq(it)) { <span class="req">*</span> }</label>
              <p-datepicker [(ngModel)]="endDate" dateFormat="M d, yy" placeholder="Select end date" [showIcon]="true"
                [showButtonBar]="true" [minDate]="startDate" appendTo="body" styleClass="w-full" inputStyleClass="w-full"
                [disabled]="readonly()" />
            </div>
          }
          @case ('duration') {
            <div class="field">
              <label>{{ cap(it) }}@if (isReq(it)) { <span class="req">*</span> }</label>
              <div class="duration" [class.muted]="!duration()">{{ duration() ?? 'Pick the start and end dates' }}</div>
            </div>
          }
          @case ('salesGroup') {
            <div class="field">
              <label>{{ cap(it) }}@if (isReq(it)) { <span class="req">*</span> }</label>
              <p-select [focusOnHover]="false" [ngModel]="m.salesGroupId" (ngModelChange)="onSalesGroup($event)" [options]="list('sales-groups')" optionLabel="name" optionValue="id"
                [showClear]="true" placeholder="Select" styleClass="w-full" [disabled]="readonly()" />
            </div>
          }
          @case ('area') {
            <div class="field">
              <label>{{ cap(it) }}@if (isReq(it)) { <span class="req">*</span> }</label>
              <p-multiSelect #areaMs [focusOnHover]="false" [ngModel]="m.areaIds" (ngModelChange)="onAreas($event)" [options]="list('areas')" optionLabel="name" optionValue="id"
                [filter]="true" [showClear]="true" display="chip" [maxSelectedLabels]="100000"
                placeholder="Select one or more areas" styleClass="w-full chips-wrap" appendTo="body" [disabled]="readonly()">
                <ng-template pTemplate="footer">
                  <div class="ms-footer"><span>{{ m.areaIds.length }} selected</span><p-button label="Done" size="small" (onClick)="areaMs.hide()" /></div>
                </ng-template>
              </p-multiSelect>
            </div>
          }
          @case ('chain') {
            <div class="field">
              <label>{{ cap(it) }}@if (isReq(it)) { <span class="req">*</span> }</label>
              <p-select [focusOnHover]="false" [ngModel]="m.chainId" (ngModelChange)="onChain($event)" [options]="chainOptions()" optionLabel="name" optionValue="id"
                [showClear]="true" [filter]="true" filterBy="name" placeholder="Select" styleClass="w-full" [disabled]="readonly()" />
            </div>
          }
          @case ('custKey') {
            <div class="field">
              <label>{{ cap(it) }} <span class="req">*</span></label>
              @if (chainCustomers().length) {
                <!-- The chain's stores (TASAF Setup › Customers / Chains › customer name keywords). -->
                <p-multiSelect #custMs [focusOnHover]="false" [ngModel]="custKeys()" (ngModelChange)="setCustKeys($event)"
                  [options]="chainCustomers()" optionValue="custKey" optionLabel="label" [filter]="true"
                  filterBy="custKey,cusName,address,branch" [showClear]="true" [virtualScroll]="chainCustomers().length > 500" [virtualScrollItemSize]="48" scrollHeight="20rem"
                  panelStyleClass="opt2-panel" [panelStyle]="{ width: '36rem', maxWidth: '95vw' }"
                  [placeholder]="'Select from ' + chainCustomers().length + ' ' + scopeName() + ' stores'" appendTo="body"
                  display="chip" [maxSelectedLabels]="100000"
                  styleClass="w-full chips-wrap" [disabled]="readonly()" [loading]="chainLoading()">
                  <ng-template let-c pTemplate="item">
                    <div class="cust-opt" [title]="c.custKey + ' — ' + c.cusName + (c.address ? ' · ' + c.address : '')">
                      <span><strong>{{ c.custKey }}</strong> — {{ c.cusName }}</span>
                      <small class="muted">{{ c.branch }}@if (c.address) { · {{ c.address }} }</small>
                    </div>
                  </ng-template>
                  <ng-template pTemplate="footer">
                    <div class="ms-footer"><span>{{ customers().length }} selected</span><p-button label="Done" size="small" (onClick)="custMs.hide()" /></div>
                  </ng-template>
                </p-multiSelect>
              } @else {
                <p-autoComplete [ngModel]="customers()" (ngModelChange)="setCustomers($event)" [multiple]="true"
                  [suggestions]="customerHits()" optionLabel="cusName" [forceSelection]="true" dataKey="custKey"
                  [minLength]="2" [delay]="600" (completeMethod)="searchCustomer($event.query)"
                  appendTo="body" styleClass="w-full" [inputStyle]="{ width: '100%' }"
                  placeholder="Type cust key or name — pick as many as needed" [disabled]="readonly()">
                  <ng-template let-c pTemplate="item">{{ c.custKey }} — {{ c.cusName }}</ng-template>
                  <ng-template let-c pTemplate="selecteditem"><span [pTooltip]="c.cusName" tooltipPosition="top">{{ c.custKey }}</span></ng-template>
                </p-autoComplete>
                @if (chainNoMatch()) { <small class="muted">No {{ scopeName() }} stores found — search all customers.</small> }
              }
              @if (customers().length) {
                <small class="muted">{{ customers().length }} store{{ customers().length === 1 ? '' : 's' }} selected</small>
              }
            </div>
          }
          @case ('branches') {
            <div class="field">
              <label>{{ cap(it) }}@if (isReq(it)) { <span class="req">*</span> }</label>
              <p-multiSelect #branchMs [focusOnHover]="false" [ngModel]="branchSel" (ngModelChange)="onBranches($event)" [options]="branchOptions()" optionLabel="name" dataKey="name"
                [filter]="true" display="chip" [maxSelectedLabels]="100000" [placeholder]="areaCodes() ? 'Select branches of the chosen area' : 'Select one or more branches'" styleClass="w-full chips-wrap"
                appendTo="body" [disabled]="readonly()">
                <ng-template pTemplate="footer">
                  <div class="ms-footer"><span>{{ branchSel.length }} selected</span><p-button label="Done" size="small" (onClick)="branchMs.hide()" /></div>
                </ng-template>
              </p-multiSelect>
            </div>
          }
          @case ('programType') {
            <div class="field">
              <label>{{ cap(it) }}@if (isReq(it)) { <span class="req">*</span> }</label>
              <p-select [focusOnHover]="false" [ngModel]="m.programTypeId" (ngModelChange)="onProgramType($event)" [options]="list('program-types')" optionLabel="name" optionValue="id"
                [showClear]="true" placeholder="Choose the type of trade activity" styleClass="w-full" [disabled]="readonly()" />
            </div>
          }
          @case ('programTypeOther') {
            <div class="field">
              @if (isOthers('program-types', m.programTypeId)) {
                <label>{{ cap(it) }}@if (isReq(it)) { <span class="req">*</span> }</label>
                <input pInputText [(ngModel)]="m.programTypeOther" maxlength="200" class="w-full" />
              } @else if (approvalTypeName()) {
                <label>Approval Matrix</label>
                <span class="muted">{{ approvalTypeName() }}@if (ctsLimit() != null) { · CTS must be within {{ ctsLimit() }}% }</span>
              }
            </div>
          }
          @case ('objective') {
            <div class="field">
              <label>{{ cap(it) }}@if (isReq(it)) { <span class="req">*</span> }</label>
              <p-select [focusOnHover]="false" [(ngModel)]="m.objectiveId" [options]="list('objectives')" optionLabel="name" optionValue="id"
                [showClear]="true" placeholder="What do you want to achieve?" styleClass="w-full" [disabled]="readonly()" />
            </div>
          }
          @case ('objectiveOther') {
            <div class="field">
              @if (isOthers('objectives', m.objectiveId)) {
                <label>{{ cap(it) }}@if (isReq(it)) { <span class="req">*</span> }</label>
                <input pInputText [(ngModel)]="m.objectiveOther" maxlength="200" class="w-full" />
              }
            </div>
          }
          @case ('targetMarket') {
            <div class="field">
              <label>{{ cap(it) }}@if (isReq(it)) { <span class="req">*</span> }</label>
              <p-select [focusOnHover]="false" [(ngModel)]="m.targetMarketId" [options]="list('target-markets')" optionLabel="name" optionValue="id"
                [showClear]="true" placeholder="Who is the target audience?" styleClass="w-full" [disabled]="readonly()" />
            </div>
          }
          @case ('targetMarketOther') {
            <div class="field">
              @if (isOthers('target-markets', m.targetMarketId)) {
                <label>{{ cap(it) }}@if (isReq(it)) { <span class="req">*</span> }</label>
                <input pInputText [(ngModel)]="m.targetMarketOther" maxlength="200" class="w-full" />
              }
            </div>
          }
          @case ('channel') {
            <div class="field">
              <label>{{ cap(it) }}@if (isReq(it)) { <span class="req">*</span> }</label>
              <p-multiSelect #channelMs [focusOnHover]="false" [(ngModel)]="m.channelIds" [options]="list('channels')" optionLabel="name" optionValue="id"
                [filter]="true" [showClear]="true" display="chip" [maxSelectedLabels]="100000"
                placeholder="Where will it be implemented? (one or more)" styleClass="w-full chips-wrap" [disabled]="readonly()">
                <ng-template pTemplate="footer">
                  <div class="ms-footer"><span>{{ m.channelIds.length }} selected</span><p-button label="Done" size="small" (onClick)="channelMs.hide()" /></div>
                </ng-template>
              </p-multiSelect>
            </div>
          }
          @case ('productGroup') {
            <div class="field">
              <label>{{ cap(it) }}@if (isReq(it)) { <span class="req">*</span> }</label>
              <p-select [focusOnHover]="false" [ngModel]="m.productGroupId" (ngModelChange)="m.productGroupId = $event; loadProductOptions()" [options]="list('product-groups')" optionLabel="name" optionValue="id"
                [showClear]="true" placeholder="Primary product group" styleClass="w-full" [filter]="true" filterBy="code,name" [disabled]="readonly()">
                <ng-template let-o pTemplate="item">{{ codeName(o) }}</ng-template>
                <ng-template let-o pTemplate="selectedItem">{{ codeName(o) }}</ng-template>
              </p-select>
            </div>
          }
          @case ('productGroup2') {
            <div class="field">
              <label>{{ cap(it) }} <span class="opt">(optional)</span></label>
              <p-select [focusOnHover]="false" [ngModel]="m.productGroup2Id" (ngModelChange)="m.productGroup2Id = $event; loadProductOptions()" [options]="list('product-groups')" optionLabel="name" optionValue="id"
                [showClear]="true" placeholder="Other product group" styleClass="w-full" [filter]="true" filterBy="code,name" [disabled]="readonly()">
                <ng-template let-o pTemplate="item">{{ codeName(o) }}</ng-template>
                <ng-template let-o pTemplate="selectedItem">{{ codeName(o) }}</ng-template>
              </p-select>
            </div>
          }
          @case ('category') {
            <div class="field">
              <label>{{ cap(it) }}@if (isReq(it)) { <span class="req">*</span> }</label>
              <p-select [focusOnHover]="false" [ngModel]="m.categoryId" (ngModelChange)="m.categoryId = $event; loadProductOptions()" [options]="categoryOptions(m.productGroupId)" optionLabel="name" optionValue="id"
                [showClear]="true" placeholder="Category to activate" styleClass="w-full" [filter]="true" filterBy="code,name" [disabled]="readonly()">
                <ng-template let-o pTemplate="item">{{ codeName(o) }}</ng-template>
                <ng-template let-o pTemplate="selectedItem">{{ codeName(o) }}</ng-template>
              </p-select>
            </div>
          }
          @case ('category2') {
            <div class="field">
              <label>{{ cap(it) }} <span class="opt">(optional)</span></label>
              <p-select [focusOnHover]="false" [ngModel]="m.category2Id" (ngModelChange)="m.category2Id = $event; loadProductOptions()" [options]="categoryOptions(m.productGroup2Id)" optionLabel="name" optionValue="id"
                [showClear]="true" placeholder="Other category" styleClass="w-full" [filter]="true" filterBy="code,name" [disabled]="readonly()">
                <ng-template let-o pTemplate="item">{{ codeName(o) }}</ng-template>
                <ng-template let-o pTemplate="selectedItem">{{ codeName(o) }}</ng-template>
              </p-select>
            </div>
          }
          @case ('categoryNote') {
            <small class="muted note">{{ cap(it) }}</small>
          }
          @case ('background') {
            <div class="field">
              <label>{{ cap(it) }}@if (isReq(it)) { <span class="req">*</span> }</label>
              <textarea pTextarea [(ngModel)]="m.background" [rows]="it.h * 2" [autoResize]="true" class="w-full"
                placeholder="Why this activity? What opportunity in the category, channel or account does it address? Give figures if possible."></textarea>
            </div>
          }
          @case ('concessions') {
            <div class="field">
              <label>{{ cap(it) }}@if (isReq(it)) { <span class="req">*</span> }</label>
              <textarea pTextarea [(ngModel)]="m.concessions" [rows]="it.h * 2" [autoResize]="true" class="w-full"
                placeholder="What do we get in return — extra facings, displays, free posting on the account's social media page, ..."></textarea>
            </div>
          }
          @case ('mechanics') {
            <div class="field">
              <label>{{ cap(it) }}@if (isReq(it)) { <span class="req">*</span> }</label>
              <textarea pTextarea [(ngModel)]="m.mechanics" [rows]="it.h * 2" [autoResize]="true" class="w-full"
                placeholder="How will the program be executed? Describe the promo scheme."></textarea>
            </div>
          }
          @case ('products') {
            <div class="sec-head">
              <h3>{{ cap(it) }}</h3>
              <span class="muted">Skip if not applicable (e.g. sotex / moveout).</span>
              <i class="pi pi-info-circle muted" style="cursor: help" tooltipPosition="top"
                pTooltip="Every column except SPLY is required on submit (enter 0 when there were no sales). LSP / Case is the picked Cust Keys' price with VAT as of the start date (list price + zone add-ons, as in Sales Orders); all picked stores must share one price. You can change it — approvers see the computed price."></i>
              @if (!readonly()) { <p-button label="Add SKU" icon="pi pi-plus" size="small" [text]="true" (onClick)="addProduct()" /> }
            </div>
            @if (m.products.length) {
              <table class="lines" appResizableColumns="tasaf-products">
                <thead><tr>
                  <th style="width:9rem">ProdNo <span class="req">*</span></th><th>SKU <span class="req">*</span></th>
                  <th class="num">P3 Mo Avg (Cs) <span class="req">*</span></th>
                  <th class="num" pTooltip="Sales of the SKU in the same period last year, for the planned promo duration">SPLY (Cs)</th>
                  <th class="num">Promoted Vol (Cs) <span class="req">*</span></th><th class="num">Incr Vol (Cs) <span class="req">*</span></th>
                  <th class="num">LSP / Case <span class="req">*</span></th><th class="num">Promoted Value</th><th></th>
                </tr></thead>
                <tbody>
                  @for (p of m.products; track $index; let i = $index) {
                    <tr>
                      <td>
                        <!-- SKUs of the picked Product Group / Category (active only). -->
                        <p-select [focusOnHover]="false" [autofocusFilter]="false" [ngModel]="p.cProdNo || null" (ngModelChange)="pickProduct(p, $event)"
                          [options]="productOptions()" optionValue="cProdNo" optionLabel="label" [filter]="true"
                          filterBy="cProdNo,prodDesc,packSize,categoryName" [virtualScroll]="productOptions().length > 500" [virtualScrollItemSize]="48" scrollHeight="20rem" panelStyleClass="opt2-panel"
                          [placeholder]="productOptions().length ? 'Select' : 'No SKUs'" appendTo="body" styleClass="w-full"
                          [panelStyle]="{ minWidth: '30rem' }" [loading]="productLoading()" [disabled]="readonly()">
                          <ng-template let-s pTemplate="item">
                            <div class="cust-opt" [title]="s.cProdNo + ' — ' + s.prodDesc + ' ' + s.packSize">
                              <span><strong>{{ s.cProdNo }}</strong> — {{ s.prodDesc }} {{ s.packSize }}</span>
                              <small class="muted">{{ s.category }}@if (s.categoryName) { · {{ s.categoryName }} }</small>
                            </div>
                          </ng-template>
                          <ng-template let-s pTemplate="selectedItem">{{ s.cProdNo }}</ng-template>
                        </p-select>
                      </td>
                      <td><input pInputText [(ngModel)]="p.sku" class="w-full" maxlength="100" /></td>
                      <td><p-inputNumber [(ngModel)]="p.p3MoAvgCs" [maxFractionDigits]="2" [min]="0" inputStyleClass="numin" [disabled]="readonly()" /></td>
                      <td><p-inputNumber [(ngModel)]="p.splyCs" [maxFractionDigits]="2" [min]="0" inputStyleClass="numin" [disabled]="readonly()" /></td>
                      <td><p-inputNumber [(ngModel)]="p.promotedVolCs" [maxFractionDigits]="2" [min]="0" inputStyleClass="numin" [disabled]="readonly()" /></td>
                      <td><p-inputNumber [(ngModel)]="p.incrVolCs" [maxFractionDigits]="2" inputStyleClass="numin" [disabled]="readonly()" /></td>
                      <td>
                        <p-inputNumber [(ngModel)]="p.lspPerCase" [minFractionDigits]="2" [maxFractionDigits]="2" [min]="0" inputStyleClass="numin" [disabled]="readonly()" />
                        @if (lspNote(p); as n) {
                          <small class="lsp-note" [class.warn]="n.warn" [class.link]="n.reset" [pTooltip]="n.tip" tooltipPosition="top"
                            (click)="n.reset && p.lspComputed != null && (p.lspPerCase = p.lspComputed)">{{ n.text }}</small>
                        }
                      </td>
                      <td class="num">{{ (p.promotedVolCs || 0) * (p.lspPerCase || 0) | number:'1.2-2' }}</td>
                      <td>@if (!readonly()) { <p-button icon="pi pi-trash" [text]="true" severity="danger" size="small" (onClick)="m.products.splice(i, 1)" /> }</td>
                    </tr>
                  }
                </tbody>
              </table>
              <!-- Every picked store must have one price per SKU: offer to keep the stores of one price. -->
              @if (!readonly()) {
                @for (x of mixedSkus(); track x.cProdNo) {
                  <div class="lsp-mixed">
                    <i class="pi pi-exclamation-triangle"></i>
                    <span><strong>{{ x.cProdNo }}</strong>: the picked stores have different prices — keep only one price:</span>
                    @for (g of x.groups; track g.price) {
                      <p-button size="small" [outlined]="true" severity="warn" (onClick)="keepStoresAt(g)"
                        [label]="(g.price | number:'1.2-2') + ' (' + g.custKeys.length + ' store' + (g.custKeys.length === 1 ? '' : 's') + ')'"
                        [pTooltip]="g.custKeys.slice(0, 30).join(', ') + (g.custKeys.length > 30 ? '…' : '')" tooltipPosition="top" />
                    }
                  </div>
                }
              }
            }
          }
          @case ('costs') {
            <div class="sec-head">
              <h3>{{ cap(it) }}</h3>
              @if (!readonly()) { <p-button label="Add Cost" icon="pi pi-plus" size="small" [text]="true" (onClick)="m.costs.push({ costTypeId: null, amount: 0, remarks: '' })" /> }
            </div>
            <table class="lines" appResizableColumns="tasaf-costs">
              <thead><tr><th style="width:40%">Description</th><th class="num" style="width:12rem">Cost</th><th>Remarks / Details</th><th></th></tr></thead>
              <tbody>
                @for (c of m.costs; track $index; let i = $index) {
                  <tr>
                    <td>
                      <p-select [focusOnHover]="false" [autofocusFilter]="false" [(ngModel)]="c.costTypeId" [options]="costTypeOptions(c.costTypeId)" optionLabel="name" optionValue="id"
                        [filter]="true" filterBy="name" [showClear]="true" placeholder="Select" styleClass="w-full" appendTo="body" [disabled]="readonly()" />
                    </td>
                    <td><p-inputNumber [(ngModel)]="c.amount" [minFractionDigits]="2" [maxFractionDigits]="2" [min]="0" inputStyleClass="numin" [disabled]="readonly()" /></td>
                    <td><input pInputText [(ngModel)]="c.remarks" class="w-full" maxlength="500" /></td>
                    <td>@if (!readonly()) { <p-button icon="pi pi-trash" [text]="true" severity="danger" size="small" (onClick)="m.costs.splice(i, 1)" /> }</td>
                  </tr>
                }
              </tbody>
              <tfoot><tr><td class="num"><strong>Total Cost</strong></td><td class="num"><strong>{{ totalCost() | number:'1.2-2' }}</strong></td><td colspan="2"></td></tr></tfoot>
            </table>
          }
          @case ('computation') {
            <!-- Account Support & Convention: no products, no pre-promo / incremental; Promoted Value = Annual Projected Sales. -->
            @let sup = isSupport();
            <h3>{{ sup ? 'Account Support & Convention Computation' : cap(it) }}</h3>
            <table class="lines calc">
              <colgroup><col /><col style="width: 15rem" />@if (!sup) { <col style="width: 10rem" /> }</colgroup>
              <thead><tr><th>Description</th><th class="num">Amount (₱)</th>
                @if (!sup) { <th class="num" pTooltip="Amount ÷ Pre-Promo Monthly Avg Sales" tooltipPosition="top">vs Pre-Promo <i class="pi pi-info-circle muted"></i></th> }</tr></thead>
              <tbody>
                <tr><td>TOTAL COST / BUDGET</td><td class="num">{{ totalCost() | number:'1.2-2' }}</td>@if (!sup) { <td></td> }</tr>
                <tr>
                  <td>{{ sup ? 'Annual Projected Sales' : 'Promoted Value' }} @if (hasProducts()) { <span class="muted">(from products)</span> }</td>
                  <td class="num">
                    @if (hasProducts()) { {{ promotedValue() | number:'1.2-2' }} }
                    @else { <p-inputNumber [(ngModel)]="m.promotedValue" placeholder="0.00" [minFractionDigits]="2" [maxFractionDigits]="2" [min]="0" inputStyleClass="numin" [disabled]="readonly()" /> }
                  </td>
                  @if (!sup) { <td class="num">{{ (ratio(promotedValue(), prePromoSales()) | percent:'1.0-2') ?? '—' }}</td> }
                </tr>
                @if (!sup) {
                <tr>
                  <td pTooltip="Can use the past 3 months average, but if the SKU is seasonal use SPLY (actual sales of last year, same promo duration)">
                    Pre-Promo Monthly Avg Sales (Pesos) <i class="pi pi-info-circle muted"></i>
                    @if (hasProducts()) { <span class="muted">(from products: P3Mo Avg × LSP)</span> }
                  </td>
                  <td class="num">
                    @if (hasProducts()) { {{ prePromoSales() | number:'1.2-2' }} }
                    @else { <p-inputNumber [(ngModel)]="m.prePromoSales" placeholder="0.00" [minFractionDigits]="2" [maxFractionDigits]="2" [min]="0" inputStyleClass="numin" [disabled]="readonly()" /> }
                  </td>
                  <td></td>
                </tr>
                <tr>
                  <td>Incremental Sales @if (hasProducts()) { <span class="muted">(from products)</span> }</td>
                  <td class="num">
                    @if (hasProducts()) { {{ incrementalSales() | number:'1.2-2' }} }
                    @else { <p-inputNumber [(ngModel)]="m.incrementalSales" placeholder="0.00" [minFractionDigits]="2" [maxFractionDigits]="2" inputStyleClass="numin" [disabled]="readonly()" /> }
                  </td>
                  <td class="num">{{ (ratio(incrementalSales(), prePromoSales()) | percent:'1.0-2') ?? '—' }}</td>
                </tr>
                }
                <tr class="hl">
                  <td>CTS ({{ sup ? 'Annual Projected Sales' : 'Promoted Value' }}) @if (ctsLimit() != null) { <span class="muted">— must be within {{ ctsLimit() }}%</span> }</td>
                  <td class="num" [style.color]="ctsColor()">
                    {{ (ratio(totalCost(), promotedValue()) | percent:'1.2-2') ?? '—' }}</td>@if (!sup) { <td></td> }
                </tr>
              </tbody>
            </table>
            @if (ctsOver() || m.ctsJustification) {
              <!-- Over the limit: allowed only with a reason, which the approvers see. -->
              <div class="field mt-2">
                @if (ctsOver()) {
                  <p-message severity="warn" styleClass="w-full mb-2"
                    [text]="'CTS is ' + (ratio(totalCost(), promotedValue()) | percent:'1.2-2') + ' — over the ' + ctsLimit() + '% limit for this program type. Justify it below to submit.'" />
                }
                <label>CTS Justification @if (ctsOver()) { <span class="req">*</span> }</label>
                <textarea pTextarea [(ngModel)]="m.ctsJustification" rows="2" [autoResize]="true" class="w-full" maxlength="1000"
                  [disabled]="readonly()" placeholder="Why is CTS over the limit? e.g. new listing, account anniversary, competitive response…"></textarea>
              </div>
            }
          }
          @case ('payment') {
            <h3>{{ cap(it) }}</h3>
            <!-- ONE payment type per TASAF; it carries the whole Total Cost (saved into that type's amount field). -->
            <table class="lines pay">
              <colgroup><col /><col style="width: 15rem" /><col style="width: 12rem" /></colgroup>
              <thead><tr><th>Payment Type <span class="req">*</span></th><th class="num">Amount (₱)</th><th class="num">Total (₱)</th></tr></thead>
              <tbody>
                <tr>
                  <td>
                    <p-select [focusOnHover]="false" [(ngModel)]="payType" [options]="payRows" optionLabel="label" optionValue="ap"
                      placeholder="Select payment type" styleClass="w-full" [showClear]="true" [disabled]="readonly()" />
                  </td>
                  <td class="num">@if (payType) { {{ totalCost() | number:'1.2-2' }} }</td>
                  <td class="num">@if (payType) { {{ totalCost() | number:'1.2-2' }} }</td>
                </tr>
              </tbody>
              <tfoot><tr><td><strong>Total</strong></td>
                <td class="num">@if (payType) { <strong>{{ totalCost() | number:'1.2-2' }}</strong> }</td>
                <td class="num">@if (payType) { <strong>{{ totalCost() | number:'1.2-2' }}</strong> }</td></tr></tfoot>
            </table>
            <small class="muted">One payment type per TASAF — the amount is the Total Cost from Cost Details.</small>
          }
        }
        </div>
      }
      </div>
      </fieldset>
      }

      <!-- ── Signatures / routing ── -->
      <section class="card">
        <h3>Approval</h3>
        <div class="sign">
          <div class="signer">
            <span class="muted">Proposed by</span>
            <strong>{{ current()?.proponentName || 'You' }}</strong>
            <span class="muted">KAM</span>
            @if (current()?.submittedAt) { <span class="ok">Submitted {{ utc(current()!.submittedAt) | date:'dd MMM yyyy h:mm a' }}</span> }
          </div>
          @for (s of current()?.steps ?? []; track s.seq) {
            <div class="signer" [class.now]="s.seq === current()!.currentStepSeq" [class.skip]="s.status === 'Skipped'">
              <span class="muted">{{ s.roleName }}</span>
              <strong>{{ s.userName || 'Vacant' }}</strong>
              <span class="muted">{{ s.roleCode }}</span>
              @switch (s.status) {
                @case ('Approved') { <span class="ok">Approved {{ utc(s.actedAt) | date:'dd MMM yyyy h:mm a' }}</span>
                  <span class="muted" style="font-style: italic; font-size: 0.75rem">Electronically approved</span> }
                @case ('Returned') { <span class="bad">Returned {{ utc(s.actedAt) | date:'dd MMM yyyy h:mm a' }}</span> }
                @case ('Rejected') { <span class="bad">Rejected {{ utc(s.actedAt) | date:'dd MMM yyyy h:mm a' }}</span> }
                @case ('Skipped')  { <span class="muted">{{ s.comment || 'Skipped' }}</span> }
                @default { <span class="muted">{{ s.seq === current()!.currentStepSeq ? 'Waiting for approval' : 'Pending' }}</span> }
              }
            </div>
          }
        </div>
        @if (current()?.approvalTypeName) {
          <p class="muted">Approval matrix: {{ current()!.approvalTypeName }}</p>
        }
        @if (!current()?.steps?.length) {
          <p class="muted">On submit the approval chain is built from the program type's approval matrix
            @if (approvalTypeName()) { ({{ approvalTypeName() }}) } and the sales group's approvers.
            Promo / Near Ex: GKAM → TMM; over 20,000 + NSM; over 50,000 + NSD; over 100,000 + COO.
            Listing fees, conventions and other account support always go to NSD and COO.</p>
        }

      </section>
    </div>

    <!-- Approval history (toolbar History button). -->
    <p-dialog [(visible)]="historyVisible" [modal]="true" [dismissableMask]="true" [style]="{ width: '760px', maxWidth: '95vw' }"
      [header]="'History — ' + (current()?.refNo ?? current()?.title ?? '')">
      @if (current()?.actions?.length) {
        <table class="lines">
          <thead><tr><th>When</th><th>Action</th><th>By</th><th>Comment</th></tr></thead>
          <tbody>
            @for (a of current()!.actions; track $index) {
              <tr>
                <td style="white-space: nowrap">{{ utc(a.at) | date:'dd MMM yyyy h:mm a' }}</td>
                <td><p-tag [value]="a.action" [severity]="sev(a.toStatus)" /></td>
                <td>{{ a.byName }} <span class="muted">({{ a.role }})</span></td>
                <td>{{ a.comment || '—' }}</td>
              </tr>
            }
          </tbody>
        </table>
      } @else {
        <p class="muted">No history yet — it starts when the TASAF is submitted.</p>
      }
    </p-dialog>

    <p-dialog [(visible)]="decisionVisible" [modal]="true" [style]="{ width: '460px' }" [header]="decisionTitle()">
      @if (decisionHint()) { <p class="muted dlg-hint">{{ decisionHint() }}</p> }
      <div class="field full">
        <label>{{ decision() === 'approve' ? 'Comment' : 'Reason' }}
          <span class="req">*</span></label>
        <textarea pTextarea [(ngModel)]="decisionComment" rows="4" [autoResize]="true" class="w-full" maxlength="1000"
          [placeholder]="decisionPlaceholder()"></textarea>
      </div>
      <div class="dlg-actions">
        <p-button label="Close" [text]="true" severity="secondary" (onClick)="decisionVisible = false" />
        <p-button [label]="decisionTitle()" [severity]="decisionSeverity()"
          [loading]="deciding()" (onClick)="decide()" />
      </div>
    </p-dialog>
  `,
  styles: [`
    .doc-head { display: flex; justify-content: space-between; align-items: flex-start;
      padding-bottom: 0.75rem; margin-bottom: 1rem; border-bottom: 2px solid #800000; }
    .doc-title { font-size: 1.15rem; font-weight: 700; color: #800000; letter-spacing: 0.02em; }
    .doc-meta { display: grid; grid-template-columns: auto auto; gap: 0.2rem 0.75rem; font-size: 0.85rem; align-items: center; }
    .card { background: var(--p-surface-card); border-radius: 8px; padding: 1rem 1.25rem; margin-bottom: 0.85rem; }
    .card h3 { margin: 0 0 0.6rem; font-size: 0.95rem; color: #800000; }
    .sec-head { display: flex; align-items: center; gap: 0.75rem; margin-bottom: 0.4rem; }
    .sec-head h3 { margin: 0; }
    .sec-head p-button { margin-left: auto; }
    .fl-grid { display: grid; grid-template-columns: repeat(12, minmax(0, 1fr)); column-gap: 1rem; }
    .fl-cell { grid-column: var(--c) / span var(--w); grid-row: var(--r) / span var(--h); min-width: 0; }
    /* Section blocks (Participating Products, Cost Details, Computation, Mode of Payment):
       maroon top bar + title rule + space between, so each reads as its own section. */
    .fl-cell.blk { border: 1px solid var(--p-surface-border); border-top: 2px solid #e2c4c4; border-radius: 0;
      padding: 0.75rem 1rem; margin: 0.5rem 0 1.25rem; overflow-x: auto; background: var(--p-surface-card); }
    .fl-cell.blk > h3, .fl-cell.blk > .sec-head { padding-bottom: 0.5rem; margin-bottom: 0.75rem;
      border-bottom: 1px solid var(--p-surface-border); }
    .fl-cell.blk > h3 { text-transform: uppercase; letter-spacing: 0.04em; }
    .fl-cell.blk > .sec-head h3 { text-transform: uppercase; letter-spacing: 0.04em; }
    .fl-cell > .note { display: block; margin-bottom: 0.75rem; }
    .field { display: flex; flex-direction: column; gap: 0.3rem; margin-bottom: 0.75rem; min-width: 0; }
    .field.full { grid-column: 1 / -1; }
    .field label { font-weight: 500; font-size: 0.85rem; }
    .duration { display: flex; align-items: center; min-height: 2.5rem; padding: 0 0.75rem; border-radius: 6px;
      background: rgba(128, 0, 0, 0.05); color: #800000; font-weight: 600; font-size: 0.875rem; }
    .duration.muted { color: var(--p-text-muted-color); font-weight: 400; }
    .cust-opt { display: flex; flex-direction: column; line-height: 1.25; }
    /* Cust Key / Branch chips: all shown (never "N selected"), wrapping onto more lines. */
    :host ::ng-deep .chips-wrap .p-multiselect-label { display: flex; flex-wrap: wrap; gap: 0.25rem; white-space: normal; overflow: visible; }
    :host ::ng-deep .chips-wrap .p-multiselect-chip { margin: 0; }
    .req { color: var(--p-red-500, #ef4444); }
    .opt, .muted { font-weight: 400; font-size: 0.75rem; color: var(--p-text-muted-color); }
    fieldset { border: 0; padding: 0; margin: 0; min-width: 0; }
    table.lines { width: 100%; border-collapse: collapse; font-size: 0.85rem; }
    table.lines th { text-align: left; font-weight: 600; padding: 0.35rem; border-bottom: 1px solid var(--p-surface-border); }
    table.lines td { padding: 0.25rem 0.35rem; vertical-align: middle; }
    table.lines tfoot td { border-top: 1px solid var(--p-surface-border); padding-top: 0.45rem; }
    .num, table.lines th.num { text-align: right; }
    /* Number boxes fill their cell, so the right-aligned totals below line up with them. */
    :host ::ng-deep table.lines p-inputnumber, :host ::ng-deep table.lines .p-inputnumber { display: flex; width: 100%; }
    :host ::ng-deep .numin { width: 100%; text-align: right; }
    /* Long text: grow to fit (autoResize), easy to read, and not faded when read-only. */
    textarea { line-height: 1.5; resize: none; overflow-wrap: anywhere; }
    textarea:disabled { opacity: 1; color: var(--p-text-color); background: var(--p-surface-50, #fafafa); }
    tr.hl td { font-weight: 700; color: #800000; }
    .sign { display: grid; grid-template-columns: repeat(auto-fill, minmax(170px, 1fr)); gap: 0.75rem; }
    .signer { display: flex; flex-direction: column; gap: 0.1rem; padding: 0.5rem 0.6rem; border-radius: 6px;
      border: 1px solid var(--p-surface-border); }
    .signer.now { border-color: #800000; background: rgba(128, 0, 0, 0.05); }
    .signer.skip { opacity: 0.55; }
    .ok { font-size: 0.75rem; color: var(--p-green-600, #16a34a); }
    .bad { font-size: 0.75rem; color: var(--p-red-600, #dc2626); }
    .mt { margin-top: 1rem !important; }
    .actbar { display: flex; align-items: center; gap: 0.5rem; background: var(--p-surface-card); border-radius: 8px;
      padding: 0.6rem 1rem; margin-bottom: 0.85rem; }
    .actbar > span { flex: 1; }
    .actbar.mine { border: 1px solid #800000; background: rgba(128, 0, 0, 0.05); font-weight: 500; }
    .dlg-hint { margin: 0 0 0.75rem; }
    .ref-preview { color: var(--p-text-muted-color); font-weight: 500; }
    .dlg-actions { display: flex; justify-content: flex-end; gap: 0.5rem; }
    /* Small screens: one column in tab order. */
    @media (max-width: 800px) { .fl-cell { grid-column: 1 / -1; grid-row: auto; } }
  `]
})
export class TasafPageComponent implements OnInit, OnDestroy {
  private api        = inject(TasafService);
  private router     = inject(Router);
  private toolbar    = inject(GlobalToolbarService);
  private tabBar     = inject(TabBarService);
  private messageSvc = inject(MessageService);
  private confirmSvc = inject(ConfirmationService);
  private layoutApi  = inject(FormLayoutService);
  protected auth     = inject(AuthService);

  // ── Form Designer layout ──
  protected readonly formKey   = TASAF_FORM_KEY;
  protected readonly fields    = TASAF_FIELDS;
  protected readonly cellStyle = cellStyle;
  private readonly fieldDefs   = new Map(TASAF_FIELDS.map(d => [d.key, d]));
  protected layout    = signal<FormLayoutItem[]>(defaultItems(TASAF_FIELDS));
  protected shown     = computed(() => this.layout().filter(i => !i.hidden));
  /** Layout items to render: Participating Products is hidden for Account Support & Convention. */
  visibleItems() { return this.isSupport() ? this.shown().filter(i => i.key !== 'products') : this.shown(); }
  protected designing = signal(false);

  private currentTabKey = TAB;
  private navSub?: Subscription;
  private snapshot = '';

  protected readonly payRows = PAY_ROWS;
  protected readonly utc     = utc;

  protected options      = signal<TasafFormOptions | null>(null);
  protected current      = signal<TasafDto | null>(null);
  protected saving       = signal(false);
  protected deleting     = signal(false);
  protected refreshing   = signal(false);
  protected apiError     = signal<string | null>(null);
  protected customerHits = signal<TasafCustomerSuggestion[]>([]);
  /** Participating SKU dropdown (see loadProductOptions). */
  protected productOptions = signal<TasafProductOption[]>([]);
  protected productLoading = signal(false);
  private productReq = 0;
  /**
   * A saved TASAF opens read-only; Edit (toolbar) unlocks it while it is still the proponent's
   * Draft / Returned, until the next Save or Cancel. A new, unsaved TASAF is always editable.
   */
  protected editing      = signal(false);
  protected readonly     = computed(() => !!this.current() && (!this.current()!.canEdit || !this.editing()));
  protected cannotDelete = computed(() => !this.current()?.canDelete);
  protected severity     = computed(() => this.sev(this.current()?.status ?? 'Draft'));
  protected submitting   = signal(false);
  /** Submit only once the TASAF has been saved (unsaved edits after that are saved on submit). */
  protected submitBlocked = computed(() => this.designing() || !this.current());
  protected deciding     = signal(false);
  protected decision     = signal<TasafDialogAction>('approve');
  protected decisionTitle = computed(() => {
    switch (this.decision()) {
      case 'return': return 'Return to KAM';
      case 'reject': return 'Reject';
      case 'revert': return 'Revert TASAF';
      case 'cancel': return 'Cancel TASAF';
      default:       return `Approve as ${this.current()?.currentRoleCode ?? ''}`.trim();
    }
  });
  protected decisionHint = computed(() => {
    const t = this.current();
    switch (this.decision()) {
      case 'revert': return `Sends this ${t?.status} TASAF back to ${t?.proponentName} as Returned. They can correct and resubmit it, or cancel it. The approval history is kept.`;
      case 'cancel': return 'The TASAF is withdrawn and kept as Cancelled (not deleted). This cannot be undone.';
      default:       return '';
    }
  });
  protected decisionPlaceholder = computed(() => ({
    approve: '', return: 'What should the KAM change?', reject: 'Reason for rejecting',
    revert: 'What was wrong with the approval?', cancel: 'Reason for cancelling'
  })[this.decision()]);
  protected decisionSeverity = computed(() => ({
    approve: 'success', return: 'secondary', reject: 'danger', revert: 'warn', cancel: 'danger'
  } as const)[this.decision()]);
  protected decisionVisible = false;
  protected decisionComment = '';
  protected historyVisible = false;

  // Editable model (ngModel). Totals below are plain methods so they follow every keystroke.
  protected m: SaveTasafDto = blankModel();
  /** Participating stores (Cust Keys). */
  protected customers = signal<TasafCustomerRef[]>([]);
  protected custKeys  = computed(() => this.customers().map(c => c.custKey));
  protected branchSel: TasafBranch[] = [];
  /** Stores of the selected chain (empty = use the free customer search). */
  protected chainCustomers = signal<TasafChainCustomer[]>([]);
  protected chainLoading   = signal(false);
  protected chainNoMatch   = signal(false);
  private chainReq = 0;
  /** Stores per chain in the picked branches / areas (chain-store-counts); null = no filter. */
  protected chainCounts = signal<Record<number, number> | null>(null);
  private countsReq = 0;
  /** Program duration pickers (Date); sent as 'yyyy-MM-dd'. */
  protected startDate: Date | null = null;
  protected endDate: Date | null = null;
  duration() { return programDuration(this.startDate, this.endDate); }

  ngOnInit() {
    this.api.getOptions().subscribe(res => this.options.set(res.data ?? null));
    this.layoutApi.get(TASAF_FORM_KEY).subscribe({
      next: res => this.layout.set(mergeLayout(TASAF_FIELDS, res.data?.layout)),
      error: () => {} // keep the default layout
    });
    this.handleNavState();
    if (!this.productReq) this.loadProductOptions(); // new TASAF: every active SKU until a scope is picked
    this.navSub = this.router.events.pipe(filter(e => e instanceof NavigationEnd)).subscribe(() => this.handleNavState());
    this.syncToolbar();
    this.tabBar.registerDirtyChecker(TAB, () => this.isDirty());
  }

  ngOnDestroy() {
    if (!this.current() && this.isDirty()) {
      this.tabBar.updateTabState(TAB, { draftForm: this.payload() });
    }
    this.tabBar.unregisterDirtyChecker(TAB);
    this.tabBar.unregisterDirtyChecker(this.currentTabKey);
    this.toolbar.clear();
    this.navSub?.unsubscribe();
  }

  // ── Tabs / navigation ──────────────────────────────────────
  private handleNavState() {
    // Email link "/tasafs?id=42": reopen as a normal state navigation (opens the TASAF's own tab).
    const qid = Number(new URLSearchParams(location.search).get('id'));
    if (qid > 0) {
      this.router.navigate(['/tasafs'], { state: { tasafId: qid }, replaceUrl: true });
      return;
    }
    const state     = history.state as Record<string, unknown>;
    const tasafId   = state?.['tasafId'] as number | undefined;
    const draftForm = state?.['draftForm'] as SaveTasafDto | undefined;
    if (tasafId) {
      if (tasafId !== this.current()?.id) {
        this.api.getById(tasafId).subscribe({
          next: res => { if (res.data) this.loadTasaf(res.data); },
          error: err => this.apiError.set(err?.error?.message ?? 'Failed to open TASAF.')
        });
      }
    } else {
      if (this.current()) this.resetFormOnly();
      if (draftForm) {
        this.setModel(draftForm);
        this.snapshot = '';
        this.tabBar.updateTabState(TAB, undefined);
      }
    }
  }

  private resetFormOnly() {
    this.tabBar.unregisterDirtyChecker(this.currentTabKey);
    this.current.set(null);
    this.apiError.set(null);
    this.setModel(blankModel());
    this.currentTabKey = TAB;
    this.tabBar.updateTabState(TAB, undefined);
    this.tabBar.registerDirtyChecker(TAB, () => this.isDirty());
    this.syncToolbar();
  }

  private loadTasaf(t: TasafDto) {
    this.current.set(t);
    this.prefetchLogo(t.id); // Export › PDF is then instant
    this.apiError.set(null);
    this.setModel(t);
    this.tabBar.unregisterDirtyChecker(this.currentTabKey);
    this.currentTabKey = `${TAB}#${t.id}`;
    this.tabBar.openTab({
      key: this.currentTabKey,
      label: `TASAF - ${t.refNo ?? t.title}`,
      route: TAB,
      icon: 'pi-file-edit',
      state: { tasafId: t.id }
    });
    this.tabBar.registerDirtyChecker(this.currentTabKey, () => this.isDirty());
    this.editing.set(false); // saved / opened: read-only until Edit
    this.syncToolbar();
  }

  private setModel(src: SaveTasafDto) {
    const b = blankModel();
    this.m = {
      ...b,
      ...Object.fromEntries(Object.keys(b).map(k => [k, (src as unknown as Record<string, unknown>)[k] ?? (b as unknown as Record<string, unknown>)[k]])),
      areaIds: [...(src.areaIds ?? [])],
      channelIds: [...(src.channelIds ?? [])],
      branches: [],  // held in branchSel
      customers: [], // held in customers()
      products: (src.products ?? []).map(p => ({ ...p })),
      costs: (src.costs ?? []).length ? src.costs.map(c => ({ ...c })) : b.costs,
    } as SaveTasafDto;
    this.branchSel = (src.branches ?? []).map(x => ({ ...x }));
    this.customers.set((src.customers ?? []).map(c => ({ custKey: c.custKey, cusName: c.cusName })));
    this.payType = this.toPayType(src);
    this.startDate = parseIsoDate(src.startDate);
    this.endDate = parseIsoDate(src.endDate);
    this.loadCustomerOptions(false);
    this.loadChainCounts(false);
    this.loadProductOptions();
    this.snapshot = JSON.stringify(this.payload());
  }

  private payload(): SaveTasafDto {
    // Account Support & Convention: no products / pre-promo / incremental are saved.
    const sup = this.isSupport();
    return {
      ...this.m,
      ...(sup ? { products: [], prePromoSales: null, incrementalSales: null } : {}),
      ...this.payFields(),
      startDate: toIsoDate(this.startDate),
      endDate: toIsoDate(this.endDate),
      customers: this.customers().map(c => ({ custKey: c.custKey, cusName: c.cusName })),
      branches: this.branchSel.map(b => ({ siteId: b.siteId ?? null, name: b.name })),
    };
  }

  private isDirty() {
    if (this.readonly()) return false;
    const now = JSON.stringify(this.payload());
    return this.current() ? now !== this.snapshot : now !== JSON.stringify(blankModel());
  }

  private syncToolbar() {
    const t = this.current();
    this.toolbar.set({
      title:   t ? `TASAF ${t.refNo ?? '(Draft)'} — ${t.title}` : 'New TASAF',
      save:    { onClick: () => this.save(), loading: this.saving, disabled: this.readonly },
      // Edit unlocks a saved Draft / Returned TASAF (it opens read-only).
      ...(t?.canEdit && !this.editing() ? { edit: { onClick: () => this.startEdit(), disabled: this.designing } } : {}),
      // Submit: the proponent's own Draft / Returned TASAF, read-only or being edited.
      ...(!t || t.canEdit ? { submit: {
        label: !t ? 'Save the TASAF first, then submit' : t.status === 'Returned' ? 'Resubmit for approval' : 'Submit for approval',
        onClick: () => this.confirmSubmit(), loading: this.submitting, disabled: this.submitBlocked
      } } : {}),
      ...(t?.myAction ? {
        approve: { label: `Approve as ${t.currentRoleCode ?? ''}`.trim(), onClick: () => this.openDecision('approve'), disabled: this.designing },
        return:  { onClick: () => this.openDecision('return'), disabled: this.designing },
        reject:  { onClick: () => this.openDecision('reject'), disabled: this.designing },
      } : {}),
      // Admin fixes an approval mistake; proponent/Admin withdraws (kept as Cancelled).
      ...(t?.canRevert ? { revert: { onClick: () => this.openDecision('revert'), disabled: this.designing } } : {}),
      ...(t?.canCancel ? { cancelDoc: { label: 'Cancel TASAF', onClick: () => this.openDecision('cancel'), disabled: this.designing } } : {}),
      cancel: { onClick: () => this.cancel() },
      ...(!this.readonly() ? { clearForm: { onClick: () => this.confirmClear(), disabled: this.designing } } : {}),
      add:    { onClick: () => this.newTasaf() },
      // Delete is Admin-only (also enforced by the API).
      ...(this.auth.isAdmin() ? { delete: { onClick: () => this.confirmDelete(), disabled: this.cannotDelete, loading: this.deleting } } : {}),
      refresh: { onClick: () => this.refresh(), loading: this.refreshing },
      // Approval history of a saved TASAF (dialog).
      ...(t ? { history: { onClick: () => (this.historyVisible = true), disabled: this.designing } } : {}),
      list:    { onClick: () => this.router.navigate(['/tasafs/list']) },
      // Print / PDF: the saved TASAF as a document (same layout as the approval email).
      print:   { onClick: () => this.printTasaf() },
      ...(t ? { export: { pdf: () => this.exportPdf() } } : {}),
      ...(this.auth.isAdmin() ? { design: { onClick: () => this.designing.set(true), disabled: this.designing } } : {}),
    });
  }

  /** Cancel: leave the designer, else discard changes and go back to a blank New TASAF. */
  /**
   * Toolbar Print: the saved TASAF as a clean document (server layout, same as the approval email)
   * printed from a hidden frame — the browser's print dialog also offers "Save as PDF". An unsaved
   * new TASAF has no document yet, so that prints the screen.
   */
  private printTasaf() {
    const t = this.current();
    if (!t) { window.print(); return; }
    this.warnUnsaved();
    this.api.document(t.id).subscribe({
      next: res => {
        const html = res.data?.html;
        if (!html) return;
        const title = (t.refNo ?? t.title).replace(/[<>&"]/g, '');
        const frame = document.createElement('iframe');
        frame.style.cssText = 'position:fixed;right:0;bottom:0;width:0;height:0;border:0';
        document.body.appendChild(frame);
        const d = frame.contentDocument!;
        d.open();
        d.write(`<!doctype html><html><head><meta charset="utf-8"><title>${title}</title><style>
          @page { size: A4; margin: 12mm; } body { margin: 0; }
          * { -webkit-print-color-adjust: exact; print-color-adjust: exact; } tr { page-break-inside: avoid; }
          </style></head><body>${html}</body></html>`);
        d.close();
        setTimeout(() => {
          frame.contentWindow?.focus();
          frame.contentWindow?.print();
          setTimeout(() => frame.remove(), 1000);
        }, 300);
      },
      error: err => this.messageSvc.add({ severity: 'error', summary: 'Print', detail: err?.error?.message ?? 'Failed to load the TASAF for printing.' })
    });
  }

  /** Toolbar Export › PDF: the saved TASAF as an A4 PDF (tasaf-pdf.ts, same sections as print). */
  private exportPdf() {
    const t = this.current();
    if (!t) return;
    this.warnUnsaved();
    const save = (logo: string | null) => buildTasafPdf(t, logo).save(`${t.refNo ?? 'TASAF-draft'}.pdf`);
    // Logo prefetched when the TASAF opened → instant. Otherwise fetch it (no logo / an error still gives the PDF).
    if (this.logoFor.has(t.id)) { save(this.logoFor.get(t.id) ?? null); return; }
    this.messageSvc.add({ severity: 'info', summary: 'PDF', detail: 'Preparing PDF…', life: 2000 });
    this.api.document(t.id).subscribe({
      next: res => { this.logoFor.set(t.id, res.data?.logo ?? null); save(res.data?.logo ?? null); },
      error: () => save(null)
    });
  }

  /** Company logo per TASAF (data: URI or null), loaded in the background when the TASAF opens. */
  private logoFor = new Map<number, string | null>();
  private prefetchLogo(id: number) {
    if (this.logoFor.has(id)) return;
    this.api.document(id).subscribe({ next: res => this.logoFor.set(id, res.data?.logo ?? null), error: () => {} });
  }

  /** Print / PDF use the saved version. */
  private warnUnsaved() {
    if (this.isDirty())
      this.messageSvc.add({ severity: 'warn', summary: 'Unsaved changes', detail: 'This uses the saved version — save first to include your changes.' });
  }

  /** Toolbar Edit: unlock the saved TASAF until the next Save / Cancel. */
  private startEdit() {
    this.editing.set(true);
    this.syncToolbar();
    this.schedulePrices();
  }

  private cancel() {
    if (this.designing()) { this.designing.set(false); return; }
    // Editing a saved TASAF: Cancel drops the edits and locks it again (stays on it).
    const t = this.current();
    if (t && this.editing()) {
      if (!this.isDirty()) { this.editing.set(false); this.syncToolbar(); return; }
      this.confirmSvc.confirm({
        header: 'Discard changes',
        message: 'Discard your unsaved changes to this TASAF?',
        icon: 'pi pi-exclamation-triangle',
        accept: () => this.loadTasaf(t)
      });
      return;
    }
    if (!this.isDirty()) { this.newTasaf(); return; }
    this.confirmSvc.confirm({
      header: 'Discard changes',
      message: 'Discard your unsaved changes?',
      icon: 'pi pi-exclamation-triangle',
      accept: () => this.newTasaf()
    });
  }

  /** Clear: empty every field but stay on this TASAF (an open one still needs Save to keep it). */
  private confirmClear() {
    this.confirmSvc.confirm({
      header: 'Clear form',
      message: 'Clear all fields on this form?' + (this.current() ? ' Nothing is saved until you click Save.' : ''),
      icon: 'pi pi-eraser',
      accept: () => {
        const snap = this.snapshot;
        this.setModel(blankModel());
        if (this.current()) this.snapshot = snap; // still dirty vs. the saved TASAF
        this.apiError.set(null);
      }
    });
  }

  private newTasaf() {
    if (this.current()) this.tabBar.switchToDefaultTab(TAB);
    this.resetFormOnly();
  }

  private refresh() {
    const t = this.current();
    if (!t) { this.resetFormOnly(); return; }
    this.refreshing.set(true);
    this.api.getById(t.id).subscribe({
      next: res => { this.refreshing.set(false); if (res.data) this.loadTasaf(res.data); },
      error: () => this.refreshing.set(false)
    });
  }

  /**
   * RefNo preview before the first save (GUIDE sheet): YEAR-SG-INITIALS-NN, e.g. 2026-RKA-ER-NN.
   * Only for the proponent's own TASAF not numbered yet; the server assigns the real NN on save.
   */
  refPreview(): string | null {
    const u = this.auth.user();
    const t = this.current();
    if (!u || (t && t.proponentUserId !== u.id)) return null;
    const lead = (s: string | undefined) => (s ?? '').match(/\p{L}/u)?.[0]?.toUpperCase() ?? '';
    const sg = this.list('sales-groups').find(o => o.id === this.m.salesGroupId);
    const sgCode = sg ? (sg.code || sg.name).toUpperCase() : 'SG';
    const year = new Date(Date.now() + 8 * 3600_000).getUTCFullYear(); // Asia/Manila
    return `${year}-${sgCode}-${lead(u.firstName)}${lead(u.lastName)}-NN`;
  }

  /** Caption: the designer's custom label, else the field's default. */
  cap(it: FormLayoutItem) { return it.label || this.fieldDefs.get(it.key)?.label || ''; }
  isBlock(key: string)    { return this.fieldDefs.get(key)?.kind === 'block'; }
  /** Required on submit (TASAF_FIELDS.required) — label gets a red *. */
  isReq(it: FormLayoutItem) { return !!this.fieldDefs.get(it.key)?.required; }

  // ── Lookups ────────────────────────────────────────────────
  /** "01 — Tom Sauce/Paste" for dropdowns whose entries have a code. */
  codeName(o: TasafOption | null | undefined) {
    if (!o) return '';
    return o.code && o.code !== o.name ? `${o.code} — ${o.name}` : o.name;
  }

  list(key: string): TasafOption[] { return this.options()?.lists[key] ?? []; }

  /**
   * Customer / Chain dropdown: chains of the picked sales group; with branches / areas picked, only
   * chains that have stores there, labelled with the count. Chains with no name keywords (e.g. "LKA
   * Others") can't be counted and are always offered, as is the chain already picked.
   */
  private chainOptsCache: { key: string; list: TasafOption[] } | null = null;
  chainOptions(): TasafOption[] {
    const sg = this.m.salesGroupId;
    const all = this.list('chains');
    const counts = this.chainCounts();
    const key = `${sg}|${this.m.chainId}|${JSON.stringify(counts)}|${all.length}`;
    if (this.chainOptsCache?.key === key) return this.chainOptsCache.list;
    let list = sg ? all.filter(c => !c.parentId || c.parentId === sg) : all;
    if (counts) {
      list = list
        .filter(c => counts[c.id] === undefined || counts[c.id] > 0 || c.id === this.m.chainId)
        .map(c => counts[c.id] === undefined ? c : { ...c, name: `${c.name} (${counts[c.id].toLocaleString()} stores)` });
    }
    this.chainOptsCache = { key, list };
    return list;
  }

  categoryOptions(groupId: number | null): TasafOption[] {
    const all = this.list('categories');
    return groupId ? all.filter(c => c.parentId === groupId) : all;
  }

  isOthers(key: string, id: number | null): boolean {
    if (!id) return false;
    return (this.list(key).find(o => o.id === id)?.name ?? '').toLowerCase().startsWith('others');
  }

  searchCustomer(term: string) {
    this.api.searchCustomers(term).subscribe(res => this.customerHits.set(res.data ?? []));
  }

  /** Free-search mode (autocomplete, multiple). */
  setCustomers(list: TasafCustomerRef[] | null) {
    const seen = new Set<string>();
    this.customers.set((list ?? [])
      .filter(c => c?.custKey && !seen.has(c.custKey) && !!seen.add(c.custKey))
      .map(c => ({ custKey: c.custKey, cusName: c.cusName })));
  }

  /** Chain mode (multi-select of the chain's stores). */
  setCustKeys(keys: string[] | null) {
    const byKey = new Map<string, TasafCustomerRef>(this.customers().map(c => [c.custKey, c]));
    for (const c of this.chainCustomers()) byKey.set(c.custKey, { custKey: c.custKey, cusName: c.cusName });
    this.customers.set((keys ?? []).map(k => byKey.get(k)).filter((c): c is TasafCustomerRef => !!c));
  }

  // ── Cust Key per chain ─────────────────────────────────────
  /** User picked a chain: load its stores; drop picked stores that aren't one of them. */
  onChain(chainId: number | null) {
    this.m.chainId = chainId;
    // A chain belongs to one sales group (TASAF Setup › Customers / Chains) — fill it in.
    const sg = this.list('chains').find(o => o.id === chainId)?.parentId;
    if (sg && !this.m.salesGroupId) this.m.salesGroupId = sg;
    this.loadCustomerOptions(true);
  }

  /** Areas changed: the Cust Key list narrows to the areas' branches (TASAF Setup › Areas). */
  onAreas(areaIds: number[] | null) {
    this.m.areaIds = areaIds ?? [];
    // Branches outside the picked areas no longer apply.
    const codes = this.areaCodes();
    if (codes) {
      const before = this.branchSel.length;
      this.branchSel = this.branchSel.filter(b => b.siteId != null && this.inAreas(b, codes));
      const cleared = before - this.branchSel.length;
      if (cleared) this.messageSvc.add({ severity: 'info', summary: 'Branch',
        detail: `${cleared} branch${cleared === 1 ? '' : 'es'} removed — not in the chosen area.` });
    }
    this.loadCustomerOptions(true);
    this.loadChainCounts(true);
  }

  /**
   * Store counts per chain for the picked branches / areas. A chain with no stores there drops out
   * of the Customer / Chain dropdown; if it was the picked one it is cleared (userChange only — a
   * saved TASAF keeps its chain).
   */
  private loadChainCounts(userChange: boolean) {
    const req = ++this.countsReq;
    if (!this.m.areaIds.length && !this.branchSiteIds().length) { this.chainCounts.set(null); return; }
    this.api.chainStoreCounts(this.m.areaIds, this.branchSiteIds()).subscribe({
      next: res => {
        if (req !== this.countsReq) return;
        const counts = res.data ?? null;
        this.chainCounts.set(counts);
        const id = this.m.chainId;
        if (userChange && id && counts && counts[id] === 0) {
          const name = this.chainName();
          this.onChain(null);
          this.messageSvc.add({ severity: 'info', summary: 'Customer / Chain',
            detail: `${name} cleared — it has no stores in the chosen ${this.branchSiteIds().length ? 'branches' : 'area'}.` });
        }
      },
      error: () => { if (req === this.countsReq) this.chainCounts.set(null); }
    });
  }

  /** Branches changed: the Cust Key list narrows to the picked branches' stores. */
  onBranches(sel: TasafBranch[] | null) {
    this.branchSel = sel ?? [];
    this.loadCustomerOptions(true);
    this.loadChainCounts(true);
  }

  /**
   * Picked branches that narrow the Cust Key list (Site ids). Empty = use the area instead — none
   * picked, or an "ALL …" scope is among them.
   */
  private branchSiteIds(): number[] {
    if (this.branchSel.some(b => b.siteId == null)) return [];
    return this.branchSel.map(b => b.siteId!);
  }

  /**
   * Site codes of the picked areas (TASAF Setup › Areas), or null = no filter (no area picked, or
   * one with no branch codes, e.g. Nationwide). Same rule as the Cust Key list on the server.
   */
  protected areaCodes(): Set<string> | null {
    const areas = this.list('areas').filter(a => this.m.areaIds.includes(a.id));
    if (!areas.length) return null;
    const codes = new Set<string>();
    for (const a of areas) {
      const list = (a.branchCodes ?? '').split(',').map(c => c.trim().toUpperCase()).filter(c => c);
      if (!list.length) return null;
      list.forEach(c => codes.add(c));
    }
    return codes;
  }

  private inAreas(b: TasafBranch, codes: Set<string>) {
    return codes.has((b.code ?? '').toUpperCase()) || codes.has(b.name.toUpperCase());
  }

  /**
   * Branch dropdown: with an area picked, only that area's sites (the ALL … scopes don't apply);
   * otherwise every site plus the scopes. Branches already on the TASAF stay listed. Cached so the
   * multiselect gets the same array between change-detection runs.
   */
  private branchOptsCache: { key: string; all: TasafBranch[] | undefined; list: TasafBranch[] } | null = null;
  protected branchOptions(): TasafBranch[] {
    const all = this.options()?.branches;
    const key = this.m.areaIds.join(',') + '|' + this.branchSel.map(b => b.name).join(',');
    if (this.branchOptsCache?.key === key && this.branchOptsCache.all === all) return this.branchOptsCache.list;
    const codes = this.areaCodes();
    let list = all ?? [];
    if (codes) {
      list = list.filter(b => b.siteId != null && this.inAreas(b, codes));
      const extra = this.branchSel.filter(s => !list.some(b => b.name === s.name));
      list = [...list, ...extra];
    }
    this.branchOptsCache = { key, all, list };
    return list;
  }

  /** Sales group changed: a chain of another group no longer applies. */
  onSalesGroup(salesGroupId: number | null) {
    this.m.salesGroupId = salesGroupId;
    if (this.m.chainId && !this.chainOptions().some(c => c.id === this.m.chainId)) this.onChain(null);
  }

  private loadCustomerOptions(clearIfNotInList: boolean) {
    const req = ++this.chainReq;
    this.chainCustomers.set([]);
    this.chainNoMatch.set(false);
    const siteIds = this.branchSiteIds();
    if (!this.m.chainId && !this.m.areaIds.length && !siteIds.length) return;
    this.chainLoading.set(true);
    this.api.customerOptions(this.m.chainId, this.m.areaIds, siteIds).subscribe({
      next: res => {
        if (req !== this.chainReq) return; // a newer chain / area / branch was picked meanwhile
        this.chainLoading.set(false);
        let list = res.data?.customers ?? [];
        const inList = new Set(list.map(c => c.custKey));
        const outside = this.customers().filter(c => !inList.has(c.custKey));
        if (list.length && outside.length) {
          if (clearIfNotInList) {
            // Chain / area changed: keep only stores that belong to it.
            this.customers.update(cs => cs.filter(c => inList.has(c.custKey)));
          } else {
            // Saved stores that no longer match the keywords still show as selected.
            list = [...outside.map(c => ({ custKey: c.custKey, cusName: c.cusName, branch: '', address: '' })), ...list];
          }
        }
        this.chainCustomers.set(list.map(c => ({ ...c, label: c.custKey }))); // chips show the code only
        this.chainNoMatch.set(!!res.data?.filtered && list.length === 0);
      },
      error: () => { if (req === this.chainReq) this.chainLoading.set(false); }
    });
  }

  chainName() { return this.list('chains').find(o => o.id === this.m.chainId)?.name ?? ''; }

  /** "Puregold · South Luzon, Bicol" (or the picked branches) — what the Cust Key list is narrowed to. */
  scopeName() {
    const where = this.branchSiteIds().length
      ? this.branchSel.map(b => b.name).join(', ')
      : this.list('areas').filter(a => this.m.areaIds.includes(a.id)).map(a => a.name).join(', ');
    return [this.chainName(), where].filter(s => s).join(' · ');
  }

  // ── Participating SKUs ─────────────────────────────────────
  /**
   * SKU dropdown = active products of the picked Product Group / Category (and the secondary
   * pair). Products already on the TASAF stay listed even if they fall outside the scope.
   */
  loadProductOptions() {
    const req = ++this.productReq;
    this.productLoading.set(true);
    this.api.productOptions({
      productGroupId: this.m.productGroupId, categoryId: this.m.categoryId,
      productGroup2Id: this.m.productGroup2Id, category2Id: this.m.category2Id
    }).subscribe({
      next: res => {
        if (req !== this.productReq) return; // a newer scope was picked meanwhile
        this.productLoading.set(false);
        const list = res.data ?? [];
        const have = new Set(list.map(o => o.cProdNo));
        const extra = this.m.products
          .filter(p => p.cProdNo && !have.has(p.cProdNo))
          .map(p => ({ cProdNo: p.cProdNo, prodDesc: p.sku, packSize: '', category: '', categoryName: 'not in the selected category', lspPerCase: p.lspPerCase }));
        this.productOptions.set([...extra, ...list].map(o => ({ ...o, label: `${o.cProdNo} — ${o.prodDesc} ${o.packSize}`.trim() })));
      },
      error: err => {
        if (req !== this.productReq) return;
        this.productLoading.set(false);
        this.messageSvc.add({ severity: 'warn', summary: 'SKU list',
          detail: err?.status === 404 ? 'The server is out of date — restart the API to load the SKU list.'
                                      : 'Could not load the SKU list.' });
      }
    });
  }

  /** Picking a SKU fills the SKU name; LSP / Case is then priced for the picked stores. */
  pickProduct(p: TasafProduct, cProdNo: string | null) {
    const o = this.productOptions().find(x => x.cProdNo === cProdNo);
    if ((cProdNo ?? '') !== p.cProdNo) { p.lspPerCase = 0; p.lspComputed = null; }
    p.cProdNo = cProdNo ?? '';
    this.schedulePrices();
    if (!o) return;
    p.sku = `${o.prodDesc} ${o.packSize}`.trim();
  }

  // ── LSP / Case = the picked stores' price ──────────────────
  /**
   * Each SKU's price (with VAT) for the picked Cust Keys as of the start date — base price + the
   * store's zone add-ons, the same rule as Sales Orders (server: TasafPriceService). All picked
   * stores must share one price per SKU. LSP stays editable: a changed value is kept (and shown to
   * approvers next to the computed price); an untouched one follows the stores.
   */
  protected skuPrices = signal<Record<string, TasafSkuPrice>>({});
  private pricesReq = 0;
  private pricesTimer?: ReturnType<typeof setTimeout>;
  private pricesOnStores = effect(() => { this.customers(); this.schedulePrices(); });

  schedulePrices() {
    clearTimeout(this.pricesTimer);
    this.pricesTimer = setTimeout(() => this.refreshPrices(), 300);
  }

  private refreshPrices() {
    const req = ++this.pricesReq;
    const rows = this.m.products.filter(p => p.cProdNo?.trim());
    const keys = this.customers().map(c => c.custKey);
    const edit = !this.readonly();
    if (!rows.length || !keys.length) {
      this.skuPrices.set({});
      // No stores picked: LSP is left blank (unless the proponent typed one).
      if (edit) for (const p of rows) if (!p.lspPerCase || p.lspPerCase === p.lspComputed) { p.lspPerCase = 0; p.lspComputed = null; }
      return;
    }
    this.api.productPrices([...new Set(rows.map(p => p.cProdNo))], keys, toIsoDate(this.startDate)).subscribe({
      next: res => {
        if (req !== this.pricesReq) return;
        const map: Record<string, TasafSkuPrice> = {};
        for (const x of res.data ?? []) map[x.cProdNo] = x;
        this.skuPrices.set(map);
        if (!edit) return; // approvers see the saved LSP / computed price
        for (const p of rows) {
          const g = map[p.cProdNo]?.groups ?? [];
          const one = g.length === 1 ? g[0].price : null;
          const untouched = !p.lspPerCase || p.lspPerCase === p.lspComputed;
          p.lspComputed = one;
          if (untouched) p.lspPerCase = one ?? 0;
        }
      },
      error: () => { if (req === this.pricesReq) this.skuPrices.set({}); }
    });
  }

  /** SKUs on the form whose picked stores have more than one price. */
  mixedSkus(): TasafSkuPrice[] {
    const map = this.skuPrices();
    return Object.values(map).filter(x => x.groups.length > 1 && this.m.products.some(p => p.cProdNo === x.cProdNo));
  }

  /** Keep only the picked stores at one price (resolves a mixed-price SKU). */
  keepStoresAt(g: { price: number; custKeys: string[] }) {
    const keep = new Set(g.custKeys);
    const before = this.customers().length;
    this.customers.update(cs => cs.filter(c => keep.has(c.custKey)));
    const removed = before - this.customers().length;
    this.messageSvc.add({ severity: 'info', summary: 'Cust Key',
      detail: `${removed} store${removed === 1 ? '' : 's'} with a different price removed.` });
  }

  /** Small note under LSP / Case: mixed prices, changed from the computed price, or not priced. */
  lspNote(p: TasafProduct): { text: string; tip: string; warn: boolean; reset: boolean } | null {
    if (!p.cProdNo?.trim()) return null;
    const x = this.skuPrices()[p.cProdNo];
    const fmt = (n: number) => n.toLocaleString('en-PH', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    if (x && x.groups.length > 1)
      return { text: `${x.groups.length} different prices`, warn: true, reset: false,
        tip: x.groups.map(g => `${fmt(g.price)} — ${g.custKeys.length} store(s): ${g.custKeys.slice(0, 10).join(', ')}${g.custKeys.length > 10 ? '…' : ''}`).join('\n') };
    if (p.lspComputed != null && p.lspPerCase !== p.lspComputed)
      return { text: `computed ${fmt(p.lspComputed)}`, warn: true, reset: !this.readonly(),
        tip: `Changed by the proponent. The picked stores' price is ${fmt(p.lspComputed)}.` + (this.readonly() ? '' : ' Click to use it.') };
    if (x && !x.hasPrice) return { text: 'no price found', warn: true, reset: false, tip: 'No list price for this product as of the start date.' };
    if (!this.customers().length && !this.readonly()) return { text: 'pick Cust Keys', warn: false, reset: false, tip: 'LSP is the picked stores\' price — pick the Cust Keys first.' };
    return null;
  }

  addProduct() { this.m.products.push(blankProduct()); }

  // ── Computation (mirrors the sheet; the server recomputes on save) ──
  totalCost()        { return this.m.costs.reduce((s, c) => s + (c.amount || 0), 0); }
  /** A product row counts once it has a SKU (blank rows are dropped on save, like the server does). */
  /**
   * Account Support & Convention program types (ACCOUNT SUPPORT- Anniversary / Opening / Sponsorship,
   * Convention): no Participating Products, no pre-promo / incremental sales, and Promoted Value is the
   * Annual Projected Sales. Same rule as TasafService.IsSupportProgram.
   */
  isSupport() {
    const n = (this.list('program-types').find(o => o.id === this.m.programTypeId)?.name ?? '').trim().toUpperCase();
    return n.startsWith('ACCOUNT SUPPORT') || n === 'CONVENTION';
  }
  // ── Cost Details descriptions per program type ─────────────
  /**
   * The cost-description group of the picked program type — cost types are named "<Group> - <Item>":
   * "ACCOUNT SUPPORT- Anniversary Support" → "Anniversary Support", "PROMO- …" → "Promotions",
   * "Convention" → "Convention", etc. Null = no program type (all descriptions).
   */
  private costGroup(): string | null {
    const n = (this.list('program-types').find(o => o.id === this.m.programTypeId)?.name ?? '').trim();
    if (!n) return null;
    if (/^PROMO-/i.test(n)) return 'Promotions';
    if (/^ACCOUNT SUPPORT-/i.test(n)) return n.replace(/^ACCOUNT SUPPORT-\s*/i, '');
    return n;
  }
  /**
   * Cost-description groups reserved for their own program type: Anniversary Support shows only
   * "Anniversary Support - …", Convention only "Convention - …"; every other program type shows all
   * descriptions EXCEPT these groups. Keeps the line's current pick so it never shows blank.
   */
  private static readonly RESERVED_COST_GROUPS = ['anniversary support', 'convention'];
  costTypeOptions(currentId: number | null): TasafOption[] {
    const all = this.list('cost-types');
    const groupOf = (c: TasafOption) => c.name.split(' - ')[0].trim().toLowerCase();
    const reserved = TasafPageComponent.RESERVED_COST_GROUPS;
    const g = this.costGroup()?.toLowerCase();
    let opts = g && reserved.includes(g)
      ? all.filter(c => groupOf(c) === g)
      : all.filter(c => !reserved.includes(groupOf(c)));
    if (!opts.length) opts = all;
    return currentId && !opts.some(c => c.id === currentId) ? [...opts, ...all.filter(c => c.id === currentId)] : opts;
  }
  /** Program type changed: clear cost descriptions that don't belong to its group. */
  onProgramType(id: number | null) {
    this.m.programTypeId = id;
    const allowed = new Set(this.costTypeOptions(null).map(c => c.id));
    let cleared = 0;
    for (const c of this.m.costs) if (c.costTypeId && !allowed.has(c.costTypeId)) { c.costTypeId = null; cleared++; }
    if (cleared) this.messageSvc.add({ severity: 'info', summary: 'Cost Details',
      detail: `${cleared} cost description${cleared === 1 ? '' : 's'} cleared — not used by this program type.` });
  }

  hasProducts()      { return !this.isSupport() && this.m.products.some(p => !!(p.cProdNo?.trim() || p.sku?.trim())); }
  promotedValue()    { return this.hasProducts() ? this.m.products.reduce((s, p) => s + (p.promotedVolCs || 0) * (p.lspPerCase || 0), 0) : (this.m.promotedValue ?? 0); }
  /** Σ P3Mo Avg (cs) × LSP — SPLY when a (seasonal) SKU has no P3M — else as entered. Same as the server. */
  prePromoSales()    { return this.hasProducts() ? this.m.products.reduce((s, p) => s + (p.p3MoAvgCs ?? p.splyCs ?? 0) * (p.lspPerCase || 0), 0) : (this.m.prePromoSales ?? 0); }
  incrementalSales() { return this.hasProducts() ? this.m.products.reduce((s, p) => s + (p.incrVolCs || 0) * (p.lspPerCase || 0), 0) : (this.m.incrementalSales ?? 0); }
  ratio(num: number | null, den: number | null) { return num != null && den ? num / den : null; }
  /** Approval matrix (approval type) of the selected program type. */
  approvalTypeName() {
    const typeId = this.list('program-types').find(o => o.id === this.m.programTypeId)?.parentId;
    return typeId ? this.list('approval-types').find(t => t.id === typeId)?.name ?? null : null;
  }
  /** Max CTS % of the selected program type (promo packs: 8), else null. */
  ctsLimit() { return this.list('program-types').find(o => o.id === this.m.programTypeId)?.amount ?? null; }
  ctsOver() {
    const lim = this.ctsLimit();
    const cts = this.ratio(this.totalCost(), this.promotedValue());
    return lim != null && cts != null && cts * 100 > lim;
  }
  /**
   * CTS value colour: green = within the program type's CTS limit, red = over it. Account Support &
   * Convention types have no limit, so their CTS (vs Annual Projected Sales) is just green at 8% or less.
   */
  ctsColor() {
    const cts = this.ratio(this.totalCost(), this.promotedValue());
    if (cts == null) return null;
    const green = 'var(--p-green-600, #16a34a)';
    if (this.ctsLimit() != null) return this.ctsOver() ? 'var(--p-red-600, #dc2626)' : green;
    return this.isSupport() && cts * 100 <= 8 ? green : null;
  }
  // ── Mode of Payment: ONE payment type per TASAF, carrying the whole Total Cost ──
  /** The picked type (its payXxxAp field); payload() puts the Total Cost there. */
  protected payType: PayKey | null = null;
  /** Saved amounts → the type with the largest amount (AP + legacy Salesman); null if none. */
  private toPayType(src: SaveTasafDto): PayKey | null {
    let best: PayKey | null = null, max = 0;
    for (const r of PAY_ROWS) {
      const amt = (src[r.ap] || 0) + (src[r.sm] || 0);
      if (amt > max) { max = amt; best = r.ap; }
    }
    return best;
  }
  /** The picked type gets the Total Cost; every other amount field is 0 (Salesman no longer used). */
  private payFields(): Pick<SaveTasafDto, PayKey> {
    const f = Object.fromEntries(PAY_ROWS.flatMap(r => [[r.ap, 0], [r.sm, 0]])) as Record<PayKey, number>;
    if (this.payType) f[this.payType] = Math.round(this.totalCost() * 100) / 100;
    return f;
  }

  sev(status: string) { return TASAF_STATUS_SEVERITY[status] ?? 'info'; }

  readonlyReason() {
    const t = this.current();
    switch (t?.status) {
      case 'For Approval': {
        const s = t.steps.find(x => x.seq === t.currentStepSeq);
        return `waiting for approval by ${s?.userName} (${s?.roleCode}).`;
      }
      case 'Approved':     return 'approved.';
      case 'Rejected':     return 'rejected.';
      case 'Cancelled':    return 'cancelled.';
      default:             return 'only the proponent can edit this TASAF.';
    }
  }

  // ── Workflow ───────────────────────────────────────────────
  /** Participating Products rows missing a required column (same rule as the server), e.g. "row 2: SPLY". */
  productRowsMissing(): string[] {
    const out: string[] = [];
    if (this.isSupport()) return out; // no products for Account Support & Convention
    this.m.products.forEach((p, i) => {
      if (!p.cProdNo?.trim() && !p.sku?.trim()) return; // blank row — dropped on save
      const miss = [
        !p.cProdNo?.trim() && 'ProdNo', !p.sku?.trim() && 'SKU',
        p.p3MoAvgCs == null && 'P3 Mo Avg',
        !(p.promotedVolCs > 0) && 'Promoted Vol', p.incrVolCs == null && 'Incr Vol',
        !(p.lspPerCase > 0) && 'LSP / Case',
      ].filter(Boolean);
      if (miss.length) out.push(`row ${i + 1}: ${miss.join(', ')}`);
    });
    return out;
  }

  confirmSubmit() {
    const rows = this.productRowsMissing();
    if (rows.length) {
      this.messageSvc.add({ severity: 'warn', summary: 'Participating Products', life: 8000,
        detail: `Complete every product column before submitting — ${rows.join('; ')}.` });
      return;
    }
    const mixed = this.isSupport() ? [] : this.mixedSkus();
    if (mixed.length) {
      this.messageSvc.add({ severity: 'warn', summary: 'LSP / Case', life: 8000,
        detail: `The picked Cust Keys have different prices for ${mixed.map(x => x.cProdNo).join(', ')} — keep only stores with the same price (see under Participating Products).` });
      return;
    }
    if (this.ctsOver() && !this.m.ctsJustification.trim()) {
      this.messageSvc.add({ severity: 'warn', summary: 'CTS', detail: `CTS is over the ${this.ctsLimit()}% limit — enter the CTS Justification (below Computation) to submit.` });
      return;
    }
    if (!this.payType) {
      this.messageSvc.add({ severity: 'warn', summary: 'Mode of Payment', detail: 'Select the payment type before submitting.' });
      return;
    }
    const resubmit = this.current()?.status === 'Returned';
    this.confirmSvc.confirm({
      header: resubmit ? 'Resubmit TASAF' : 'Submit TASAF',
      message: `${resubmit ? 'Resubmit' : 'Submit'} this TASAF for approval? You can no longer edit it unless it is returned to you.`,
      icon: 'pi pi-send',
      accept: () => {
        // Unsaved changes are saved first, then submitted.
        if (!this.current() || this.isDirty()) this.save(t => this.submit(t.id));
        else this.submit(this.current()!.id);
      }
    });
  }

  private submit(id: number) {
    this.submitting.set(true);
    this.apiError.set(null);
    this.api.submit(id).subscribe({
      next: res => {
        this.submitting.set(false);
        if (res.data) this.loadTasaf(res.data);
        this.messageSvc.add({ severity: 'success', summary: 'Submitted', detail: `${res.data?.refNo} sent to ${res.data?.steps.find(s => s.seq === res.data?.currentStepSeq)?.userName} (${res.data?.currentRoleCode}).` });
      },
      error: err => {
        this.submitting.set(false);
        const msg = err?.error?.message ?? 'Failed to submit.';
        this.apiError.set(msg);
        this.messageSvc.add({ severity: 'error', summary: 'Cannot submit', detail: msg, life: 8000 });
      }
    });
  }

  openDecision(d: TasafDialogAction) {
    this.decision.set(d);
    this.decisionComment = '';
    this.decisionVisible = true;
  }

  decide() {
    const t = this.current();
    const d = this.decision();
    if (!t) return;
    if (!this.decisionComment.trim()) { // always required, approve included
      this.messageSvc.add({ severity: 'warn', summary: 'Comment required', detail: d === 'approve' ? 'Please enter a comment.' : 'Please give a reason.' });
      return;
    }
    const comment = this.decisionComment.trim();
    const call = d === 'revert' ? this.api.revert(t.id, comment)
               : d === 'cancel' ? this.api.cancel(t.id, comment)
               : this.api.decide(t.id, d, comment);
    this.deciding.set(true);
    call.subscribe({
      next: res => {
        this.deciding.set(false);
        this.decisionVisible = false;
        if (res.data) this.loadTasaf(res.data);
        this.messageSvc.add({ severity: 'success', summary: 'Done', detail: `TASAF is now ${res.data?.status}.` });
      },
      error: err => {
        this.deciding.set(false);
        this.messageSvc.add({ severity: 'error', summary: 'Error', detail: err?.error?.message ?? 'Failed.' });
      }
    });
  }

  // ── Save / delete ──────────────────────────────────────────
  save(after?: (t: TasafDto) => void) {
    if (this.readonly()) return;
    if (!this.m.title.trim()) {
      this.apiError.set('Activity Title is required.');
      this.messageSvc.add({ severity: 'warn', summary: 'Validation', detail: 'Activity Title is required.' });
      return;
    }
    const t = this.current();
    const body = this.payload();
    this.saving.set(true);
    this.apiError.set(null);
    (t ? this.api.update(t.id, body) : this.api.create(body)).subscribe({
      next: res => {
        this.saving.set(false);
        if (!after) this.messageSvc.add({ severity: 'success', summary: 'Saved', detail: `TASAF saved (${res.data?.status}).` });
        if (!res.data) return;
        if (!t) {
          // The "New" tab gives way to this TASAF's own tab.
          this.tabBar.updateTabState(TAB, undefined);
          this.snapshot = JSON.stringify(body);
        }
        this.loadTasaf(res.data);
        after?.(res.data);
      },
      error: err => {
        this.saving.set(false);
        const msg = err?.error?.message ?? err?.error?.title ?? `Error ${err?.status ?? ''}: failed to save.`;
        this.apiError.set(msg);
        this.messageSvc.add({ severity: 'error', summary: 'Error', detail: msg });
      }
    });
  }

  private confirmDelete() {
    const t = this.current();
    if (!t) return;
    this.confirmSvc.confirm({
      message: `Delete TASAF <strong>${t.refNo ?? t.title}</strong>`
        + (t.status !== 'Draft' ? ` (<strong>${t.status}</strong>) and its approval history` : '')
        + '? This cannot be undone.',
      header: 'Confirm Delete',
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.deleting.set(true);
        this.api.delete(t.id).subscribe({
          next: () => {
            this.deleting.set(false);
            this.messageSvc.add({ severity: 'success', summary: 'Deleted', detail: 'TASAF deleted.' });
            this.tabBar.switchToDefaultTab(TAB);
            this.resetFormOnly();
          },
          error: err => {
            this.deleting.set(false);
            this.messageSvc.add({ severity: 'error', summary: 'Cannot delete', detail: err?.error?.message ?? 'Failed to delete.' });
          }
        });
      }
    });
  }
}
