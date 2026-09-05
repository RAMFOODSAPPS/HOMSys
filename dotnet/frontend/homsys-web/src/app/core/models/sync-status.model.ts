export interface PricingTableStatusDto {
  table: string;
  lastUpdatedUtc: string;
}

export interface PricingSyncStatusDto {
  lastRunUtc: string | null;
  addonBranches: string[];
  customerBranches: string[];
  tables: PricingTableStatusDto[];
  canTriggerSync: boolean;
}

export interface SyncStatusDto {
  pricingMasters: PricingSyncStatusDto;
}
