using EmailApi.DTOs.Responses;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EmailApi.Services
{
	public interface IEmailApiService
	{
		public Result NewEmail(EmailDTO request);
		public Result DeleteEmail(string fileName);
		public Result RunOutbox();
		public Task<Result<IList<EmailDTO>>> ListOutbox();
		public Task<Result<IList<EmailDTO>>> ListSentItems();
	}
}