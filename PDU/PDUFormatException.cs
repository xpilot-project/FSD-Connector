using System;

namespace Vatsim.Fsd.Connector.PDU
{
	public class PDUFormatException : Exception
	{
		public string RawMessage { get; set; }

		public PDUFormatException(string error, string rawMessage)
			: this(error, rawMessage, null)
		{
		}

		public PDUFormatException(string error, string rawMessage, Exception innerException)
			: base(error, innerException)
		{
			RawMessage = rawMessage;
		}
	}
}
