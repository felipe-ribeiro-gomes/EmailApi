using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace EmailApi.Token
{
    public static class TokenHelper
	{
        /// <summary>
        /// Cria um JWT access token.
        /// </summary>
        /// <param name="login">login do usuário</param>
        /// <param name="role">papel (ou grupo) que o usuário terá permissão</param>
        /// <returns>token JWT formato string</returns>
        public static string CreateToken(string login, string role)
        {
            try
            {
                string audience = Startup.Configuration.GetValue<string>("JWT:Audience");
                string issuer = Startup.Configuration.GetValue<string>("JWT:Issuer");
                ClaimsIdentity subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.Name, login),
                    new Claim(ClaimTypes.Role, role)
                });
                DateTime expires = DateTime.UtcNow.AddSeconds(Startup.Configuration.GetValue<int>("JWT:ExpirationSeconds"));
                SigningCredentials signingCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.ASCII.GetBytes(Startup.Configuration.GetValue<string>("JWT:Secret"))), SecurityAlgorithms.HmacSha256Signature);

                var tokenHandler = new JwtSecurityTokenHandler();

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Audience = audience,
                    Issuer = issuer,
                    IssuedAt = DateTime.Now,
                    Subject = subject,
                    NotBefore = DateTime.Now,
                    Expires = expires,
                    SigningCredentials = signingCredentials,
                };

                return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Algoritmo para a criação de um refresh token.
        /// Este token será associado ao usuário para que possa autenticá-lo caso o access token expire.
        /// </summary>
        /// <returns>token formato string</returns>
        public static string CreateRefreshToken()
        {
            try
            {
                string token;
                var randomNumber = new byte[32];

                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(randomNumber);
                    token = Convert.ToBase64String(randomNumber);
                }

                return token.Replace("+", string.Empty)
                                          .Replace("=", string.Empty)
                                          .Replace("/", string.Empty);
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Algoritmo para a criação de um token de redefinição de senha.
        /// Este token será enviado exclusivamente para o e-mail do aluno que solicitar, garantindo que nenhuma outra pessoa além do aluno solicitante poderá redefinir a própria senha.
        /// </summary>
        /// <returns>token formato string</returns>
        public static string CreateResetPasswordToken()
        {
            try
            {
                string token;
                var randomNumber = new byte[32];

                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(randomNumber);
                    token = Convert.ToBase64String(randomNumber);
                }

                return token.Replace("+", string.Empty)
                                          .Replace("=", string.Empty)
                                          .Replace("/", string.Empty);
            }
            catch
            {
                throw;
            }
        }
    }
}