﻿using Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RevEng.Core.Abstractions;
using RevEng.Core.Abstractions.Metadata;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;

namespace RevEng.Core.Procedures
{

    //TODO: This needs to be altered for Postgres.  Currently just a copy of SQL Server stuff.  

    public abstract class PostgresRoutineModelFactory
    {
        public string RoutineSql { get; set; }

        public string RoutineType { get; set; }

#pragma warning disable CA1716 // Identifiers should not match keywords
        protected abstract List<List<ModuleResultElement>> GetResultElementLists(NpgsqlConnection connection, Routine module, bool multipleResults, bool useLegacyResultSetDiscovery);
#pragma warning restore CA1716 // Identifiers should not match keywords

        protected RoutineModel GetRoutines(string connectionString, ModuleModelFactoryOptions options)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var result = new List<Routine>();
            var found = new List<Tuple<string, string, bool>>();
            var errors = new List<string>();

            var filter = options.Modules.ToHashSet();

            using (var connection = new NpgsqlConnection(connectionString))
            {
                var sql = new StringBuilder();
                sql.AppendLine(RoutineSql);
#if CORE31
                // Filters out table-valued functions without filtering out stored procedures
                sql.AppendLine("AND COALESCE(DATA_TYPE, '') != 'TABLE'");
#endif
                sql.AppendLine("ORDER BY ROUTINE_NAME;");

#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities
                using (var command = new NpgsqlCommand(sql.ToString(), connection))
                {
                    connection.Open();
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            // Schema, Name, IsScalar
                            found.Add(new Tuple<string, string, bool>(reader.GetString(0), reader.GetString(1), reader.GetBoolean(2)));
                        }
                    }
                }
#pragma warning restore CA2100 // Review SQL queries for security vulnerabilities

                var allParameters = options.FullModel ? GetParameters(connection) : new Dictionary<string, List<ModuleParameter>>();

