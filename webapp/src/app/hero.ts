import {heroui} from "@heroui/react";
import {lightTheme, darkTheme} from "../lib/theme/colors";

// ── All colours live in src/lib/theme/colors.ts ──────────────────────────────
// Edit that single file to change brand, semantic, surface, or foreground colors.
// This plugin just wires them into HeroUI / Tailwind.

export default heroui({
    themes: {
        light: { colors: lightTheme },
        dark:  { colors: darkTheme },
    },
});
