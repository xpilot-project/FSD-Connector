using System;

namespace Vatsim.Fsd.Connector
{
	public class NetworkEventArgs : EventArgs
	{
		public object UserData { get; }

		public NetworkEventArgs(object userData)
		{
			UserData = userData;
		}
	}
}
