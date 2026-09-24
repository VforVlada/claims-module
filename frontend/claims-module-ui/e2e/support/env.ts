/** Where the stack under test lives. Override via environment variables in CI. */
export const BASE_URL = process.env['E2E_BASE_URL'] ?? 'http://localhost:4200';
export const API_URL = process.env['E2E_API_URL'] ?? 'http://localhost:5299';
