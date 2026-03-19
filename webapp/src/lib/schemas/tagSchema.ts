import {z} from "zod";

const required = (name: string, max: number) =>
    z.string().trim().min(1, {message: `${name} is required`}).max(max, {message: `${name} must be at most ${max} characters`});

export const tagSchema = z.object({
    name: required('Name', 50),
    slug: z.string().trim()
        .min(1, {message: 'Slug is required'})
        .max(50, {message: 'Slug must be at most 50 characters'})
        .regex(/^[a-z0-9-]+$/, {message: 'Slug can only contain lowercase letters, numbers, and hyphens'}),
    description: required('Description', 1000),
});

export const updateTagSchema = tagSchema.omit({slug: true});

export type TagSchema = z.infer<typeof tagSchema>;
export type UpdateTagSchema = z.infer<typeof updateTagSchema>;

