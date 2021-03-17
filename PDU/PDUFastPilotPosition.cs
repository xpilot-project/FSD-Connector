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

		public double VelocityX
		{
			get;
			set;
		}

		public double VelocityY
		{
			get;
			set;
		}

		public double VelocityZ
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

		public PDUFastPilotPosition(string from, double lat, double lon, double alt, double pitch, double bank, double heading, double velocityLongitude, double velocityAltitude, double velocityLatitude, double velocityPitch, double velocityHeading, double velocityBank)
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
			Bank = bank;
			Heading = heading;
			VelocityX = velocityLongitude;
			VelocityY = velocityAltitude;
			VelocityZ = velocityLatitude;
			VelocityPitch = velocityPitch;
			VelocityHeading = velocityHeading;
			VelocityBank = velocityBank;
		}

		public override string Serialize()
		{
			double p = Pitch / -360.0;
			if (p < 0.0)
			{
				p += 1.0;
			}
			p *= 1024.0;
			double b = Bank / -360.0;
			if (b < 0.0)
			{
				b += 1.0;
			}
			b *= 1024.0;
			double h = Heading / 360.0 * 1024.0;
			uint pbh = ((uint)p << 22) | ((uint)b << 12) | ((uint)h << 2);
			StringBuilder msg = new StringBuilder("^");
			msg.Append(base.From);
			msg.Append(':');
			msg.Append(Lat.ToString("#0.0000000", CultureInfo.InvariantCulture));
			msg.Append(':');
			msg.Append(Lon.ToString("#0.0000000", CultureInfo.InvariantCulture));
			msg.Append(':');
			msg.Append(Altitude.ToString("#0.00", CultureInfo.InvariantCulture));
			msg.Append(':');
			msg.Append(pbh);
			msg.Append(':');
			msg.Append(VelocityX.ToString("#0.0000", CultureInfo.InvariantCulture));
			msg.Append(':');
			msg.Append(VelocityY.ToString("#0.0000", CultureInfo.InvariantCulture));
			msg.Append(':');
			msg.Append(VelocityZ.ToString("#0.0000", CultureInfo.InvariantCulture));
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
				uint pbh = uint.Parse(fields[4]);
				uint pitch = pbh >> 22;
				uint bank = (pbh >> 12) & 0x3FFu;
				uint hdg = (pbh >> 2) & 0x3FFu;
				double pitchDbl = (double)pitch / 1024.0 * -360.0;
				double bankDbl = (double)bank / 1024.0 * -360.0;
				double hdgDbl = (double)hdg / 1024.0 * 360.0;
				if (pitchDbl > 180.0)
				{
					pitchDbl -= 360.0;
				}
				else if (pitchDbl <= -180.0)
				{
					pitchDbl += 360.0;
				}
				if (bankDbl > 180.0)
				{
					bankDbl -= 360.0;
				}
				else if (bankDbl <= -180.0)
				{
					bankDbl += 360.0;
				}
				if (hdgDbl < 0.0)
				{
					hdgDbl += 360.0;
				}
				else if (hdgDbl >= 360.0)
				{
					hdgDbl -= 360.0;
				}
				return new PDUFastPilotPosition(fields[0], double.Parse(fields[1], CultureInfo.InvariantCulture), double.Parse(fields[2], CultureInfo.InvariantCulture), double.Parse(fields[3], CultureInfo.InvariantCulture), pitchDbl, bankDbl, hdgDbl, double.Parse(fields[5], CultureInfo.InvariantCulture), double.Parse(fields[6], CultureInfo.InvariantCulture), double.Parse(fields[7], CultureInfo.InvariantCulture), double.Parse(fields[8], CultureInfo.InvariantCulture), double.Parse(fields[9], CultureInfo.InvariantCulture), double.Parse(fields[10], CultureInfo.InvariantCulture));
			}
			catch (Exception ex)
			{
				throw new PDUFormatException("Parse error.", PDUBase.Reassemble(fields), ex);
			}
		}
	}
}