using System;

namespace Vatsim.FsdClient
{
	public class ClientProperties
	{
		public string Name { get; set; }
		public string ClientHash { get; set; }
		public string PluginHash { get; set; }
		public Version Version { get; set; }

		public ClientProperties(string name, Version ver, string clientHash, string pluginHash)
		{
			Name = name;
			Version = ver;
			ClientHash = clientHash;
			PluginHash = pluginHash;
		}

		public override string ToString()
		{
			return string.Format("{0} {1}", Name, Version);
		}
	}
}
