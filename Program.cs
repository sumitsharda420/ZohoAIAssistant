using System.Reflection;
using ZohoAIAssistant.Configuration;
using ZohoAIAssistant.Services;
using ZohoAIAssistant.Services.Exceptions;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ─────────────────────────────────────────────────────────────
builder.Services.Configure<ZohoSettings>(
    builder.Configuration.GetSection(ZohoSettings.SectionName));
builder.Services.Configure<OpenAISettings>(
    builder.Configuration.GetSection(OpenAISettings.SectionName));

// ── Controllers & API documentation ───────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Zoho AI Assistant API",
        Version = "v1",
        Description = """
            REST API for Zoho CRM lead retrieval, OAuth management, and OpenAI-powered lead analysis.

            ## OpenAI Endpoints

            Configure `OpenAI:ApiKey` and `OpenAI:Model` in appsettings.json, then:

            | Endpoint | Description |
            |----------|-------------|
            | `POST /api/ai/analyze-lead/{leadId}` | Full analysis: summary, next action, email draft, quality score |
            | `POST /api/ai/generate-email/{leadId}` | Professional follow-up email only |
            | `POST /api/ai/generate-summary/{leadId}` | Lead summary only |

            ## Complete OAuth Workflow

            Follow these steps in order to authenticate with Zoho CRM via Swagger:

            | Step | Action | Endpoint |
            |------|--------|----------|
            | **1** | Generate the Zoho authorization URL | `GET /api/oauth/authorize` |
            | **2** | Open `authorizationUrl` in a browser and sign in to Zoho | *(browser)* |
            | **3** | After consent, copy the `code` parameter from the redirect URL | *(browser redirect)* |
            | **4** | Exchange the code for access + refresh tokens | `GET /api/oauth/callback?code={code}` |
            | **5** | Verify tokens are stored (refresh token saved to `Data/zoho-tokens.json`) | `GET /api/oauth/tokens` |
            | **6** | Test lead retrieval | `GET /api/leads/test` |

            ### Optional steps

            - **Manual refresh:** `POST /api/oauth/refresh` — forces a new access token.
            - **Health check:** `GET /api/health/zoho` — verifies OAuth + CRM connectivity.
            - **Full leads list:** `GET /api/leads` — returns the first page of all leads.

            ### Prerequisites

            1. Set `Zoho:ClientId` and `Zoho:ClientSecret` in `appsettings.json`.
            2. Register redirect URI `http://localhost:5000/api/oauth/callback` in the [Zoho API Console](https://api-console.zoho.in/).
            3. Run the app on port 5000 (`dotnet run`).

            ### Automatic token refresh

            Access tokens expire after ~1 hour. All CRM endpoints automatically refresh the token
            using the stored refresh token when needed. Failed requests due to 401 are retried once.
            """
    });

    // Include XML comments for detailed Swagger descriptions on controllers and DTOs.
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// ── HttpClientFactory ─────────────────────────────────────────────────────────
builder.Services.AddHttpClient("ZohoOAuth", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient("ZohoCrm", client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

builder.Services.AddHttpClient("OpenAI", (serviceProvider, client) =>
{
    var settings = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpenAISettings>>().Value;
    client.BaseAddress = new Uri($"{settings.ApiBaseUrl.TrimEnd('/')}/");
    client.Timeout = TimeSpan.FromSeconds(120);
    client.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
});

// ── Dependency Injection ────────────────────────────────────────────────────────
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ITokenStorageService, TokenStorageService>();
builder.Services.AddSingleton<IZohoFieldMetadataService, ZohoFieldMetadataService>();
builder.Services.AddSingleton<IZohoOAuthService, ZohoOAuthService>();
builder.Services.AddScoped<IZohoLeadService, ZohoLeadService>();
builder.Services.AddScoped<IZohoHealthService, ZohoHealthService>();
builder.Services.AddScoped<IOpenAIService, OpenAIService>();

var app = builder.Build();

// ── HTTP pipeline ─────────────────────────────────────────────────────────────
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Zoho AI Assistant API v1");
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "Zoho AI Assistant — OAuth & CRM API";
});

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;

        if (exception is ZohoApiException zohoEx)
        {
            context.Response.StatusCode = zohoEx.StatusCode;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                title = "Zoho CRM Error",
                detail = zohoEx.Message,
                status = zohoEx.StatusCode
            });
            return;
        }

        if (exception is OpenAIApiException openAiEx)
        {
            context.Response.StatusCode = openAiEx.StatusCode;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                title = "OpenAI Error",
                detail = openAiEx.Message,
                status = openAiEx.StatusCode
            });
            return;
        }

        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(exception, "An unhandled exception occurred.");

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            title = "Internal Server Error",
            detail = "An unexpected error occurred. Check server logs for details.",
            status = StatusCodes.Status500InternalServerError
        });
    });
});

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
