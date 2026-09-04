import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import {
  SalesOrderDto,
  CreateSalesOrderDto,
  CustomerLookupDto,
  ProductLookupDto,
  CustomerSuggestionDto,
  ProductSuggestionDto,
  PoCheckDto,
  PriceQuoteDto,
  DocClassDto,
  CustomerIdentifierMapDto,
  ImportCheckRowDto,
  FileImportCheckResultDto,
  RowDuplicateCheckResultDto,
  PoImportCheckResultDto
} from '../models/sales-order.model';
import { ApiResponse } from '../models/auth.model';

@Injectable({ providedIn: 'root' })
export class SalesOrderService {
  private readonly base = `${environment.apiUrl}/salesorders`;

  constructor(private http: HttpClient) {}

  getAll()            { return this.http.get<ApiResponse<SalesOrderDto[]>>(this.base); }
  getById(id: number) { return this.http.get<ApiResponse<SalesOrderDto>>(`${this.base}/${id}`); }

  create(dto: CreateSalesOrderDto) {
    return this.http.post<ApiResponse<SalesOrderDto>>(this.base, dto);
  }

  update(soId: number, dto: CreateSalesOrderDto) {
    return this.http.put<ApiResponse<SalesOrderDto>>(`${this.base}/${soId}`, dto);
  }

  lookupCustomer(custKey: string) {
    return this.http.get<ApiResponse<CustomerLookupDto>>(
      `${this.base}/lookup/customer/${encodeURIComponent(custKey)}`);
  }

  lookupProduct(cProdNo: string) {
    return this.http.get<ApiResponse<ProductLookupDto>>(
      `${this.base}/lookup/product/${encodeURIComponent(cProdNo)}`);
  }

  searchCustomers(term: string) {
    return this.http.get<ApiResponse<CustomerSuggestionDto[]>>(
      `${this.base}/search/customer?term=${encodeURIComponent(term)}`);
  }

  searchProducts(term: string) {
    return this.http.get<ApiResponse<ProductSuggestionDto[]>>(
      `${this.base}/search/product?term=${encodeURIComponent(term)}`);
  }

  getDocClasses() {
    return this.http.get<ApiResponse<DocClassDto[]>>(`${this.base}/docclasses`);
  }

  /** Warning-only duplicate check — always resolves 200. */
  checkPo(poNum: string) {
    return this.http.get<ApiResponse<PoCheckDto>>(
      `${this.base}/check-po/${encodeURIComponent(poNum)}`);
  }

  /** Import file-hash check — HARD BLOCK when alreadyProcessed is true. */
  checkImportFile(fileHash: string) {
    return this.http.get<ApiResponse<FileImportCheckResultDto>>(
      `${this.base}/check-import-file/${encodeURIComponent(fileHash)}`);
  }

  /** Fallback Customer+PO duplicate check — warning only, never blocks. */
  checkImportDuplicates(rows: ImportCheckRowDto[]) {
    return this.http.post<ApiResponse<RowDuplicateCheckResultDto>>(
      `${this.base}/check-import-duplicates`, rows);
  }

  /** Early PO-Number-only import check, run right after Next — HARD BLOCK when any match is found. */
  checkImportPoNumbers(poNums: string[]) {
    return this.http.post<ApiResponse<PoImportCheckResultDto>>(
      `${this.base}/check-import-ponums`, poNums);
  }

  /** Display-only LP w/ VAT quote — never persisted onto the line. */
  getQuote(cProdNo: string, custKey: string) {
    const params = new URLSearchParams({ cProdNo });
    if (custKey) params.set('custKey', custKey);
    return this.http.get<ApiResponse<PriceQuoteDto>>(
      `${environment.apiUrl}/pricing/quote?${params.toString()}`);
  }

  /** Known Customer Identifier -> CustKey mappings, to pre-fill the mapping dialog. */
  getCustomerIdentifierMaps(identifiers: string[]) {
    const params = new URLSearchParams();
    identifiers.forEach(id => params.append('identifiers', id));
    return this.http.get<ApiResponse<CustomerIdentifierMapDto[]>>(
      `${this.base}/customer-identifier-map?${params.toString()}`);
  }

  /** Persists the encoder's Customer Identifier -> CustKey choices from the mapping dialog. */
  saveCustomerIdentifierMaps(mappings: { identifier: string; custKey: string }[]) {
    return this.http.post<ApiResponse<null>>(`${this.base}/customer-identifier-map`, mappings);
  }
}
