'use client';

import {Button} from "@heroui/button";

export default function LoginButton() {
    return (
        <Button
            as="a"
            href='/login?callbackUrl=/'
            color='primary'
            variant='bordered'
        >
            Login
        </Button>
    );
}