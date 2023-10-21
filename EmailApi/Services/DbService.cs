using AutoMapper;
using Biznuvem.Extensions.Db;
using EmailApi.DTOs.Responses;
using SEEDUC.Email.Extensions;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;

namespace EmailApi.Services
{
    public class DbService : IEmailApiService
	{
		private readonly IDbConnection _dbConnection;
		private readonly IMapper _mapper;

		public DbService(IDbConnection dbConnection)
		{
			this._dbConnection = dbConnection;

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
			using var transaction = _dbConnection.BeginTransaction();

			using var outboxCmd = _dbConnection.CreateCommand();
			outboxCmd.Transaction = transaction;

			outboxCmd.CommandText = @"
			insert into Outbox (Id, Host, Port, Username, Password, EnableSSL, Subject, Body, IsBodyHtml, FromAddress, FromName)
			values (@Id, @Host, @Port, @Username, @Password, @EnableSSL, @Subject, @Body, @IsBodyHtml, @FromAddress, @FromName)
			";

			var id = Guid.NewGuid();

			outboxCmd.AddParameter("@Id", id);
			outboxCmd.AddParameter("@Host", request.Smtp.Host);
			outboxCmd.AddParameter("@Port", request.Smtp.Port);
			outboxCmd.AddParameter("@Username", request.Smtp.UserName);
			outboxCmd.AddParameter("@Password", request.Smtp.Password);
			outboxCmd.AddParameter("@EnableSSL", request.Smtp.EnableSSL);
			outboxCmd.AddParameter("@Subject", request.Message.Subject);
			outboxCmd.AddParameter("@Body", request.Message.Body);
			outboxCmd.AddParameter("@IsBodyHtml", request.Message.IsBodyHtml);
			outboxCmd.AddParameter("@FromAddress", request.Message.From.Address);
			outboxCmd.AddParameter("@FromName", request.Message.From.Name);
			
			outboxCmd.ExecuteNonQuery();

			foreach (var to in request.Message.To)
            {
				using var recipientCmd = _dbConnection.CreateCommand();
				recipientCmd.Transaction = transaction;
				recipientCmd.CommandText = @"insert into OutboxTo (Id, Address, Name) values (@Id, @Address, @Name)";
				recipientCmd.AddParameter("@Id", id);
				recipientCmd.AddParameter("@Address", to.Address);
				recipientCmd.AddParameter("@Name", to.Name);
				recipientCmd.ExecuteNonQuery();
			}

			foreach (var cc in request.Message.Cc)
			{
				using var recipientCmd = _dbConnection.CreateCommand();
				recipientCmd.Transaction = transaction;
				recipientCmd.CommandText = @"insert into OutboxCc (Id, Address, Name) values (@Id, @Address, @Name)";
				recipientCmd.AddParameter("@Id", id);
				recipientCmd.AddParameter("@Address", cc.Address);
				recipientCmd.AddParameter("@Name", cc.Name);
				recipientCmd.ExecuteNonQuery();
			}

			foreach (var bcc in request.Message.Bcc)
			{
				using var recipientCmd = _dbConnection.CreateCommand();
				recipientCmd.Transaction = transaction;
				recipientCmd.CommandText = @"insert into OutboxBcc (Id, Address, Name) values (@Id, @Address, @Name)";
				recipientCmd.AddParameter("@Id", id);
				recipientCmd.AddParameter("@Address", bcc.Address);
				recipientCmd.AddParameter("@Name", bcc.Name);
				recipientCmd.ExecuteNonQuery();
			}

			transaction.Commit();

			return new(success: true, message: $"Email criado com sucesso: {id}");
		}

