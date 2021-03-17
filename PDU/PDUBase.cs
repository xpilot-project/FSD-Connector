namespace Vatsim.FsdClient.PDU
{
	public abstract class PDUBase
	{
		// Constants:

		public const string CLIENT_QUERY_BROADCAST_RECIPIENT = "@94835";
		public const string CLIENT_QUERY_BROADCAST_RECIPIENT_PILOTS = "@94836";
		public const char DELIMITER = ':';
		public const string PACKET_DELIMITER = "\r\n";
		public const string SERVER_CALLSIGN = "SERVER";

		public string From { get; set; }
		public string To { get; set; }

		public PDUBase(string from, string to)
		{
			From = from;
			To = to;
		}

		public abstract string Serialize();

		public static string Reassemble(string[] fields)
		{
			return string.Join(DELIMITER.ToString(), fields);
		}
	}
}
