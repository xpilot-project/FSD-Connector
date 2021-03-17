using System;

namespace Vatsim.FsdClient
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