		public Result DeleteEmail(string id)
		{
			using var transaction = _dbConnection.BeginTransaction();

			using var outboxCmd = _dbConnection.CreateCommand();
			outboxCmd.Transaction = transaction;

			outboxCmd.CommandText = @"
			delete from OutboxTo where Id = @Id;
			delete from OutboxCc where Id = @Id;
			delete from OutboxBcc where Id = @Id;
			delete from Outbox where Id = @Id;
			delete from SentItemsTo where Id = @Id;
			delete from SentItemsCc where Id = @Id;
			delete from SentItemsBcc where Id = @Id;
			delete from SentItems where Id = @Id;
			";

			outboxCmd.AddParameter("@Id", id);

			outboxCmd.ExecuteNonQuery();
			
			transaction.Commit();

			return new(success: true, message: $"E-mail \"{id}\" deletado com sucesso.");
		}

		public Result RunOutbox()
		{
			var list = List("outbox").Result;

			using var transaction = _dbConnection.BeginTransaction();
			
			using var cmd = _dbConnection.CreateCommand();
			cmd.Transaction = transaction;

			cmd.CommandText = @"
			insert into SentItems (Id, Host, Port, Username, Password, EnableSSL, Subject, Body, IsBodyHtml, FromAddress, FromName)
			select Id, Host, Port, Username, Password, EnableSSL, Subject, Body, IsBodyHtml, FromAddress, FromName
			from Outbox o (nolock) where o.Id = @Id;

			insert into SentItemsTo (Id, Address, Name) select Id, Address, Name from OutboxTo o (nolock) where o.Id = @Id;
			insert into SentItemsCc (Id, Address, Name) select Id, Address, Name from OutboxCc o (nolock) where o.Id = @Id;
			insert into SentItemsBcc (Id, Address, Name) select Id, Address, Name from OutboxBcc o (nolock) where o.Id = @Id;

			delete from OutboxTo where Id = @Id;
			delete from OutboxCc where Id = @Id;
			delete from OutboxBcc where Id = @Id;
			delete from Outbox where Id = @Id;
			";

			foreach (var email in list)
            {
                TrySend(email);

				cmd.AddParameter("@Id", email.File);
				cmd.ExecuteNonQuery();
			}

			transaction.Commit();

			return new(success: true, message: $"{list.Count()} e-mail(s) enviado(s) com sucesso.");
		}

		public async Task<Result<IList<EmailDTO>>> ListOutbox()
		{
			return new(success: true, value: await List("Outbox"), message: "Caixa de saída listada com sucesso.");
		}

		public async Task<Result<IList<EmailDTO>>> ListSentItems()
		{
			return new(success: true, value: await List("SentItems"), message: "Itens enviados listados com sucesso.");
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

		private async Task<IList<EmailDTO>> List(string box)
		{
			using var outboxCmd = _dbConnection.CreateCommand();

			outboxCmd.CommandText = @$"
			select
			o.Id 'File'
			,o.Host 'Smtp.Host'
			,o.[Port] 'Smtp.Port'
			,o.Username 'Smtp.Username'
			,o.Password 'Smtp.Password'
			,o.EnableSSL 'Smtp.EnableSSL'
			,o.FromAddress 'Message.From.Address'
			,o.FromName 'Message.From.Name'
			,(
				select
				oto.[Address]
				,oto.[Name]

				from {box}To oto (nolock)
				where oto.Id = o.Id
				for JSON PATH
			) 'Message.To'
			,(
				select
				occ.[Address]
				,occ.[Name]

				from {box}Cc occ (nolock)
				where occ.Id = o.Id
				for JSON PATH
			) 'Message.Cc'
			,(
				select
				obcc.[Address]
				,obcc.[Name]

				from {box}Bcc obcc (nolock)
				where obcc.Id = o.Id
				for JSON PATH
			) 'Message.Bcc'
			,o.Subject 'Message.Subject'
			,o.Body 'Message.Body'
			,o.IsBodyHtml 'Message.IsBodyHtml'

			from {box} o (nolock)
			for JSON PATH
			";

			return outboxCmd.TryDeserializeJSON<List<EmailDTO>>() ?? new List<EmailDTO>();
		}
	}
}
