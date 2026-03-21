'use client';

import {Button} from "@heroui/button";
import Link from "next/link";

export default function RegisterButton() {
    return (
        <Button
            as={Link}
            href='/signup?callbackUrl=/'
            color='primary'
        >
            Register
        </Button>
    );
}