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
                "IX_users_email" => "EmailAlreadyUsed",
                "IX_merchants_tax_code" => "TaxCodeAlreadyUsed",
                _ => null
            };
        }
        return null;
    }
}
