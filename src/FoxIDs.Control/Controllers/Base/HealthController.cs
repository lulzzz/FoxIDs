﻿using Microsoft.AspNetCore.Mvc;

namespace FoxIDs.Controllers
{
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [Route(Constants.Routes.HealthController)]
    public class HealthController : Controller
    {
        public IActionResult Index()
        {
            return Ok();
        }
    }
}
