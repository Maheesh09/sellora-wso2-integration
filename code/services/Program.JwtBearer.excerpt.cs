// Source: https://github.com/Team-Sellora/sellora-order/blob/500d36a848d5e2dc2c87a1b1b67eea7ca217d3fa/src/Sellora.OrderService.Api/Program.cs#L31-L66
// Excerpt (lines 31-66): JWT bearer validation against WSO2 Identity Server.

var jwt = builder.Configuration.GetSection("Jwt");
var audiences = jwt.GetSection("Audience").Get<string[]>()
    ?? new[] { jwt["Audience"]! };

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = jwt["Authority"];
        options.MetadataAddress = jwt["MetadataAddress"]!;
        // Same as sellora-organization: keep WSO2's raw claim names
        // ("roles", "companyId", "sub") so RolePolicies can match "roles".
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt["Issuer"],
            ValidateAudience = true,
            ValidAudiences = audiences,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = "roles"
        };

        // Local and staging hosts may not trust the shared WSO2 CA.
        if (builder.Environment.IsDevelopment() || builder.Environment.IsStaging())
        {
            options.BackchannelHttpHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
        }
    });
