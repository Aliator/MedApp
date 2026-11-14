import js from '@eslint/js';
import reactHooks from 'eslint-plugin-react-hooks';
import globals from 'globals';
import tseslint from 'typescript-eslint';

export default tseslint.config(
  {ignores: ['dist', 'coverage', 'src/routeTree.gen.ts', 'src/api/schema.d.ts', 'src/themes/*/medapp.js', 'src/themes/*/medapp.d.ts']},
  {
    files: ['**/*.{ts,tsx}'],
    extends: [js.configs.recommended, ...tseslint.configs.recommended, reactHooks.configs.flat.recommended],
    languageOptions: {ecmaVersion: 2022, globals: globals.browser},
  },
);
