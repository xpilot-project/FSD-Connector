using System;
using System.Globalization;
using System.Text;

namespace Vatsim.FsdClient.PDU
{
	public class PDUFastPilotPosition : PDUBase
	{
		public double Lat
		{
			get;
			set;
		}

		public double Lon
		{
			get;
			set;
		}

		public double Altitude
		{
			get;
			set;
		}

		public double Pitch
		{
			get;
			set;
		}

		public double Bank
		{
			get;
			set;
		}

		public double Heading
		{
			get;
			set;
		}

		public double VelocityLongitude
		{
			get;
			set;
		}

		public double VelocityAltitude
		{
			get;
			set;
		}

		public double VelocityLatitude
		{
			get;
			set;
		}

		public double VelocityPitch
		{
			get;
			set;
		}

		public double VelocityBank
		{
			get;
			set;
		}

		public double VelocityHeading
		{
			get;
			set;
		}

		public PDUFastPilotPosition(string from, double lat, double lon, double alt, double pitch, double heading, double bank, double velocityLongitude, double velocityAltitude, double velocityLatitude, double velocityPitch, double velocityHeading, double velocityBank)
				: base(from, "")
		{
			if (double.IsNaN(lat))
			{
				throw new ArgumentException("Latitude must be a valid double precision number.", "lat");
			}
			if (double.IsNaN(lon))
			{
				throw new ArgumentException("Longitude must be a valid double precision number.", "lon");
			}
			Lat = lat;
			Lon = lon;
			Altitude = alt;
			Pitch = pitch;
			Heading = heading;
			Bank = bank;
			VelocityLongitude = velocityLongitude;
			VelocityAltitude = velocityAltitude;
			VelocityLatitude = velocityLatitude;
			VelocityPitch = velocityPitch;
			VelocityHeading = velocityHeading;
			VelocityBank = velocityBank;
		}

		public override string Serialize()
		{
			StringBuilder msg = new StringBuilder("^");
			msg.Append(From);
			msg.Append(':');
			msg.Append(Lat.ToString("#0.0000000", CultureInfo.InvariantCulture));
			msg.Append(':');
			msg.Append(Lon.ToString("#0.0000000", CultureInfo.InvariantCulture));
			msg.Append(':');
			msg.Append(Altitude.ToString("#0.00", CultureInfo.InvariantCulture));
			msg.Append(':');
			msg.Append(PackPitchBankHeading(Pitch, Bank, Heading));
			msg.Append(':');
			msg.Append(VelocityLongitude.ToString("#0.0000", CultureInfo.InvariantCulture));
			msg.Append(':');
			msg.Append(VelocityAltitude.ToString("#0.0000", CultureInfo.InvariantCulture));
			msg.Append(':');
			msg.Append(VelocityLatitude.ToString("#0.0000", CultureInfo.InvariantCulture));
			msg.Append(':');
			msg.Append(VelocityPitch.ToString("#0.0000", CultureInfo.InvariantCulture));
			msg.Append(':');
			msg.Append(VelocityHeading.ToString("#0.0000", CultureInfo.InvariantCulture));
			msg.Append(':');
			msg.Append(VelocityBank.ToString("#0.0000", CultureInfo.InvariantCulture));
			return msg.ToString();
		}

		public static PDUFastPilotPosition Parse(string[] fields)
		{
			if (fields.Length < 10)
			{
				throw new PDUFormatException("Invalid field count", PDUBase.Reassemble(fields));
			}
			try
			{
				UnpackPitchBankHeading(uint.Parse(fields[4]), out double pitch, out double bank, out double heading);
				return new PDUFastPilotPosition(
					fields[0],
					double.Parse(fields[1], CultureInfo.InvariantCulture),
					double.Parse(fields[2], CultureInfo.InvariantCulture),
					double.Parse(fields[3], CultureInfo.InvariantCulture),
					pitch,
					heading,
					bank,
					double.Parse(fields[5], CultureInfo.InvariantCulture),
					double.Parse(fields[6], CultureInfo.InvariantCulture),
					double.Parse(fields[7], CultureInfo.InvariantCulture),
					double.Parse(fields[8], CultureInfo.InvariantCulture),
					double.Parse(fields[9], CultureInfo.InvariantCulture),
					double.Parse(fields[10], CultureInfo.InvariantCulture));
			}
			catch (Exception ex)
			{
				throw new PDUFormatException("Parse error.", PDUBase.Reassemble(fields), ex);
			}
		}
	}
}