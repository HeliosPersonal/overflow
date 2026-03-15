# Overflow — Password Reset & Notification Service

## Overview

Overflow uses its **own password reset flow** — users never see Keycloak's built-in
"Update Your Account" page or receive Keycloak's default admin-style emails.

All outbound notifications (email, and future channels like Telegram) are sent through
the **NotificationService** microservice.

### Password Reset Flow

```
User clicks "Forgot password?"
  └─► POST /api/auth/forgot-password  (Next.js API route)
        ├── Finds user in Keycloak via Admin API
        ├── Generates a reset token (15-min expiry, in-memory store)
        ├── POST /notifications/send  (via YARP gateway)
        │     { channel: "Email", template: "PasswordReset",
        │       recipient: "user@...", parameters: { resetUrl, appName } }
        └── Returns 200 (always, to prevent email enumeration)

NotificationService receives HTTP POST
  └─► Publishes SendNotification to RabbitMQ
        └─► SendNotificationHandler consumes message
              ├── TemplateRenderer resolves PasswordReset → subject + HTML + text
              └── EmailChannel sends via FluentEmail.Mailgun

User clicks link in email
  └─► /reset-password?token=...&email=...  (Next.js page)
        └── Submits new password
              └─► POST /api/auth/reset-password  (Next.js API route)
                    ├── Verifies & consumes the reset token
                    ├── Sets new password via Keycloak Admin API
                    └── Redirects to /login
```

---

## NotificationService Architecture

```
POST /notifications/send  ([ApiController] + [Authorize])
  │  { channel: "Email", template: "PasswordReset", recipient: "...", parameters: {...} }
  ▼
RabbitMQ  (via Wolverine PublishAsync)
  │
  ▼
SendNotificationHandler  (Wolverine consumer)
  ├── ITemplateRenderer.Render(template, parameters)
  │     → Loads Templates/Html/{Template}.html + Templates/Text/{Template}.txt
  │     → RenderedTemplate { Subject, HtmlBody, TextBody }
  └── INotificationChannel.SendAsync(recipient, subject, body)
        ├── EmailChannel      → FluentEmail.Mailgun
        ├── TelegramChannel   → (future)
        └── SlackChannel      → (future)
```

### Adding a New Channel

1. Add the enum value to `NotificationChannel` in `Overflow.Contracts`:

   ```csharp
   public enum NotificationChannel { Email, Telegram }
   ```

2. Implement `INotificationChannel` in `Channels/`:

   ```csharp
   public class TelegramChannel(...) : INotificationChannel
   {
       public string ChannelName => "Telegram";
       public async Task SendAsync(string recipient, string subject, string body, string? plainTextBody = null)
       {
           // recipient = Telegram chat ID; use plainTextBody (no HTML)
       }
   }
   ```

3. Register in `Program.cs`:

   ```csharp
   builder.Services.AddTransient<INotificationChannel, TelegramChannel>();
   ```

4. Callers send `"channel": "Telegram"` — no other changes needed.

### Adding a New Template

1. Add the enum value to `NotificationTemplate` in `Overflow.Contracts`:

   ```csharp
   public enum NotificationTemplate { PasswordReset, Welcome, VerifyEmail, YourNew }
   ```

2. Create the template files:
   - `Templates/Html/YourNew.html` — HTML body with `{{placeholder}}` syntax
   - `Templates/Text/YourNew.txt` — plain text fallback

3. Add a subject line in `TemplateRenderer.Subjects`:

   ```csharp
   [NotificationTemplate.YourNew] = "Your subject with {{appName}}",
   ```

Built-in placeholder `{{year}}` is always available. All other placeholders come from the `Parameters` dictionary.

### Available Templates

| Template (enum) | Parameters | Used By |
|---|---|---|
| `PasswordReset` | `resetUrl`, `appName` | `POST /api/auth/forgot-password` |
| `Welcome` | `displayName`, `appName`, `loginUrl` | (ready for use) |
| `VerifyEmail` | `verifyUrl`, `appName` | (ready for use) |

---

## Key Files

### NotificationService (C#)

| File | Purpose |
|---|---|
| `Program.cs` | Service setup: controllers, FluentEmail.Mailgun, Wolverine |
| `Controllers/NotificationsController.cs` | `POST /notifications/send` → publishes to RabbitMQ |
| `Channels/INotificationChannel.cs` | Channel abstraction (email, Telegram, etc.) |
| `Channels/EmailChannel.cs` | Email channel via FluentEmail.Mailgun |
| `Templates/ITemplateRenderer.cs` | Template renderer interface + `RenderedTemplate` record |
| `Templates/TemplateRenderer.cs` | Loads embedded HTML/text files, replaces `{{placeholders}}`, maps subjects |
| `Templates/Html/*.html` | HTML email templates (embedded resources) |
| `Templates/Text/*.txt` | Plain text email templates (embedded resources) |
| `MessageHandlers/SendNotificationHandler.cs` | Wolverine handler: renders template → dispatches to channel |
| `Options/MailgunOptions.cs` | Mailgun config binding (used by FluentEmail setup) |

