﻿using System;
using System.Linq;
using System.Threading.Tasks;
using ITfoxtec.Identity;
using ITfoxtec.Identity.Messages;
using FoxIDs.Infrastructure;
using FoxIDs.Models;
using FoxIDs.Repository;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Security.Claims;
using ITfoxtec.Identity.Saml2.Claims;
using FoxIDs.Models.Logic;
using FoxIDs.Models.Sequences;
using FoxIDs.Models.Session;

namespace FoxIDs.Logic
{
    public class OidcRpInitiatedLogoutDownLogic<TParty, TClient, TScope, TClaim> : LogicBase where TParty : OidcDownParty<TClient, TScope, TClaim> where TClient : OidcDownClient<TScope, TClaim> where TScope : OidcDownScope<TClaim> where TClaim : OidcDownClaim
    {
        private readonly TelemetryScopedLogger logger;
        private readonly IServiceProvider serviceProvider;
        private readonly ITenantRepository tenantRepository;
        private readonly SequenceLogic sequenceLogic;
        private readonly SecurityHeaderLogic securityHeaderLogic;
        private readonly JwtDownLogic<TClient, TScope, TClaim> jwtDownLogic;

        public OidcRpInitiatedLogoutDownLogic(TelemetryScopedLogger logger, IServiceProvider serviceProvider, ITenantRepository tenantRepository, SequenceLogic sequenceLogic, SecurityHeaderLogic securityHeaderLogic, JwtDownLogic<TClient, TScope, TClaim> jwtDownLogic, IHttpContextAccessor httpContextAccessor) : base(httpContextAccessor)
        {
            this.logger = logger;
            this.serviceProvider = serviceProvider;
            this.tenantRepository = tenantRepository;
            this.sequenceLogic = sequenceLogic;
            this.securityHeaderLogic = securityHeaderLogic;
            this.jwtDownLogic = jwtDownLogic;
        }

        public async Task<IActionResult> EndSessionRequestAsync(string partyId)
        {
            logger.ScopeTrace("Down, End session request.");
            logger.SetScopeProperty("downPartyId", partyId);
            var party = await tenantRepository.GetAsync<TParty>(partyId);
            if (party.Client == null)
            {
                throw new NotSupportedException("Party Client not configured.");
            }

            var formOrQueryDictionary = HttpContext.Request.Method switch
            {
                "POST" => party.Client.ResponseMode == IdentityConstants.ResponseModes.FormPost ? HttpContext.Request.Form.ToDictionary() : throw new NotSupportedException($"POST not supported by response mode '{party.Client.ResponseMode}'."),
                "GET" => party.Client.ResponseMode == IdentityConstants.ResponseModes.Query ? HttpContext.Request.Query.ToDictionary() : throw new NotSupportedException($"GET not supported by response mode '{party.Client.ResponseMode}'."),
                _ => throw new NotSupportedException($"Request method not supported by response mode '{party.Client.ResponseMode}'")
            };
           
            var rpInitiatedLogoutRequest = formOrQueryDictionary.ToObject<RpInitiatedLogoutRequest>();

            logger.ScopeTrace($"end session request '{rpInitiatedLogoutRequest.ToJsonIndented()}'.");
            logger.SetScopeProperty("downPartyClientId", party.Client.ClientId);

            ValidateEndSessionRequest(party.Client, rpInitiatedLogoutRequest);
            logger.ScopeTrace("Down, OIDC End session request accepted.", triggerEvent: true);

            (var validIdToken, var sessionId, var idTokenClaims) = await ValidateIdTokenHintAsync(party.Client, rpInitiatedLogoutRequest.IdTokenHint);
            if (!validIdToken)
            {
                if (party.Client.RequireLogoutIdTokenHint)
                {
                    if (!rpInitiatedLogoutRequest.IdTokenHint.IsNullOrEmpty())
                    {
                        throw new OAuthRequestException($"Invalid ID Token hint.") { RouteBinding = RouteBinding };
                    }

                    throw new OAuthRequestException($"ID Token hint is required.") { RouteBinding = RouteBinding };
                }
            }
            else
            {
                logger.ScopeTrace("Valid ID token hint.");
            }

            var postLogoutRedirectUri = !rpInitiatedLogoutRequest.PostLogoutRedirectUri.IsNullOrWhiteSpace() ? rpInitiatedLogoutRequest.PostLogoutRedirectUri : party.Client.PostLogoutRedirectUri;
            await sequenceLogic.SaveSequenceDataAsync(new OidcDownSequenceData
            {
                RedirectUri = postLogoutRedirectUri,
                State = rpInitiatedLogoutRequest.State,
            });

            var toUpPartie = GetToUpParty(idTokenClaims);
            logger.ScopeTrace($"Request, Up type '{toUpPartie.Type}'.");
            switch (toUpPartie.Type)
            {
                case PartyTypes.Login:
                    return await serviceProvider.GetService<LogoutUpLogic>().LogoutRedirect(toUpPartie, GetLogoutRequest(party, sessionId, validIdToken, postLogoutRedirectUri));
                case PartyTypes.OAuth2:
                    throw new NotImplementedException();
                case PartyTypes.Oidc:
                    return await serviceProvider.GetService<OidcRpInitiatedLogoutUpLogic<OidcUpParty, OidcUpClient>>().EndSessionRequestRedirectAsync(toUpPartie, GetLogoutRequest(party, sessionId, validIdToken, postLogoutRedirectUri));
                case PartyTypes.Saml2:
                    if (!validIdToken)
                    {
                        throw new OAuthRequestException($"ID Token hint is required for SAML 2.0 Up-party.") { RouteBinding = RouteBinding };
                    }
                    return await serviceProvider.GetService<SamlLogoutUpLogic>().LogoutRequestRedirectAsync(toUpPartie, GetSamlLogoutRequest( party, sessionId, idTokenClaims));

                default:
                    throw new NotSupportedException($"Party type '{toUpPartie.Type}' not supported.");
            }
        }

