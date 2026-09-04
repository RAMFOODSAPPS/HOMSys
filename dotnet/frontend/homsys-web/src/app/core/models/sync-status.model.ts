export interface ReferenceSyncStatusDto {
  lastRunUtc: string | null;
  customers: number;
  products: number;
}

export interface PricingFileStatusDto {
  file: string;
  lastWriteUtc: string;
}

export interface PricingSyncStatusDto {
  lastRunUtc: string | null;
  addonBranches: string[];
  customerBranches: string[];
  files: PricingFileStatusDto[];
}

export interface SyncStatusDto {
  referenceData: ReferenceSyncStatusDto;
  pricingMasters: PricingSyncStatusDto;
}
