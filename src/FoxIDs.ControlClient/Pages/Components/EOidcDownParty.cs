﻿using FoxIDs.Client.Models.ViewModels;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FoxIDs.Client.Infrastructure;
using FoxIDs.Models.Api;
using FoxIDs.Client.Services;
using Microsoft.AspNetCore.Components.Forms;
using ITfoxtec.Identity.BlazorWebAssembly.OpenidConnect;
using FoxIDs.Client.Infrastructure.Security;
using Microsoft.AspNetCore.Components.Web;

namespace FoxIDs.Client.Pages.Components
{
    public partial class EOidcDownParty : DownPartyBase
    {
        private void OidcDownPartyViewModelAfterInit(GeneralOidcDownPartyViewModel oidcDownParty, OidcDownPartyViewModel model)
        {
            if (oidcDownParty.CreateMode)
            {
                model.Client = oidcDownParty.EnableClientTab ? new OidcDownClientViewModel() : null;
                model.Resource = oidcDownParty.EnableResourceTab ? new OAuthDownResource() : null;

                if (model.Client != null)
                {
                    model.Client.ResponseTypes.Add("code");

                    model.Client.ScopesViewModel.Add(new OidcDownScopeViewModel { Scope = "offline_access" });
                    model.Client.ScopesViewModel.Add(new OidcDownScopeViewModel
                    {
                        Scope = "profile",
                        VoluntaryClaims = new List<OidcDownClaim>
                        {
                            new OidcDownClaim { Claim = "name", InIdToken = true }, new OidcDownClaim { Claim = "given_name", InIdToken = true }, new OidcDownClaim { Claim = "middle_name", InIdToken = true }, new OidcDownClaim { Claim = "family_name", InIdToken = true },
                            new OidcDownClaim { Claim = "nickname", InIdToken = false }, new OidcDownClaim { Claim = "preferred_username", InIdToken = false },
                            new OidcDownClaim { Claim = "birthdate", InIdToken = false }, new OidcDownClaim { Claim = "gender", InIdToken = false }, new OidcDownClaim { Claim = "picture", InIdToken = false }, new OidcDownClaim { Claim = "profile", InIdToken = false },
                            new OidcDownClaim { Claim = "website", InIdToken = false }, new OidcDownClaim { Claim = "locale", InIdToken = true }, new OidcDownClaim { Claim = "zoneinfo", InIdToken = false }, new OidcDownClaim { Claim = "updated_at", InIdToken = false }
                        }
                    });
                    model.Client.ScopesViewModel.Add(new OidcDownScopeViewModel { Scope = "email", VoluntaryClaims = new List<OidcDownClaim> { new OidcDownClaim { Claim = "email", InIdToken = true }, new OidcDownClaim { Claim = "email_verified", InIdToken = false } } });
                    model.Client.ScopesViewModel.Add(new OidcDownScopeViewModel { Scope = "address", VoluntaryClaims = new List<OidcDownClaim> { new OidcDownClaim { Claim = "address", InIdToken = true } } });
                    model.Client.ScopesViewModel.Add(new OidcDownScopeViewModel { Scope = "phone", VoluntaryClaims = new List<OidcDownClaim> { new OidcDownClaim { Claim = "phone_number", InIdToken = true }, new OidcDownClaim { Claim = "phone_number_verified", InIdToken = false } } });
                }
            }
        }

        private void OnOidcDownPartyClientTabChange(GeneralOidcDownPartyViewModel oidcDownParty, bool enableTab) => oidcDownParty.Form.Model.Client = enableTab ? new OidcDownClientViewModel() : null;

        private void OnOidcDownPartyResourceTabChange(GeneralOidcDownPartyViewModel oidcDownParty, bool enableTab) => oidcDownParty.Form.Model.Resource = enableTab ? new OAuthDownResource() : null;

        private void AddOidcScope(MouseEventArgs e, List<OidcDownScopeViewModel> scopesViewModel)
        {
            scopesViewModel.Add(new OidcDownScopeViewModel { ShowVoluntaryClaims = true });
        }

        private void RemoveOidcScope(MouseEventArgs e, List<OidcDownScopeViewModel> scopesViewModel, OidcDownScopeViewModel removeScope)
        {
            scopesViewModel.Remove(removeScope);
        }

