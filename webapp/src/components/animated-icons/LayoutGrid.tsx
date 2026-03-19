'use client';

import type {Variants} from "framer-motion";
import {motion, useAnimation} from "framer-motion";
import type {HTMLAttributes} from "react";
import {forwardRef, useCallback, useImperativeHandle, useRef} from "react";
import clsx from "clsx";

export interface LayoutGridIconHandle {
    startAnimation: () => void;
    stopAnimation: () => void;
}

const RECT_VARIANTS: Variants = {
    normal: {opacity: 1, scale: 1},
    animate: (i: number) => ({
        opacity: [0, 1],
        scale: [0.5, 1],
        transition: {delay: i * 0.08, duration: 0.3, ease: "easeOut"},
    }),
};

const LayoutGrid = forwardRef<LayoutGridIconHandle, HTMLAttributes<HTMLDivElement> & { size?: number }>(
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
                    {[
                        {x: 3, y: 3, w: 7, h: 7, rx: 1},
                        {x: 14, y: 3, w: 7, h: 7, rx: 1},
                        {x: 14, y: 14, w: 7, h: 7, rx: 1},
                        {x: 3, y: 14, w: 7, h: 7, rx: 1},
                    ].map((r, index) => (
                        <motion.rect
                            key={index}
                            x={r.x}
                            y={r.y}
                            width={r.w}
                            height={r.h}
                            rx={r.rx}
                            animate={controls}
                            variants={RECT_VARIANTS}
                            custom={index}
                        />
                    ))}
                </svg>
            </div>
        );
    },
);

LayoutGrid.displayName = "LayoutGrid";
export {LayoutGrid};

