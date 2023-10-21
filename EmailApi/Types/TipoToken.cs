using System.ComponentModel;

namespace EmailApi.Types
{
	public enum TipoToken
	{
		[Description("LOGIN_E_SENHA")]
		LOGIN_E_SENHA,

		[Description("REFRESH_TOKEN")]
		REFRESH_TOKEN
	}
}
