'use client';

import {Button} from "@heroui/button";

export default function RegisterButton() {
    return (
        <Button
            href='/signup?callbackUrl=/'
            color='primary'
        >
            Register
        </Button>
    );
}