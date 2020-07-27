using System;

namespace Vatsim.Fsd.Connector
{
	public class ClientProperties
	{
		public string Name { get; set; }
		public string ClientPath { get; set; }
		public string PluginPath { get; set; }
		public Version Version { get; set; }

		public ClientProperties(string name, Version ver, string clientPath, string pluginPath)
		{
			Name = name;
			Version = ver;
			ClientPath = clientPath;
			PluginPath = pluginPath;
		}

		public override string ToString()
		{
			return string.Format("{0} {1}", Name, Version);
		}
	}
}
