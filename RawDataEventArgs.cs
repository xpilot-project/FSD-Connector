using System;

namespace Vatsim.FsdClient
{
	public class RawDataEventArgs : EventArgs
	{
		public string Data { get; }
		public object UserData { get; }

		public RawDataEventArgs(string data, object userData)
		{
			Data = data;
			UserData = userData;
		}
	}
}