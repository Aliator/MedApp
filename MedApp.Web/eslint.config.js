import js from '@eslint/js';
import reactHooks from 'eslint-plugin-react-hooks';
import globals from 'globals';
import tseslint from 'typescript-eslint';

export default tseslint.config(
  {ignores: ['dist', 'dist-demo', 'coverage', 'src/routeTree.gen.ts', 'src/api/schema.d.ts', 'src/themes/*/medapp.js', 'src/themes/*/medapp.d.ts']},
  {
    files: ['**/*.{ts,tsx}'],
    extends: [js.configs.recommended, ...tseslint.configs.recommended, reactHooks.configs.flat.recommended],
    languageOptions: {ecmaVersion: 2022, globals: globals.browser},
  },
  {
    // Astryx's Selector opens its list over its field, measured from wherever the list was last scrolled, so it jumps
    // about between opens, and both it and MultiSelector reopen scrolled where they were left. AnchoredSelector and
    // AnchoredMultiSelector open in a fixed place: below their field, at the selected option.
    files: ['src/**/*.{ts,tsx}'],
    ignores: ['src/components/forms/AnchoredSelector.tsx'],
    rules: {
      'no-restricted-imports': [
        'error',
        {
          paths: [
            {
              name: '@astryxdesign/core/Selector',
              importNames: ['Selector'],
              message: 'Use AnchoredSelector (src/components/forms/AnchoredSelector), which opens its list in a fixed place.',
            },
            {
              name: '@astryxdesign/core/MultiSelector',
              importNames: ['MultiSelector'],
              message: 'Use AnchoredMultiSelector (src/components/forms/AnchoredSelector), which opens its list at the selected options.',
            },
          ],
        },
      ],
    },
  },
);
