namespace EmailApi.DTOs.Responses
{
	public class Result
	{
		public Result(bool success = false, string message = null)
		{
			Success = success;
			Message = message;
		}

		public bool Success { get; set; }

		public string Message { get; set; }
	}

	public class Result<T>
	{
		public Result(bool success = false, T value = default(T), string message = null)
		{
			Success = success;
			Value = value;
			Message = null;
		}

		public bool Success { get; set; }
		public T Value { get; set; }
		public string Message { get; set; }
	}
}