### Webapp (TypeScript)

| File | Purpose |
|---|---|
| `src/lib/resetTokens.ts` | In-memory token store with 15-minute expiry |
| `src/app/api/auth/forgot-password/route.ts` | Generates token, POSTs to NotificationService |
| `src/app/api/auth/reset-password/route.ts` | Verifies token, sets password via Keycloak Admin API |
| `src/app/(auth)/forgot-password/page.tsx` | "Enter your email" form |
| `src/app/(auth)/reset-password/page.tsx` | "Set new password" form |

### Contract (`Overflow.Contracts`)

| File | Purpose |
|---|---|
| `SendNotification.cs` | RabbitMQ message record |
| `NotificationChannel.cs` | Enum: `Email`, `Telegram` |
| `NotificationTemplate.cs` | Enum: `PasswordReset`, `Welcome`, `VerifyEmail` |

---

## Mailgun Configuration

The `EmailChannel` sends via **FluentEmail.Mailgun** — a well-maintained NuGet package
that wraps the Mailgun REST API. Uses HTTPS (port 443), no SMTP.

Under the hood `FluentEmail.Mailgun` calls the same endpoint as the official Mailgun SDK:

```
POST https://api.eu.mailgun.net/v3/{Domain}/messages
Authorization: Basic api:{ApiKey}
```

### How `MailgunOptions` maps to the official SDK

| MailgunOptions property | Official Mailgun SDK equivalent | Example |
|---|---|---|
| `ApiKey` | `Environment.GetEnvironmentVariable("API_KEY")` | `key-abc123…` |
| `Domain` | REST path `/v3/{domain}/messages` | `devoverflow.org` |
| `Region` | Base URL: `EU` → `api.eu.mailgun.net`, `US` → `api.mailgun.net` | `EU` |
| `FromEmail` | `from` parameter | `noreply@devoverflow.org` |
| `FromName` | Display name in `from` | `Overflow` |

### NotificationService appsettings

```jsonc
// appsettings.json (defaults)
{
  "Mailgun": {
    "ApiKey": "",                          // Set per environment
    "Domain": "devoverflow.org",
    "FromEmail": "noreply@devoverflow.org",
    "FromName": "Overflow",
    "Region": "EU"                         // "EU" or "US"
  }
}
```

### Per-environment

| Environment | Config | Notes |
|---|---|---|
| **Development** | `appsettings.Development.json` | API key committed for local dev |
| **Staging** | Infisical secret `Mailgun__ApiKey` | Domain + From overridden in `appsettings.Staging.json` (`staging.devoverflow.org`) |
| **Production** | Infisical secret `Mailgun__ApiKey` | Uses defaults from `appsettings.json` (`devoverflow.org`) |

> **Important:** Staging sends from `staging.devoverflow.org`, so `appsettings.Staging.json`
> overrides `Domain` to `staging.devoverflow.org`. Mailgun requires the API domain to match
> the verified sending domain. Both domains must be verified in the Mailgun dashboard (EU region).

### Infisical Secrets

Two secrets are needed per environment:

| Secret Key (Infisical) | Used By | Value | Where to find |
|---|---|---|---|
| `Mailgun__ApiKey` | NotificationService | Mailgun private API key | Mailgun dashboard → Sending → Domain settings → API Keys |
| `NOTIFICATION_API_KEY` | NotificationService + webapp | Shared API key for server-to-server calls | Generate with `openssl rand -hex 32` |

> **`NOTIFICATION_API_KEY`** secures `POST /notifications/send` for server-to-server calls (e.g. forgot-password).
> The webapp sends it via `X-Api-Key` header. The NotificationService also accepts Keycloak JWT (`Authorization: Bearer`)
> for user-authenticated calls — both mechanisms work side by side.
> Same key name is used in both services and in Infisical — no mapping needed.

The `__` separator in Infisical maps to `:` in .NET configuration (e.g. `Mailgun__ApiKey` → `Mailgun:ApiKey`).

---

## SendNotification Contract

```csharp
// Overflow.Contracts/SendNotification.cs
public record SendNotification(
    NotificationChannel Channel,              // Email, Telegram
    string Recipient,                          // email address, chat ID, etc.
    NotificationTemplate Template,             // PasswordReset, Welcome, VerifyEmail
    Dictionary<string, string> Parameters);    // template variables

// Overflow.Contracts/NotificationChannel.cs
public enum NotificationChannel { Email, Telegram }

// Overflow.Contracts/NotificationTemplate.cs
public enum NotificationTemplate { PasswordReset, Welcome, VerifyEmail }
```

Any service (or the webapp) can publish this message to trigger a notification.
The API accepts enum values as strings (e.g. `"Email"`, `"PasswordReset"`) via `JsonStringEnumConverter`.

---

## Why Not Use Keycloak's Built-in Password Reset?

1. **Ugly default email** — says *"Your administrator has just requested..."*
2. **Ugly password form** — redirects to Keycloak's themed page
3. **No control** — customizing FreeMarker templates requires a custom Keycloak theme
4. **Better UX** — users stay within the app, email matches the brand

