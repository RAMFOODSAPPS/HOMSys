/** Server DateTimes are UTC but arrive without a zone; mark them UTC so the date pipe shows local (Manila) time. */
export function utc(s: string | null | undefined): string | null {
  if (!s) return null;
  return /([zZ]|[+-]\d\d:\d\d)$/.test(s) ? s : `${s}Z`;
}