        private UpPartyLink GetToUpParty(IEnumerable<Claim> idTokenClaims)
        {
            if (RouteBinding.DefaultToUpParties)
            {
                var upPartyName = idTokenClaims.FindFirstValue(c => c.Type == Constants.JwtClaimTypes.UpPary);
                var upPartyTypeValue = idTokenClaims.FindFirstValue(c => c.Type == Constants.JwtClaimTypes.UpParyType);
                if (!upPartyName.IsNullOrWhiteSpace() && !upPartyTypeValue.IsNullOrWhiteSpace() && Enum.TryParse(upPartyTypeValue, true, out PartyTypes upPartyType))
                {
                    return new UpPartyLink { Name = upPartyName, Type = upPartyType };
                }
            }

            return RouteBinding.ToUpParties.First();
        }

        private LogoutRequest GetLogoutRequest(TParty party, string sessionId, bool validIdToken, string postLogoutRedirectUri)
        {
            var logoutRequest = new LogoutRequest
            {
                DownPartyLink = new DownPartySessionLink { SupportSingleLogout = !string.IsNullOrWhiteSpace(party.Client?.FrontChannelLogoutUri), Id = party.Id, Type = party.Type },
                SessionId = sessionId,
                RequireLogoutConsent = !validIdToken,
                PostLogoutRedirect = !postLogoutRedirectUri.IsNullOrWhiteSpace()
            };

            return logoutRequest;
        }

        private LogoutRequest GetSamlLogoutRequest(TParty party, string sessionId, IEnumerable<Claim> idTokenClaims)
        {
            var samlClaims = new List<Claim>();
            var nameIdClaim = idTokenClaims.FirstOrDefault(c => c.Type == JwtClaimTypes.Subject);
            if(nameIdClaim == null)
            {
                throw new OAuthRequestException($"Require '{JwtClaimTypes.Subject}' claim in ID Token hint.") { RouteBinding = RouteBinding };
            }
            samlClaims.AddClaim(Saml2ClaimTypes.NameId, nameIdClaim.Value);

            var subFormatClaim = idTokenClaims.FirstOrDefault(c => c.Type == Constants.JwtClaimTypes.SubFormat);
            if (subFormatClaim != null)
            {
                samlClaims.AddClaim(Saml2ClaimTypes.NameIdFormat, subFormatClaim.Value);
            }

            return new LogoutRequest
            {
                DownPartyLink = new DownPartySessionLink { SupportSingleLogout = !string.IsNullOrWhiteSpace(party.Client?.FrontChannelLogoutUri), Id = party.Id, Type = party.Type },
                SessionId = sessionId,
                RequireLogoutConsent = false,
                PostLogoutRedirect = true,
                Claims = samlClaims,
            };
        }

        private async Task<(bool, string, IEnumerable<Claim>)> ValidateIdTokenHintAsync(TClient client, string idToken)
        {
            if (!idToken.IsNullOrEmpty())
            {
                var claimsPrincipal = await jwtDownLogic.ValidatePartyClientTokenAsync(client, idToken, validateLifetime: false);
                if (claimsPrincipal != null)
                {
                    return (true, claimsPrincipal.FindFirstValue(JwtClaimTypes.SessionId), claimsPrincipal.Claims);
                }
            }
            return (false, null, null);
        }

        private void ValidateEndSessionRequest(TClient client, RpInitiatedLogoutRequest rpInitiatedLogoutRequest)
        {
            rpInitiatedLogoutRequest.Validate();

            if (!rpInitiatedLogoutRequest.PostLogoutRedirectUri.IsNullOrWhiteSpace() && 
                !client.RedirectUris.Any(u => u.Equals(rpInitiatedLogoutRequest.PostLogoutRedirectUri, StringComparison.InvariantCultureIgnoreCase)) &&
                !rpInitiatedLogoutRequest.PostLogoutRedirectUri.Equals(rpInitiatedLogoutRequest.PostLogoutRedirectUri, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new OAuthRequestException($"Invalid post logout redirect Uri '{rpInitiatedLogoutRequest.PostLogoutRedirectUri}'.");
            }
        }

        public async Task<IActionResult> EndSessionResponseAsync(string partyId)
        {
            logger.ScopeTrace("Down, End session response.");
            logger.SetScopeProperty("downPartyId", partyId);

            var sequenceData = await sequenceLogic.GetSequenceDataAsync<OidcDownSequenceData>(false);

            var rpInitiatedLogoutResponse = new RpInitiatedLogoutResponse
            {
                State = sequenceData.State,
            };

            logger.ScopeTrace($"End session response '{rpInitiatedLogoutResponse.ToJsonIndented()}'.");
            var nameValueCollection = rpInitiatedLogoutResponse.ToDictionary();

            logger.ScopeTrace($"Redirect Uri '{sequenceData.RedirectUri}'.");
            logger.ScopeTrace("Down, OIDC End session response.", triggerEvent: true);

            await sequenceLogic.RemoveSequenceDataAsync<OidcDownSequenceData>();
            securityHeaderLogic.AddFormAction(sequenceData.RedirectUri);
            return await nameValueCollection.ToRedirectResultAsync(sequenceData.RedirectUri);
        }
    }
}
