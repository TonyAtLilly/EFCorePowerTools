using NetTopologySuite.Geometries;
using RevEng.Core.Abstractions.Metadata;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;

namespace RevEng.Core
{
    public static class PostgresTypeExtensions
    {
        private static readonly ISet<DbType> ScaleTypes = new HashSet<DbType>
        {
            System.Data.DbType.Decimal,
            System.Data.DbType.Currency,
        };

        private static readonly ISet<DbType> VarTimeTypes = new HashSet<DbType>
        {
            System.Data.DbType.DateTimeOffset,
            System.Data.DbType.DateTime2,
            System.Data.DbType.Time,
        };

        private static readonly ISet<DbType> LengthRequiredTypes = new HashSet<DbType>
        {
            System.Data.DbType.Binary,
            System.Data.DbType.VarNumeric,
            System.Data.DbType.StringFixedLength,
            System.Data.DbType.AnsiStringFixedLength,
        };

        //TODO: This is still an MS SQL server alias list.  Needs to be translated? 
        private static readonly ReadOnlyDictionary<string, string> GeneralTypeAliases
        = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>()
            {
                { "numeric", "decimal" },
                { "rowversion", "timestamp" },
                { "table type", "structured" },
                { "sql_variant", "variant" },
                { "geography", "udt" },
                { "geometry", "udt" },
                { "hierarchyid", "udt" },
                { "sysname", "nvarchar" },
            });

        public static bool IsScaleType(this DbType dbType)
        {
            return ScaleTypes.Contains(dbType);
        }

        public static bool IsVarTimeType(this DbType dbType)
        {
            return VarTimeTypes.Contains(dbType);
        }

        public static bool IsLengthRequiredType(this DbType dbType)
        {
            return LengthRequiredTypes.Contains(dbType);
        }

        public static Type PgClrType(this ModuleParameter storedProcedureParameter, bool asMethodParameter = false)
        {
            if (storedProcedureParameter is null)
            {
                throw new ArgumentNullException(nameof(storedProcedureParameter));
            }

            return GetPgClrType(storedProcedureParameter.StoreType, storedProcedureParameter.Nullable, asMethodParameter);
        }

        public static Type PgClrType(this ModuleResultElement moduleResultElement)
        {
            if (moduleResultElement is null)
            {
                throw new ArgumentNullException(nameof(moduleResultElement));
            }

            return GetPgClrType(moduleResultElement.StoreType, moduleResultElement.Nullable);
        }

        public static DbType PgDbType(this ModuleParameter storedProcedureParameter)
        {
            if (storedProcedureParameter is null)
            {
                throw new ArgumentNullException(nameof(storedProcedureParameter));
            }

            return GetPgDbType(storedProcedureParameter.StoreType);
        }

        public static Type GetPgClrType(string storeType, bool isNullable, bool asParameter = false)
        {
            var sqlType = GetPgDbType(storeType);

            switch (sqlType)
            {
                case System.Data.DbType.Int64:
                    return isNullable ? typeof(long?) : typeof(long);

                case System.Data.DbType.Binary:
                    return typeof(byte[]);

                case System.Data.DbType.Boolean:
                    return isNullable ? typeof(bool?) : typeof(bool);

                case System.Data.DbType.AnsiString:
                case System.Data.DbType.String:
                case System.Data.DbType.AnsiStringFixedLength:
                case System.Data.DbType.StringFixedLength:
                case System.Data.DbType.Xml:
                    return typeof(string);

                case System.Data.DbType.DateTime:
                case System.Data.DbType.Date:
                case System.Data.DbType.DateTime2:
                    return isNullable ? typeof(DateTime?) : typeof(DateTime);

                case System.Data.DbType.Time:
                    return isNullable ? typeof(TimeSpan?) : typeof(TimeSpan);

                case System.Data.DbType.Decimal:
                case System.Data.DbType.Currency:
                    return isNullable ? typeof(decimal?) : typeof(decimal);

                case System.Data.DbType.Int32:
                    return isNullable ? typeof(int?) : typeof(int);

                case System.Data.DbType.Single:
                    return isNullable ? typeof(float?) : typeof(float);

                case System.Data.DbType.Guid:
                    return isNullable ? typeof(Guid?) : typeof(Guid);

                case System.Data.DbType.Int16:
                    return isNullable ? typeof(short?) : typeof(short);

                case System.Data.DbType.Byte:
                    return isNullable ? typeof(byte?) : typeof(byte);

                case System.Data.DbType.Object:
                    return typeof(object);

                //case System.Data.DbType.g:
                //    switch (storeType)
                //    {
                //        case "geometry":
                //        case "geography":
                //            if (asParameter)
                //            {
                //                return typeof(byte[]);
                //            }

                //            return typeof(Geometry);

                //        default:
                //            return typeof(byte[]);
                //    }

                //case System.Data.DbType.Structured:
                //    return typeof(DataTable);

                case System.Data.DbType.DateTimeOffset:
                    return isNullable ? typeof(DateTimeOffset?) : typeof(DateTimeOffset);

                default:
                    throw new ArgumentOutOfRangeException(nameof(storeType), $"storetype: {storeType}");
            }
        }

        private static DbType GetPgDbType(string storeType)
        {
            if (string.IsNullOrEmpty(storeType))
            {
                throw new ArgumentException("storeType not specified");
            }

            var cleanedTypeName = RemoveMatchingBraces(storeType);

            if (cleanedTypeName == null)
            {
                throw new ArgumentOutOfRangeException(nameof(storeType), $"Unable to remove braces: {storeType}");
            }

#pragma warning disable CA1308 // Normalize strings to uppercase
            if (GeneralTypeAliases.TryGetValue(cleanedTypeName.ToLowerInvariant(), out string alias))
            {
                cleanedTypeName = alias;
            }
#pragma warning restore CA1308 // Normalize strings to uppercase

            if (!Enum.TryParse(cleanedTypeName, true, out DbType result))
            {
                throw new ArgumentOutOfRangeException(nameof(storeType), $"cleanedTypeName: {cleanedTypeName}");
            }

            return result;
        }

        private static string RemoveMatchingBraces(string s)
        {
            var stack = new Stack<char>();
            int count = 0;
            foreach (char ch in s)
            {
                switch (ch)
                {
                    case '(':
                        count += 1;
                        stack.Push(ch);
                        break;
                    case ')':
                        if (count == 0)
                        {
                            stack.Push(ch);
                        }
                        else
                        {
                            char popped;
                            do
                            {
                                popped = stack.Pop();
                            }
                            while (popped != '(');
                            count -= 1;
                        }

                        break;
                    default:
                        stack.Push(ch);
                        break;
                }
            }

            return string.Join(string.Empty, stack.Reverse());
        }
    }
}