                foreach (var foundModule in found)
                {
                    var key = $"[{foundModule.Item1}].[{foundModule.Item2}]";

                    if (filter.Count == 0 || filter.Contains(key))
                    {
                        var isScalar = foundModule.Item3;

                        var module = RoutineType == "PROCEDURE"
                            ? (Routine)new Procedure()
                            : new Function { IsScalar = isScalar };

                        module.Schema = foundModule.Item1;
                        module.Name = foundModule.Item2;

                        if (options.FullModel)
                        {
                            module.HasValidResultSet = true;

                            if (options.MappedModules?.ContainsKey(key) ?? false)
                            {
                                module.MappedType = options.MappedModules[key];
                            }

                            if (allParameters.TryGetValue($"[{module.Schema}].[{module.Name}]", out var moduleParameters))
                            {
                                module.Parameters = moduleParameters;
                            }

                            if (RoutineType == "PROCEDURE")
                            {
                                module.Parameters.Add(GetReturnParameter());
                            }

                            if (!isScalar)
                            {
#pragma warning disable CA1031 // Do not catch general exception types
                                try
                                {
                                    var forceLegacy = options.UseLegacyResultSetDiscovery;
                                    if (!forceLegacy)
                                    {
                                        forceLegacy = options.ModulesUsingLegacyDiscovery?.Contains($"[{module.Schema}].[{module.Name}]") ?? false;
                                    }

                                    module.Results.AddRange(GetResultElementLists(connection, module, options.DiscoverMultipleResultSets, forceLegacy));
                                }
                                catch (Exception ex)
                                {
                                    module.HasValidResultSet = false;
                                    module.Results = new List<List<ModuleResultElement>>
                                    {
                                        new List<ModuleResultElement>(),
                                    };
                                    errors.Add($"Unable to get result set shape for {RoutineType} '{module.Schema}.{module.Name}'.{Environment.NewLine}{ex.Message}{Environment.NewLine}");
                                }
#pragma warning restore CA1031 // Do not catch general exception types
                            }

                            if (module is Function func
                                && func.IsScalar
                                && func.Parameters.Count > 0
                                && func.Parameters.Any(p => p.StoreType == "table type"))
                            {
                                errors.Add($"Unable to scaffold {RoutineType} '{module.Schema}.{module.Name}' as it has TVP parameters.{Environment.NewLine}");
                                continue;
                            }

                            bool dupesFound = false;
                            foreach (var resultElement in module.Results)
                            {
                                var duplicates = resultElement.GroupBy(x => x.Name)
                                    .Where(g => g.Count() > 1)
                                    .Select(y => y.Key)
                                    .ToList();

                                if (duplicates.Any())
                                {
                                    dupesFound = true;
                                    errors.Add($"Unable to scaffold {RoutineType} '{module.Schema}.{module.Name}' as it has duplicate result column names: '{duplicates[0]}'.{Environment.NewLine}");
                                }
                            }

                            if (dupesFound)
                            {
                                continue;
                            }
                        }

                        result.Add(module);
                    }
                }
            }

            return new RoutineModel
            {
                Routines = result,
                Errors = errors,
            };
        }

        private static Dictionary<string, List<ModuleParameter>> GetParameters(NpgsqlConnection connection)
        {
            using var dtResult = new DataTable();
            var result = new List<ModuleParameter>();

            // Validate this - based on https://stackoverflow.com/questions/20115881/how-to-get-stored-procedure-parameters-details/41330791
            var sql = $@"
SELECT  
    'Parameter' = p.name,  
    'Type'   = COALESCE(type_name(p.system_type_id), type_name(p.user_type_id)),  
    'Length'   = CAST(p.max_length AS INT),  
    'Precision'   = CAST(case when type_name(p.system_type_id) = 'uniqueidentifier' 
                then p.precision  
                else OdbcPrec(p.system_type_id, p.max_length, p.precision) end AS INT),  
    'Scale'   = CAST(OdbcScale(p.system_type_id, p.scale) AS INT),  
    'Order'  = CAST(parameter_id AS INT),  
    p.is_output AS output,
    'TypeName' = QUOTENAME(SCHEMA_NAME(t.schema_id)) + '.' + QUOTENAME(TYPE_NAME(p.user_type_id)),
	'TypeSchema' = t.schema_id,
	'TypeId' = p.user_type_id,
    'RoutineName' = OBJECT_NAME(p.object_id),
    'RoutineSchema' = OBJECT_SCHEMA_NAME(p.object_id)
    from sys.parameters p
	LEFT JOIN sys.table_types t ON t.user_type_id = p.user_type_id
    ORDER BY p.object_id, p.parameter_id;";

            using var adapter = new NpgsqlDataAdapter
            {
                SelectCommand = new NpgsqlCommand(sql, connection),
            };

            adapter.Fill(dtResult);

            foreach (DataRow par in dtResult.Rows)
            {
                if (par != null)
                {
                    var parameterName = par["Parameter"].ToString();
                    if (parameterName!.StartsWith("@", StringComparison.Ordinal))
                    {
                        parameterName = parameterName.Substring(1);
                    }

                    var parameter = new ModuleParameter()
                    {
                        Name = parameterName,
                        RoutineName = par["RoutineName"].ToString(),
                        RoutineSchema = par["RoutineSchema"].ToString(),
                        StoreType = par["Type"].ToString(),
                        Length = (par["Length"] is DBNull) ? (int?)null : int.Parse(par["Length"].ToString()!, CultureInfo.InvariantCulture),
                        Precision = (par["Precision"] is DBNull) ? (int?)null : int.Parse(par["Precision"].ToString()!, CultureInfo.InvariantCulture),
                        Scale = (par["Scale"] is DBNull) ? (int?)null : int.Parse(par["Scale"].ToString()!, CultureInfo.InvariantCulture),
                        Output = (bool)par["output"],
                        Nullable = true,
                        TypeName = (par["TypeName"] is DBNull) ? par["Type"].ToString() : par["TypeName"].ToString(),
                        TypeId = (par["TypeId"] is DBNull) ? (int?)null : int.Parse(par["TypeId"].ToString()!, CultureInfo.InvariantCulture),
                        TypeSchema = (par["TypeSchema"] is DBNull) ? (int?)null : int.Parse(par["TypeSchema"].ToString()!, CultureInfo.InvariantCulture),
                    };

                    result.Add(parameter);
                }
            }

            return result.GroupBy(x => $"[{x.RoutineSchema}].[{x.RoutineName}]").ToDictionary(g => g.Key, g => g.ToList(), StringComparer.InvariantCulture);
        }

        private static ModuleParameter GetReturnParameter()
        {
            // Add parameter to hold the standard return value
            return new ModuleParameter()
            {
                Name = "returnValue",
                StoreType = "int",
                Output = true,
                Nullable = false,
                IsReturnValue = true,
            };
        }
    }
}
