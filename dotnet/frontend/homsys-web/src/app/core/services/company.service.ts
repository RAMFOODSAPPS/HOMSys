import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { CompanyDto, CreateCompanyDto, UpdateCompanyDto } from '../models/company.model';
import { ApiResponse } from '../models/auth.model';

@Injectable({ providedIn: 'root' })
export class CompanyService {
  private readonly base = `${environment.apiUrl}/companies`;

  constructor(private http: HttpClient) {}

  getAll() { return this.http.get<ApiResponse<CompanyDto[]>>(this.base); }
  getById(id: number) { return this.http.get<ApiResponse<CompanyDto>>(`${this.base}/${id}`); }
  create(dto: CreateCompanyDto) { return this.http.post<ApiResponse<CompanyDto>>(this.base, dto); }
  update(id: number, dto: UpdateCompanyDto) { return this.http.put<ApiResponse<CompanyDto>>(`${this.base}/${id}`, dto); }
  delete(id: number) { return this.http.delete<ApiResponse<null>>(`${this.base}/${id}`); }

  /** Logo (Azure Blob Storage via the API). The image is resized in the browser before upload. */
  uploadLogo(id: number, file: Blob, fileName: string) {
    const body = new FormData();
    body.append('file', file, fileName);
    return this.http.post<ApiResponse<CompanyDto>>(`${this.base}/${id}/logo`, body);
  }
  removeLogo(id: number) { return this.http.delete<ApiResponse<CompanyDto>>(`${this.base}/${id}/logo`); }
  logoUrl(c: CompanyDto) { return `${this.base}/${c.id}/logo?v=${c.logoVersion ?? 0}`; }
}
