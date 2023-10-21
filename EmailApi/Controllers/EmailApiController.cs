using EmailApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace EmailApi.Controllers
{
    [Route("")]
	[ApiController]
	public class EmailApiController : ControllerBase
	{
		private readonly ILogger<EmailApiController> _logger;
		private readonly IEmailApiService _emailApi;

		public EmailApiController(ILogger<EmailApiController> logger, IEmailApiService emailApi)
		{
			this._logger = logger;
			this._emailApi = emailApi;
		}

		[HttpPost]
		[Authorize(Roles = "USUARIO")]
		[Route("[action]")]
		public async Task<IActionResult> NewEmail(EmailDTO request)
		{
			return Ok(_emailApi.NewEmail(request).Message);
		}

		[HttpPost]
		[Authorize(Roles = "USUARIO")]
		[Route("[action]/{fileName?}")]
		public async Task<IActionResult> DeleteEmail(string fileName)
		{
			return Ok(_emailApi.DeleteEmail(fileName).Message);
		}

		[HttpPost]
		[Authorize(Roles = "USUARIO")]
		[Route("[action]")]
		public async Task<IActionResult> RunOutbox()
        {
			return Ok(_emailApi.RunOutbox().Message);
		}

		[HttpGet]
		[Authorize(Roles = "USUARIO")]
		[Route("[action]")]
		public async Task<IActionResult> ListOutbox()
		{
			return Ok(await _emailApi.ListOutbox());
		}

		[HttpGet]
		[Authorize(Roles = "USUARIO")]
		[Route("[action]")]
		public async Task<IActionResult> ListSentItems()
		{
			return Ok(await _emailApi.ListSentItems());
		}
	}
}
