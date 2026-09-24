/**
 * Maximum lengths for free-text inputs, matching the database column sizes (EF configurations in
 * ClaimsModule.Persistence) and the API's FluentValidation rules. Bound to each input's `maxlength`
 * so a value that can't be stored can't be typed or pasted in the first place.
 */
export const FIELD_LIMITS = {
  assignedHandler: 255,
  lossLocation: 500,
  lossDescription: 2000,
  partyName: 255,
  email: 255,
  phone: 50,
  riskObjectDescription: 1000,
  riskObjectIdentifier: 255,
  reason: 1000,
  /** Search boxes: not stored, but a policy number or client name never needs more. */
  search: 100
} as const;
