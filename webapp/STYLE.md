# Overflow — Frontend Style Guide

> **Philosophy:** Good UI comes from *depth*, not decoration. Use layered surfaces and paired
> shadows to make interfaces feel three-dimensional. Everything else (colour, type, spacing) follows
> from that single idea.

---

## Table of Contents

0. [Colour Palette](#0-colour-palette)
1. [Surface & Elevation System](#1-surface--elevation-system)
2. [Shadow System](#2-shadow-system)
3. [Typography](#3-typography)
4. [Spacing & Layout](#4-spacing--layout)
5. [Border Policy](#5-border-policy)
6. [Interactive States](#6-interactive-states)
7. [Component Patterns](#7-component-patterns)
8. [Theme Parity (Light / Dark)](#8-theme-parity-light--dark)
9. [globals.css Utilities to Add](#9-globalscss-utilities-to-add)
10. [Pre-Ship Checklist](#10-pre-ship-checklist)

---

## 0. Colour Palette

> **Source of truth:** `src/lib/theme/colors.ts`. Do not hardcode hex values anywhere else.  
> `src/app/hero.ts` imports from this file and wires colors into HeroUI / Tailwind.  
> Use HeroUI token classes (`bg-primary`, `text-danger`, `bg-content2`, etc.) so both themes work automatically.

---

### 0.1 Brand (Primary)

Purple — the single accent colour used for primary actions, focus rings, and active states.

> 🎨 **Visual reference:** open [`STYLE-colors.html`](./STYLE-colors.html) in a browser for live colour swatches.

| Step | Hex | Tailwind class | Notes |
|------|-----|---------------|-------|
| 50 | `#f3eff8` | `bg-primary-50` | Tinted backgrounds, hover fills |
| 100 | `#e4daf0` | `bg-primary-100` | |
| 200 | `#c9b5e1` | `bg-primary-200` | |
| 300 | `#a98bcb` | `bg-primary-300` | `secondary` DEFAULT |
| **400** | **`#8a64b3`** | `bg-primary-400` | **Dark-mode primary DEFAULT** |
| **500** | **`#6b4899`** | `bg-primary-500` | **Light-mode primary DEFAULT** |
| 600 | `#543a7a` | `bg-primary-600` | |
| 700 | `#3d2b5c` | `bg-primary-700` | |
| 800 | `#271b3d` | `bg-primary-800` | |
| 900 | `#130d1f` | `bg-primary-900` | Darkest tint |

**Always use `bg-primary` / `text-primary`** — not a numbered step — unless you explicitly need a
tint (e.g. `bg-primary/10` for a subtle highlight background).

---

### 0.2 Surfaces (Elevation Palette)

These are the backgrounds that create the depth system (detailed in §1).

#### Dark theme

| Token | Hex | Foreground |
|-------|-----|-----------|
| `background` | `#000000` | — |
| `content1` | `#18181b` | `#ECEDEE` |
| `content2` | `#27272a` | `#d4d4d8` |
| `content3` | `#3f3f46` | `#e4e4e7` |
| `content4` | `#52525b` | `#f4f4f5` |

#### Light theme

| Token | Hex | Foreground |
|-------|-----|-----------|
| `background` | `#FFFFFF` | — |
| `content1` | `#ffffff` | `#11181C` |
| `content2` | `#f4f4f5` | `#27272a` |
| `content3` | `#e4e4e7` | `#3f3f46` |
| `content4` | `#d4d4d8` | `#52525b` |

---

### 0.3 Foreground (Text) Scale

The scale goes light → dark in dark mode, dark → light in light mode. Both map to the same token
names — the theme resolves automatically.

| Token | Dark hex | Light hex | Typical use |
|-------|---------|-----------|-------------|
| `foreground-300` | `#52525b` | `#d4d4d8` | Disabled text |
| `foreground-400` | `#71717a` | `#a1a1aa` | Muted / timestamps / meta |
| `foreground-500` | `#a1a1aa` | `#71717a` | Secondary / descriptions |
| `foreground-600` | `#d4d4d8` | `#52525b` | Body copy, headings (global default) |
| `foreground-700` | `#e4e4e7` | `#3f3f46` | Hover text |
| `foreground-800` | `#f4f4f5` | `#27272a` | Primary labels, active text |
| `foreground-900` | `#fafafa` | `#18181b` | Maximum contrast (use sparingly) |

---

### 0.4 Semantic Colours

Used **only** for status / feedback. Never repurpose these for decorative use.

| Colour | DEFAULT | Tailwind class | When to use |
|--------|---------|---------------|-------------|
| **Danger** | `#f31260` | `text-danger`, `bg-danger` | Errors, destructive actions, delete buttons |
| **Success** | `#17c964` | `text-success`, `bg-success` | Confirmations, positive states, online indicators |
| **Warning** | `#f5a524` | `text-warning`, `bg-warning` | Caution states, guest badges, archived rooms |
| **Secondary** | `#a98bcb` (brand-300) | `text-secondary`, `bg-secondary` | Supporting accent, rarely needed |

For subtle tinted backgrounds use the `/10`–`/20` opacity modifier:
```tsx
<div className="bg-danger/10 text-danger">  {/* soft error banner */}
<div className="bg-success/10 text-success"> {/* soft success badge */}
```

---

### 0.5 Utility Colours

| Token | Value | Use |
|-------|-------|-----|
| `divider` | `rgba(0,0,0,0.12)` / `rgba(255,255,255,0.12)` | `border-divider` for subtle HR-style lines |
| `focus` | `brand[500]` / `brand[400]` | Applied automatically by HeroUI focus-visible — use `ring-primary` in custom components |
| `overlay` | `#000000` | Modal backdrops via HeroUI — do not override |

---

## 1. Surface & Elevation System

### 1.1 The Core Principle

> **The background colour itself is the depth signal — not borders.**

A lighter surface feels closer to the user. A darker surface feels further away (or sunken).
This is the entire system. Borders are redundant whenever two adjacent surfaces already differ
in background colour — see §5.

The transcript puts it plainly:
> *"We are increasing the lightness value by 0.1 and then add the lighter color on top to create
> a sense of depth."*
> *"Right now everything is kind of blending in together. There is no hierarchy. Let's choose a
> darker shade for the background and leave these four main elements as it is. And immediately
> these four elements start popping off the page like they want your attention."*

### 1.2 The Four-Layer Stack

| Level | Token | Dark hex | Light hex | Rule |
|-------|-------|----------|-----------|------|
| 0 — Page canvas | `bg-background` | `#000000` | `#FFFFFF` | The floor. Everything sits on top of this. |
| 1 — Chrome | `bg-content1` | `#18181b` | `#ffffff` | Persistent UI shell: TopNav, sidebars, sticky headers, pagination strip |
| 2 — Card | `bg-content2` | `#27272a` | `#f4f4f5` | Content cards, panels, section blocks — pop off the canvas |
| 3 — Interactive | `bg-content3` | `#3f3f46` | `#e4e4e7` | Active nav item, selected option, stats sidebar, tag chips |
| 4 — Active focus | `bg-content4` | `#52525b` | `#d4d4d8` | Pressed state, deepest highlight |

**Never place two adjacent surfaces on the same level.** If they look identical, one needs to move up or down.

### 1.3 Real layer assignments in this app

```
bg-background  ← <body>, <main> canvas
  ├── bg-content1  ← TopNav, left sidebar, right sidebar, sticky header, pagination strip
  │     └── bg-content2  ← question cards, CTA panels, sidebar widgets (TrendingTags, TopUsers)
  │           ├── bg-content3  ← stats column inside QuestionCard, tag chips, active nav item,
  │           │                  table inside a card (sunken via shadow-inset)
  │           └── bg-content4  ← pressed/focus states only
```

### 1.4 The two moves

- **Raise** something → move it one level lighter than its parent (`content2` on `content1`, etc.)
  Add `shadow-raise-sm` to reinforce the elevation visually.
- **Sink** something → keep it at the parent's level or one darker, add `shadow-inset-sm/md`.
  Tables, input tracks, progress bars, code blocks — things you look *into*.

### 1.5 Current violations to fix

| File | Current | Should be |
|------|---------|-----------|
| `app/layout.tsx` (body) | `dark:bg-default-50` | `bg-background` ✅ fixed |
| `app/(main)/layout.tsx` (main) | `bg-content1` | `bg-background` ✅ fixed |
| `app/(main)/layout.tsx` (aside) | raw hex | `bg-content1` ✅ fixed |
| `questions/page.tsx` sticky header | raw hex | `bg-content1` ✅ fixed |
| `questions/page.tsx` list items | flat dividers | `bg-content2` cards ✅ fixed |
| `TopNav` | raw hex + border-b | `bg-content1 shadow-raise-sm` ✅ fixed |
| `TrendingTags` / `TopUsers` | raw hex + border | `bg-content2 shadow-raise-sm` ✅ fixed |

---

## 2. Shadow System

Shadows always come in **pairs**: a light inset on top (light reflection) + a dark drop on the bottom
(cast shadow). This simulates a single light source above the screen.

### 2.1 Raised elements

Use for cards, buttons, selected options — anything elevated.

```
shadow-raise-sm   ← small cards, inputs, nav active items
shadow-raise-md   ← standard card default
shadow-raise-lg   ← hover state, modals, feature CTAs
```

### 2.2 Recessed elements

Use for table bodies, progress tracks, text inputs, "deep" containers.

```
shadow-inset-sm   ← input fields, progress bar track
shadow-inset-md   ← table body wrapper, code blocks
```

### 2.3 Shadow grammar

```
raised  = light inset (top)  +  dark drop (bottom)
recessed = dark inset (top)  +  faint light inset (bottom)
```

The exact CSS lives in `globals.css` — see [§9](#9-globalscss-utilities-to-add).

### 2.4 Theme mechanics — how dark mode actually works

`next-themes` is configured with `attribute='class'`, so it toggles a **`.dark` class on `<html>`**
(not a `data-` attribute, not on `:root`). Tailwind v4 resolves this via:

```css
/* globals.css — already present */
@custom-variant dark (&:is(.dark *));
```

This means:
- `dark:bg-content2` compiles to `.dark .element { … }` — correct ✅
- Shadow utilities must be **light-first** in their base definition, with a `.dark &` or
  `dark:` override for the darker values — the same pattern every other utility in the project uses.
- **Never use `:root:not(.dark)`** as a light-mode selector — `.dark` is on `<html>`, not on `:root`
  in the way you'd expect when nesting matters. Use the plain base rule as light, override with `.dark &`.

### 2.5 Hover shadow transition

Interactive cards always animate the shadow on hover, never the background:

```tsx
className="shadow-raise-sm hover:shadow-raise-lg transition-shadow duration-200"
```

Do **not** use `transition-all` — it causes layout recalculations. Target `transition-shadow` only.

### 2.6 Choosing the right shadow

| Component | Default | Hover | Active / Pressed |
|-----------|---------|-------|------------------|
| Card (list item) | `shadow-raise-sm` | `shadow-raise-lg` | — |
| Card (featured / CTA) | `shadow-raise-md` | `shadow-raise-lg` | — |
| Button (primary) | `shadow-raise-sm` | `shadow-raise-md` | `shadow-inset-sm` |
| Input / Textarea | `shadow-inset-sm` | `shadow-inset-sm` | `shadow-inset-sm` |
| Table body | `shadow-inset-md` | — | — |
| Progress track | `shadow-inset-sm` | — | — |
| Selected option card | `shadow-raise-sm` | — | — |
| Modal / Dropdown | `shadow-raise-lg` | — | — |

---

## 3. Typography

### 3.1 Heading scale

Defined globally in `globals.css` — do not override per-component unless strictly necessary.

| Tag | Classes |
|-----|---------|
| `h1` | `text-4xl font-bold tracking-tight text-foreground-600` |
| `h2` | `text-3xl font-semibold tracking-tight text-foreground-600` |
| `h3` | `text-2xl font-semibold text-foreground-600` |
| `h4` | `text-xl font-semibold text-foreground-600` |
| `h5` | `text-lg font-medium text-foreground-600` |
| `h6` | `text-base font-medium text-foreground-600` |

### 3.2 Body text scale

| Role | Class | Hex (dark) | Hex (light) |
|------|-------|-----------|-------------|
| Primary label / heading | `text-foreground-800` | `#f4f4f5` | `#27272a` |
| Body copy | `text-foreground-600` | `#d4d4d8` | `#52525b` |
| Secondary / description | `text-foreground-500` | `#a1a1aa` | `#71717a` |
| Muted / meta / timestamps | `text-foreground-400` | `#71717a` | `#a1a1aa` |
| Disabled | `text-foreground-300` | `#52525b` | `#d4d4d8` |

### 3.3 Background-text compensation rule

When a background gets **lighter** (e.g. selected state), the text on it must get **darker** by the
same number of steps. This preserves contrast automatically.

```tsx
// ✅ Correct — lighter bg, darker text
className={isActive
  ? "bg-content3 text-foreground-800"   // bg moved +2 levels → text moved +2 levels
  : "bg-content1 text-foreground-500"}  // base
```

### 3.4 Hierarchy through weight + shade — not size alone

Before reaching for a larger `font-size`, try:
1. Heavier `font-weight` (`font-semibold` → `font-bold`)
2. Darker `text-foreground-*` shade

Reserve size increases for genuine heading jumps.

---

## 4. Spacing & Layout

### 4.1 Border radius scale

| Element | Radius |
|---------|--------|
| Large containers, modals, CTA banners | `rounded-2xl` |
| Standard cards, dropdowns | `rounded-xl` |
| Small cards, option tiles | `rounded-xl` |
| Inputs, buttons, chips | `rounded-lg` |
| Avatars, icon containers | `rounded-full` |

### 4.2 Padding inside surfaces

| Surface size | Padding |
|---|---|
| Page section / large card | `p-6` |
| Standard card | `p-4` |
| Compact card / list item | `p-3` |
| Input field | `px-3 py-2` |

### 4.3 Gap between stacked cards / list items

Prefer `gap-3` or `gap-4` inside a flex/grid container. Avoid `margin-top` on individual items.

---

## 5. Border Policy

### 5.1 Borders are redundant when color separates

If two adjacent surfaces differ by at least one `content` step, **remove the border between them**.
The background contrast is the separator.

```tsx
// ❌ Redundant — bg-content1 vs bg-content2 is already visible
<aside className="bg-content1 border-r border-content2">

// ✅ Let the color do the work
<aside className="bg-content1">
```

### 5.2 When borders are still appropriate

- Subtle `border-content2` dividers inside a list where items share the same background level.
- `ring` (not `border`) for focus indicators: `ring-2 ring-primary/40 ring-offset-1`.
- `border-primary/30` for highlighted containers that need a branded outline (e.g. the CTA banner in Planning Poker).

### 5.3 Dividers inside the same surface

Use `divide-y divide-content2` on the parent instead of adding `border-b` to every child.

---

## 6. Interactive States

### 6.1 State machine for any interactive surface

```
default  →  hover  →  active/pressed  →  focused
```

| State | Background | Shadow | Text |
|-------|-----------|--------|------|
| Default | `bg-content2` | `shadow-raise-sm` | `text-foreground-600` |
| Hover | `bg-content2` | `shadow-raise-lg` | `text-foreground-700` |
| Active / Selected | `bg-content3` | `shadow-raise-sm` | `text-foreground-800` |
| Focused | `bg-content2` | `shadow-raise-sm` + `ring-2 ring-primary/40` | `text-foreground-800` |
| Disabled | `bg-content1` | none | `text-foreground-300 opacity-50` |

### 6.2 Selected state always uses a lighter background

The selected item must be visually *above* its siblings. Use `bg-content3` (or `bg-content4` for
high-contrast needs) and pair it with `shadow-raise-sm`.

### 6.3 Pressed / active buttons use inset shadow

```tsx
<button className="
  bg-primary shadow-raise-sm
  hover:shadow-raise-md
  active:shadow-inset-sm
  transition-shadow duration-150
">
```

---

## 7. Component Patterns

> The layer each component sits on is the most important decision.
> Get that right first, then add shadow to reinforce it.

### 7.1 Navigation (SideMenu)

Sidebar chrome is `bg-content1`. Active item is `bg-content3` — one step lighter, pops off
the surrounding items which share the `bg-content1` base.

```tsx
classNames={{
    base: pathname.startsWith(href)
        ? 'bg-content3 shadow-raise-sm rounded-xl text-foreground-800'
        : 'hover:bg-content2 rounded-xl text-foreground-500',
    title: 'text-base font-medium',
}}
```

No `border-r` on the aside — `bg-content1` sidebar next to `bg-background` main canvas is the separator.

---

### 7.2 Question list

```
bg-background (canvas)
  └── bg-content2 rounded-xl shadow-raise-sm     ← question card (raised off canvas)
        └── bg-content3 px-4 py-4                ← stats column (lighter inner layer)
```

```tsx
{/* Card list on canvas */}
<div className='flex flex-col gap-2 p-4'>
  <div className='w-full flex bg-content2 rounded-xl shadow-raise-sm
                  hover:shadow-raise-lg transition-shadow duration-200'>
    <QuestionCard question={question} />
  </div>
</div>

{/* Stats column inside card — one step lighter */}
<div className='bg-content3 px-4 py-4 text-foreground-500'>
  {/* votes / answers / views */}
</div>
```

---

### 7.3 Sticky header & pagination strip

Chrome elements — `bg-content1` — visually above the `bg-background` canvas.

```tsx
<div className='sticky top-0 z-40 bg-content1 border-b border-content2'>
<div className='bg-content1 border-t border-content2 flex justify-between ...'>
```

The thin `border-content2` line sharpens the edge. It is acceptable here because the two
surfaces (`content1` chrome vs `background` canvas) are adjacent and need a crisp cut-off.

---

### 7.4 Sidebar widgets (TrendingTags, TopUsers)

```
bg-content1 (right sidebar chrome)
  └── bg-content2 shadow-raise-sm rounded-2xl    ← widget card (raised off chrome)
        ├── border-b border-content3              ← section divider inside card
        └── bg-content3 shadow-inset-sm           ← progress bar track (sunken)
              └── bg-primary (fill — raised)
```

---

### 7.5 Planning Poker landing

```
bg-background (canvas)
  ├── bg-content2 shadow-raise-md border-primary/20   ← CTA card
  └── bg-content2 shadow-raise-sm                     ← Recent Sessions card
        └── bg-content1 shadow-inset-md rounded-xl    ← table (sunken into card)
              └── hover:bg-content2                   ← row hover lifts back up
```

---

### 7.6 Cards (general)

```tsx
// Standard card on canvas
<div className="bg-content2 rounded-xl shadow-raise-sm
                hover:shadow-raise-lg transition-shadow duration-200 p-4">

// Featured / CTA card
<div className="bg-content2 rounded-2xl shadow-raise-md p-6 border border-primary/20">
```

---

### 7.7 Tables (sunken)

Tables hold dense data — recessed into their parent card.

```tsx
<div className="bg-content1 rounded-xl shadow-inset-md overflow-hidden">
  <Table removeWrapper classNames={{
    th: 'bg-content1 text-foreground-400 uppercase text-xs tracking-wide',
    td: 'text-foreground-600',
    tr: 'hover:bg-content2 transition-colors duration-150 cursor-pointer',
  }}>
```

---

### 7.8 Inputs & Textareas

```tsx
<input className="bg-content1 rounded-lg px-3 py-2 shadow-inset-sm
                  text-foreground-800 placeholder:text-foreground-400
                  focus:outline-none focus:ring-2 focus:ring-primary/40" />
```

---

### 7.9 Progress bars

```tsx
{/* Track — sunken */}
<div className="bg-content3 rounded-full shadow-inset-sm h-2 overflow-hidden">
  {/* Fill — raised */}
  <div className="bg-primary rounded-full h-full shadow-raise-sm transition-[width] duration-700"
       style={{ width: `${pct}%` }} />
</div>
```

---

### 7.10 Buttons

```tsx
// Primary — raised, pressed = sunken
<Button color="primary"
        className="shadow-raise-sm hover:shadow-raise-md active:shadow-inset-sm
                   transition-shadow duration-150" />

// Flat / ghost
<Button variant="flat"
        className="bg-content2 hover:shadow-raise-sm transition-shadow duration-150" />
```

---

### 7.11 Tag chips

Chips live inside `bg-content2` cards → use `bg-content3` so they are one step lighter:

```tsx
<Chip variant='flat' size='sm'
      className='bg-content3 text-foreground-600 hover:bg-content4 transition-colors'>
  {slug}
</Chip>
```

---

### 7.12 Option / Radio cards

```tsx
{/* Wrapper card on canvas */}
<div className="bg-content2 rounded-2xl p-4 shadow-raise-sm">
  {/* Unselected */}
  <div className="bg-content1 rounded-xl p-3 hover:bg-content2 transition-colors">
  {/* Selected — raised and highlighted */}
  <div className="bg-content3 rounded-xl p-3 shadow-raise-sm ring-2 ring-primary/30 text-foreground-800">
```

---

## 8. Theme Parity (Light / Dark)

**Rule: every component must look intentional in both themes.** Do not design only for dark mode.

### 8.1 Use tokens, not raw hex

Never write `bg-[#18181b]`, `dark:bg-[#27272a]`, `bg-white`, etc. in component files.  
Always use `bg-content1`, `bg-content2` etc. The token automatically resolves to the correct hex
for each theme. Raw hex in layout files is a known debt (listed in §1.4).

### 8.2 Shadow direction in each theme

Shadow utilities are defined **light-first** (soft drop shadows on white surfaces) with a `.dark &`
override that strengthens the drop and adds a white inset highlight (dark surfaces reflect light
differently). This matches exactly how every `dark:*` utility is resolved in this project.

- **Light mode base:** soft `rgba(0,0,0,…)` drop, subtle white inset highlight on top
- **Dark mode override:** stronger `rgba(0,0,0,…)` drop, brighter white inset highlight

The `globals.css` utilities in §9 handle this automatically — never write raw `box-shadow` in JSX.

### 8.3 Test both themes before PR

Toggle `dark` class on `<html>` and verify:
- No invisible text (white on white / black on black).
- No hard-coded hex that looks correct in one theme but wrong in the other.
- Shadow contrast is visible but not harsh in either theme.

---

## 9. globals.css Utilities to Add

Add the following block to `src/app/globals.css` after the existing heading overrides. These are
reusable shadow utilities consumed by all the component patterns above.

```css
/* ── Depth / Elevation Utilities ────────────────────────────────────────────
   next-themes toggles .dark on <html>; Tailwind v4 resolves dark: via
   @custom-variant dark (&:is(.dark *)).

   Rules are LIGHT-FIRST. Dark overrides use the same nesting pattern as
   every other dark: utility in the project.

   Raised (shadow-raise-*):   light inset top  + soft drop  → element appears elevated.
   Recessed (shadow-inset-*): dark inset top   + faint lift → element appears sunken.   */

@layer utilities {

  /* ── Raised — light mode base ── */
  .shadow-raise-sm {
    box-shadow:
      inset 0 1px 0 rgba(255, 255, 255, 0.80),
      0 2px 6px rgba(0, 0, 0, 0.10);
  }
  .shadow-raise-md {
    box-shadow:
      inset 0 1px 0 rgba(255, 255, 255, 0.90),
      0 4px 12px rgba(0, 0, 0, 0.14);
  }
  .shadow-raise-lg {
    box-shadow:
      inset 0 1px 0 rgba(255, 255, 255, 0.90),
      0 8px 28px rgba(0, 0, 0, 0.18);
  }

  /* ── Raised — dark mode overrides ── */
  .dark .shadow-raise-sm {
    box-shadow:
      inset 0 -1px 0 rgba(255, 255, 255, 0.06),
      0 2px 4px rgba(0, 0, 0, 0.25);
  }
  .dark .shadow-raise-md {
    box-shadow:
      inset 0 -2px 0 rgba(255, 255, 255, 0.08),
      0 4px 8px rgba(0, 0, 0, 0.30);
  }
  .dark .shadow-raise-lg {
    box-shadow:
      inset 0 -2px 0 rgba(255, 255, 255, 0.08),
      0 8px 24px rgba(0, 0, 0, 0.35);
  }

  /* ── Recessed — light mode base ── */
  .shadow-inset-sm {
    box-shadow:
      inset 0 2px 4px rgba(0, 0, 0, 0.10),
      inset 0 -1px 0 rgba(255, 255, 255, 0.60);
  }
  .shadow-inset-md {
    box-shadow:
      inset 0 3px 8px rgba(0, 0, 0, 0.13),
      inset 0 -1px 0 rgba(255, 255, 255, 0.70);
  }

  /* ── Recessed — dark mode overrides ── */
  .dark .shadow-inset-sm {
    box-shadow:
      inset 0 2px 4px rgba(0, 0, 0, 0.35),
      inset 0 -1px 0 rgba(255, 255, 255, 0.04);
  }
  .dark .shadow-inset-md {
    box-shadow:
      inset 0 3px 8px rgba(0, 0, 0, 0.45),
      inset 0 -1px 0 rgba(255, 255, 255, 0.06);
  }
}
```

---

## 10. Pre-Ship Checklist

Run through this before merging any UI change:

### Elevation
- [ ] Page `<main>` / sidebar use `bg-content1` (not raw hex)
- [ ] Cards sit on `bg-content2` — one step above their container
- [ ] Interactive / selected elements use `bg-content3` or `bg-content4`
- [ ] No surface skips more than one elevation step vs its parent

### Shadows
- [ ] Every card has at least `shadow-raise-sm`
- [ ] Tables / inputs have `shadow-inset-sm` or `shadow-inset-md`
- [ ] Interactive cards have `hover:shadow-raise-lg transition-shadow`
- [ ] Primary buttons use `active:shadow-inset-sm`

### Borders
- [ ] No `border-neutral-*` in component files (use `border-content2`)
- [ ] Borders removed where two surfaces already differ by a color step
- [ ] `divide-y divide-content2` used on list parents instead of `border-b` on each child

### Typography
- [ ] Active/selected text is darker (`text-foreground-700/800`) when background is lighter
- [ ] Muted info uses `text-foreground-400`, not `text-gray-*` or `text-neutral-*`

### Theme
- [ ] Tested in light mode
- [ ] No raw `bg-white`, `dark:bg-[#…]`, `text-white`, `text-black` in component JSX
- [ ] No `transition-all` — use specific transition properties (`transition-shadow`, `transition-colors`)

