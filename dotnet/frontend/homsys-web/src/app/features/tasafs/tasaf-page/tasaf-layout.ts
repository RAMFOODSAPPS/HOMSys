import { FormFieldDef } from '../../../core/models/form-layout.model';

export const TASAF_FORM_KEY = 'tasaf';

/**
 * TASAF form fields for the Form Designer, with the built-in default layout
 * (the original two-column sheet order). Required = checked on save/submit
 * (TasafService.MissingForSubmit), so it can't be hidden.
 */
export const TASAF_FIELDS: FormFieldDef[] = [
  { key: 'title',             label: 'Activity Title',          kind: 'field', required: true, minW: 4, col: 1, row: 1,  w: 12, h: 1 },
  { key: 'start',             label: 'Start Date',              kind: 'field', required: true, minW: 3, col: 1, row: 2,  w: 4,  h: 1 },
  { key: 'end',               label: 'End Date',                kind: 'field', required: true, minW: 3, col: 5, row: 2,  w: 4,  h: 1 },
  { key: 'duration',          label: 'Program Duration',        kind: 'field', hint: 'Calculated from the start and end dates', minW: 3, col: 9, row: 2, w: 4, h: 1 },
  { key: 'salesGroup',        label: 'Sales Group',             kind: 'field', required: true,          col: 1, row: 3,  w: 6,  h: 1 },
  { key: 'area',              label: 'Area',                    kind: 'field', required: true,          col: 7, row: 3,  w: 6,  h: 1 },
  { key: 'chain',             label: 'Customer / Chain',        kind: 'field', required: true,          col: 1, row: 4,  w: 6,  h: 1 },
  { key: 'custKey',           label: 'Cust Key',                kind: 'field', hint: 'Customer master lookup (optional)', col: 7, row: 4, w: 6, h: 1 },
  { key: 'branches',          label: 'Branch(es)',              kind: 'field', required: true, minW: 4, col: 1, row: 5,  w: 12, h: 1 },
  { key: 'programType',       label: 'Program Type',            kind: 'field', required: true,          col: 1, row: 6,  w: 6,  h: 1 },
  { key: 'programTypeOther',  label: 'Others, indicate',        kind: 'field', required: true, hint: 'Program Type "Others" text — otherwise shows the approval matrix', col: 7, row: 6, w: 6, h: 1 },
  { key: 'objective',         label: 'Objective',               kind: 'field', required: true,          col: 1, row: 7,  w: 6,  h: 1 },
  { key: 'objectiveOther',    label: 'Others, indicate',        kind: 'field', required: true, hint: 'Shown when Objective is "Others"', col: 7, row: 7, w: 6, h: 1 },
  { key: 'targetMarket',      label: 'Target Market',           kind: 'field', required: true,          col: 1, row: 8,  w: 6,  h: 1 },
  { key: 'targetMarketOther', label: 'Others, indicate',        kind: 'field', required: true, hint: 'Shown when Target Market is "Others"', col: 7, row: 8, w: 6, h: 1 },
  { key: 'channel',           label: 'Channel',                 kind: 'field', required: true,          col: 1, row: 9,  w: 6,  h: 1 },
  { key: 'productGroup',      label: 'Product Group',           kind: 'field', required: true,          col: 1, row: 10, w: 6,  h: 1 },
  { key: 'productGroup2',     label: 'Secondary Product Group', kind: 'field',                          col: 7, row: 10, w: 6,  h: 1 },
  { key: 'category',          label: 'Category',                kind: 'field', required: true,          col: 1, row: 11, w: 6,  h: 1 },
  { key: 'category2',         label: 'Secondary Category',      kind: 'field',                          col: 7, row: 11, w: 6,  h: 1 },
  { key: 'categoryNote',      label: 'Product categories not in the list may not be promoted — discuss with GKAM and TMM.',
                                                                kind: 'note',                           col: 1, row: 12, w: 12, h: 1 },
  { key: 'background',        label: 'Background',              kind: 'field', required: true, minW: 4, maxH: 6, col: 1, row: 13, w: 12, h: 2 },
  { key: 'concessions',       label: 'Concessions',             kind: 'field', minW: 4, maxH: 6,          col: 1, row: 15, w: 12, h: 2 },
  { key: 'mechanics',         label: 'Program Mechanics',       kind: 'field', required: true, minW: 4, maxH: 6, col: 1, row: 17, w: 12, h: 2 },
  { key: 'products',          label: 'Participating Products',  kind: 'block', minW: 8, minH: 2,          col: 1, row: 19, w: 12, h: 2 },
  { key: 'costs',             label: 'Cost Details',            kind: 'block', required: true, minW: 6, minH: 2, col: 1, row: 21, w: 12, h: 2 },
  { key: 'computation',       label: 'Computation',             kind: 'block', required: true, minW: 5, minH: 2, col: 1, row: 23, w: 12, h: 2 },
  { key: 'payment',           label: 'Mode of Payment',         kind: 'block', required: true, minW: 5, minH: 2, col: 1, row: 25, w: 12, h: 2 },
];
