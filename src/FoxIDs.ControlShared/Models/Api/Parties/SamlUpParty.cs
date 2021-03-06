﻿using FoxIDs.Infrastructure.DataAnnotations;
using ITfoxtec.Identity.Models;
using ITfoxtec.Identity.Saml2.Schemas;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel.Security;

namespace FoxIDs.Models.Api
{
    public class SamlUpParty : INameValue, IClaimTransform<SamlClaimTransform>
    {
        [Required]
        [MaxLength(Constants.Models.Party.NameLength)]
        [RegularExpression(Constants.Models.Party.NameRegExPattern)]
        public string Name { get; set; }

        /// <summary>
        /// Optional custom SP issuer (default auto generated).
        /// </summary>
        [MaxLength(Constants.Models.SamlParty.IssuerLength)]
        public string SpIssuer { get; set; }

        /// <summary>
        /// Claim transforms.
        /// </summary>
        [Length(Constants.Models.Claim.TransformsMin, Constants.Models.Claim.TransformsMax)]
        public List<SamlClaimTransform> ClaimTransforms { get; set; }

        [Length(Constants.Models.SamlParty.ClaimsMin, Constants.Models.SamlParty.ClaimsMax, Constants.Models.Claim.SamlTypeLength, Constants.Models.Claim.SamlTypeRegExPattern)]
        public List<string> Claims { get; set; }

        /// <summary>
        /// Default 20 days.
        /// </summary>
        [Range(Constants.Models.SamlParty.MetadataLifetimeMin, Constants.Models.SamlParty.MetadataLifetimeMax)]
        public int? MetadataLifetime { get; set; } = 1728000;

        /// <summary>
        /// Default SHA256.
        /// </summary>
        [MaxLength(Constants.Models.SamlParty.SignatureAlgorithmLength)]
        public string SignatureAlgorithm { get; set; } = Saml2SecurityAlgorithms.RsaSha256Signature;

        /// <summary>
        /// Default None.
        /// </summary>
        public X509CertificateValidationMode CertificateValidationMode { get; set; } = X509CertificateValidationMode.None;

        /// <summary>
        /// Default NoCheck.
        /// </summary>
        public X509RevocationMode RevocationMode { get; set; } = X509RevocationMode.NoCheck;

        [Required]
        [MaxLength(Constants.Models.SamlParty.IssuerLength)]
        public string Issuer { get; set; }

        [Required]
        public SamlBinding AuthnBinding { get; set; }

        [Required]
        [MaxLength(Constants.Models.SamlParty.Up.AuthnUrlLength)]
        public string AuthnUrl { get; set; }

        [Required]
        [Length(Constants.Models.SamlParty.Up.KeysMin, Constants.Models.SamlParty.KeysMax)]
        public List<JsonWebKey> Keys { get; set; }

        [ValidateComplexType]
        public SamlBinding LogoutBinding { get; set; }

        [MaxLength(Constants.Models.SamlParty.Up.LogoutUrlLength)]
        public string LogoutUrl { get; set; }

        [MaxLength(Constants.Models.SamlParty.Up.LogoutUrlLength)]
        public string SingleLogoutResponseUrl { get; set; }

        /// <summary>
        /// Default 10 hours.
        /// </summary>
        [Range(Constants.Models.UpParty.SessionLifetimeMin, Constants.Models.UpParty.SessionLifetimeMax)]
        public int SessionLifetime { get; set; } = 36000;

        /// <summary>
        /// Default 24 hours.
        /// </summary>
        [Range(Constants.Models.UpParty.SessionAbsoluteLifetimeMin, Constants.Models.UpParty.SessionAbsoluteLifetimeMax)]
        public int SessionAbsoluteLifetime { get; set; } = 86400;

        /// <summary>
        /// Default 0 minutes.
        /// </summary>
        [Range(Constants.Models.UpParty.PersistentAbsoluteSessionLifetimeMin, Constants.Models.UpParty.PersistentAbsoluteSessionLifetimeMax)]
        public int PersistentSessionAbsoluteLifetime { get; set; } = 0;

        /// <summary>
        /// Default false.
        /// </summary>
        public bool PersistentSessionLifetimeUnlimited { get; set; } = false;

        public bool DisableSingleLogout { get; set; }
    }
}
