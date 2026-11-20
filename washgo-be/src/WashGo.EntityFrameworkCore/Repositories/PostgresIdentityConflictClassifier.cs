using System;
using Npgsql;
using Volo.Abp.DependencyInjection;
using WashGo.Domain.Services.Identity;

namespace WashGo.EntityFrameworkCore.Repositories;

public class PostgresIdentityConflictClassifier : IIdentityConflictClassifier, ITransientDependency
{
    public string? Classify(Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current is not PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } error) continue;
            return error.ConstraintName switch
            {
                // Unique only among accounts that are not soft-deleted (lower(email) / provider identity).
                "IX_users_email" or "ux_users_email_active" => "EmailAlreadyUsed",
                "ux_users_provider_active" => "IdentityAlreadyInUse",
                "IX_merchants_tax_code" => "TaxCodeAlreadyUsed",
                _ => null
            };
        }
        return null;
    }
}
