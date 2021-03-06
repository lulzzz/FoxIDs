﻿using System;
using System.Threading.Tasks;
using FoxIDs.Infrastructure;
using FoxIDs.Infrastructure.Filters;
using FoxIDs.Logic;
using FoxIDs.Models;
using FoxIDs.Models.Sequences;
using FoxIDs.Models.ViewModels;
using FoxIDs.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace FoxIDs.Controllers
{
    [Sequence]
    public class ActionController : EndpointController
    {
        private readonly TelemetryScopedLogger logger;
        private readonly IStringLocalizer localizer;
        private readonly ITenantRepository tenantRepository;
        private readonly AccountLogic userAccountLogic;
        private readonly AccountActionLogic accountActionLogic;

        public ActionController(TelemetryScopedLogger logger, IStringLocalizer localizer, ITenantRepository tenantRepository, AccountLogic userAccountLogic, AccountActionLogic accountActionLogic) : base(logger)
        {
            this.logger = logger;
            this.localizer = localizer;
            this.tenantRepository = tenantRepository;
            this.userAccountLogic = userAccountLogic;
            this.accountActionLogic = accountActionLogic;
        }

        public async Task<IActionResult> Confirmation()
        {
            try
            {
                logger.ScopeTrace("Start confirmation verification.");

                var verified = await accountActionLogic.VerifyConfirmationAsync();

                var uiLoginUpParty = await tenantRepository.GetAsync<UiLoginUpPartyData>(Sequence.UiUpPartyId);
                return View(new ConfirmationViewModel
                {
                    CssStyle = uiLoginUpParty.CssStyle,
                    Verified = verified
                });
            }
            catch (Exception ex)
            {
                throw new EndpointException($"Confirmation failed, Name '{RouteBinding.UpParty.Name}'.", ex) { RouteBinding = RouteBinding };
            }
        }

        public async Task<IActionResult> ForgotPassword()
        {
            try
            {
                logger.ScopeTrace("Start forgot password.");

                var uiLoginUpParty = await tenantRepository.GetAsync<UiLoginUpPartyData>(Sequence.UiUpPartyId);
                if (uiLoginUpParty.DisableResetPassword)
                {
                    throw new InvalidOperationException("Reset password not enabled.");
                }

                return View(new ForgotPasswordViewModel
                {
                    SequenceString = SequenceString,
                    CssStyle = uiLoginUpParty.CssStyle,
                    Receipt = false
                });
            }
            catch (Exception ex)
            {
                throw new EndpointException($"Forgot password failed, Name '{RouteBinding.UpParty.Name}'.", ex) { RouteBinding = RouteBinding };
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel forgotPassword)
        {
            try
            {
                logger.ScopeTrace("Forgot password receipt.");

                await accountActionLogic.SendResetPasswordEmailAsync(forgotPassword.Email);

                var uiLoginUpParty = await tenantRepository.GetAsync<UiLoginUpPartyData>(Sequence.UiUpPartyId);
                if (uiLoginUpParty.DisableResetPassword)
                {
                    throw new InvalidOperationException("Reset password not enabled.");
                }

                return View(new ForgotPasswordViewModel
                {
                    CssStyle = uiLoginUpParty.CssStyle,
                    Receipt = true
                });
            }
            catch (Exception ex)
            {
                throw new EndpointException($"Forgot password failed, Name '{RouteBinding.UpParty.Name}'.", ex) { RouteBinding = RouteBinding };
            }
        }

        public async Task<IActionResult> ResetPassword()
        {
            try
            {
                logger.ScopeTrace("Start reset password.");

                (var verified, _) = await accountActionLogic.VerifyResetPasswordAsync();

                var uiLoginUpParty = await tenantRepository.GetAsync<UiLoginUpPartyData>(Sequence.UiUpPartyId);
                if (uiLoginUpParty.DisableResetPassword)
                {
                    throw new InvalidOperationException("Reset password not enabled.");
                }

                return View(new ResetPasswordViewModel
                {
                    CssStyle = uiLoginUpParty.CssStyle,
                    Verified = verified,
                    Receipt = false
                });
            }
            catch (Exception ex)
            {
                throw new EndpointException($"Reset password failed, Name '{RouteBinding.UpParty.Name}'.", ex) { RouteBinding = RouteBinding };
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel resetPassword)
        {
            try
            {
                logger.ScopeTrace("Resetting password.");

                (var verified, var user) = await accountActionLogic.VerifyResetPasswordAsync();

                var uiLoginUpParty = await tenantRepository.GetAsync<UiLoginUpPartyData>(Sequence.UiUpPartyId);
                if (uiLoginUpParty.DisableResetPassword)
                {
                    throw new InvalidOperationException("Reset password not enabled.");
                }

                Func<bool, IActionResult> viewResponse = (receipt) =>
                {
                    resetPassword.CssStyle = uiLoginUpParty.CssStyle;
                    resetPassword.Verified = verified;
                    resetPassword.Receipt = receipt;
                    return View(resetPassword);
                };

                if (!ModelState.IsValid)
                {
                    return viewResponse(false);
                }

                try
                {
                    await userAccountLogic.SetPasswordUser(user, resetPassword.NewPassword);

                    await accountActionLogic.RemoveResetPasswordSequenceDataAsync();
                    return viewResponse(true);
                }
                catch (PasswordLengthException plex)
                {
                    logger.ScopeTrace(plex.Message);
                    ModelState.AddModelError(nameof(resetPassword.NewPassword), RouteBinding.CheckPasswordComplexity ?
                        localizer["Please use {0} characters or more with a mix of letters, numbers and symbols.", RouteBinding.PasswordLength] :
                        localizer["Please use {0} characters or more.", RouteBinding.PasswordLength]);
                }
                catch (PasswordComplexityException pcex)
                {
                    logger.ScopeTrace(pcex.Message);
                    ModelState.AddModelError(nameof(resetPassword.NewPassword), localizer["Please use a mix of letters, numbers and symbols"]);
                }
                catch (PasswordEmailTextComplexityException pecex)
                {
                    logger.ScopeTrace(pecex.Message);
                    ModelState.AddModelError(nameof(resetPassword.NewPassword), localizer["Please do not use the email or parts of it."]);
                }
                catch (PasswordUrlTextComplexityException pucex)
                {
                    logger.ScopeTrace(pucex.Message);
                    ModelState.AddModelError(nameof(resetPassword.NewPassword), localizer["Please do not use parts of the URL."]);
                }
                catch (PasswordRiskException prex)
                {
                    logger.ScopeTrace(prex.Message);
                    ModelState.AddModelError(nameof(resetPassword.NewPassword), localizer["The password has previously appeared in a data breach. Please choose a more secure alternative."]);
                }

                return viewResponse(false);
            }
            catch (Exception ex)
            {
                throw new EndpointException($"Resetting password failed, Name '{RouteBinding.UpParty.Name}'.", ex) { RouteBinding = RouteBinding };
            }
        }
    }
}
