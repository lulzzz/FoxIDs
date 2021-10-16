﻿using FoxIDs.Infrastructure;
using FoxIDs.Models;
using ITfoxtec.Identity;
using ITfoxtec.Identity.Saml2.Claims;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FoxIDs.Logic
{
    public class ClaimTransformLogic : LogicBase
    {
        private readonly TelemetryScopedLogger logger;
        private readonly ClaimTransformValidationLogic claimTransformValidationLogic;

        public ClaimTransformLogic(TelemetryScopedLogger logger, ClaimTransformValidationLogic claimTransformValidationLogic, IHttpContextAccessor httpContextAccessor) : base(httpContextAccessor)
        {
            this.logger = logger;
            this.claimTransformValidationLogic = claimTransformValidationLogic;
        }

        public Task<List<Claim>> Transform(IEnumerable<ClaimTransform> claimTransforms, IEnumerable<Claim> claims)
        {
            if(claimTransforms == null|| claims == null)
            {
                return Task.FromResult(new List<Claim>(claims));
            }

            claimTransformValidationLogic.ValidateAndPrepareClaimTransforms(claimTransforms);

            logger.ScopeTrace(() => "Transform claims.");
            var outputClaims = new List<Claim>(claims);
            var orderedClaimTransforms = claimTransforms.OrderBy(t => t.Order);
            foreach(var claimTransform in orderedClaimTransforms)
            {
                try
                {
                    switch (claimTransform.Type)
                    {
                        case ClaimTransformTypes.Constant:
                            ConstantTransformation(outputClaims, claimTransform);
                            break;
                        case ClaimTransformTypes.MatchClaim:
                            MatchClaimTransformation(outputClaims, claimTransform);
                            break;
                        case ClaimTransformTypes.Match:
                            MatchTransformation(outputClaims, claimTransform);
                            break;
                        case ClaimTransformTypes.RegexMatch:
                            RegexMatchTransformation(outputClaims, claimTransform);
                            break;
                        case ClaimTransformTypes.Map:
                            MapTransformation(outputClaims, claimTransform);
                            break;
                        case ClaimTransformTypes.RegexMap:
                            RegexMapTransformation(outputClaims, claimTransform);
                            break;
                        case ClaimTransformTypes.Concatenate:
                            ConcatenateTransformation(outputClaims, claimTransform);
                            break;
                        default:
                            throw new NotSupportedException($"Claim transform type '{claimTransform.Type}' not supported.");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Claim transform type '{claimTransform.Type}' with output claim '{claimTransform.ClaimOut}' failed.", ex);
                }
            }
            return Task.FromResult(outputClaims);
        }

        private string[] ReplaceClaimOutJwtTypes = new [] { JwtClaimTypes.Subject, JwtClaimTypes.SessionId, JwtClaimTypes.AuthTime, JwtClaimTypes.Acr, JwtClaimTypes.Amr, JwtClaimTypes.ExpirationTime, JwtClaimTypes.NotBefore, JwtClaimTypes.IssuedAt, JwtClaimTypes.Nonce, JwtClaimTypes.Azp, JwtClaimTypes.AtHash, JwtClaimTypes.CHash };
        private string[] ReplaceClaimOutSamlTypes = new[] { ClaimTypes.NameIdentifier, Saml2ClaimTypes.NameIdFormat, Saml2ClaimTypes.SessionIndex, ClaimTypes.Upn, ClaimTypes.AuthenticationInstant, ClaimTypes.AuthenticationMethod };

        private static void UpdateClaims(List<Claim> outputClaims, ClaimTransform claimTransform, Claim newClaim)
        {
            switch (claimTransform.Action)
            {
                case ClaimTransformActions.Add:
                    outputClaims.Add(newClaim);
                    break;
                case ClaimTransformActions.Replace:
                    outputClaims.RemoveAll(c => claimTransform.ClaimOut.Equals(c.Type, StringComparison.OrdinalIgnoreCase));
                    outputClaims.Add(newClaim);
                    break;
                case ClaimTransformActions.Remove:
                    throw new NotImplementedException();
                    break;
                default:
                    throw new NotImplementedException();
                    break;
            }
        }

        private static void UpdateClaims(List<Claim> outputClaims, ClaimTransform claimTransform, List<Claim> newClaims)
        {
            if (newClaims.Count() > 0)
            {
                switch (claimTransform.Action)
                {
                    case ClaimTransformActions.Add:
                        outputClaims.AddRange(newClaims);
                        break;
                    case ClaimTransformActions.Replace:
                        outputClaims.RemoveAll(c => claimTransform.ClaimOut.Equals(c.Type, StringComparison.OrdinalIgnoreCase));
                        outputClaims.AddRange(newClaims);
                        break;
                    case ClaimTransformActions.Remove:
                        throw new NotImplementedException();
                        break;
                    default:
                        throw new NotImplementedException();
                        break;
                }
            }
        }

        private void ConstantTransformation(List<Claim> claims, ClaimTransform claimTransform)
        {
            var newClaim = new Claim(claimTransform.ClaimOut, claimTransform.Transformation);
            UpdateClaims(claims, claimTransform, newClaim);
        }

        private void MatchClaimTransformation(List<Claim> claims, ClaimTransform claimTransform)
        {
            var newClaims = new List<Claim>();
            foreach (var claim in claims)
            {
                if (claim.Type.Equals(claimTransform.ClaimsIn.Single(), StringComparison.OrdinalIgnoreCase))
                {
                    newClaims.Add(new Claim(claimTransform.ClaimOut, claimTransform.Transformation));
                }
            }
            UpdateClaims(claims, claimTransform, newClaims);
        }

        private void MatchTransformation(List<Claim> claims, ClaimTransform claimTransform)
        {
            var newClaims = new List<Claim>();
            foreach (var claim in claims)
            {
                if(claim.Type.Equals(claimTransform.ClaimsIn.Single(), StringComparison.OrdinalIgnoreCase) && claim.Value.Equals(claimTransform.Transformation, StringComparison.OrdinalIgnoreCase))
                {
                    newClaims.Add(new Claim(claimTransform.ClaimOut, claimTransform.TransformationExtension));
                }
            }
            UpdateClaims(claims, claimTransform, newClaims);
        }

        private void RegexMatchTransformation(List<Claim> claims, ClaimTransform claimTransform)
        {
            var newClaims = new List<Claim>();
            var regex = new Regex(claimTransform.Transformation, RegexOptions.IgnoreCase);
            var claimIn = claimTransform.ClaimsIn.Single();
            foreach (var claim in claims)
            {
                if (claim.Type.Equals(claimIn, StringComparison.OrdinalIgnoreCase))
                {
                    var match = regex.Match(claim.Value);
                    if(match.Success)
                    {
                        newClaims.Add(new Claim(claimTransform.ClaimOut, claimTransform.TransformationExtension));
                    }
                }
            }
            UpdateClaims(claims, claimTransform, newClaims);
        }

        private void MapTransformation(List<Claim> claims, ClaimTransform claimTransform)
        {
            var newClaims = new List<Claim>();
            var claimIn = claimTransform.ClaimsIn.Single();
            foreach (var claim in claims)
            {
                if (claim.Type.Equals(claimIn, StringComparison.OrdinalIgnoreCase))
                {
                    newClaims.Add(new Claim(claimTransform.ClaimOut, claim.Value));
                }
            }
            UpdateClaims(claims, claimTransform, newClaims);
        }

        private void RegexMapTransformation(List<Claim> claims, ClaimTransform claimTransform)
        {
            var newClaims = new List<Claim>();
            var regex = new Regex(claimTransform.Transformation, RegexOptions.IgnoreCase);
            var claimIn = claimTransform.ClaimsIn.Single();
            foreach (var claim in claims)
            {
                if (claim.Type.Equals(claimIn, StringComparison.OrdinalIgnoreCase))
                {
                    var match = regex.Match(claim.Value);
                    if (match.Success && match.Groups.ContainsKey("map"))
                    {
                        newClaims.Add(new Claim(claimTransform.ClaimOut, match.Groups["map"].Value));
                    }
                }
            }
            UpdateClaims(claims, claimTransform, newClaims);
        }

        private void ConcatenateTransformation(List<Claim> claims, ClaimTransform claimTransform)
        {
            var newClaims = new List<Claim>();
            var addTransformationClaim = false;
            var values = new string[claimTransform.ClaimsIn.Count()];
            int i = 0;
            foreach (var claimIn in claimTransform.ClaimsIn)
            {
                var value = claims.Where(c => c.Type.Equals(claimIn, StringComparison.OrdinalIgnoreCase)).Select(c => c.Value).FirstOrDefault();
                if(value != null)
                {
                    addTransformationClaim = true;
                    values[i++] = value;
                }
                else
                {
                    values[i++] = string.Empty;
                }
            }

            if(addTransformationClaim)
            {
                var transformationValue = string.Format(claimTransform.Transformation, values);
                newClaims.Add(new Claim(claimTransform.ClaimOut, transformationValue));
            }
            UpdateClaims(claims, claimTransform, newClaims);
        }
    }
}
