namespace Vatsim.Fsd.Connector
{
	public class NetworkServerInfo
	{
		public string Name { get; set; }
		public string Address { get; set; }
		public string Location { get; set; }
		public string Description { get; set; }
		public override string ToString()
		{
			return Name;
		}
	}
}
