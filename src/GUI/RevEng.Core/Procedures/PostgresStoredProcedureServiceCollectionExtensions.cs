using ErikEJ.EntityFrameworkCore.Postgres.Scaffolding;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RevEng.Core.Abstractions.Model;
using RevEng.Core.Functions;

namespace RevEng.Core.Procedures
{
    public static class PostgresStoredProcedureServiceCollectionExtensions
    {
        public static IServiceCollection AddPostgresStoredProcedureDesignTimeServices(
            this IServiceCollection services,
            IOperationReporter reporter = null)
        {
            if (reporter == null)
            {
                reporter = new OperationReporter(handler: null);
            }

            return services
                .AddSingleton<IProcedureModelFactory, PostgresStoredProcedureModelFactory>()
                .AddSingleton<IProcedureScaffolder, PostgresStoredProcedureScaffolder>()
                .AddLogging(b => b.SetMinimumLevel(LogLevel.Debug).AddProvider(new OperationLoggerProvider(reporter)));
        }

        public static IServiceCollection AddPostgresFunctionDesignTimeServices(
            this IServiceCollection services,
            IOperationReporter reporter = null)
        {
            if (reporter == null)
            {
                reporter = new OperationReporter(handler: null);
            }

            return services
                .AddSingleton<IFunctionModelFactory, PostgresFunctionModelFactory>()
                .AddSingleton<IFunctionScaffolder, PostgresFunctionScaffolder>()
                .AddLogging(b => b.SetMinimumLevel(LogLevel.Debug).AddProvider(new OperationLoggerProvider(reporter)));
        }

        public static IServiceCollection AddPostgresDacpacStoredProcedureDesignTimeServices(
            this IServiceCollection services,
            PostgresDacpacDatabaseModelFactoryOptions factoryOptions,
            IOperationReporter reporter = null)
        {
            if (reporter == null)
            {
                reporter = new OperationReporter(handler: null);
            }

            return services
                .AddSingleton<IProcedureModelFactory, PostgresDacpacStoredProcedureModelFactory>(
                    provider => new PostgresDacpacStoredProcedureModelFactory(factoryOptions))
                .AddSingleton<IProcedureScaffolder, PostgresStoredProcedureScaffolder>()
                .AddLogging(b => b.SetMinimumLevel(LogLevel.Debug).AddProvider(new OperationLoggerProvider(reporter)));
        }
    }
}