        private void AddOidcScopeVoluntaryClaim(MouseEventArgs e, OidcDownScope scope)
        {
            if (scope.VoluntaryClaims == null)
            {
                scope.VoluntaryClaims = new List<OidcDownClaim>();
            }
            scope.VoluntaryClaims.Add(new OidcDownClaim());
        }

        private void RemoveOidcScopeVoluntaryClaim(MouseEventArgs e, List<OidcDownClaim> voluntaryClaims, OidcDownClaim removeVoluntaryClaim)
        {
            voluntaryClaims.Remove(removeVoluntaryClaim);
        }

        private void AddOidcClaim(MouseEventArgs e, List<OidcDownClaim> claims)
        {
            claims.Add(new OidcDownClaim());
        }

        private void RemoveOidcClaim(MouseEventArgs e, List<OidcDownClaim> claims, OidcDownClaim removeClaim)
        {
            claims.Remove(removeClaim);
        }

        private async Task OnEditOidcDownPartyValidSubmitAsync(GeneralOidcDownPartyViewModel generalOidcDownParty, EditContext editContext)
        {
            try
            {
                var oidcDownParty = generalOidcDownParty.Form.Model.Map<OidcDownParty>(afterMap: afterMap =>
                {
                    if (generalOidcDownParty.Form.Model.Client?.DefaultResourceScope == true)
                    {
                        afterMap.Client.ResourceScopes.Add(new OAuthDownResourceScope { Resource = generalOidcDownParty.Form.Model.Name, Scopes = generalOidcDownParty.Form.Model.Client.DefaultResourceScopeScopes });
                    }
                    if (!(afterMap.Resource?.Scopes?.Count > 0))
                    {
                        afterMap.Resource = null;
                    }
                    if (generalOidcDownParty.Form.Model.Client?.ScopesViewModel?.Count() > 0)
                    {
                        afterMap.Client.Scopes = generalOidcDownParty.Form.Model.Client.ScopesViewModel.Map<List<OidcDownScope>>();
                    }
                });

                if (generalOidcDownParty.CreateMode)
                {
                    await DownPartyService.CreateOidcDownPartyAsync(oidcDownParty);
                }
                else
                {
                    await DownPartyService.UpdateOidcDownPartyAsync(oidcDownParty);
                    if (oidcDownParty.Client != null)
                    {
                        foreach (var existingSecret in generalOidcDownParty.Form.Model.Client.ExistingSecrets.Where(s => s.Removed))
                        {
                            await DownPartyService.DeleteOidcClientSecretDownPartyAsync(existingSecret.Name);
                        }
                    }
                }
                if (oidcDownParty.Client != null && generalOidcDownParty.Form.Model.Client.Secrets.Count() > 0)
                {
                    await DownPartyService.CreateOidcClientSecretDownPartyAsync(new OAuthClientSecretRequest { PartyName = generalOidcDownParty.Form.Model.Name, Secrets = generalOidcDownParty.Form.Model.Client.Secrets });
                }

                generalOidcDownParty.Name = generalOidcDownParty.Form.Model.Name;
                generalOidcDownParty.Edit = false;
                await OnStateHasChanged.InvokeAsync(DownParty);
            }
            catch (FoxIDsApiException ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    generalOidcDownParty.Form.SetFieldError(nameof(generalOidcDownParty.Form.Model.Name), ex.Message);
                }
                else
                {
                    throw;
                }
            }
        }

        private async Task DeleteOidcDownPartyAsync(GeneralOidcDownPartyViewModel generalOidcDownParty)
        {
            try
            {
                await DownPartyService.DeleteOidcDownPartyAsync(generalOidcDownParty.Name);
                DownParties.Remove(generalOidcDownParty);
                await OnStateHasChanged.InvokeAsync(DownParty);
            }
            catch (TokenUnavailableException)
            {
                await (OpenidConnectPkce as TenantOpenidConnectPkce).TenantLoginAsync();
            }
            catch (Exception ex)
            {
                generalOidcDownParty.Form.SetError(ex.Message);
            }
        }
    }
}
