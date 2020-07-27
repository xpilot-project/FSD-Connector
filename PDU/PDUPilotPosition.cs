using System;
using System.Globalization;
using System.Text;

namespace Vatsim.Fsd.Connector.PDU
{
	public class PDUPilotPosition : PDUBase
	{
		// Fields:

		private bool mIsSquawkingModeC;

		public int SquawkCode { get; set; }
		public bool IsSquawkingModeC { get { return mIsSquawkingModeC; } set { mIsSquawkingModeC = value; } }
		public bool IsSquawkingCharlie { get { return mIsSquawkingModeC; } set { mIsSquawkingModeC = value; } }
		public bool IsTransponderOn { get { return mIsSquawkingModeC; } set { mIsSquawkingModeC = value; } }
		public bool IsIdenting { get; set; }
		public NetworkRating Rating { get; set; }
		public double Lat { get; set; }
		public double Lon { get; set; }
		public int TrueAltitude { get; set; }
		public int PressureAltitude { get; set; }
		public int GroundSpeed { get; set; }
		public int Pitch { get; set; }
		public int Bank { get; set; }
		public int Heading { get; set; }

		public PDUPilotPosition(string from, int txCode, bool squawkingModeC, bool identing, NetworkRating rating, double lat, double lon, int trueAlt, int pressureAlt, int gs, int pitch, int bank, int heading)
			: base(from, "")
		{
			if (Double.IsNaN(lat)) throw new ArgumentException("Latitude must be a valid double precision number.", "lat");
			if (Double.IsNaN(lon)) throw new ArgumentException("Longitude must be a valid double precision number.", "lon");
			SquawkCode = txCode;
			mIsSquawkingModeC = squawkingModeC;
			IsIdenting = identing;
			Rating = rating;
			Lat = lat;
			Lon = lon;
			TrueAltitude = trueAlt;
			PressureAltitude = pressureAlt;
			GroundSpeed = gs;
			Pitch = pitch;
			Bank = bank;
			Heading = heading;
		}

		public override string Serialize()
		{
			// Convert PBH values into MSFS format.
			double p = (double)Pitch / -360.0;
			if (p < 0) p += 1.0;
			p *= 1024.0;
			double b = (double)Bank / -360.0;
			if (b < 0) b += 1.0;
			b *= 1024.0;
			double h = (double)Heading / 360.0 * 1024.0;

			// Shift the values into a 32 bit integer.
			uint pbh = ((uint)p << 22) | ((uint)b << 12) | ((uint)h << 2);

			// Assemble the PDU.
			StringBuilder msg = new StringBuilder("@");
			msg.Append(IsIdenting ? "Y" : (mIsSquawkingModeC ? "N" : "S"));
			msg.Append(DELIMITER);
			msg.Append(From);
			msg.Append(DELIMITER);
			msg.Append(SquawkCode.ToString("0000"));
			msg.Append(DELIMITER);
			msg.Append((int)Rating);
			msg.Append(DELIMITER);
			msg.Append(Lat.ToString("#0.0000000", CultureInfo.InvariantCulture));
			msg.Append(DELIMITER);
			msg.Append(Lon.ToString("#0.0000000", CultureInfo.InvariantCulture));
			msg.Append(DELIMITER);
			msg.Append(TrueAltitude);
			msg.Append(DELIMITER);
			msg.Append(GroundSpeed);
			msg.Append(DELIMITER);
			msg.Append(pbh);
			msg.Append(DELIMITER);
			msg.Append(PressureAltitude - TrueAltitude);
			return msg.ToString();
		}

		public static PDUPilotPosition Parse(string[] fields)
		{
			if (fields.Length < 10) throw new PDUFormatException("Invalid field count.", Reassemble(fields));
			try {
				uint pbh = uint.Parse(fields[8]);
				uint pitch = pbh >> 22;
				uint bank = (pbh >> 12) & 0x3FF;
				uint hdg = (pbh >> 2) & 0x3FF;
				double pitchDbl = (double)pitch / 1024.0 * -360.0;
				double bankDbl = (double)bank / 1024.0 * -360.0;
				double hdgDbl = (double)hdg / 1024.0 * 360.0;
				if (pitchDbl > 180.0) {
					pitchDbl -= 360.0;
				} else if (pitchDbl <= -180.0) {
					pitchDbl += 360.0;
				}
				if (bankDbl > 180.0) {
					bankDbl -= 360.0;
				} else if (bankDbl <= -180.0) {
					bankDbl += 360.0;
				}
				if (hdgDbl < 0.0) {
					hdgDbl += 360.0;
				} else if (hdgDbl >= 360.0) {
					hdgDbl -= 360.0;
				}
				bool identing = false;
				bool charlie = false;
				switch (fields[0].ToUpper()) {
					case "S":
						break;
					case "N":
						charlie = true;
						break;
					case "Y":
						charlie = true;
						identing = true;
						break;
				}
				return new PDUPilotPosition(
					fields[1],
					int.Parse(fields[2]),
					charlie,
					identing,
					(NetworkRating)Enum.Parse(typeof(NetworkRating), fields[3]),
					double.Parse(fields[4], CultureInfo.InvariantCulture),
					double.Parse(fields[5], CultureInfo.InvariantCulture),
					Convert.ToInt32(double.Parse(fields[6], CultureInfo.InvariantCulture)),
					Convert.ToInt32(double.Parse(fields[6], CultureInfo.InvariantCulture) + double.Parse(fields[9], CultureInfo.InvariantCulture)),
					Convert.ToInt32(double.Parse(fields[7], CultureInfo.InvariantCulture)),
					Convert.ToInt32(pitchDbl),
					Convert.ToInt32(bankDbl),
					Convert.ToInt32(hdgDbl)
				);
			}
			catch (Exception ex) {
				throw new PDUFormatException("Parse error.", Reassemble(fields), ex);
			}
		}
	}
}
