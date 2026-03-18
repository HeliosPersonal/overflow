'use client';

import type {Variants} from "framer-motion";
import {motion, useAnimation} from "framer-motion";
import type {HTMLAttributes} from "react";
import {forwardRef, useCallback, useImperativeHandle, useRef} from "react";
import clsx from "clsx";

export interface LayersIconHandle {
    startAnimation: () => void;
    stopAnimation: () => void;
}

const LAYER_VARIANTS: Variants = {
    normal: {y: 0, opacity: 1},
    animate: (i: number) => ({
        y: [0, -3 * (3 - i), 0],
        opacity: [1, 0.6, 1],
        transition: {delay: i * 0.1, duration: 0.45, ease: "easeInOut"},
    }),
};

const Layers = forwardRef<LayersIconHandle, HTMLAttributes<HTMLDivElement> & { size?: number }>(
    ({onMouseEnter, onMouseLeave, className, size = 24, ...props}, ref) => {
        const controls = useAnimation();
        const isControlledRef = useRef(false);

        useImperativeHandle(ref, () => {
            isControlledRef.current = true;
            return {
                startAnimation: () => controls.start("animate"),
                stopAnimation: () => controls.start("normal"),
            };
        });

        const handleMouseEnter = useCallback(
            (e: React.MouseEvent<HTMLDivElement>) => {
                if (!isControlledRef.current) controls.start("animate");
                onMouseEnter?.(e);
            },
            [controls, onMouseEnter],
        );

        const handleMouseLeave = useCallback(
            (e: React.MouseEvent<HTMLDivElement>) => {
                if (!isControlledRef.current) controls.start("normal");
                onMouseLeave?.(e);
            },
            [controls, onMouseLeave],
        );

        return (
            <div
                className={clsx("inline-flex items-center justify-center", className)}
                onMouseEnter={handleMouseEnter}
                onMouseLeave={handleMouseLeave}
                {...props}
            >
                <svg
                    xmlns="http://www.w3.org/2000/svg"
                    width={size}
                    height={size}
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                >
                    {/* Bottom layer lifts most, top layer lifts least — "fanning" effect */}
                    <motion.path
                        d="m12.83 2.18a2 2 0 0 0-1.66 0L2.6 6.08a1 1 0 0 0 0 1.83l8.58 3.91a2 2 0 0 0 1.66 0l8.58-3.9a1 1 0 0 0 0-1.83Z"
                        animate={controls}
                        variants={LAYER_VARIANTS}
                        custom={0}
                    />
                    <motion.path
                        d="m22 17.65-9.17 4.16a2 2 0 0 1-1.66 0L2 17.65"
                        animate={controls}
                        variants={LAYER_VARIANTS}
                        custom={2}
                    />
                    <motion.path
                        d="m22 12.65-9.17 4.16a2 2 0 0 1-1.66 0L2 12.65"
                        animate={controls}
                        variants={LAYER_VARIANTS}
                        custom={1}
                    />
                </svg>
            </div>
        );
    },
);

Layers.displayName = "Layers";
export {Layers};

