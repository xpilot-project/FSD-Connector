using System;

namespace Vatsim.Fsd.Connector
{
	public class DataReceivedEventArgs<T> : EventArgs
	{
		public T PDU { get; }

		public object UserData { get; }

		public DataReceivedEventArgs(T pdu, object userData)
		{
			PDU = pdu;
			UserData = userData;
		}
	}
}

