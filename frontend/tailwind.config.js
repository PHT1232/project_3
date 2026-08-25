/**
 * Design tokens for the Stationery Management System.
 *
 * Values are read off the approved wireframes in `docs/Wireframe/`. The Plan (§9.2) requires
 * these tokens to be fixed here and documented in `docs/GUI-Standards.md` — that document does
 * not exist yet, so this file is currently the only source of truth for the visual system.
 *
 * SHARED FILE: every page depends on these. Add tokens; avoid changing existing values.
 */
export default {
  content: ['./index.html', './src/**/*.{js,jsx}'],
  theme: {
    extend: {
      colors: {
        // Primary navy — sidebar logo, primary buttons, active nav text.
        brand: {
          50: '#eef2fb',
          100: '#dce4f6',
          200: '#b8c8ec',
          500: '#2f4fa8',
          600: '#24408f',
          700: '#1e3a8a',
          800: '#172f6d',
          900: '#132555',
        },
        // Neutral surfaces — page background, cards, borders, text.
        surface: {
          page: '#f1f5f9',
          card: '#ffffff',
          muted: '#f8fafc',
          border: '#e2e8f0',
        },
        ink: {
          DEFAULT: '#0f172a',
          muted: '#64748b',
          subtle: '#94a3b8',
        },
        // Status colours for stock badges.
        status: {
          ok: '#0f172a',
          warn: '#475569',
          danger: '#dc2626',
          dangerBg: '#fee2e2',
        },
      },
      borderRadius: {
        card: '10px',
      },
      fontFamily: {
        sans: [
          'ui-sans-serif',
          'system-ui',
          '-apple-system',
          'Segoe UI',
          'Roboto',
          'Helvetica Neue',
          'Arial',
          'sans-serif',
        ],
        mono: ['ui-monospace', 'SFMono-Regular', 'Menlo', 'Consolas', 'monospace'],
      },
    },
  },
  plugins: [],
}
