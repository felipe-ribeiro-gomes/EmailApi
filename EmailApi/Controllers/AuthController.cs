using EmailApi.DTOs.Requests;
using EmailApi.DTOs.Responses;
using EmailApi.Token;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace EmailApi.Controllers
{
	[Route("[controller]")]
	[ApiController]
	public class AuthController : ControllerBase
	{
        private readonly IConfiguration _configuration;

		public AuthController(IConfiguration configuration)
		{
            _configuration = configuration;
		}

        [HttpPost()]
        [AllowAnonymous]
        public async Task<ActionResult> Index([FromBody] AuthRequestDTO req)
        {
            var senha = _configuration.GetValue<string>($"Usuarios:USUARIO:{req.Login}");
            
            if (string.IsNullOrEmpty(senha))
                return Unauthorized("Login e senha inválidos");

            if (senha != req.Senha)
                return Unauthorized("Login e senha inválidos");

            var result = new AuthResponseDTO
            {
                Login = req.Login,
                AccessToken = TokenHelper.CreateToken(req.Login, "USUARIO"),
            };

            return Ok(result);
        }
    }
}