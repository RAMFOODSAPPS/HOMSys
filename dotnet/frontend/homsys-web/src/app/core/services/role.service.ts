import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { RoleDto, CreateRoleDto, UpdateRoleDto } from '../models/user.model';
import { ApiResponse } from '../models/auth.model';

@Injectable({ providedIn: 'root' })
export class RoleService {
  private readonly base = `${environment.apiUrl}/roles`;

  constructor(private http: HttpClient) {}

  getAll()                          { return this.http.get<ApiResponse<RoleDto[]>>(this.base); }
  getById(id: number)               { return this.http.get<ApiResponse<RoleDto>>(`${this.base}/${id}`); }
  create(dto: CreateRoleDto)        { return this.http.post<ApiResponse<RoleDto>>(this.base, dto); }
  update(id: number, dto: UpdateRoleDto) { return this.http.put<ApiResponse<RoleDto>>(`${this.base}/${id}`, dto); }
  delete(id: number)                { return this.http.delete<ApiResponse<null>>(`${this.base}/${id}`); }
}
