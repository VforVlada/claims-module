// @ts-check
const eslint = require('@eslint/js');
const tseslint = require('typescript-eslint');
const angular = require('angular-eslint');

/**
 * UI-02: components, guards, interceptors and specs must go through the typed API services in
 * core/services — only *.service.ts files may inject HttpClient directly.
 */
const httpClientRestriction = {
  paths: [
    {
      name: '@angular/common/http',
      importNames: ['HttpClient'],
      message: 'Inject HttpClient only in *.service.ts files (UI-02). Use the typed API services in core/services instead.'
    }
  ]
};

module.exports = tseslint.config(
  {
    ignores: ['dist/**', '.angular/**', 'coverage/**', 'e2e/test-results/**', 'e2e/playwright-report/**']
  },
  {
    files: ['**/*.ts'],
    extends: [eslint.configs.recommended, ...tseslint.configs.recommended, ...tseslint.configs.stylistic, ...angular.configs.tsRecommended],
    processor: angular.processInlineTemplates,
    rules: {
      '@angular-eslint/directive-selector': ['error', { type: 'attribute', prefix: 'app', style: 'camelCase' }],
      '@angular-eslint/component-selector': ['error', { type: 'element', prefix: 'app', style: 'kebab-case' }],
      'no-restricted-imports': ['error', httpClientRestriction]
    }
  },
  {
    files: ['**/*.service.ts'],
    rules: {
      'no-restricted-imports': 'off'
    }
  },
  {
    files: ['**/*.html'],
    extends: [...angular.configs.templateRecommended, ...angular.configs.templateAccessibility],
    rules: {}
  }
);
