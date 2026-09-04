import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { SiteDto, CreateSiteDto, UpdateSiteDto } from '../models/site.model';
import { ApiResponse } from '../models/auth.model';

@Injectable({ providedIn: 'root' })
export class SiteService {
  private readonly base = `${environment.apiUrl}/sites`;

  constructor(private http: HttpClient) {}

  getAll()             { return this.http.get<ApiResponse<SiteDto[]>>(this.base); }
  getById(id: number)  { return this.http.get<ApiResponse<SiteDto>>(`${this.base}/${id}`); }
  create(dto: CreateSiteDto) { return this.http.post<ApiResponse<SiteDto>>(this.base, dto); }
  update(id: number, dto: UpdateSiteDto) { return this.http.put<ApiResponse<SiteDto>>(`${this.base}/${id}`, dto); }
  delete(id: number)   { return this.http.delete<ApiResponse<null>>(`${this.base}/${id}`); }
}
