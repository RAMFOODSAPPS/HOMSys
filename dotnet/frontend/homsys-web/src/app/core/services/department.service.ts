import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { DepartmentDto, CreateDepartmentDto, UpdateDepartmentDto } from '../models/department.model';
import { ApiResponse } from '../models/auth.model';

@Injectable({ providedIn: 'root' })
export class DepartmentService {
  private readonly base = `${environment.apiUrl}/departments`;

  constructor(private http: HttpClient) {}

  getAll() { return this.http.get<ApiResponse<DepartmentDto[]>>(this.base); }
  getById(id: number) { return this.http.get<ApiResponse<DepartmentDto>>(`${this.base}/${id}`); }
  create(dto: CreateDepartmentDto) { return this.http.post<ApiResponse<DepartmentDto>>(this.base, dto); }
  update(id: number, dto: UpdateDepartmentDto) { return this.http.put<ApiResponse<DepartmentDto>>(`${this.base}/${id}`, dto); }
  delete(id: number) { return this.http.delete<ApiResponse<null>>(`${this.base}/${id}`); }
}
