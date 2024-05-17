﻿using Azure.Core;
using Azure.Identity;
using FoxIDs.Infrastructure.Localization;
using FoxIDs.Logic;
using FoxIDs.Logic.Tracks;
using FoxIDs.Models;
using FoxIDs.Models.Config;
using FoxIDs.Repository;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using StackExchange.Redis;

namespace FoxIDs.Infrastructure.Hosting
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddLogic(this IServiceCollection services, Settings settings)
        {
            services.AddSharedLogic(settings);

            services.AddSingleton<EmbeddedResourceLogic>();
            services.AddSingleton<LocalizationLogic>();

            services.AddTransient<SequenceLogic>();
            services.AddTransient<SecurityHeaderLogic>();
            services.AddTransient<TrackKeyLogic>();
            services.AddTransient<TrackIssuerLogic>();

            services.AddTransient<LoginPageLogic>();
            services.AddTransient<LoginUpLogic>();
            services.AddTransient<LogoutUpLogic>();
            services.AddTransient<SingleLogoutDownLogic>();            
            services.AddTransient<SecretHashLogic>();
            services.AddTransient<FailingLoginLogic>();            
            services.AddTransient<AccountLogic>();
            services.AddTransient<AccountActionLogic>();
            services.AddTransient<AccountTwoFactorLogic>();
            services.AddTransient<ExternalUserLogic>();
            services.AddTransient<SendEmailLogic>();
            services.AddTransient<HrdLogic>();
            services.AddTransient<SessionLoginUpPartyLogic>();
            services.AddTransient<SessionUpPartyLogic>();
            services.AddTransient<ClaimTransformLogic>();
            services.AddTransient<DynamicElementLogic>();

            services.AddTransient<OidcDiscoveryExposeDownLogic<OAuthDownParty, OAuthDownClient, OAuthDownScope, OAuthDownClaim>>();
            services.AddTransient<OidcDiscoveryExposeDownLogic<OidcDownParty, OidcDownClient, OidcDownScope, OidcDownClaim>>();
            services.AddTransient<OidcDiscoveryReadLogic<OAuthUpParty, OAuthUpClient>>();
            services.AddTransient<OidcDiscoveryReadLogic<OidcUpParty, OidcUpClient>>();
            services.AddTransient<OidcDiscoveryReadUpLogic<OAuthUpParty, OAuthUpClient>>();
            services.AddTransient<OidcDiscoveryReadUpLogic<OidcUpParty, OidcUpClient>>();

            services.AddTransient<OAuthJwtDownLogic<OAuthDownClient, OAuthDownScope, OAuthDownClaim>>();
            services.AddTransient<OAuthJwtDownLogic<OidcDownClient, OidcDownScope, OidcDownClaim>>();
            services.AddTransient<OAuthJwtUpLogic<OAuthUpParty, OAuthUpClient>>();
            services.AddTransient<OAuthJwtUpLogic<OidcUpParty, OidcUpClient>>();  
            services.AddTransient<OAuthAuthUpLogic<OAuthUpParty, OAuthUpClient>>();
            services.AddTransient<OAuthAuthUpLogic<OidcUpParty, OidcUpClient>>();
            services.AddTransient<OAuthAuthCodeGrantDownLogic<OAuthDownClient, OAuthDownScope, OAuthDownClaim>>();
            services.AddTransient<OAuthTokenDownLogic<OAuthDownParty, OAuthDownClient, OAuthDownScope, OAuthDownClaim>>();
            services.AddTransient<OAuthTokenDownLogic<OidcDownParty, OidcDownClient, OidcDownScope, OidcDownClaim>>();
            services.AddTransient<OAuthAuthCodeGrantDownLogic<OAuthDownClient, OAuthDownScope, OAuthDownClaim>>();
            services.AddTransient<OAuthAuthCodeGrantDownLogic<OidcDownClient, OidcDownScope, OidcDownClaim>>();
            services.AddTransient<OAuthRefreshTokenGrantDownLogic<OAuthDownClient, OAuthDownScope, OAuthDownClaim>>();
            services.AddTransient<OAuthRefreshTokenGrantDownLogic<OidcDownClient, OidcDownScope, OidcDownClaim>>();
            services.AddTransient<OAuthTokenExchangeDownLogic<OAuthDownParty, OAuthDownClient, OAuthDownScope, OAuthDownClaim>>();
            services.AddTransient<OAuthTokenExchangeDownLogic<OidcDownParty, OidcDownClient, OidcDownScope, OidcDownClaim>>();
            services.AddTransient<OAuthResourceScopeDownLogic<OAuthDownClient, OAuthDownScope, OAuthDownClaim>>();
            services.AddTransient<OAuthResourceScopeDownLogic<OidcDownClient, OidcDownScope, OidcDownClaim>>();

            services.AddTransient<ClientKeySecretLogic<OAuthUpClient>>();
            services.AddTransient<ClientKeySecretLogic<OidcUpClient>>();
            services.AddTransient<OidcJwtDownLogic<OidcDownClient, OidcDownScope, OidcDownClaim>>();
            services.AddTransient<OidcJwtUpLogic<OidcUpParty, OidcUpClient>>();
            services.AddTransient<OidcAuthUpLogic<OidcUpParty, OidcUpClient>>();
            services.AddTransient<OidcAuthDownLogic<OidcDownParty, OidcDownClient, OidcDownScope, OidcDownClaim>>();
            services.AddTransient<OidcTokenDownLogic<OidcDownParty, OidcDownClient, OidcDownScope, OidcDownClaim>>();
            services.AddTransient<OidcUserInfoDownLogic<OidcDownParty, OidcDownClient, OidcDownScope, OidcDownClaim>>();
            services.AddTransient<OidcRpInitiatedLogoutUpLogic<OidcUpParty, OidcUpClient>>();
            services.AddTransient<OidcRpInitiatedLogoutDownLogic<OidcDownParty, OidcDownClient, OidcDownScope, OidcDownClaim>>();
            services.AddTransient<OidcFrontChannelLogoutUpLogic<OidcUpParty, OidcUpClient>>();            
            services.AddTransient<OidcFrontChannelLogoutDownLogic<OidcDownParty, OidcDownClient, OidcDownScope, OidcDownClaim>>();

            services.AddTransient<ClaimsDownLogic>();
            services.AddTransient<ClaimValidationLogic>();
            services.AddTransient<ClaimsOAuthDownLogic<OAuthDownClient, OAuthDownScope, OAuthDownClaim>>();
            services.AddTransient<ClaimsOAuthDownLogic<OidcDownClient, OidcDownScope, OidcDownClaim>>();

            services.AddTransient<SamlClaimsDownLogic>();
            services.AddTransient<Saml2ConfigurationLogic>();
            services.AddTransient<SamlMetadataExposeLogic>();
            services.AddTransient<SamlMetadataReadLogic>();
            services.AddTransient<SamlMetadataReadUpLogic>();
            services.AddTransient<SamlAuthnUpLogic>();
            services.AddTransient<SamlAuthnDownLogic>();
            services.AddTransient<SamlLogoutUpLogic>();
            services.AddTransient<SamlLogoutDownLogic>();

            services.AddTransient<TrackLinkAuthUpLogic>();
            services.AddTransient<TrackLinkAuthDownLogic>();
            services.AddTransient<TrackLinkRpInitiatedLogoutUpLogic>();
            services.AddTransient<TrackLinkRpInitiatedLogoutDownLogic>();
            services.AddTransient<TrackLinkFrontChannelLogoutUpLogic>();
            services.AddTransient<TrackLinkFrontChannelLogoutDownLogic>();

            return services;
        }

        public static IServiceCollection AddRepository(this IServiceCollection services, Settings settings)
        {
            services.AddSharedRepository(settings);

            services.AddTransient(typeof(TrackCookieRepository<>));
            services.AddTransient(typeof(UpPartyCookieRepository<>));

            return services;
        }

        public static IServiceCollection AddInfrastructure(this IServiceCollection services, FoxIDsSettings settings, IWebHostEnvironment environment)
        {
            services.AddSharedInfrastructure(settings, environment);

            services.AddSingleton<IStringLocalizer, FoxIDsStringLocalizer>();
            services.AddSingleton<IStringLocalizerFactory, FoxIDsStringLocalizerFactory>();
            services.AddSingleton<IValidationAttributeAdapterProvider, LocalizedValidationAttributeAdapterProvider>();

            services.AddScoped<FoxIDsRouteTransformer>();
            services.AddScoped<ICorsPolicyProvider, CorsPolicyProvider>();

            if (settings.Options.KeyStorage == KeyStorageOptions.KeyVault)
            {
                if (!environment.IsDevelopment())
                {
                    services.AddSingleton<TokenCredential, DefaultAzureCredential>();
                }
                else
                {
                    services.AddSingleton<TokenCredential>(serviceProvider =>
                    {
                        return new ClientSecretCredential(settings.ServerClientCredential?.TenantId, settings.ServerClientCredential?.ClientId, settings.ServerClientCredential?.ClientSecret);
                    });
                }
            }

            if (settings.Options.Cache == CacheOptions.Redis)
            {
                var connectionMultiplexer = ConnectionMultiplexer.Connect(settings.RedisCache.ConnectionString);
                services.AddSingleton<IConnectionMultiplexer>(connectionMultiplexer);

                services.AddDataProtection()
                    .PersistKeysToStackExchangeRedis(connectionMultiplexer, "data_protection_keys");

                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = settings.RedisCache.ConnectionString;
                    options.InstanceName = "cache";
                });
            }

            return services;
        }
    }
}
