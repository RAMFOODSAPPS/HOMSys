import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/auth.model';
import { SaveTasafWorksheet, TasafWorksheet, TasafWorksheetListItem } from '../models/tasaf-worksheet.model';

/** TASAF worksheets (LF Breakdown / Near Ex) — api/tasaf-worksheets. */
@Injectable({ providedIn: 'root' })
export class TasafWorksheetService {
  private readonly base = `${environment.apiUrl}/tasaf-worksheets`;

  constructor(private http: HttpClient) {}

  getAll()            { return this.http.get<ApiResponse<TasafWorksheetListItem[]>>(this.base); }
  getById(id: number) { return this.http.get<ApiResponse<TasafWorksheet>>(`${this.base}/${id}`); }
  create(dto: SaveTasafWorksheet)             { return this.http.post<ApiResponse<TasafWorksheet>>(this.base, dto); }
  update(id: number, dto: SaveTasafWorksheet) { return this.http.put<ApiResponse<TasafWorksheet>>(`${this.base}/${id}`, dto); }
  delete(id: number)  { return this.http.delete<ApiResponse<null>>(`${this.base}/${id}`); }
  attach(id: number, tasafId: number) { return this.http.post<ApiResponse<TasafWorksheet>>(`${this.base}/${id}/attach`, { tasafId }); }
  detach(id: number)  { return this.http.post<ApiResponse<TasafWorksheet>>(`${this.base}/${id}/detach`, {}); }
}
