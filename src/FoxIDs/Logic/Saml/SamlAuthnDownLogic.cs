﻿using ITfoxtec.Identity;
using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.Claims;
using ITfoxtec.Identity.Saml2.MvcCore;
using ITfoxtec.Identity.Saml2.Schemas;
using FoxIDs.Infrastructure;
using FoxIDs.Infrastructure.Saml2;
using FoxIDs.Models;
using FoxIDs.Models.Config;
using FoxIDs.Models.Logic;
using FoxIDs.Repository;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens.Saml2;
using FoxIDs.Models.Sequences;
using FoxIDs.Models.Session;

namespace FoxIDs.Logic
{
    public class SamlAuthnDownLogic : LogicBase
    {
        private readonly FoxIDsSettings settings;
        private readonly TelemetryScopedLogger logger;
        private readonly IServiceProvider serviceProvider;
        private readonly ITenantRepository tenantRepository;
        private readonly SequenceLogic sequenceLogic;
        private readonly SecurityHeaderLogic securityHeaderLogic;
        private readonly ClaimTransformationsLogic claimTransformationsLogic;
        private readonly SamlClaimsDownLogic samlClaimsDownLogic;
        private readonly ClaimsDownLogic<OidcDownClient, OidcDownScope, OidcDownClaim> claimsDownLogic;
        private readonly Saml2ConfigurationLogic saml2ConfigurationLogic;

        public SamlAuthnDownLogic(FoxIDsSettings settings, TelemetryScopedLogger logger, IServiceProvider serviceProvider, ITenantRepository tenantRepository, SequenceLogic sequenceLogic, SecurityHeaderLogic securityHeaderLogic, ClaimTransformationsLogic claimTransformationsLogic, SamlClaimsDownLogic samlClaimsDownLogic, ClaimsDownLogic<OidcDownClient, OidcDownScope, OidcDownClaim> claimsDownLogic, Saml2ConfigurationLogic saml2ConfigurationLogic, IHttpContextAccessor httpContextAccessor) : base(httpContextAccessor)
        {
            this.settings = settings;
            this.logger = logger;
            this.serviceProvider = serviceProvider;
            this.tenantRepository = tenantRepository;
            this.sequenceLogic = sequenceLogic;
            this.securityHeaderLogic = securityHeaderLogic;
            this.claimTransformationsLogic = claimTransformationsLogic;
            this.samlClaimsDownLogic = samlClaimsDownLogic;
            this.claimsDownLogic = claimsDownLogic;
            this.saml2ConfigurationLogic = saml2ConfigurationLogic;
        }

        public async Task<IActionResult> AuthnRequestAsync(string partyId)
        {
            logger.ScopeTrace("Down, SAML Authn request.");
            logger.SetScopeProperty("downPartyId", partyId);
            var party = await tenantRepository.GetAsync<SamlDownParty>(partyId);

            switch (party.AuthnBinding.RequestBinding)
            {
                case SamlBindingTypes.Redirect:
                    return await AuthnRequestAsync(party, new Saml2RedirectBinding());
                case SamlBindingTypes.Post:
                    return await AuthnRequestAsync(party, new Saml2PostBinding());
                default:
                    throw new NotSupportedException($"Binding '{party.AuthnBinding.RequestBinding}' not supported.");
            }
        }

