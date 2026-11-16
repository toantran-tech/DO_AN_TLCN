namespace WashGo.Domain.Shared.Validation;
public class CatalogRuleException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
