import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/auth.model';
import { FormLayout } from '../models/form-layout.model';

export interface FormLayoutDto { formKey: string; layout: FormLayout; updatedAt: string; updatedBy: string; }

/** Form Designer layouts. Read by everyone; save/reset are Admin-only on the server. */
@Injectable({ providedIn: 'root' })
export class FormLayoutService {
  private http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/form-layouts`;

  /** data = null when the form uses its built-in default layout. */
  get(formKey: string)                     { return this.http.get<ApiResponse<FormLayoutDto | null>>(`${this.base}/${formKey}`); }
  save(formKey: string, layout: FormLayout) { return this.http.put<ApiResponse<FormLayoutDto>>(`${this.base}/${formKey}`, { layout }); }
  reset(formKey: string)                   { return this.http.delete<ApiResponse<null>>(`${this.base}/${formKey}`); }
}