        private async Task<IActionResult> AuthnRequestAsync<T>(SamlDownParty party, Saml2Binding<T> binding)
        {
            var samlConfig = saml2ConfigurationLogic.GetSamlDownConfig(party);
            var request = HttpContext.Request;

            var saml2AuthnRequest = new Saml2AuthnRequest(samlConfig);
            binding.ReadSamlRequest(request.ToGenericHttpRequest(), saml2AuthnRequest);
            logger.ScopeTrace($"SAML Authn request '{saml2AuthnRequest.XmlDocument.OuterXml}'.");

            try
            {
                ValidateAuthnRequest(party, saml2AuthnRequest);
                binding.Unbind(request.ToGenericHttpRequest(), saml2AuthnRequest);
                logger.ScopeTrace("Down, SAML Authn request accepted.", triggerEvent: true);

                await sequenceLogic.SaveSequenceDataAsync(new SamlDownSequenceData
                {
                    Id = saml2AuthnRequest.Id.Value,
                    RelayState = binding.RelayState,
                    AcsResponseUrl = GetAcsUrl(party, saml2AuthnRequest),
                });

                var type = RouteBinding.ToUpParties.First().Type;
                logger.ScopeTrace($"Request, Up type '{type}'.");
                switch (type)
                {
                    case PartyTypes.Login:
                        return await serviceProvider.GetService<LoginUpLogic>().LoginRedirectAsync(RouteBinding.ToUpParties.First(), GetLoginRequestAsync(party, saml2AuthnRequest));
                    case PartyTypes.OAuth2:
                        throw new NotImplementedException();
                    case PartyTypes.Oidc:
                        return await serviceProvider.GetService<OidcAuthUpLogic<OidcUpParty, OidcUpClient>>().AuthenticationRequestRedirectAsync(RouteBinding.ToUpParties.First(), GetLoginRequestAsync(party, saml2AuthnRequest));
                    case PartyTypes.Saml2:
                        return await serviceProvider.GetService<SamlAuthnUpLogic>().AuthnRequestRedirectAsync(RouteBinding.ToUpParties.First(), GetLoginRequestAsync(party, saml2AuthnRequest));

                    default:
                        throw new NotSupportedException($"Party type '{type}' not supported.");
                }
            }
            catch (SamlRequestException ex)
            {
                logger.Error(ex);
                return await AuthnResponseAsync(party, samlConfig, saml2AuthnRequest.Id.Value, binding.RelayState, GetAcsUrl(party, saml2AuthnRequest), ex.Status);
            }
        }

        private string GetAcsUrl(SamlDownParty party, Saml2AuthnRequest saml2AuthnRequest)
        {
            if (saml2AuthnRequest.AssertionConsumerServiceUrl != null)
            {
                return saml2AuthnRequest.AssertionConsumerServiceUrl.OriginalString;
            }
            else
            {
                return party.AcsUrls.First();
            }
        }

        private void ValidateAuthnRequest(SamlDownParty party, Saml2AuthnRequest saml2AuthnRequest)
        {
            if (saml2AuthnRequest.AssertionConsumerServiceUrl != null && !party.AcsUrls.Any(u => u.Equals(saml2AuthnRequest.AssertionConsumerServiceUrl.OriginalString, StringComparison.InvariantCultureIgnoreCase)))
            {
                throw new EndpointException($"Invalid assertion consumer service URL '{saml2AuthnRequest.AssertionConsumerServiceUrl.OriginalString}'.") { RouteBinding = RouteBinding };
            }

            var requestIssuer = saml2AuthnRequest.Issuer;
            logger.SetScopeProperty("Issuer", requestIssuer);

            if (!party.Issuer.Equals(requestIssuer))
            {
                throw new SamlRequestException($"Invalid issuer '{requestIssuer}'.") { RouteBinding = RouteBinding, Status = Saml2StatusCodes.Responder };
            }
        }

        private LoginRequest GetLoginRequestAsync(SamlDownParty party, Saml2AuthnRequest saml2AuthnRequest)
        {
            var loginRequest = new LoginRequest { DownPartyLink = new DownPartySessionLink { SupportSingleLogout = !string.IsNullOrWhiteSpace(party.SingleLogoutUrl), Id = party.Id, Type = party.Type } };

            if(saml2AuthnRequest.ForceAuthn.HasValue && saml2AuthnRequest.ForceAuthn.Value)
            {
                loginRequest.LoginAction = LoginAction.RequireLogin;
            }
            else if(saml2AuthnRequest.IsPassive.HasValue && saml2AuthnRequest.IsPassive.Value)
            {
                loginRequest.LoginAction = LoginAction.ReadSession;
            }
            else
            {
                loginRequest.LoginAction = LoginAction.ReadSessionOrLogin;
            }

            return loginRequest;
        }

        public async Task<IActionResult> AuthnResponseAsync(string partyId, Saml2StatusCodes status = Saml2StatusCodes.Success, IEnumerable<Claim> jwtClaims = null)
        {
            logger.ScopeTrace($"Down, SAML Authn response{(status != Saml2StatusCodes.Success ? " error" : string.Empty )}, Status code '{status}'.");
            logger.SetScopeProperty("downPartyId", partyId);

            var party = await tenantRepository.GetAsync<SamlDownParty>(partyId);

            var samlConfig = saml2ConfigurationLogic.GetSamlDownConfig(party, true);
            var sequenceData = await sequenceLogic.GetSequenceDataAsync<SamlDownSequenceData>(false);
            var claims = jwtClaims != null ? await claimsDownLogic.FromJwtToSamlClaimsAsync(jwtClaims) : null;
            return await AuthnResponseAsync(party, samlConfig, sequenceData.Id, sequenceData.RelayState, sequenceData.AcsResponseUrl, status, claims);
        }

