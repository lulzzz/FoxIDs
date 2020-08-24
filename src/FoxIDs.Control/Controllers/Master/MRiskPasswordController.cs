﻿using FoxIDs.Infrastructure;
using FoxIDs.Models;
using Api = FoxIDs.Models.Api;
using FoxIDs.Repository;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using AutoMapper;

namespace FoxIDs.Controllers
{
    public class MRiskPasswordController : MasterApiController
    {
        private readonly TelemetryScopedLogger logger;
        private readonly IMapper mapper;
        private readonly IMasterRepository masterRepository;

        public MRiskPasswordController(TelemetryScopedLogger logger, IMapper mapper, IMasterRepository masterRepository) : base(logger)
        {
            this.logger = logger;
            this.mapper = mapper;
            this.masterRepository = masterRepository;
        }

        /// <summary>
        /// Get risk password.
        /// </summary>
        /// <param name="passwordSha1Hash">Password SHA1 hash.</param>
        /// <returns>Risk password.</returns>
        [ProducesResponseType(typeof(Api.RiskPassword), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<Api.RiskPassword>> GetRiskPassword(string passwordSha1Hash)
        {
            try
            {
                if (!ModelState.TryValidateRequiredParameter(passwordSha1Hash, nameof(passwordSha1Hash))) return BadRequest(ModelState);

                var mRiskPassword = await masterRepository.GetAsync<RiskPassword>(await RiskPassword.IdFormat(passwordSha1Hash));
                return Ok(mapper.Map<Api.RiskPassword>(mRiskPassword));
            }
            catch (CosmosDataException ex)
            {
                if (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    logger.Warning(ex, $"NotFound, Get '{typeof(Api.RiskPassword).Name}' by password SHA1 hash '{passwordSha1Hash}'.");
                    return NotFound(typeof(Api.RiskPassword).Name, passwordSha1Hash);
                }
                throw;
            }
        }

        /// <summary>
        /// Create risk passwords.
        /// </summary>
        /// <param name="riskPasswordRequest">Risk passwords.</param>
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> PostRiskPassword([FromBody] Api.RiskPasswordRequest riskPasswordRequest)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var riskPasswords = new List<RiskPassword>();
            foreach (var item in riskPasswordRequest.RiskPasswords)
            {
                riskPasswords.Add(new RiskPassword
                {
                    Id = await RiskPassword.IdFormat(item.PasswordSha1Hash),
                    Count = item.Count,
                    CreateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                });
            }

            await masterRepository.SaveBulkAsync(riskPasswords);

            return NoContent();
        }

        /// <summary>
        /// Delete risk password.
        /// </summary>
        /// <param name="passwordSha1Hash">Password SHA1 hash.</param>
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteRiskPassword(string passwordSha1Hash)
        {
            try
            {
                //TODO delete all elements
                // Waiting for https://feedback.azure.com/forums/263030-azure-cosmos-db/suggestions/17296813-add-the-ability-to-delete-all-data-in-a-partition
                //            Add the ability to delete ALL data in a partition
                var passwordRiskList = new RiskPassword { Id = await RiskPassword.IdFormat(passwordSha1Hash) };
                await masterRepository.DeleteAsync(passwordRiskList);
                return NoContent();
            }
            catch (CosmosDataException ex)
            {
                if (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    logger.Warning(ex, $"Delete by password sha1 hash '{passwordSha1Hash}'.");
                    return NotFound();
                }
                throw;
            }
        }
    }
}