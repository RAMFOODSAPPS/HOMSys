import { FormFieldDef } from '../../core/models/form-layout.model';

export const TPAR_FORM_KEY = 'tpar';

/**
 * New TPAR form fields for the Form Designer, with the built-in default layout.
 * Required = checked on save (TparService.Clean / PickBranchAsync), so it can't be hidden.
 */
export const TPAR_FIELDS: FormFieldDef[] = [
  { key: 'tasaf',     label: 'TASAF',        kind: 'field', required: true, col: 1,  row: 1, w: 8,  h: 1 },
  { key: 'tparNo',    label: 'TPAR No.',     kind: 'field', required: true, col: 9,  row: 1, w: 4,  h: 1 },
  { key: 'branch',    label: 'Branch',       kind: 'field', required: true, hint: "One of the TASAF's branches (any branch when it covers ALL …)", col: 1, row: 2, w: 4, h: 1 },
  { key: 'amount',    label: 'Amount',       kind: 'field', required: true, col: 5,  row: 2, w: 4,  h: 1 },
  { key: 'balance',   label: 'Balance',      kind: 'field', hint: 'Approved amount, TPAR totals and the balance left on the TASAF', col: 1, row: 3, w: 12, h: 1 },
  { key: 'remarks',   label: 'Remarks',      kind: 'field', maxH: 4,        col: 1,  row: 4, w: 12, h: 1 },
  { key: 'createdBy', label: 'Created By',   kind: 'field',                 col: 1,  row: 5, w: 3,  h: 1 },
  { key: 'createdAt', label: 'Created Date', kind: 'field',                 col: 4,  row: 5, w: 3,  h: 1 },
  { key: 'updatedBy', label: 'Updated By',   kind: 'field', hint: 'Last change, submit, approve, revert or cancel', col: 7, row: 5, w: 3, h: 1 },
  { key: 'updatedAt', label: 'Updated Date', kind: 'field',                 col: 10, row: 5, w: 3,  h: 1 },
  { key: 'workflow',  label: 'Workflow',     kind: 'field', hint: 'Submitted / approved / cancelled by and when', col: 1, row: 6, w: 12, h: 1 },
  { key: 'help',      label: 'Save keeps it as Draft (it already holds its amount) → Submit → Approve. When the Approved TPARs reach the approved amount, the TASAF is Completed. Revert sends a Submitted / Approved TPAR back to Draft so it can be changed. Cancel frees the amount and the TPAR No.',
                                             kind: 'note',                  col: 1,  row: 7, w: 12, h: 1 },
];