        private Task<IActionResult> AuthnResponseAsync(SamlDownParty party, Saml2Configuration samlConfig, string inResponseTo, string relayState, string acsUrl, Saml2StatusCodes status, IEnumerable<Claim> claims = null) 
        {
            logger.ScopeTrace($"Down, SAML Authn response{(status != Saml2StatusCodes.Success ? " error" : string.Empty)}, Status code '{status}'.");

            var binding = party.AuthnBinding.ResponseBinding;
            logger.ScopeTrace($"Binding '{binding}'");
            switch (binding)
            {
                case SamlBindingTypes.Redirect:
                    return AuthnResponseAsync( party, samlConfig, inResponseTo, relayState, acsUrl, new Saml2RedirectBinding(), status,claims);
                case SamlBindingTypes.Post:
                    return AuthnResponseAsync(party, samlConfig, inResponseTo, relayState, acsUrl, new Saml2PostBinding(), status, claims);
                default:
                    throw new NotSupportedException($"SAML binding '{binding}' not supported.");
            }
        }

        private async Task<IActionResult> AuthnResponseAsync<T>(SamlDownParty party, Saml2Configuration samlConfig, string inResponseTo, string relayState, string acsUrl, Saml2Binding<T> binding, Saml2StatusCodes status, IEnumerable<Claim> claims)
        {
            binding.RelayState = relayState;

            var saml2AuthnResponse = new FoxIDsSaml2AuthnResponse(settings, samlConfig)
            {
                InResponseTo = new Saml2Id(inResponseTo),
                Status = status,
                Destination = new Uri(acsUrl),
            };
            if (status == Saml2StatusCodes.Success && party != null && claims != null)
            {
                claims = await claimTransformationsLogic.Transform(party.ClaimTransforms?.ConvertAll(t => (ClaimTransform)t), claims);

                saml2AuthnResponse.SessionIndex = samlClaimsDownLogic.GetSessionIndex(claims);

                saml2AuthnResponse.NameId = samlClaimsDownLogic.GetNameId(claims);

                var tokenIssueTime = DateTimeOffset.UtcNow;
                var tokenDescriptor = saml2AuthnResponse.CreateTokenDescriptor(samlClaimsDownLogic.GetSubjectClaims(party, claims), party.Issuer, tokenIssueTime, party.IssuedTokenLifetime);

                var authnContext = claims.FindFirstValue(c => c.Type == ClaimTypes.AuthenticationMethod);
                var authenticationInstant = claims.FindFirstValue(c => c.Type == ClaimTypes.AuthenticationInstant);
                var authenticationStatement = saml2AuthnResponse.CreateAuthenticationStatement(authnContext, DateTime.Parse(authenticationInstant));

                var subjectConfirmation = saml2AuthnResponse.CreateSubjectConfirmation(tokenIssueTime, party.SubjectConfirmationLifetime);

                await saml2AuthnResponse.CreateSecurityTokenAsync(tokenDescriptor, authenticationStatement, subjectConfirmation);
            }

            binding.Bind(saml2AuthnResponse);
            logger.ScopeTrace($"SAML Authn response '{saml2AuthnResponse.XmlDocument.OuterXml}'.");
            logger.ScopeTrace($"Acs URL '{acsUrl}'.");
            logger.ScopeTrace("Down, SAML Authn response.", triggerEvent: true);

            await sequenceLogic.RemoveSequenceDataAsync<SamlDownSequenceData>();
            securityHeaderLogic.AddFormAction(acsUrl);
            if (binding is Saml2Binding<Saml2RedirectBinding>)
            {
                return await (binding as Saml2RedirectBinding).ToActionFormResultAsync();
            }
            else if (binding is Saml2Binding<Saml2PostBinding>)
            {
                return await (binding as Saml2PostBinding).ToActionFormResultAsync();
            }
            else
            {
                throw new NotSupportedException();
            }
        }
    }
}
