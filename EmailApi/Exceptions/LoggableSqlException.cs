using System;
using System.Data;

namespace EmailApi.Exceptions
{
	public class LoggableSqlException : Exception
	{
		public Exception SqlException { get; private set; }
		public string SQL { get; private set; }
		public IDbDataParameter[] Parameters { get; private set; }

		public LoggableSqlException(Exception ex, string sql, IDbDataParameter[] parameters)
		{
			SqlException = ex;
			SQL = sql;
			Parameters = parameters;
		}

		public override string ToString()
		{
			string parameters = "";
			foreach (IDbDataParameter p in Parameters)
			{
				parameters += (parameters.Length > 0 ? Environment.NewLine : "");
				parameters += "@" + p.ParameterName + " = " + p.Value;
			}

			string result = @"
SQL:
{0}

Parameters: 
{1}

{2}
";

			return string.Format(result, SQL, parameters, SqlException);
		}
	}
}
