import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { UserDto, CreateUserDto, UpdateUserDto, RoleDto } from '../models/user.model';
import { ApiResponse } from '../models/auth.model';

@Injectable({ providedIn: 'root' })
export class UserService {
  private readonly base = `${environment.apiUrl}/users`;
  private readonly rolesBase = `${environment.apiUrl}/roles`;

  constructor(private http: HttpClient) {}

  getAll() { return this.http.get<ApiResponse<UserDto[]>>(this.base); }
  getById(id: number) { return this.http.get<ApiResponse<UserDto>>(`${this.base}/${id}`); }
  create(dto: CreateUserDto) { return this.http.post<ApiResponse<UserDto>>(this.base, dto); }
  update(id: number, dto: UpdateUserDto) { return this.http.put<ApiResponse<UserDto>>(`${this.base}/${id}`, dto); }
  delete(id: number) { return this.http.delete<ApiResponse<null>>(`${this.base}/${id}`); }
  getRoles() { return this.http.get<ApiResponse<RoleDto[]>>(this.rolesBase); }
}
