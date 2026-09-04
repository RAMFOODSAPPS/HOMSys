export interface SiteDto {
  id: number;
  name: string;
  code: string;
  companyId: number;
  companyName: string;
  siteTypeId?: number | null;
  siteTypeName?: string | null;
  address: string;
  phone: string;
  contactPerson: string;
  description: string;
  isActive: boolean;
  createdAt: string;
  createdBy: string;
  updatedAt?: string;
  updatedBy?: string;
}

export interface CreateSiteDto {
  name: string;
  code: string;
  companyId: number;
  siteTypeId?: number | null;
  address: string;
  phone: string;
  contactPerson: string;
  description: string;
}

export interface UpdateSiteDto {
  name: string;
  code: string;
  companyId: number;
  siteTypeId?: number | null;
  address: string;
  phone: string;
  contactPerson: string;
  description: string;
  isActive: boolean;
}
