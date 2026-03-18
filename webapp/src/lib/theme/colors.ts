/**
 * ─── Overflow Color Palette ──────────────────────────────────────────────────
 *
 * Single source of truth for every color used in the app.
 * Edit palettes here → hero.ts (Tailwind/HeroUI plugin) and any JS code
 * that needs raw hex values all pull from this file.
 *
 * Each semantic palette follows HeroUI's ColorScale shape:
 *   50–900 shades  +  DEFAULT  +  foreground (contrast text)
 *
 * @see https://www.heroui.com/docs/customization/colors
 * ─────────────────────────────────────────────────────────────────────────────
 */

// ── Brand (Primary) ─────────────────────────────────────────────────────────
// Purple accent used for primary actions, focus rings, and active states.
export const brand = {
  50:  "#f3eff8",
  100: "#e4daf0",
  200: "#c9b5e1",
  300: "#a98bcb",
  400: "#8a64b3", // dark-mode primary DEFAULT
  500: "#6b4899", // light-mode primary DEFAULT
  600: "#543a7a",
  700: "#3d2b5c",
  800: "#271b3d",
  900: "#130d1f",
} as const;

// ── Secondary ───────────────────────────────────────────────────────────────
export const secondary = {
  50:  "#f5f0ff",
  100: "#ece0ff",
  200: "#d4bbf7",
  300: "#b894e8",
  400: "#9573da",
  500: "#7c5fc2",
  600: "#634ca0",
  700: "#4a3978",
  800: "#322650",
  900: "#191328",
  DEFAULT: "#9573da",
  foreground: "#ffffff",
} as const;

// ── Danger ──────────────────────────────────────────────────────────────────
export const danger = {
  50:  "#fee7ef",
  100: "#fdd0df",
  200: "#faa0bf",
  300: "#f871a0",
  400: "#f54180",
  500: "#f31260",
  600: "#c20e4d",
  700: "#920b3a",
  800: "#610726",
  900: "#310413",
  DEFAULT: "#f31260",
  foreground: "#ffffff",
} as const;

// ── Success ─────────────────────────────────────────────────────────────────
export const success = {
  50:  "#e8faf0",
  100: "#d1f4e0",
  200: "#a2e9c1",
  300: "#74dfa2",
  400: "#45d483",
  500: "#17c964",
  600: "#12a150",
  700: "#0e793c",
  800: "#095028",
  900: "#052814",
  DEFAULT: "#17c964",
  foreground: "#000000",
} as const;

// ── Warning ─────────────────────────────────────────────────────────────────
export const warning = {
  50:  "#fefce8",
  100: "#fdedd3",
  200: "#fbdba7",
  300: "#f9c97c",
  400: "#f7b750",
  500: "#f5a524",
  600: "#c4841d",
  700: "#936316",
  800: "#62420e",
  900: "#312107",
  DEFAULT: "#f5a524",
  foreground: "#000000",
} as const;

// ── Zinc (neutral base for default + foreground) ────────────────────────────
// Shared palette — light and dark themes pick from opposite ends.
export const zinc = {
  50:  "#fafafa",
  100: "#f4f4f5",
  200: "#e4e4e7",
  300: "#d4d4d8",
  400: "#a1a1aa",
  500: "#71717a",
  600: "#52525b",
  700: "#3f3f46",
  800: "#27272a",
  900: "#18181b",
} as const;

// ── Theme-specific compositions ─────────────────────────────────────────────
// These combine the raw palettes above into the shapes hero.ts needs.

export const lightTheme = {
  background: "#FFFFFF",
  foreground: {
    DEFAULT: "#11181C",
    ...zinc, // 50=lightest … 900=darkest
  },
  primary: {
    ...brand,
    DEFAULT: brand[500],
    foreground: "#ffffff",
  },
  secondary,
  danger,
  success,
  warning,
  default: {
    ...zinc,
    DEFAULT: zinc[300],
    foreground: "#11181C",
  },
  divider: "rgba(0,0,0,0.12)" as string,
  focus: brand[500],
  overlay: "#000000",
  content1: { DEFAULT: "#ffffff",  foreground: "#11181C" },
  content2: { DEFAULT: zinc[100],  foreground: zinc[800] },
  content3: { DEFAULT: zinc[200],  foreground: zinc[700] },
  content4: { DEFAULT: zinc[300],  foreground: zinc[600] },
} as const;

export const darkTheme = {
  background: "#000000",
  foreground: {
    DEFAULT: "#ECEDEE",
    // Inverted: 50=darkest … 900=lightest
    50:  zinc[900],
    100: zinc[800],
    200: zinc[700],
    300: zinc[600],
    400: zinc[500],
    500: zinc[400],
    600: zinc[300],
    700: zinc[200],
    800: zinc[100],
    900: zinc[50],
  },
  primary: {
    ...brand,
    DEFAULT: brand[400],
    foreground: "#ffffff",
  },
  secondary,
  danger,
  success,
  warning,
  default: {
    // Inverted
    DEFAULT: zinc[700],
    foreground: "#ECEDEE",
    50:  zinc[900],
    100: zinc[800],
    200: zinc[700],
    300: zinc[600],
    400: zinc[500],
    500: zinc[400],
    600: zinc[300],
    700: zinc[200],
    800: zinc[100],
    900: zinc[50],
  },
  divider: "rgba(255,255,255,0.12)" as string,
  focus: brand[400],
  overlay: "#000000",
  content1: { DEFAULT: zinc[900], foreground: "#ECEDEE" },
  content2: { DEFAULT: zinc[800], foreground: zinc[300] },
  content3: { DEFAULT: zinc[700], foreground: zinc[200] },
  content4: { DEFAULT: zinc[600], foreground: zinc[100] },
} as const;

// ── Confetti / decorative palette ───────────────────────────────────────────
// Pre-built array of brand-matched hex values for confetti, charts, etc.
export const celebrationColors = [
  secondary.DEFAULT,  // purple
  warning[500],       // gold
  warning[400],       // lighter gold
  success[500],       // green
  success[400],       // lighter green
  danger[500],        // pink-red
  danger[400],        // lighter pink
] as const;

