using Biznuvem.Logging.TextFile;
using EmailApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text;

namespace EmailApi
{
    public class Startup
	{
		public static IConfiguration Configuration { get; private set; }
		public static IHttpContextAccessor HttpContextAcessor { get; private set; }

		public Startup(IConfiguration configuration)
		{
			Configuration = configuration;
		}

		public void ConfigureServices(IServiceCollection services)
		{
			services.AddCors();

			services.AddControllers()
				.AddJsonOptions(config => {
					config.JsonSerializerOptions.PropertyNamingPolicy = null;
				});

			services.AddSwaggerGen(c =>
			{
				c.SwaggerDoc("v1", new OpenApiInfo { Title = "EmailApi", Version = GetType().Assembly.GetName().Version.ToString() });
				c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
				{
					Description = @"Por favor, insira o Token no campo abaixo.",
					Name = "Authorization",
					In = ParameterLocation.Header,
					Type = SecuritySchemeType.Http,
					Scheme = "Bearer"
				});

				c.AddSecurityRequirement(new OpenApiSecurityRequirement()
				{
					{
						new OpenApiSecurityScheme
						{
							Reference = new OpenApiReference
							{
								Type = ReferenceType.SecurityScheme,
								Id = "Bearer"
							},
							Scheme = "oauth2",
							Name = "Bearer",
							In = ParameterLocation.Header,
						},
						new List<string>()
					}
				});
			});

			services.AddAuthentication(x =>
			{
				x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
				x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
			})
			.AddJwtBearer(x =>
			{
				x.RequireHttpsMetadata = false;
				x.SaveToken = true;
				x.TokenValidationParameters = new TokenValidationParameters
				{
					ValidateIssuerSigningKey = true,
					IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(Configuration.GetValue<string>("JWT:Secret"))),
					ValidateIssuer = true,
					ValidIssuer = Configuration.GetValue<string>("JWT:Issuer"),
					ValidateAudience = true,
					ValidAudience = Configuration.GetValue<string>("JWT:Audience"),
					ValidateLifetime = true,
					RequireExpirationTime = true,
					ClockSkew = TimeSpan.Zero,
				};
			});

			services.AddHttpContextAccessor();

            services.AddTransient<IDbConnection, SqlConnection>(db =>
            {
                var conexao = new SqlConnection(Configuration.GetConnectionString("ConnStr"));
                conexao.Open();
                return conexao;
            });

            services.AddLogging(builderConfig =>
			{
				builderConfig.AddCustomLogger(config =>
				{
					config.LogLevel = Configuration.GetValue<LogLevel>("Logging:LogLevel:Default");
					config.LogPath = Configuration.GetValue<string>("Logging:LogPath");
				});
			});

			switch (Configuration.GetValue<string>("StorageToBeUsed"))
			{
				case "Db":
					services.AddScoped<IEmailApiService, DbService>();
					break;

				case "FileSystem":
					services.AddScoped<IEmailApiService, FileSystemService>();
					break;

				default: throw new ArgumentException("O parâmetro \"StorageToBeUsed\" do appsettings.json está com um valor inválido. Valores permitidos: \"Db\" e \"FileSystem\"");
			}
		}

		public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
		{
			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
			}

			app.UseSwagger(c =>
			{
				c.RouteTemplate = "swagger/{documentName}/swagger.json";
			});

			app.UseSwaggerUI(c =>
			{
				c.RoutePrefix = "swagger";
				c.SwaggerEndpoint("v1/swagger.json", "1.0");
			});

			app.UseRouting();

			app.UseCors(x => x
				.AllowAnyOrigin()
				.AllowAnyMethod()
				.AllowAnyHeader());

			app.UseAuthentication();
			app.UseAuthorization();

			app.UseEndpoints(endpoints =>
			{
				endpoints.MapControllers();
			});
		}
	}
}
