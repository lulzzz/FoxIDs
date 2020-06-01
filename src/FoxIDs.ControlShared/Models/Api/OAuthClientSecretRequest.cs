﻿using System.ComponentModel.DataAnnotations;

namespace FoxIDs.Models.Api
{
    /// <summary>
    /// OAuth 2.0 client secret request.
    /// </summary>
    public class OAuthClientSecretRequest
    {
        /// <summary>
        /// OAuth 2.0 or OIDC party name.
        /// </summary>
        [Required]
        [MaxLength(Constants.Models.Party.NameLength)]
        [RegularExpression(Constants.Models.Party.NameRegExPattern)]
        public string PartyName { get; set; }

        /// <summary>
        /// Secret.
        /// </summary>
        [MaxLength(Constants.Models.SecretHash.SecretLength)]
        public string Secret { get; set; }
    }
}
