// Source: https://github.com/Team-Sellora/sellora-organization/blob/9772eb48d4a05a6a037c408e5c5ab850ba5f2fbe/src/Sellora.CoreService.Api/Authorization/RolePolicies.cs
// Copied unchanged for reference. Team-Sellora, SLIIT SE3022.

using Microsoft.AspNetCore.Authorization;

namespace Sellora.CoreService.Api.Authorization;

public static class RolePolicies
{
    public const string RequireCompanyAdmin = "RequireCompanyAdmin";
    public const string RequireAreaManager = "RequireAreaManager";
    public const string RequireAgencyOperator = "RequireAgencyOperator";
    public const string RequireSalesRep = "RequireSalesRep";
    public const string RequireShopOwner = "RequireShopOwner";
    public const string RequireHierarchyReader = "RequireHierarchyReader";
    public const string RequireStaffManager = "RequireStaffManager";

    public static void AddSelloraRolePolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(RequireCompanyAdmin, p => p.RequireAssertion(HasRole("CompanyAdmin")));
        options.AddPolicy(RequireAreaManager, p => p.RequireAssertion(HasRole("AreaManager")));
        options.AddPolicy(RequireAgencyOperator, p => p.RequireAssertion(HasRole("AgencyOperator")));
        options.AddPolicy(RequireSalesRep, p => p.RequireAssertion(HasRole("SalesRep")));
        options.AddPolicy(RequireShopOwner, p => p.RequireAssertion(HasRole("ShopOwner")));
        // Adding staff: Company Admins (any staff role) and Agency Operators
        // (sales reps). The service enforces which role each may create.
        options.AddPolicy(RequireStaffManager, p => p.RequireAssertion(HasAnyRole(
            "CompanyAdmin", "AgencyOperator")));
        options.AddPolicy(RequireHierarchyReader, p => p.RequireAssertion(HasAnyRole(
            "CompanyAdmin", "AreaManager", "AgencyOperator", "SalesRep", "ShopOwner")));
    }

    // WSO2 IS emits the "roles" claim (plural, an array). Program.cs sets
    // RoleClaimType = "roles" and MapInboundClaims = false, so "roles" arrives
    // unmapped — match on "roles" directly, not ClaimTypes.Role.
    private static Func<AuthorizationHandlerContext, bool> HasRole(string role) =>
        ctx => ctx.User.HasClaim("roles", role);

    private static Func<AuthorizationHandlerContext, bool> HasAnyRole(params string[] roles) =>
        ctx => roles.Any(r => ctx.User.HasClaim("roles", r));
}