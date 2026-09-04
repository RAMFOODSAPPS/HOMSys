import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { SiteTypeDto, CreateSiteTypeDto, UpdateSiteTypeDto } from '../models/site-type.model';
import { ApiResponse } from '../models/auth.model';

@Injectable({ providedIn: 'root' })
export class SiteTypeService {
  private readonly base = `${environment.apiUrl}/site-types`;

  constructor(private http: HttpClient) {}

  getAll()             { return this.http.get<ApiResponse<SiteTypeDto[]>>(this.base); }
  getById(id: number)  { return this.http.get<ApiResponse<SiteTypeDto>>(`${this.base}/${id}`); }
  create(dto: CreateSiteTypeDto) { return this.http.post<ApiResponse<SiteTypeDto>>(this.base, dto); }
  update(id: number, dto: UpdateSiteTypeDto) { return this.http.put<ApiResponse<SiteTypeDto>>(`${this.base}/${id}`, dto); }
  delete(id: number)   { return this.http.delete<ApiResponse<null>>(`${this.base}/${id}`); }
}
