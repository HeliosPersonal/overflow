import { heroui, commonColors } from "@heroui/react";

// Pick any palette: commonColors.blue | purple | green | pink | cyan | red | yellow | zinc
const brand = commonColors.purple;

export default heroui({
    themes: {
        light: {
            colors: {
                primary: {
                    ...brand,
                    DEFAULT: brand[500],
                    foreground: "#ffffff",
                },
            },
        },
        dark: {
            colors: {
                primary: {
                    ...brand,
                    DEFAULT: brand[400],
                    foreground: "#ffffff",
                },
            },
        },
    },
});
