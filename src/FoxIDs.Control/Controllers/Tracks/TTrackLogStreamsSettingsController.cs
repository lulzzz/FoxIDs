﻿using AutoMapper;
using FoxIDs.Infrastructure;
using FoxIDs.Repository;
using FoxIDs.Models;
using Api = FoxIDs.Models.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Collections.Generic;
using FoxIDs.Logic;
using FoxIDs.Infrastructure.Security;
using FoxIDs.Models.Config;

namespace FoxIDs.Controllers
{
    [TenantScopeAuthorize(Constants.ControlApi.Segment.Log)]
    public class TTrackLogStreamsSettingsController : ApiController
    {
        private readonly FoxIDsControlSettings settings;
        private readonly TelemetryScopedLogger logger;
        private readonly IMapper mapper;
        private readonly ITenantDataRepository tenantDataRepository;
        private readonly TrackCacheLogic trackCacheLogic;

        public TTrackLogStreamsSettingsController(FoxIDsControlSettings settings, TelemetryScopedLogger logger, IMapper mapper, ITenantDataRepository tenantDataRepository, TrackCacheLogic trackCacheLogic) : base(logger)
        {
            this.settings = settings;
            this.logger = logger;
            this.mapper = mapper;
            this.tenantDataRepository = tenantDataRepository;
            this.trackCacheLogic = trackCacheLogic;
        }

        /// <summary>
        /// Get all environment log stream settings.
        /// </summary>
        /// <returns>All log stream settings.</returns>
        [ProducesResponseType(typeof(Api.LogStreams), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<Api.LogStreams>> GetTrackLogStreamsSettings()
        {
            try
            {
                var mTrack = await tenantDataRepository.GetTrackByNameAsync(new Track.IdKey { TenantName = RouteBinding.TenantName, TrackName = RouteBinding.TrackName });
                if (mTrack.Logging != null && mTrack.Logging.ScopedStreamLoggers?.Count > 0)
                {
                    return Ok(new Api.LogStreams { LogStreamSettings = mapper.Map<List<Api.LogStreamSettings>>(mTrack.Logging.ScopedStreamLoggers) });
                }
                else
                {
                    return NoContent();
                }
            }
            catch (FoxIDsDataException ex)
            {
                if (ex.StatusCode == DataStatusCode.NotFound)
                {
                    logger.Warning(ex, $"NotFound, Get {nameof(Track)}.{nameof(Track.Logging)}.{nameof(Track.Logging.ScopedStreamLoggers)} by environment name '{RouteBinding.TrackName}'.");
                    return NotFound($"{nameof(Track)}.{nameof(Track.Logging)}.{nameof(Track.Logging.ScopedStreamLoggers)}", RouteBinding.TrackName);
                }
                throw;
            }
        }

        /// <summary>
        /// Save all environment log stream settings.
        /// </summary>
        /// <param name="logStreams">All log stream settings.</param>
        /// <returns>All log stream settings.</returns>
        [ProducesResponseType(typeof(Api.LogStreams), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<Api.LogStreams>> PostTrackLogStreamsSettings([FromBody] Api.LogStreams logStreams)
        {
            try
            {
                if (!await ModelState.TryValidateObjectAsync(logStreams)) return BadRequest(ModelState);

                var trackIdKey = new Track.IdKey { TenantName = RouteBinding.TenantName, TrackName = RouteBinding.TrackName };
                var mTrack = await tenantDataRepository.GetTrackByNameAsync(trackIdKey);
                if (mTrack.Logging == null)
                {
                    mTrack.Logging = new Logging();
                }
                mTrack.Logging.ScopedStreamLoggers = mapper.Map<List<ScopedStreamLogger>>(logStreams.LogStreamSettings);
                await tenantDataRepository.UpdateAsync(mTrack);

                await trackCacheLogic.InvalidateTrackCacheAsync(trackIdKey);

                return Ok(mapper.Map<HashSet<Api.LogStreamSettings>>(mTrack.Logging.ScopedStreamLoggers));
            }
            catch (FoxIDsDataException ex)
            {
                if (ex.StatusCode == DataStatusCode.NotFound)
                {
                    logger.Warning(ex, $"NotFound, Save {nameof(Track)}.{nameof(Track.Logging)}.{nameof(Track.Logging.ScopedStreamLoggers)} by environment name '{RouteBinding.TrackName}'.");
                    return NotFound($"{nameof(Track)}.{nameof(Track.Logging)}.{nameof(Track.Logging.ScopedStreamLoggers)}", RouteBinding.TrackName);
                }
                throw;
            }
        }
    }
}
