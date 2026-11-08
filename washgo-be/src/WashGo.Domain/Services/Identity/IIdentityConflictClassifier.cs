using System;

namespace WashGo.Domain.Services.Identity;

/// <summary>Infrastructure classifies only known identity unique constraints; other errors stay errors.</summary>
public interface IIdentityConflictClassifier
{
    string? Classify(Exception exception);
}
