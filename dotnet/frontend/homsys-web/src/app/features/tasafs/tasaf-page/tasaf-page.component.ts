import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
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
import {
  SaveTasafDto, TASAF_STATUS_SEVERITY, utc, parseIsoDate, programDuration, toIsoDate, TasafBranch, TasafDialogAction, TasafDto, TasafFormOptions, TasafCustomerRef, TasafOption, TasafProduct
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
    targetMarketId: null, targetMarketOther: '', channelId: null,
    productGroupId: null, productGroup2Id: null, categoryId: null, category2Id: null,
    background: '', concessions: '', mechanics: '',
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
            <strong class="ref-preview" pTooltip="YEAR - Sales Group - Proponent Initials - Proposal No. The proposal number is one continuous series across all TASAFs, assigned when you submit.">{{ refPreview() }}</strong>
          }
          @else { <strong class="ref-preview">assigned on submit</strong> }
          <span>Date</span><strong>{{ current() ? (utc(current()!.createdAt) | date:'dd MMM yyyy') : 'today' }}</strong>
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
      @for (it of shown(); track it.key) {
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
              <p-datepicker [(ngModel)]="startDate" dateFormat="M d, yy" placeholder="Select start date" [showIcon]="true"
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
              <p-multiSelect #branchMs [focusOnHover]="false" [(ngModel)]="branchSel" [options]="options()?.branches ?? []" optionLabel="name" dataKey="name"
                [filter]="true" display="chip" [maxSelectedLabels]="100000" placeholder="Select one or more branches" styleClass="w-full chips-wrap"
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
              <p-select [focusOnHover]="false" [(ngModel)]="m.programTypeId" [options]="list('program-types')" optionLabel="name" optionValue="id"
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
              <p-select [focusOnHover]="false" [(ngModel)]="m.channelId" [options]="list('channels')" optionLabel="name" optionValue="id"
                [showClear]="true" placeholder="Where will it be implemented?" styleClass="w-full" [disabled]="readonly()" />
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
              @if (!readonly()) { <p-button label="Add SKU" icon="pi pi-plus" size="small" [text]="true" (onClick)="addProduct()" /> }
            </div>
            @if (m.products.length) {
              <table class="lines" appResizableColumns="tasaf-products">
                <thead><tr>
                  <th style="width:9rem">ProdNo</th><th>SKU</th>
                  <th class="num">P3 Mo Avg (Cs)</th>
                  <th class="num" pTooltip="Sales of the SKU in the same period last year, for the planned promo duration">SPLY (Cs)</th>
                  <th class="num">Promoted Vol (Cs)</th><th class="num">Incr Vol (Cs)</th>
                  <th class="num">LSP / Case</th><th class="num">Promoted Value</th><th></th>
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
                      <td><p-inputNumber [(ngModel)]="p.lspPerCase" [minFractionDigits]="2" [maxFractionDigits]="2" [min]="0" inputStyleClass="numin" [disabled]="readonly()" /></td>
                      <td class="num">{{ (p.promotedVolCs || 0) * (p.lspPerCase || 0) | number:'1.2-2' }}</td>
                      <td>@if (!readonly()) { <p-button icon="pi pi-trash" [text]="true" severity="danger" size="small" (onClick)="m.products.splice(i, 1)" /> }</td>
                    </tr>
                  }
                </tbody>
              </table>
              <small class="muted">LSP / Case is prefilled from the product's current list price (ex-VAT) — adjust if needed.</small>
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
                      <p-select [focusOnHover]="false" [autofocusFilter]="false" [(ngModel)]="c.costTypeId" [options]="list('cost-types')" optionLabel="name" optionValue="id"
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
            <h3>{{ cap(it) }}</h3>
            <table class="lines calc">
              <colgroup><col /><col style="width: 15rem" /><col style="width: 10rem" /></colgroup>
              <thead><tr><th>Description</th><th class="num">Amount (₱)</th>
                <th class="num" pTooltip="Amount ÷ Pre-Promo Monthly Avg Sales" tooltipPosition="top">vs Pre-Promo <i class="pi pi-info-circle muted"></i></th></tr></thead>
              <tbody>
                <tr><td>TOTAL COST / BUDGET</td><td class="num">{{ totalCost() | number:'1.2-2' }}</td><td></td></tr>
                <tr>
                  <td>Promoted Value @if (m.products.length) { <span class="muted">(from products)</span> }</td>
                  <td class="num">
                    @if (m.products.length) { {{ promotedValue() | number:'1.2-2' }} }
                    @else { <p-inputNumber [(ngModel)]="m.promotedValue" placeholder="0.00" [minFractionDigits]="2" [maxFractionDigits]="2" [min]="0" inputStyleClass="numin" [disabled]="readonly()" /> }
                  </td>
                  <td class="num">{{ (ratio(promotedValue(), m.prePromoSales) | percent:'1.0-2') ?? '—' }}</td>
                </tr>
                <tr>
                  <td pTooltip="Can use the past 3 months average, but if the SKU is seasonal use SPLY (actual sales of last year, same promo duration)">
                    Pre-Promo Monthly Avg Sales (Pesos) <i class="pi pi-info-circle muted"></i>
                  </td>
                  <td class="num"><p-inputNumber [(ngModel)]="m.prePromoSales" placeholder="0.00" [minFractionDigits]="2" [maxFractionDigits]="2" [min]="0" inputStyleClass="numin" [disabled]="readonly()" /></td>
                  <td></td>
                </tr>
                <tr>
                  <td>Incremental Sales @if (m.products.length) { <span class="muted">(from products)</span> }</td>
                  <td class="num">
                    @if (m.products.length) { {{ incrementalSales() | number:'1.2-2' }} }
                    @else { <p-inputNumber [(ngModel)]="m.incrementalSales" placeholder="0.00" [minFractionDigits]="2" [maxFractionDigits]="2" inputStyleClass="numin" [disabled]="readonly()" /> }
                  </td>
                  <td class="num">{{ (ratio(incrementalSales(), m.prePromoSales) | percent:'1.0-2') ?? '—' }}</td>
                </tr>
                <tr class="hl">
                  <td>CTS (Promoted Value) @if (ctsLimit() != null) { <span class="muted">— must be within {{ ctsLimit() }}%</span> }</td>
                  <td class="num">{{ (ratio(totalCost(), promotedValue()) | percent:'1.2-2') ?? '—' }}</td><td></td>
                </tr>
              </tbody>
            </table>
            @if (ctsOver()) {
              <p-message severity="warn" styleClass="w-full mt-2"
                [text]="'CTS is over the ' + ctsLimit() + '% limit for this program type — it cannot be submitted until it is within the limit.'" />
            }
          }
          @case ('payment') {
            <h3>{{ cap(it) }}</h3>
            <table class="lines pay">
              <colgroup><col /><col style="width: 15rem" /><col style="width: 15rem" /><col style="width: 10rem" /></colgroup>
              <thead><tr><th>Payment Type</th><th class="num">AP / Collection</th><th class="num">Salesman</th><th class="num">Total</th></tr></thead>
              <tbody>
                @for (r of payRows; track r.label) {
                  <tr>
                    <td>{{ r.label }}</td>
                    <td><p-inputNumber [(ngModel)]="m[r.ap]" [minFractionDigits]="2" [maxFractionDigits]="2" [min]="0" inputStyleClass="numin" [disabled]="readonly()" /></td>
                    <td><p-inputNumber [(ngModel)]="m[r.sm]" [minFractionDigits]="2" [maxFractionDigits]="2" [min]="0" inputStyleClass="numin" [disabled]="readonly()" /></td>
                    <td class="num">{{ (m[r.ap] || 0) + (m[r.sm] || 0) | number:'1.2-2' }}</td>
                  </tr>
                }
              </tbody>
              <tfoot><tr>
                <td><strong>Total</strong></td>
                <td class="num"><strong>{{ payColumn('ap') | number:'1.2-2' }}</strong></td>
                <td class="num"><strong>{{ payColumn('sm') | number:'1.2-2' }}</strong></td>
                <td class="num"><strong>{{ payColumn('ap') + payColumn('sm') | number:'1.2-2' }}</strong></td>
              </tr></tfoot>
            </table>
            @if (payVariance() !== 0) {
              <p-message severity="warn" styleClass="w-full mt-2"
                [text]="'Mode of Payment total (' + (payColumn('ap') + payColumn('sm') | number:'1.2-2') + ') is '
                  + (payVariance() < 0 ? (-payVariance() | number:'1.2-2') + ' MORE than' : (payVariance() | number:'1.2-2') + ' LESS than')
                  + ' the Total Cost (' + (totalCost() | number:'1.2-2') + '). They must be equal before you can submit.'" />
            }
            <small class="muted">Indicate the amount per payment type and the person in charge of liquidating this activity.</small>
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
                @case ('Approved') { <span class="ok">Approved {{ utc(s.actedAt) | date:'dd MMM yyyy h:mm a' }}</span> }
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

        @if (current()?.actions?.length) {
          <h3 class="mt">History</h3>
          <table class="lines">
            <thead><tr><th>When</th><th>Action</th><th>By</th><th>Comment</th></tr></thead>
            <tbody>
              @for (a of current()!.actions; track $index) {
                <tr>
                  <td>{{ utc(a.at) | date:'dd MMM yyyy h:mm a' }}</td>
                  <td><p-tag [value]="a.action" [severity]="sev(a.toStatus)" /></td>
                  <td>{{ a.byName }} <span class="muted">({{ a.role }})</span></td>
                  <td>{{ a.comment || '—' }}</td>
                </tr>
              }
            </tbody>
          </table>
        }
      </section>
    </div>

    <p-dialog [(visible)]="decisionVisible" [modal]="true" [style]="{ width: '460px' }" [header]="decisionTitle()">
      @if (decisionHint()) { <p class="muted dlg-hint">{{ decisionHint() }}</p> }
      <div class="field full">
        <label>{{ decision() === 'approve' ? 'Comment' : 'Reason' }}
          @if (decision() !== 'approve') { <span class="req">*</span> } @else { <span class="opt">(optional)</span> }</label>
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
  protected readonly     = computed(() => !!this.current() && !this.current()!.canEdit);
  protected cannotDelete = computed(() => !this.current()?.canDelete);
  protected severity     = computed(() => this.sev(this.current()?.status ?? 'Draft'));
  protected submitting   = signal(false);
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
    this.syncToolbar();
  }

  private setModel(src: SaveTasafDto) {
    const b = blankModel();
    this.m = {
      ...b,
      ...Object.fromEntries(Object.keys(b).map(k => [k, (src as unknown as Record<string, unknown>)[k] ?? (b as unknown as Record<string, unknown>)[k]])),
      areaIds: [...(src.areaIds ?? [])],
      branches: [],  // held in branchSel
      customers: [], // held in customers()
      products: (src.products ?? []).map(p => ({ ...p })),
      costs: (src.costs ?? []).length ? src.costs.map(c => ({ ...c })) : b.costs,
    } as SaveTasafDto;
    this.branchSel = (src.branches ?? []).map(x => ({ ...x }));
    this.customers.set((src.customers ?? []).map(c => ({ custKey: c.custKey, cusName: c.cusName })));
    this.startDate = parseIsoDate(src.startDate);
    this.endDate = parseIsoDate(src.endDate);
    this.loadCustomerOptions(false);
    this.loadProductOptions();
    this.snapshot = JSON.stringify(this.payload());
  }

  private payload(): SaveTasafDto {
    return {
      ...this.m,
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
      ...(!this.readonly() ? { submit: {
        label: t?.status === 'Returned' ? 'Resubmit for approval' : 'Submit for approval',
        onClick: () => this.confirmSubmit(), loading: this.submitting, disabled: this.designing
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
      list:    { onClick: () => this.router.navigate(['/tasafs/list']) },
      print:   { onClick: () => window.print() },
      ...(this.auth.isAdmin() ? { design: { onClick: () => this.designing.set(true), disabled: this.designing } } : {}),
    });
  }

  /** Cancel: leave the designer, else discard changes and go back to a blank New TASAF. */
  private cancel() {
    if (this.designing()) { this.designing.set(false); return; }
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
   * RefNo preview before submit (GUIDE sheet): YEAR-SG-INITIALS-NN, e.g. 2026-RKA-ER-NN.
   * Only for the proponent's own unsubmitted TASAF; the server assigns the real NN.
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

  chainOptions(): TasafOption[] {
    const sg = this.m.salesGroupId;
    const all = this.list('chains');
    return sg ? all.filter(c => !c.parentId || c.parentId === sg) : all;
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
    this.loadCustomerOptions(true);
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
    if (!this.m.chainId && !this.m.areaIds.length) return;
    this.chainLoading.set(true);
    this.api.customerOptions(this.m.chainId, this.m.areaIds).subscribe({
      next: res => {
        if (req !== this.chainReq) return; // a newer chain / area was picked meanwhile
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

  /** "Puregold · South Luzon, Bicol" — what the Cust Key list is narrowed to. */
  scopeName() {
    const areas = this.list('areas').filter(a => this.m.areaIds.includes(a.id)).map(a => a.name).join(', ');
    return [this.chainName(), areas].filter(s => s).join(' · ');
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

  /** Picking a SKU fills SKU name and LSP / Case (current list price, ex-VAT — editable). */
  pickProduct(p: TasafProduct, cProdNo: string | null) {
    const o = this.productOptions().find(x => x.cProdNo === cProdNo);
    p.cProdNo = cProdNo ?? '';
    if (!o) return;
    p.sku = `${o.prodDesc} ${o.packSize}`.trim();
    if (o.lspPerCase != null) p.lspPerCase = o.lspPerCase;
  }

  addProduct() { this.m.products.push(blankProduct()); }

  // ── Computation (mirrors the sheet; the server recomputes on save) ──
  totalCost()        { return this.m.costs.reduce((s, c) => s + (c.amount || 0), 0); }
  promotedValue()    { return this.m.products.length ? this.m.products.reduce((s, p) => s + (p.promotedVolCs || 0) * (p.lspPerCase || 0), 0) : (this.m.promotedValue ?? 0); }
  incrementalSales() { return this.m.products.length ? this.m.products.reduce((s, p) => s + (p.incrVolCs || 0) * (p.lspPerCase || 0), 0) : (this.m.incrementalSales ?? 0); }
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
  payColumn(col: 'ap' | 'sm') { return PAY_ROWS.reduce((s, r) => s + (this.m[r[col]] || 0), 0); }
  payVariance() { return Math.round((this.totalCost() - this.payColumn('ap') - this.payColumn('sm')) * 100) / 100; }

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
  confirmSubmit() {
    if (this.ctsOver()) {
      this.messageSvc.add({ severity: 'warn', summary: 'CTS', detail: `CTS must be within ${this.ctsLimit()}% for this program type.` });
      return;
    }
    if (this.payVariance() !== 0) {
      this.messageSvc.add({ severity: 'warn', summary: 'Mode of Payment', detail: 'The payment total must equal the Total Cost before submitting.' });
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
    if (d !== 'approve' && !this.decisionComment.trim()) {
      this.messageSvc.add({ severity: 'warn', summary: 'Reason required', detail: 'Please give a reason.' });
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
