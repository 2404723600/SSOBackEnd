using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using SSODemo.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configure Keycloak SSO
var keycloakSection = builder.Configuration.GetSection("Keycloak");
var authority = keycloakSection["Authority"] ?? throw new InvalidOperationException("Keycloak Authority is not configured");
var audience = keycloakSection["Audience"] ?? "webapi1";
var validAudiences = keycloakSection.GetSection("ValidAudiences").Get<string[]>() ?? new[] { audience };
var requireHttpsMetadata = keycloakSection.GetValue<bool>("RequireHttpsMetadata", true);
var allowInsecureBackchannel = keycloakSection.GetValue<bool>("AllowInsecureBackchannel", false);
var validIssuer = keycloakSection["ValidIssuer"];

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.Cookie.Name = "SSODemo.Auth";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Events.OnValidatePrincipal = context =>
    {
        // Optional: Add custom principal validation logic here
        return Task.CompletedTask;
    };
})
.AddOpenIdConnect(options =>
{
    options.Authority = authority;
    options.ClientId = "webapi1"; // Adjust based on your Keycloak client setup
    options.ClientSecret = ""; // Set if using confidential client
    
    // Token validation parameters
    options.TokenValidationParameters.ValidAudience = audience;
    options.TokenValidationParameters.ValidAudiences = validAudiences;
    options.TokenValidationParameters.ValidateAudience = keycloakSection.GetValue<bool>("ValidateAudience", true);
    
    if (!string.IsNullOrEmpty(validIssuer))
    {
        options.TokenValidationParameters.ValidIssuer = validIssuer;
    }
    
    options.TokenValidationParameters.RequireSignedTokens = true;
    options.TokenValidationParameters.SaveSigninToken = true;
    
    // HTTPS/Metadata settings
    options.RequireHttpsMetadata = requireHttpsMetadata;
    
    // Allow insecure backchannel for development (self-signed certs)
    if (allowInsecureBackchannel)
    {
        options.BackchannelHttpHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
    }
    
    // Response type and scope
    options.ResponseType = "code";
    options.Scope.Clear();
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("email");
    options.Scope.Add("roles");
    
    // Claim mapping
    options.MapInboundClaims = true;
    options.GetClaimsFromUserInfoEndpoint = true;
    options.SaveTokens = true;
    
    // 保存 id_token 到 Claims，以便登出时使用
    options.Events = new OpenIdConnectEvents
    {
        OnTokenValidated = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Token validated for user: {Name}", context.Principal?.Identity?.Name);
            
            // 尝试从 ProtocolMessage 获取 id_token（某些情况下可用）
            string? idToken = context.ProtocolMessage?.IdToken;
            
            if (!string.IsNullOrEmpty(idToken))
            {
                logger.LogInformation("IdToken found in ProtocolMessage");
            }
            else
            {
                // 如果 ProtocolMessage 中没有，尝试从 TokenEndpointResponse 获取
                // 注意：在 TokenValidated 阶段，TokenEndpointResponse 可能已经处理过了
                // 所以主要依赖 SaveTokens=true 自动保存到 Properties 中
                logger.LogInformation("IdToken not in ProtocolMessage, will rely on SaveTokens mechanism");
            }
            
            if (!string.IsNullOrEmpty(idToken))
            {
                var claimsIdentity = context.Principal?.Identity as ClaimsIdentity;
                if (claimsIdentity != null)
                {
                    // 移除旧的 id_token claim（如果有）
                    var oldClaim = claimsIdentity.FindFirst("id_token");
                    if (oldClaim != null)
                    {
                        claimsIdentity.RemoveClaim(oldClaim);
                    }
                    claimsIdentity.AddClaim(new Claim("id_token", idToken));
                    logger.LogInformation("IdToken added to claims successfully");
                }
            }
            
            return Task.CompletedTask;
        },
        OnAuthorizationCodeReceived = async context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            
            // 在授权码接收阶段，从 TokenEndpointResponse 获取 id_token
            var idToken = context.TokenEndpointResponse?.IdToken;
            
            if (!string.IsNullOrEmpty(idToken))
            {
                logger.LogInformation("IdToken captured from TokenEndpointResponse: {IdTokenPrefix}", 
                    idToken.Length > 20 ? idToken.Substring(0, 20) + "..." : idToken);
                
                // 将 id_token 添加到 Claims 中，以便后续登出时使用
                var claimsIdentity = context.Principal?.Identity as ClaimsIdentity;
                if (claimsIdentity != null)
                {
                    var oldClaim = claimsIdentity.FindFirst("id_token");
                    if (oldClaim != null)
                    {
                        claimsIdentity.RemoveClaim(oldClaim);
                    }
                    claimsIdentity.AddClaim(new Claim("id_token", idToken));
                    logger.LogInformation("IdToken added to user claims");
                }
            }
            else
            {
                logger.LogWarning("IdToken not found in TokenEndpointResponse");
            }
            
            // 不需要手动调用 HandleCodeRedemptionAsync，让中间件自动处理
            // 返回已完成的任务，让默认管道继续处理
            return;
        },
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(context.Exception, "Authentication failed");
            return Task.CompletedTask;
        },
        OnRemoteFailure = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError("Remote authentication failed: {Error}", context.Failure?.Message);
            context.HandleResponse();
            context.Response.Redirect("/Home/Error?message=" + Uri.EscapeDataString(context.Failure?.Message ?? "Unknown error"));
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
