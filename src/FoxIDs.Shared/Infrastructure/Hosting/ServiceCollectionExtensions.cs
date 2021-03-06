﻿using FoxIDs.Repository;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Logging;
using System;

namespace FoxIDs.Infrastructure.Hosting
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSharedLogic(this IServiceCollection services)
        {            
            return services;
        }

        public static IServiceCollection AddSharedRepository(this IServiceCollection services)
        {            
            services.AddSingleton<IRepositoryClient, RepositoryClient>();
            services.AddSingleton<IRepositoryCosmosClient, RepositoryCosmosClient>();
            services.AddSingleton<IMasterRepository, MasterRepository>();
            services.AddSingleton<ITenantRepository, TenantRepository>();

            return services;
        }

        public static IServiceCollection AddSharedInfrastructure(this IServiceCollection services)
        {
            IdentityModelEventSource.ShowPII = true;

            services.AddHsts(options =>
            {
                options.IncludeSubDomains = true;
                options.MaxAge = TimeSpan.FromDays(365);
            });

            services.AddCors();

            services.AddSingleton<TelemetryLogger>();
            services.AddSingleton<TenantTrackLogger>();
            services.AddScoped<TelemetryScopedLogger>();
            services.AddScoped<TelemetryScopedProperties>();

            return services;
        }
    }
}
