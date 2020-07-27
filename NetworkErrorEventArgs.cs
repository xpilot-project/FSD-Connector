using System;

namespace Vatsim.Fsd.Connector
{
	public class NetworkErrorEventArgs : EventArgs
	{
		public string Error { get; set; }

		public object UserData { get; }

		public NetworkErrorEventArgs(string error, object userData)
		{
			Error = error;
			UserData = userData;
		}

		public override string ToString()
		{
			return string.Format("Network error: {0}", Error);
		}
	}
}
