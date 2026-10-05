using System;
using WashGo.Core.Domain.Shared.Enums;

namespace WashGo.Core.Domain.Attributes
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class FilterableAttribute : Attribute
    {
        public ColumnVariant Variant { get; }
        public UnixTimestampUnit UnixUnit { get; set; } = UnixTimestampUnit.Seconds;

        public FilterableAttribute(ColumnVariant variant)
        {
            Variant = variant;
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class SevagoTableAttribute : Attribute
    {
        public string TableName { get; }
        public string? Schema { get; set; }

        public SevagoTableAttribute(string tableName)
        {
            TableName = tableName;
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class WashGoTableAttribute : SevagoTableAttribute
    {
        public WashGoTableAttribute(string tableName) : base(tableName)
        {
        }
    }
}
