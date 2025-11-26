using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace MonoPayAggregator.Controllers
{
    [ApiController]
    [Route("v1/auth")]
    [Route("api/v1/auth")]
    public class AuthController : ControllerBase
    {
        /// <summary>
        /// Obtain an access token using basic auth credentials. This simplified
        /// implementation accepts any credentials and returns a dummy token.
        /// </summary>
        [HttpPost("token")]
        public IActionResult GetToken()
        {
            // In a real system you'd validate the Authorization header here
            var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            var response = new JObject
            {
                ["access_token"] = token,
                ["token_type"] = "bearer",
                ["expires_in"] = 3600
            };
            return Ok(response);
        }
    }
}