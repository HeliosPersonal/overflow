import { heroui, commonColors } from "@heroui/react";

// ─── QUICK SWAP ───────────────────────────────────────────────────────────────
// Change this one line to switch the whole app's brand color:
//   commonColors.blue | purple | green | pink | cyan | red | yellow | zinc
const brand = commonColors.purple;
// ─────────────────────────────────────────────────────────────────────────────

export default heroui({
    themes: {
        light: {
            colors: {
                // ── Page background ───────────────────────────────────────────
                // Used by <body> and HeroUI's bg-background utility
                background: "#FFFFFF",

                // ── Default text color ────────────────────────────────────────
                // text-foreground → main body text
                // text-foreground-500 → muted/secondary text (labels, captions)
                // text-foreground-400 → even more muted (placeholders)
                foreground: {
                    DEFAULT: "#11181C",  // main text
                    50:  "#fafafa",
                    100: "#f4f4f5",
                    200: "#e4e4e7",
                    300: "#d4d4d8",
                    400: "#a1a1aa",      // placeholder / very muted
                    500: "#71717a",      // secondary text, labels
                    600: "#52525b",
                    700: "#3f3f46",
                    800: "#27272a",
                    900: "#18181b",
                },

                // ── Brand / primary color ─────────────────────────────────────
                // Used by: buttons (color='primary'), links, active states,
                //          progress bars, pagination, avatars, icons (text-primary)
                primary: {
                    ...brand,
                    DEFAULT: brand[500],
                    foreground: "#ffffff", // text ON primary-colored buttons
                },

                // ── Secondary color ───────────────────────────────────────────
                // Currently unused in this app after color consolidation.
                // Can be used for badges, chips, or accent elements.
                secondary: {
                    DEFAULT: brand[300],
                    foreground: "#ffffff",
                },

                // ── Danger / destructive color ────────────────────────────────
                // Used by: Delete buttons (color='danger'), error text,
                //          form validation errors
                danger: {
                    DEFAULT: "#f31260",
                    foreground: "#ffffff",
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
                },

                // ── Success color ─────────────────────────────────────────────
                // Used by: accepted answer checkmark (text-success)
                success: {
                    DEFAULT: "#17c964",
                    foreground: "#000000",
                },

                // ── Warning color ─────────────────────────────────────────────
                // Not currently used, available for alerts/toasts
                warning: {
                    DEFAULT: "#f5a524",
                    foreground: "#000000",
                },

                // ── Content layers (card/panel backgrounds) ───────────────────
                // content1 → cards, modals (bg-content1)
                // content2 → nested content (bg-content2)
                // content3/4 → deeper nesting levels
                content1: { DEFAULT: "#ffffff", foreground: "#11181C" },
                content2: { DEFAULT: "#f4f4f5", foreground: "#27272a" },
                content3: { DEFAULT: "#e4e4e7", foreground: "#3f3f46" },
                content4: { DEFAULT: "#d4d4d8", foreground: "#52525b" },
            },
        },
        dark: {
            colors: {
                // ── Page background ───────────────────────────────────────────
                background: "#000000",

                // ── Default text color ────────────────────────────────────────
                foreground: {
                    DEFAULT: "#ECEDEE",
                    50:  "#18181b",
                    100: "#27272a",
                    200: "#3f3f46",
                    300: "#52525b",
                    400: "#71717a",      // placeholder / very muted
                    500: "#a1a1aa",      // secondary text, labels
                    600: "#d4d4d8",
                    700: "#e4e4e7",
                    800: "#f4f4f5",
                    900: "#fafafa",
                },

                // ── Brand / primary color ─────────────────────────────────────
                // Slightly lighter shade for dark mode readability
                primary: {
                    ...brand,
                    DEFAULT: brand[400],
                    foreground: "#ffffff",
                },

                // ── Secondary color ───────────────────────────────────────────
                secondary: {
                    DEFAULT: brand[300],
                    foreground: "#ffffff",
                },

                // ── Danger ────────────────────────────────────────────────────
                danger: {
                    DEFAULT: "#f31260",
                    foreground: "#ffffff",
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
                },

                // ── Success ───────────────────────────────────────────────────
                success: {
                    DEFAULT: "#17c964",
                    foreground: "#000000",
                },

                // ── Warning ───────────────────────────────────────────────────
                warning: {
                    DEFAULT: "#f5a524",
                    foreground: "#000000",
                },

                // ── Content layers ────────────────────────────────────────────
                content1: { DEFAULT: "#18181b", foreground: "#ECEDEE" },
                content2: { DEFAULT: "#27272a", foreground: "#d4d4d8" },
                content3: { DEFAULT: "#3f3f46", foreground: "#e4e4e7" },
                content4: { DEFAULT: "#52525b", foreground: "#f4f4f5" },
            },
        },
    },
});
