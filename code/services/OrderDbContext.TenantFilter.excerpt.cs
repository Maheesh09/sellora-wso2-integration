// Source: https://github.com/Team-Sellora/sellora-order/blob/500d36a848d5e2dc2c87a1b1b67eea7ca217d3fa/src/Sellora.OrderService.Infrastructure/Persistence/OrderDbContext.cs#L54-L73
// Excerpt (lines 54-73): tenant isolation from the token's companyId claim.

        // Company boundary: no tenant in the token means no rows.
        modelBuilder.Entity<Order>()
            .HasQueryFilter(order =>
                _tenantContext.CompanyId != null &&
                order.CompanyId == _tenantContext.CompanyId);

        modelBuilder.Entity<OrderCheckIn>()
            .HasQueryFilter(checkIn =>
                _tenantContext.CompanyId != null &&
                checkIn.CompanyId == _tenantContext.CompanyId);

        modelBuilder.Entity<Payment>()
            .HasQueryFilter(payment =>
                _tenantContext.CompanyId != null &&
                payment.CompanyId == _tenantContext.CompanyId);

        modelBuilder.Entity<OrderDecision>()
            .HasQueryFilter(decision =>
                _tenantContext.CompanyId != null &&
                decision.CompanyId == _tenantContext.CompanyId);
