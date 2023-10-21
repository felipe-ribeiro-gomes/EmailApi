using AutoMapper;
using EmailApi.DTOs.Responses;
using Microsoft.Extensions.Configuration;
using SEEDUC.Email.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EmailApi.Services
{
	public class FileSystemService : IEmailApiService
	{
		private readonly string _outboxPath;
		private readonly string _sentItemsPath;
		private readonly IMapper _mapper;

		public FileSystemService(IConfiguration configuration)
		{
			this._outboxPath = configuration.GetValue<string>("OutboxPath");
			this._sentItemsPath = configuration.GetValue<string>("SentItemsPath");

			var mapperConfig = new MapperConfiguration(config => {

				config.CreateMap<EmailDTO.MessageDTO.MailAddressDTO, MailAddress>()
					.ConstructUsing((s => new MailAddress(s.Address, s.Name)))
					.ReverseMap()
					.ForMember(d => d.Address, opts => opts.MapFrom(s => s.Address))
					.ForMember(d => d.Name, opts => opts.MapFrom(s => s.DisplayName))
					;

			});

			mapperConfig.AssertConfigurationIsValid();

			_mapper = mapperConfig.CreateMapper();
		}

		public Result NewEmail(EmailDTO request)
		{
			var file = new FileInfo($@"{_outboxPath}\{Guid.NewGuid()}.email");

			if (!Directory.Exists(file.DirectoryName))
				file.Directory.Create();

			using var streamWriter = new StreamWriter(file.FullName, true);
			streamWriter.Write(JsonSerializer.Serialize(request));
			streamWriter.Close();

			return new(success: true, message: $"Email criado com sucesso: {file.Name}");
		}

		public Result DeleteEmail(string fileName)
		{
			var file = new FileInfo($@"{_outboxPath}\{fileName}");

			if (!file.Exists)
				file = new FileInfo($@"{_sentItemsPath}\{fileName}");

			if (!file.Exists)
				return new(message: "Arquivo de e-mail especificado não existe, nem na Caixa de Saída, nem em Itens Enviados.");

			file.Delete();

			return new(success: true, message: $"Arquivo de e-mail \"{file.FullName}\" deletado com sucesso.");
		}

		public Result RunOutbox()
		{
			if (!Directory.Exists(_outboxPath))
				return new(message: "A caixa de saída ainda não foi criada. Crie um e-mail.");

			var list = Directory.GetFiles(_outboxPath);
			if (!list.Any())
				return new(message: "Não havia(m) e-mail(s) para ser(em) enviado(s).");

			foreach (var file in list)
			{
				var nomeArquivo = new FileInfo(file).Name;

				TrySend(_outboxPath, nomeArquivo);

				var arquivo = new FileInfo($@"{_outboxPath}\{nomeArquivo}");
				if (!Directory.Exists(_sentItemsPath))
					Directory.CreateDirectory(_sentItemsPath);
				arquivo.MoveTo($@"{_sentItemsPath}\{nomeArquivo}");
			}

			return new(success: true, message: $"{list.Count()} e-mail(s) enviado(s) com sucesso.");
		}

		public async Task<Result<IList<EmailDTO>>> ListOutbox()
		{
			return new(success: true, value: await List(_outboxPath), message: "Caixa de saída listada com sucesso.");
		}

		public async Task<Result<IList<EmailDTO>>> ListSentItems()
		{
			return new(success: true, value: await List(_sentItemsPath), message: "Itens enviados listados com sucesso.");
		}

		private void TrySend(EmailDTO email, int maximumTries = 100, int intervalBetweenTries = 1000)
		{
			Action<int> recursive = null;
			recursive = (count) => {
				try
				{
					using (SmtpClient smtpClient = new SmtpClient())
					using (MailMessage mailMessage = new MailMessage())
					{
						smtpClient.Host = email.Smtp.Host;
						smtpClient.Port = email.Smtp.Port;
						smtpClient.Credentials = new NetworkCredential(email.Smtp.UserName, email.Smtp.Password);
						smtpClient.EnableSsl = email.Smtp.EnableSSL;

						mailMessage.From = new MailAddress(email.Message.From.Address, email.Message.From.Name);
						mailMessage.To.AddRange(_mapper.Map<IEnumerable<MailAddress>>(email.Message.To));
						mailMessage.CC.AddRange(_mapper.Map<IEnumerable<MailAddress>>(email.Message.Cc));
						mailMessage.Bcc.AddRange(_mapper.Map<IEnumerable<MailAddress>>(email.Message.Bcc));
						mailMessage.Subject = email.Message.Subject;
						mailMessage.Body = email.Message.Body;
						mailMessage.IsBodyHtml = email.Message.IsBodyHtml;

						smtpClient.Send(mailMessage);
					}
				}
				catch (SmtpException ex) when (ex.InnerException?.GetType() == typeof(IOException) && ex.InnerException.Message.Contains("net_io_connectionclosed"))
				{
					/*Erro ao enviar e-mail para o Office 365
					  Dispara quando o TLS1.2 não é suportado
					  pelo Framework ou Sistema Operacional
					*/

					if (count <= maximumTries)
					{
						Thread.Sleep(intervalBetweenTries);
						recursive(++count);
					}
					else
						throw;
				}
				catch (Exception)
				{
					throw;
				}
			};

			recursive(0);
		}

		private void TrySend(string nomePastaCaixaDeSaida, string nomeArquivo, int maximumTries = 100, int intervalBetweenTries = 1000)
		{
			var file = new FileInfo($@"{nomePastaCaixaDeSaida}\{nomeArquivo}");

			using var streamReader = new StreamReader(file.FullName);
			var obj = JsonSerializer.Deserialize<EmailDTO>(streamReader.ReadToEnd());
			TrySend(obj);
			streamReader.Close();
		}

		private async Task<IList<EmailDTO>> List(string directory)
		{
			var listObj = new List<EmailDTO>();

			if (!Directory.Exists(directory))
				return listObj;

			var listFileName = Directory.GetFiles(directory);
			foreach (var fileName in listFileName)
			{
				using var streamReader = new StreamReader(fileName);
				var obj = await JsonSerializer.DeserializeAsync<EmailDTO>(streamReader.BaseStream);
				obj.File = fileName;
				listObj.Add(obj);
				streamReader.Close();
			}

			return listObj;
		}
	}
}
