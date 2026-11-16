/**
 * The warning the retention review and the anonymise dialog give, until retention classes exist: MedApp applies only
 * the adult and children periods, so a record in a longer class could show as due years early.
 */
export const SPECIALTY_RETENTION_WARNING =
  'MedApp applies only the adult (8 years) and children periods. Mental health, maternity and oncology records must be ' +
  "kept longer (20 to 30 years), and MedApp doesn't know which records those are yet: keep such a record (Keep, with " +
  'its class as the reason) rather than anonymising it. Until then, set Retention:AdultYears to the longest class your ' +
  'organisation uses.';
