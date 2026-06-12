# Zoho AI Assistant

ASP.NET Core 8 Web API that integrates with **Zoho CRM** via OAuth 2.0 to retrieve Lead records. The project is structured for clean architecture and includes a placeholder `AIService` for future **OpenAI** integration.

## Project Structure

```
ZohoAIAssistant/
├── Configuration/       # Strongly-typed appsettings bindings (ZohoSettings)
├── Controllers/         # REST API endpoints (LeadsController)
├── DTOs/                # API response shapes (ZohoLeadDto, token DTOs)
├── Models/              # Zoho CRM domain models
├── Services/            # Business logic (OAuth, Leads, AI stub)
│   └── Exceptions/      # Custom ZohoApiException
├── Program.cs           # DI, HttpClient, Swagger, middleware
└── appsettings.json     # Zoho credentials and API URLs
```

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- A Zoho account with access to **Zoho CRM**
- A Zoho API Console client (Server-based Application)

---

## How to Generate Client ID and Client Secret

1. Go to the [Zoho API Console](https://api-console.zoho.in/) (use `.com` or `.eu` if your account is in a different data center).
2. Click **Add Client** → choose **Server-based Applications**.
3. Fill in:
   - **Client Name**: e.g. `ZohoAIAssistant`
   - **Homepage URL**: `http://localhost:5000`
   - **Authorized Redirect URI**: `http://localhost:5000/callback` (must match `RedirectUri` in appsettings.json exactly)
4. Click **Create**.
5. Copy the **Client ID** and **Client Secret** shown on the client details page.

> **Data center note:** This project defaults to the **India** data center (`accounts.zoho.in` / `zohoapis.in`). If your account is in the US or EU, update `AccountsUrl` and `ApiBaseUrl` in appsettings.json accordingly.

---

## How to Generate a Refresh Token

Zoho uses a one-time authorization code flow to issue a long-lived refresh token.

### Step 1 — Build the authorization URL

Replace `YOUR_CLIENT_ID` with your Client ID:

```
https://accounts.zoho.in/oauth/v2/auth?scope=ZohoCRM.modules.leads.READ,ZohoCRM.modules.leads.ALL&client_id=YOUR_CLIENT_ID&response_type=code&access_type=offline&redirect_uri=http://localhost:5000/callback
```

Open this URL in a browser and sign in with your Zoho account. After consent, you are redirected to:

```
http://localhost:5000/callback?code=1000.xxxxx.yyyyy&location=in&accounts-server=...
```

Copy the `code` parameter value (it expires in ~60 seconds).

### Step 2 — Exchange the code for tokens

Run this `curl` command (replace placeholders):

```bash
curl -X POST "https://accounts.zoho.in/oauth/v2/token" \
  -d "grant_type=authorization_code" \
  -d "client_id=YOUR_CLIENT_ID" \
  -d "client_secret=YOUR_CLIENT_SECRET" \
  -d "redirect_uri=http://localhost:5000/callback" \
  -d "code=YOUR_AUTHORIZATION_CODE"
```

The response includes:

```json
{
  "access_token": "...",
  "refresh_token": "...",
  "expires_in": 3600,
  "api_domain": "https://www.zohoapis.in"
}
```

Copy the **`refresh_token`** — it does not expire unless revoked. Store it securely in appsettings.json (or User Secrets / environment variables in production).

---

## How to Configure appsettings.json

Edit `appsettings.json` and fill in your credentials:

```json
{
  "Zoho": {
    "ClientId": "1000.XXXXXXXXXXXX",
    "ClientSecret": "xxxxxxxxxxxxxxxxxxxxxxxx",
    "RefreshToken": "1000.xxxxxxxxxxxxxxxxxxxxxxxx",
    "RedirectUri": "http://localhost:5000/callback",
    "AccountsUrl": "https://accounts.zoho.in",
    "ApiBaseUrl": "https://www.zohoapis.in"
  }
}
```

### Production tip

Never commit real secrets. Use **User Secrets** during development:

```bash
dotnet user-secrets init
dotnet user-secrets set "Zoho:ClientId" "YOUR_CLIENT_ID"
dotnet user-secrets set "Zoho:ClientSecret" "YOUR_CLIENT_SECRET"
dotnet user-secrets set "Zoho:RefreshToken" "YOUR_REFRESH_TOKEN"
```

Or set environment variables: `Zoho__ClientId`, `Zoho__ClientSecret`, `Zoho__RefreshToken`.

---

## How to Run the Project

```bash
# Restore packages and build
dotnet restore
dotnet build

# Run the API (opens Swagger at http://localhost:5000/swagger)
dotnet run
```

### API Endpoints

| Method | Endpoint           | Description                    |
|--------|----------------------|--------------------------------|
| GET    | `/api/leads`         | List all leads (first page)    |
| GET    | `/api/leads/{id}`    | Get a single lead by record ID |

### Example requests

```bash
curl http://localhost:5000/api/leads
curl http://localhost:5000/api/leads/1234567890123456789
```

---

## How OAuth Token Refresh Works

`ZohoOAuthService` automatically:

1. Checks an in-memory cache for a valid access token.
2. If missing or expired, calls `POST {AccountsUrl}/oauth/v2/token` with `grant_type=refresh_token`.
3. Caches the new token and returns it to `ZohoLeadService`.
4. On a 401 from the CRM API, forces a refresh and retries the request once.

---

## Future OpenAI Integration

The `IAIService` / `AIService` stub is registered in DI and ready to extend:

1. Add an `OpenAI` section to appsettings.json.
2. Create `Configuration/OpenAISettings.cs`.
3. Install the OpenAI SDK: `dotnet add package OpenAI`
4. Implement `AIService.AnalyzeLeadAsync` using `IZohoLeadService` for lead context.
5. Add a new controller (e.g. `AIController`) that calls `IAIService`.

---

## License

MIT
