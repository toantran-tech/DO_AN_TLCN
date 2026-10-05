using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace WashGo.EntityFrameworkCore.BuilderConfiguration
{
    public static class ModelBuilderNamingExtensions
    {
        public static void UseWashGoSnakeCaseColumns(this ModelBuilder builder)
        {
            foreach (var entityType in builder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.GetColumnName() == property.Name)
                    {
                        property.SetColumnName(ToSnakeCase(property.Name));
                    }
                }
            }
        }

        private static string ToSnakeCase(string value)
        {
            value = Regex.Replace(value, "([A-Z]+)([A-Z][a-z])", "$1_$2");
            value = Regex.Replace(value, "([a-z0-9])([A-Z])", "$1_$2");
            return value.ToLowerInvariant();
        }
    }
}