using System;

namespace Vatsim.Fsd.Connector
{
	public class ClientProperties
	{
		public string Name { get; set; }
		public string ClientHash { get; set; }
		public string PluginHash { get; set; }
		public Version Version { get; set; }

		public ClientProperties(string name, Version ver, string client, string plugin)
		{
			Name = name;
			Version = ver;
			ClientHash = client;
			PluginHash = plugin;
		}

		public override string ToString()
		{
			return string.Format("{0} {1}", Name, Version);
		}
	}
}
