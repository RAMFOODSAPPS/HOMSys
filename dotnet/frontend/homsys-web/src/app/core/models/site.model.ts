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
  cuwhsenos: string;
  pricesOffHon?: boolean;
  acceptsOffshoreOrders?: boolean;
  /** BMS sysparam.transdate (yyyy-MM-dd), pushed by the branch's BMS; read-only here. */
  bmsDate?: string | null;
  bmsDateUpdatedUtc?: string | null;
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
  cuwhsenos: string;
  pricesOffHon?: boolean;
  acceptsOffshoreOrders?: boolean;
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
  cuwhsenos: string;
  /** Not edited on this page — sent back unchanged so an edit never resets it. */
  pricesOffHon?: boolean;
  acceptsOffshoreOrders?: boolean;
  isActive: boolean;
}
