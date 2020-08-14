using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Vatsim.Fsd.Connector.PDU;

namespace Vatsim.Fsd.Connector
{
	public class FSDSession
	{
		[DllImport("Vatsim.Fsd.ClientAuth.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
		public static extern IntPtr GenerateAuthResponse(string challengeKey, string key, string clientPath, string pluginPath);

		[DllImport("Vatsim.Fsd.ClientAuth.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
		public static extern IntPtr GenerateAuthChallenge();

		[DllImport("Vatsim.Fsd.ClientAuth.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
		public static extern ushort ClientPublicKey();

		public ushort GetClientKey()
		{
			return ClientPublicKey();
		}

		private const int SERVER_AUTH_CHALLENGE_INTERVAL = 60000;
		private const int SERVER_AUTH_CHALLENGE_RESPONSE_WINDOW = 30000;

		public event EventHandler<NetworkEventArgs> NetworkConnected;
		public event EventHandler<NetworkEventArgs> NetworkDisconnected;
		public event EventHandler<NetworkEventArgs> NetworkConnectionFailed;
		public event EventHandler<NetworkErrorEventArgs> NetworkError;
		public event EventHandler<DataReceivedEventArgs<PDUATCPosition>> ATCPositionReceived;
		public event EventHandler<DataReceivedEventArgs<PDUPilotPosition>> PilotPositionReceived;
		public event EventHandler<DataReceivedEventArgs<PDUSecondaryVisCenter>> SecondaryVisCenterReceived;
		public event EventHandler<DataReceivedEventArgs<PDUClientIdentification>> ClientIdentificationReceived;
		public event EventHandler<DataReceivedEventArgs<PDUServerIdentification>> ServerIdentificationReceived;
		public event EventHandler<DataReceivedEventArgs<PDUAddATC>> AddATCReceived;
		public event EventHandler<DataReceivedEventArgs<PDUDeleteATC>> DeleteATCReceived;
		public event EventHandler<DataReceivedEventArgs<PDUAddPilot>> AddPilotReceived;
		public event EventHandler<DataReceivedEventArgs<PDUDeletePilot>> DeletePilotReceived;
		public event EventHandler<DataReceivedEventArgs<PDUTextMessage>> TextMessageReceived;
		public event EventHandler<DataReceivedEventArgs<PDUATCMessage>> ATCMessageReceived;
		public event EventHandler<DataReceivedEventArgs<PDURadioMessage>> RadioMessageReceived;
		public event EventHandler<DataReceivedEventArgs<PDUBroadcastMessage>> BroadcastMessageReceived;
		public event EventHandler<DataReceivedEventArgs<PDUWallop>> WallopReceived;
		public event EventHandler<DataReceivedEventArgs<PDUWeatherProfileRequest>> WeatherProfileRequestReceived;
		public event EventHandler<DataReceivedEventArgs<PDUWindData>> WindDataReceived;
		public event EventHandler<DataReceivedEventArgs<PDUTemperatureData>> TemperatureDataReceived;
		public event EventHandler<DataReceivedEventArgs<PDUCloudData>> CloudDataReceived;
		public event EventHandler<DataReceivedEventArgs<PDUHandoffCancelled>> HandoffCancelledReceived;
		public event EventHandler<DataReceivedEventArgs<PDUFlightStrip>> FlightStripReceived;
		public event EventHandler<DataReceivedEventArgs<PDUPushToDepartureList>> PushToDepartureListReceived;
		public event EventHandler<DataReceivedEventArgs<PDUPointout>> PointoutReceived;
		public event EventHandler<DataReceivedEventArgs<PDUIHaveTarget>> IHaveTargetReceived;
		public event EventHandler<DataReceivedEventArgs<PDUSharedState>> SharedStateReceived;
		public event EventHandler<DataReceivedEventArgs<PDULandLineCommand>> LandLineCommandReceived;
		public event EventHandler<DataReceivedEventArgs<PDUPlaneInfoRequest>> PlaneInfoRequestReceived;
		public event EventHandler<DataReceivedEventArgs<PDUPlaneInfoResponse>> PlaneInfoResponseReceived;
		public event EventHandler<DataReceivedEventArgs<PDULegacyPlaneInfoResponse>> LegacyPlaneInfoResponseReceived;
		public event EventHandler<DataReceivedEventArgs<PDUFlightPlan>> FlightPlanReceived;
		public event EventHandler<DataReceivedEventArgs<PDUFlightPlanAmendment>> FlightPlanAmendmentReceived;
		public event EventHandler<DataReceivedEventArgs<PDUPing>> PingReceived;
		public event EventHandler<DataReceivedEventArgs<PDUPong>> PongReceived;
		public event EventHandler<DataReceivedEventArgs<PDUHandoff>> HandoffReceived;
		public event EventHandler<DataReceivedEventArgs<PDUHandoffAccept>> HandoffAcceptReceived;
		public event EventHandler<DataReceivedEventArgs<PDUMetarRequest>> AcarsQueryReceived;
		public event EventHandler<DataReceivedEventArgs<PDUMetarResponse>> AcarsResponseReceived;
		public event EventHandler<DataReceivedEventArgs<PDUClientQuery>> ClientQueryReceived;
		public event EventHandler<DataReceivedEventArgs<PDUClientQueryResponse>> ClientQueryResponseReceived;
		public event EventHandler<DataReceivedEventArgs<PDUAuthChallenge>> AuthChallengeReceived;
		public event EventHandler<DataReceivedEventArgs<PDUAuthResponse>> AuthResponseReceived;
		public event EventHandler<DataReceivedEventArgs<PDUKillRequest>> KillRequestReceived;
		public event EventHandler<DataReceivedEventArgs<PDUProtocolError>> ProtocolErrorReceived;
		public event EventHandler<DataReceivedEventArgs<PDUVersionRequest>> VersionRequestReceived;
		public event EventHandler<RawDataEventArgs> RawDataSent;
		public event EventHandler<RawDataEventArgs> RawDataReceived;

		private Socket mClientSocket;
		private AsyncCallback mIncomingDataCallBack;
		private string mPartialPacket = "";
		private string mClientAuthSessionKey = "";
		private string mClientAuthChallengeKey = "";
		private readonly object mUserData;
		private SynchronizationContext mSyncContext;
		private bool mChallengeServer;
		private string mServerAuthSessionKey = string.Empty;
		private string mServerAuthChallengeKey = string.Empty;
		private string mLastServerAuthChallenge = string.Empty;
		private Timer mServerAuthTimer;
		private string mCurrentCallsign;

		public bool Connected
		{
			get => mClientSocket != null && mClientSocket.Connected;
		}

		public bool IgnoreUnknownPackets { get; set; }

		public ClientProperties ClientProperties { get; set; }

		public FSDSession(ClientProperties properties, object userData, SynchronizationContext syncContext)
		{
			ClientProperties = properties;
			mUserData = userData;
			mSyncContext = syncContext;
		}

		public FSDSession(ClientProperties properties, object userData)
			: this(properties, userData, null)
		{
		}

		public FSDSession(ClientProperties properties, SynchronizationContext syncContext)
			: this(properties, null, syncContext)
		{
		}

		public FSDSession(ClientProperties properties)
			: this(properties, null, null)
		{
		}

		private void RaiseNetworkConnected()
		{
			if (NetworkConnected != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						NetworkConnected(this, new NetworkEventArgs(mUserData));
					}, null);
				}
				else
				{
					NetworkConnected(this, new NetworkEventArgs(mUserData));
				}
			}
		}

		private void RaiseNetworkDisconnected()
		{
			if (NetworkDisconnected != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						NetworkDisconnected(this, new NetworkEventArgs(mUserData));
					}, null);
				}
				else
				{
					NetworkDisconnected(this, new NetworkEventArgs(mUserData));
				}
			}
		}

		private void RaiseNetworkConnectionFailed()
		{
			if (NetworkConnectionFailed != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						NetworkConnectionFailed(this, new NetworkEventArgs(mUserData));
					}, null);
				}
				else
				{
					NetworkConnectionFailed(this, new NetworkEventArgs(mUserData));
				}
			}
		}

		private void RaiseNetworkError(string message)
		{
			if (NetworkError != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						NetworkError(this, new NetworkErrorEventArgs(message, mUserData));
					}, null);
				}
				else
				{
					NetworkError(this, new NetworkErrorEventArgs(message, mUserData));
				}
			}
		}

		private void RaiseRawDataSent(string data)
		{
			if (RawDataSent != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						RawDataSent(this, new RawDataEventArgs(data, mUserData));
					}, null);
				}
				else
				{
					RawDataSent(this, new RawDataEventArgs(data, mUserData));
				}
			}
		}

		private void RaiseRawDataReceived(string data)
		{
			if (RawDataReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						RawDataReceived(this, new RawDataEventArgs(data, mUserData));
					}, null);
				}
				else
				{
					RawDataReceived(this, new RawDataEventArgs(data, mUserData));
				}
			}
		}

		private void RaiseATCPositionReceived(PDUATCPosition pdu)
		{
			if (ATCPositionReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						ATCPositionReceived(this, new DataReceivedEventArgs<PDUATCPosition>(pdu, mUserData));
					}, null);
				}
				else
				{
					ATCPositionReceived(this, new DataReceivedEventArgs<PDUATCPosition>(pdu, mUserData));
				}
			}
		}

		private void RaisePilotPositionReceived(PDUPilotPosition pdu)
		{
			if (PilotPositionReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						PilotPositionReceived(this, new DataReceivedEventArgs<PDUPilotPosition>(pdu, mUserData));
					}, null);
				}
				else
				{
					PilotPositionReceived(this, new DataReceivedEventArgs<PDUPilotPosition>(pdu, mUserData));
				}
			}
		}

		private void RaiseSecondaryVisCenterReceived(PDUSecondaryVisCenter pdu)
		{
			if (SecondaryVisCenterReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						SecondaryVisCenterReceived(this, new DataReceivedEventArgs<PDUSecondaryVisCenter>(pdu, mUserData));
					}, null);
				}
				else
				{
					SecondaryVisCenterReceived(this, new DataReceivedEventArgs<PDUSecondaryVisCenter>(pdu, mUserData));
				}
			}
		}

		private void RaiseClientIdentificationReceived(PDUClientIdentification pdu)
		{
			if (ClientIdentificationReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						ClientIdentificationReceived(this, new DataReceivedEventArgs<PDUClientIdentification>(pdu, mUserData));
					}, null);
				}
				else
				{
					ClientIdentificationReceived(this, new DataReceivedEventArgs<PDUClientIdentification>(pdu, mUserData));
				}
			}
		}

		private void RaiseServerIdentificationReceived(PDUServerIdentification pdu)
		{
			if (ServerIdentificationReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						ServerIdentificationReceived(this, new DataReceivedEventArgs<PDUServerIdentification>(pdu, mUserData));
					}, null);
				}
				else
				{
					ServerIdentificationReceived(this, new DataReceivedEventArgs<PDUServerIdentification>(pdu, mUserData));
				}
			}
		}

		private void RaiseAddATCReceived(PDUAddATC pdu)
		{
			if (AddATCReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						AddATCReceived(this, new DataReceivedEventArgs<PDUAddATC>(pdu, mUserData));
					}, null);
				}
				else
				{
					AddATCReceived(this, new DataReceivedEventArgs<PDUAddATC>(pdu, mUserData));
				}
			}
		}

		private void RaiseDeleteATCReceived(PDUDeleteATC pdu)
		{
			if (DeleteATCReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						DeleteATCReceived(this, new DataReceivedEventArgs<PDUDeleteATC>(pdu, mUserData));
					}, null);
				}
				else
				{
					DeleteATCReceived(this, new DataReceivedEventArgs<PDUDeleteATC>(pdu, mUserData));
				}
			}
		}

		private void RaiseAddPilotReceived(PDUAddPilot pdu)
		{
			if (AddPilotReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						AddPilotReceived(this, new DataReceivedEventArgs<PDUAddPilot>(pdu, mUserData));
					}, null);
				}
				else
				{
					AddPilotReceived(this, new DataReceivedEventArgs<PDUAddPilot>(pdu, mUserData));
				}
			}
		}

		private void RaiseDeletePilotReceived(PDUDeletePilot pdu)
		{
			if (DeletePilotReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						DeletePilotReceived(this, new DataReceivedEventArgs<PDUDeletePilot>(pdu, mUserData));
					}, null);
				}
				else
				{
					DeletePilotReceived(this, new DataReceivedEventArgs<PDUDeletePilot>(pdu, mUserData));
				}
			}
		}

		private void RaiseTextMessageReceived(PDUTextMessage pdu)
		{
			if (TextMessageReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						TextMessageReceived(this, new DataReceivedEventArgs<PDUTextMessage>(pdu, mUserData));
					}, null);
				}
				else
				{
					TextMessageReceived(this, new DataReceivedEventArgs<PDUTextMessage>(pdu, mUserData));
				}
			}
		}

		private void RaiseATCMessageReceived(PDUATCMessage pdu)
		{
			if (ATCMessageReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						ATCMessageReceived(this, new DataReceivedEventArgs<PDUATCMessage>(pdu, mUserData));
					}, null);
				}
				else
				{
					ATCMessageReceived(this, new DataReceivedEventArgs<PDUATCMessage>(pdu, mUserData));
				}
			}
		}

		private void RaiseRadioMessageReceived(PDURadioMessage pdu)
		{
			if (RadioMessageReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						RadioMessageReceived(this, new DataReceivedEventArgs<PDURadioMessage>(pdu, mUserData));
					}, null);
				}
				else
				{
					RadioMessageReceived(this, new DataReceivedEventArgs<PDURadioMessage>(pdu, mUserData));
				}
			}
		}

		private void RaiseBroadcastMessageReceived(PDUBroadcastMessage pdu)
		{
			if (BroadcastMessageReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						BroadcastMessageReceived(this, new DataReceivedEventArgs<PDUBroadcastMessage>(pdu, mUserData));
					}, null);
				}
				else
				{
					BroadcastMessageReceived(this, new DataReceivedEventArgs<PDUBroadcastMessage>(pdu, mUserData));
				}
			}
		}

		private void RaiseWallopReceived(PDUWallop pdu)
		{
			if (WallopReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						WallopReceived(this, new DataReceivedEventArgs<PDUWallop>(pdu, mUserData));
					}, null);
				}
				else
				{
					WallopReceived(this, new DataReceivedEventArgs<PDUWallop>(pdu, mUserData));
				}
			}
		}

		private void RaiseWeatherProfileRequestReceived(PDUWeatherProfileRequest pdu)
		{
			if (WeatherProfileRequestReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						WeatherProfileRequestReceived(this, new DataReceivedEventArgs<PDUWeatherProfileRequest>(pdu, mUserData));
					}, null);
				}
				else
				{
					WeatherProfileRequestReceived(this, new DataReceivedEventArgs<PDUWeatherProfileRequest>(pdu, mUserData));
				}
			}
		}

		private void RaiseWindDataReceived(PDUWindData pdu)
		{
			if (WindDataReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						WindDataReceived(this, new DataReceivedEventArgs<PDUWindData>(pdu, mUserData));
					}, null);
				}
				else
				{
					WindDataReceived(this, new DataReceivedEventArgs<PDUWindData>(pdu, mUserData));
				}
			}
		}

		private void RaiseTemperatureDataReceived(PDUTemperatureData pdu)
		{
			if (TemperatureDataReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						TemperatureDataReceived(this, new DataReceivedEventArgs<PDUTemperatureData>(pdu, mUserData));
					}, null);
				}
				else
				{
					TemperatureDataReceived(this, new DataReceivedEventArgs<PDUTemperatureData>(pdu, mUserData));
				}
			}
		}

		private void RaiseCloudDataReceived(PDUCloudData pdu)
		{
			if (CloudDataReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						CloudDataReceived(this, new DataReceivedEventArgs<PDUCloudData>(pdu, mUserData));
					}, null);
				}
				else
				{
					CloudDataReceived(this, new DataReceivedEventArgs<PDUCloudData>(pdu, mUserData));
				}
			}
		}

		private void RaiseHandoffCancelledReceived(PDUHandoffCancelled pdu)
		{
			if (HandoffCancelledReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						HandoffCancelledReceived(this, new DataReceivedEventArgs<PDUHandoffCancelled>(pdu, mUserData));
					}, null);
				}
				else
				{
					HandoffCancelledReceived(this, new DataReceivedEventArgs<PDUHandoffCancelled>(pdu, mUserData));
				}
			}
		}

		private void RaiseFlightStripReceived(PDUFlightStrip pdu)
		{
			if (FlightStripReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						FlightStripReceived(this, new DataReceivedEventArgs<PDUFlightStrip>(pdu, mUserData));
					}, null);
				}
				else
				{
					FlightStripReceived(this, new DataReceivedEventArgs<PDUFlightStrip>(pdu, mUserData));
				}
			}
		}

		private void RaisePushToDepartureListReceived(PDUPushToDepartureList pdu)
		{
			if (PushToDepartureListReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						PushToDepartureListReceived(this, new DataReceivedEventArgs<PDUPushToDepartureList>(pdu, mUserData));
					}, null);
				}
				else
				{
					PushToDepartureListReceived(this, new DataReceivedEventArgs<PDUPushToDepartureList>(pdu, mUserData));
				}
			}
		}

		private void RaisePointoutReceived(PDUPointout pdu)
		{
			if (PointoutReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						PointoutReceived(this, new DataReceivedEventArgs<PDUPointout>(pdu, mUserData));
					}, null);
				}
				else
				{
					PointoutReceived(this, new DataReceivedEventArgs<PDUPointout>(pdu, mUserData));
				}
			}
		}

		private void RaiseIHaveTargetReceived(PDUIHaveTarget pdu)
		{
			if (IHaveTargetReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						IHaveTargetReceived(this, new DataReceivedEventArgs<PDUIHaveTarget>(pdu, mUserData));
					}, null);
				}
				else
				{
					IHaveTargetReceived(this, new DataReceivedEventArgs<PDUIHaveTarget>(pdu, mUserData));
				}
			}
		}

		private void RaiseSharedStateReceived(PDUSharedState pdu)
		{
			if (SharedStateReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						SharedStateReceived(this, new DataReceivedEventArgs<PDUSharedState>(pdu, mUserData));
					}, null);
				}
				else
				{
					SharedStateReceived(this, new DataReceivedEventArgs<PDUSharedState>(pdu, mUserData));
				}
			}
		}

		private void RaiseLandLineCommandReceived(PDULandLineCommand pdu)
		{
			if (LandLineCommandReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						LandLineCommandReceived(this, new DataReceivedEventArgs<PDULandLineCommand>(pdu, mUserData));
					}, null);
				}
				else
				{
					LandLineCommandReceived(this, new DataReceivedEventArgs<PDULandLineCommand>(pdu, mUserData));
				}
			}
		}

		private void RaisePlaneInfoRequestReceived(PDUPlaneInfoRequest pdu)
		{
			if (PlaneInfoRequestReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						PlaneInfoRequestReceived(this, new DataReceivedEventArgs<PDUPlaneInfoRequest>(pdu, mUserData));
					}, null);
				}
				else
				{
					PlaneInfoRequestReceived(this, new DataReceivedEventArgs<PDUPlaneInfoRequest>(pdu, mUserData));
				}
			}
		}

		private void RaisePlaneInfoResponseReceived(PDUPlaneInfoResponse pdu)
		{
			if (PlaneInfoResponseReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						PlaneInfoResponseReceived(this, new DataReceivedEventArgs<PDUPlaneInfoResponse>(pdu, mUserData));
					}, null);
				}
				else
				{
					PlaneInfoResponseReceived(this, new DataReceivedEventArgs<PDUPlaneInfoResponse>(pdu, mUserData));
				}
			}
		}

		private void RaiseLegacyPlaneInfoResponseReceived(PDULegacyPlaneInfoResponse pdu)
		{
			if (LegacyPlaneInfoResponseReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						LegacyPlaneInfoResponseReceived(this, new DataReceivedEventArgs<PDULegacyPlaneInfoResponse>(pdu, mUserData));
					}, null);
				}
				else
				{
					LegacyPlaneInfoResponseReceived(this, new DataReceivedEventArgs<PDULegacyPlaneInfoResponse>(pdu, mUserData));
				}
			}
		}

		private void RaiseFlightPlanReceived(PDUFlightPlan pdu)
		{
			if (FlightPlanReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						FlightPlanReceived(this, new DataReceivedEventArgs<PDUFlightPlan>(pdu, mUserData));
					}, null);
				}
				else
				{
					FlightPlanReceived(this, new DataReceivedEventArgs<PDUFlightPlan>(pdu, mUserData));
				}
			}
		}

		private void RaiseFlightPlanAmendmentReceived(PDUFlightPlanAmendment pdu)
		{
			if (FlightPlanAmendmentReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						FlightPlanAmendmentReceived(this, new DataReceivedEventArgs<PDUFlightPlanAmendment>(pdu, mUserData));
					}, null);
				}
				else
				{
					FlightPlanAmendmentReceived(this, new DataReceivedEventArgs<PDUFlightPlanAmendment>(pdu, mUserData));
				}
			}
		}

		private void RaisePingReceived(PDUPing pdu)
		{
			if (PingReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						PingReceived(this, new DataReceivedEventArgs<PDUPing>(pdu, mUserData));
					}, null);
				}
				else
				{
					PingReceived(this, new DataReceivedEventArgs<PDUPing>(pdu, mUserData));
				}
			}
		}

		private void RaisePongReceived(PDUPong pdu)
		{
			if (PongReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						PongReceived(this, new DataReceivedEventArgs<PDUPong>(pdu, mUserData));
					}, null);
				}
				else
				{
					PongReceived(this, new DataReceivedEventArgs<PDUPong>(pdu, mUserData));
				}
			}
		}

		private void RaiseHandoffReceived(PDUHandoff pdu)
		{
			if (HandoffReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						HandoffReceived(this, new DataReceivedEventArgs<PDUHandoff>(pdu, mUserData));
					}, null);
				}
				else
				{
					HandoffReceived(this, new DataReceivedEventArgs<PDUHandoff>(pdu, mUserData));
				}
			}
		}

		private void RaiseHandoffAcceptReceived(PDUHandoffAccept pdu)
		{
			if (HandoffAcceptReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						HandoffAcceptReceived(this, new DataReceivedEventArgs<PDUHandoffAccept>(pdu, mUserData));
					}, null);
				}
				else
				{
					HandoffAcceptReceived(this, new DataReceivedEventArgs<PDUHandoffAccept>(pdu, mUserData));
				}
			}
		}

		private void RaiseAcarsQueryReceived(PDUMetarRequest pdu)
		{
			if (AcarsQueryReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						AcarsQueryReceived(this, new DataReceivedEventArgs<PDUMetarRequest>(pdu, mUserData));
					}, null);
				}
				else
				{
					AcarsQueryReceived(this, new DataReceivedEventArgs<PDUMetarRequest>(pdu, mUserData));
				}
			}
		}

		private void RaiseAcarsResponseReceived(PDUMetarResponse pdu)
		{
			if (AcarsResponseReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						AcarsResponseReceived(this, new DataReceivedEventArgs<PDUMetarResponse>(pdu, mUserData));
					}, null);
				}
				else
				{
					AcarsResponseReceived(this, new DataReceivedEventArgs<PDUMetarResponse>(pdu, mUserData));
				}
			}
		}

		private void RaiseClientQueryReceived(PDUClientQuery pdu)
		{
			if (ClientQueryReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						ClientQueryReceived(this, new DataReceivedEventArgs<PDUClientQuery>(pdu, mUserData));
					}, null);
				}
				else
				{
					ClientQueryReceived(this, new DataReceivedEventArgs<PDUClientQuery>(pdu, mUserData));
				}
			}
		}

		private void RaiseClientQueryResponseReceived(PDUClientQueryResponse pdu)
		{
			if (ClientQueryResponseReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						ClientQueryResponseReceived(this, new DataReceivedEventArgs<PDUClientQueryResponse>(pdu, mUserData));
					}, null);
				}
				else
				{
					ClientQueryResponseReceived(this, new DataReceivedEventArgs<PDUClientQueryResponse>(pdu, mUserData));
				}
			}
		}

		private void RaiseAuthChallengeReceived(PDUAuthChallenge pdu)
		{
			if (AuthChallengeReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						AuthChallengeReceived(this, new DataReceivedEventArgs<PDUAuthChallenge>(pdu, mUserData));
					}, null);
				}
				else
				{
					AuthChallengeReceived(this, new DataReceivedEventArgs<PDUAuthChallenge>(pdu, mUserData));
				}
			}
		}

		private void RaiseAuthResponseReceived(PDUAuthResponse pdu)
		{
			if (AuthResponseReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						AuthResponseReceived(this, new DataReceivedEventArgs<PDUAuthResponse>(pdu, mUserData));
					}, null);
				}
				else
				{
					AuthResponseReceived(this, new DataReceivedEventArgs<PDUAuthResponse>(pdu, mUserData));
				}
			}
		}

		private void RaiseKillRequestReceived(PDUKillRequest pdu)
		{
			if (KillRequestReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						KillRequestReceived(this, new DataReceivedEventArgs<PDUKillRequest>(pdu, mUserData));
					}, null);
				}
				else
				{
					KillRequestReceived(this, new DataReceivedEventArgs<PDUKillRequest>(pdu, mUserData));
				}
			}
		}

		private void RaiseProtocolErrorReceived(PDUProtocolError pdu)
		{
			if (ProtocolErrorReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						ProtocolErrorReceived(this, new DataReceivedEventArgs<PDUProtocolError>(pdu, mUserData));
					}, null);
				}
				else
				{
					ProtocolErrorReceived(this, new DataReceivedEventArgs<PDUProtocolError>(pdu, mUserData));
				}
			}
		}

		private void RaiseVersionRequestReceived(PDUVersionRequest pdu)
		{
			if (VersionRequestReceived != null)
			{
				if (mSyncContext != null)
				{
					mSyncContext.Post((o) =>
					{
						VersionRequestReceived(this, new DataReceivedEventArgs<PDUVersionRequest>(pdu, mUserData));
					}, null);
				}
				else
				{
					VersionRequestReceived(this, new DataReceivedEventArgs<PDUVersionRequest>(pdu, mUserData));
				}
			}
		}

		public void SetSyncContext(SynchronizationContext context)
		{
			mSyncContext = context;
		}

		public void Connect(string address, int port, bool challengeServer = true)
		{
			mChallengeServer = challengeServer;
			try
			{
				mClientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				if (Regex.IsMatch(address, @"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$"))
				{
					BeginConnect(IPAddress.Parse(address), port);
					return;
				}
				Dns.BeginGetHostEntry(address, new AsyncCallback(ResolveServerCallback), port);
			}
			catch (Exception se)
			{
				RaiseNetworkError(string.Format("Connection failed: {0}", se.Message));
				RaiseNetworkConnectionFailed();
			}
		}

		private void BeginConnect(IPAddress ip, int port)
		{
			IPEndPoint ipEnd = new IPEndPoint(ip, port);
			mClientSocket.BeginConnect(ipEnd, new AsyncCallback(ConnectCallback), mClientSocket);
		}

		private void ResolveServerCallback(IAsyncResult ar)
		{
			try
			{
				IPHostEntry hostInfo = Dns.EndGetHostEntry(ar);
				IPAddress ip = (from a in hostInfo.AddressList
								where a.AddressFamily == AddressFamily.InterNetwork
								select a).First();
				BeginConnect(ip, (int)ar.AsyncState);
			}
			catch (Exception ex)
			{
				RaiseNetworkError(string.Format("Connection failed: {0}", ex.Message));
				RaiseNetworkConnectionFailed();
			}
		}

		private void ConnectCallback(IAsyncResult ar)
		{
			Socket sock = (Socket)ar.AsyncState;
			try
			{
				sock.EndConnect(ar);
				RaiseNetworkConnected();
				WaitForData();
			}
			catch (SocketException se)
			{
				RaiseNetworkError(string.Format("Connection failed: ({0}) {1}", se.ErrorCode, se.Message));
				RaiseNetworkConnectionFailed();
			}
			catch (ObjectDisposedException)
			{
				return;
			}
		}

		public void Disconnect()
		{
			ResetServerAuthSession();
			if (mClientSocket != null)
			{
				try
				{
					mClientSocket.Shutdown(SocketShutdown.Both);
					mClientSocket.Close();
				}
				catch (ObjectDisposedException) { }
				catch (SocketException) { }
				mClientSocket = null;
				RaiseNetworkDisconnected();
			}
		}

		private void SendData(string data)
		{
			if (!Connected)
			{
				return;
			}
			try
			{
				byte[] bytes = Encoding.Default.GetBytes(data);
				if (mClientSocket != null)
				{
					mClientSocket.BeginSend(bytes, 0, bytes.Length, SocketFlags.None, new AsyncCallback(SendCallback), mClientSocket);
				}

				RaiseRawDataSent(data);
			}
			catch (SocketException se)
			{
				if ((se.ErrorCode == 10053) || (se.ErrorCode == 10054))
				{
					Disconnect();
				}
				else
				{
					string err = string.Format("Send failed: ({0}) {1}", se.ErrorCode, se.Message);
					RaiseNetworkError(err);
				}
			}
		}

		private void SendCallback(IAsyncResult iar)
		{
			try
			{
				Socket sock = (Socket)iar.AsyncState;
				int bytesSent = sock.EndSend(iar);
			}
			catch (ObjectDisposedException) { } // OK to swallow these ... just means the socket was closed.
		}

		public void SendPDU(PDUBase pdu)
		{
			if (mChallengeServer)
			{
				if ((pdu is PDUClientIdentification) && string.IsNullOrEmpty((pdu as PDUClientIdentification).InitialChallenge))
				{
					string initialChallenge = Marshal.PtrToStringAnsi(GenerateAuthChallenge());
					mServerAuthSessionKey = Marshal.PtrToStringAnsi(GenerateAuthResponse(initialChallenge, null, ClientProperties.ClientHash, ClientProperties.PluginHash));
					(pdu as PDUClientIdentification).InitialChallenge = initialChallenge;
				}
				if (
					((pdu is PDUAddPilot) && ((pdu as PDUAddPilot).ProtocolRevision >= ProtocolRevision.VatsimAuth))
					||
					((pdu is PDUAddATC) && ((pdu as PDUAddATC).ProtocolRevision >= ProtocolRevision.VatsimAuth))
				)
				{
					mCurrentCallsign = pdu.From;
					mServerAuthTimer = new Timer(CheckServerAuth);
					mServerAuthTimer.Change(SERVER_AUTH_CHALLENGE_RESPONSE_WINDOW, Timeout.Infinite);
				}
			}
			SendData(pdu.Serialize() + PDUBase.PACKET_DELIMITER);
		}

		private void CheckServerAuth(object state)
		{
			// Check if this is the first auth check. If so, we generate the session key and send a challenge.
			if (string.IsNullOrEmpty(mServerAuthChallengeKey))
			{
				mServerAuthChallengeKey = mServerAuthSessionKey;
				ChallengeServer();
				return;
			}

			// Check if we have a pending auth challenge. If we do, then the server has failed to respond to
			// the challenge in time, so we disconnect.
			if (!string.IsNullOrEmpty(mLastServerAuthChallenge))
			{
				RaiseNetworkError("The server has failed to respond to the authentication challenge.");
				Disconnect();
			}

			// If none of the above, challenge the server.
			ChallengeServer();
		}

		private void ChallengeServer()
		{
			mLastServerAuthChallenge = Marshal.PtrToStringAnsi(GenerateAuthChallenge());
			PDUAuthChallenge pdu = new PDUAuthChallenge(mCurrentCallsign, PDUBase.SERVER_CALLSIGN, mLastServerAuthChallenge);
			SendPDU(pdu);
			mServerAuthTimer.Change(SERVER_AUTH_CHALLENGE_RESPONSE_WINDOW, Timeout.Infinite);
		}

		private void CheckServerAuthChallengeResponse(string response)
		{
			if (mServerAuthTimer == null)
			{
				return;
			}
			string expectedResponse = Marshal.PtrToStringAnsi(GenerateAuthResponse(mLastServerAuthChallenge, mServerAuthChallengeKey, ClientProperties.ClientHash, ClientProperties.PluginHash));
			if (response != expectedResponse)
			{
				RaiseNetworkError("The server has failed to respond correctly to the authentication challenge.");
				Disconnect();
			}
			else
			{
				mLastServerAuthChallenge = string.Empty;
				mServerAuthChallengeKey = GenerateMD5Digest(mServerAuthSessionKey + response);
				mServerAuthTimer.Change(SERVER_AUTH_CHALLENGE_INTERVAL, Timeout.Infinite);
			}
		}

		private void ResetServerAuthSession()
		{
			if (mServerAuthTimer != null)
			{
				mServerAuthTimer.Change(Timeout.Infinite, Timeout.Infinite);
			}
			mServerAuthSessionKey = string.Empty;
			mServerAuthChallengeKey = string.Empty;
			mLastServerAuthChallenge = string.Empty;
		}

		private class SocketPacket
		{
			public Socket mThisSocket;
			public byte[] mDataBuffer = new byte[1024];
		}

		private void WaitForData()
		{
			try
			{
				if (mIncomingDataCallBack == null)
				{
					mIncomingDataCallBack = new AsyncCallback(OnDataReceived);
				}
				SocketPacket theSockPkt = new SocketPacket
				{
					mThisSocket = mClientSocket
				};
				if (mClientSocket == null)
				{
					return;
				}
				mClientSocket.BeginReceive(
					theSockPkt.mDataBuffer,
					0, theSockPkt.mDataBuffer.Length,
					SocketFlags.None,
					mIncomingDataCallBack,
					theSockPkt
				);
			}
			catch (SocketException se)
			{
				if ((se.ErrorCode == 10053) || (se.ErrorCode == 10054))
				{
					Disconnect();
				}
				else
				{
					string err = string.Format("BeginReceive failed: ({0}) {1}", se.ErrorCode, se.Message);
					RaiseNetworkError(err);
				}
			}
		}

		private void OnDataReceived(IAsyncResult asyn)
		{
			try
			{
				SocketPacket theSockId = (SocketPacket)asyn.AsyncState;
				int bytesReceived = theSockId.mThisSocket.EndReceive(asyn);
				if (bytesReceived == 0)
				{
					Disconnect();
					return;
				}
				char[] chars = new char[bytesReceived + 1];
				Decoder d = Encoding.Default.GetDecoder();
				int charLen = d.GetChars(theSockId.mDataBuffer, 0, bytesReceived, chars, 0);
				String data = new String(chars);
				ProcessData(data);
				WaitForData();
			}
			catch (ObjectDisposedException)
			{
				Disconnect();
			}
			catch (SocketException se)
			{
				string err = string.Format("EndReceive failed: ({0}) {1}", se.ErrorCode, se.Message);
				RaiseNetworkError(err);
				Disconnect();
			}
		}

		private void ProcessData(string data)
		{
			if (data.Length == 0)
			{
				return;
			}

			// Strip out trailing null, if any.
			if (data.Substring(data.Length - 1) == "\0")
			{
				data = data.Substring(0, data.Length - 1);
			}

			data = mPartialPacket + data;
			mPartialPacket = "";

			// Split the data into PDUs.
			string[] packets = data.Split(new string[] { PDUBase.PACKET_DELIMITER }, StringSplitOptions.None);

			// If the last packet has content, it's an incomplete packet.
			int topIndex = packets.Length - 1;
			if (packets[topIndex].Length > 0)
			{
				mPartialPacket = packets[topIndex];
				packets[topIndex] = "";
			}

			// Process each packet.
			foreach (string packet in packets)
			{
				if (packet.Length == 0)
				{
					continue;
				}

				RaiseRawDataReceived(packet + PDUBase.PACKET_DELIMITER);
				try
				{
					string[] fields = packet.Split(new char[] { PDUBase.DELIMITER }, StringSplitOptions.None);
					char prefixChar = fields[0][0];
					switch (prefixChar)
					{
						case '@':
							fields[0] = fields[0].Substring(1);
							RaisePilotPositionReceived(PDUPilotPosition.Parse(fields));
							break;
						case '%':
							fields[0] = fields[0].Substring(1);
							RaiseATCPositionReceived(PDUATCPosition.Parse(fields));
							break;
						case '\'':
							fields[0] = fields[0].Substring(1);
							RaiseSecondaryVisCenterReceived(PDUSecondaryVisCenter.Parse(fields));
							break;
						case '#':
						case '$':
							if (fields[0].Length < 3)
							{
								throw new PDUFormatException("Invalid PDU type.", packet);
							}
							string pduTypeID = fields[0].Substring(0, 3);
							fields[0] = fields[0].Substring(3);
							switch (pduTypeID)
							{
								case "$DI":
									{
										PDUServerIdentification pdu = PDUServerIdentification.Parse(fields);
										if (GetClientKey() != 0)
										{
											mClientAuthSessionKey = Marshal.PtrToStringAnsi(GenerateAuthResponse(pdu.InitialChallengeKey, null, ClientProperties.ClientHash, ClientProperties.PluginHash));
											mClientAuthChallengeKey = mClientAuthSessionKey;
										}
										RaiseServerIdentificationReceived(pdu);
										break;
									}
								case "$ID":
									RaiseClientIdentificationReceived(PDUClientIdentification.Parse(fields));
									break;
								case "#AA":
									RaiseAddATCReceived(PDUAddATC.Parse(fields));
									break;
								case "#DA":
									RaiseDeleteATCReceived(PDUDeleteATC.Parse(fields));
									break;
								case "#AP":
									RaiseAddPilotReceived(PDUAddPilot.Parse(fields));
									break;
								case "#DP":
									RaiseDeletePilotReceived(PDUDeletePilot.Parse(fields));
									break;
								case "#TM":
									ProcessTM(fields);
									break;
								case "#WX":
									RaiseWeatherProfileRequestReceived(PDUWeatherProfileRequest.Parse(fields));
									break;
								case "#WD":
									RaiseWindDataReceived(PDUWindData.Parse(fields));
									break;
								case "#DL":
									// Specs dictate that this packet should be disregarded.
									break;
								case "#TD":
									RaiseTemperatureDataReceived(PDUTemperatureData.Parse(fields));
									break;
								case "#CD":
									RaiseCloudDataReceived(PDUCloudData.Parse(fields));
									break;
								case "#PC":
									if (fields.Length < 4)
									{
										if (!IgnoreUnknownPackets)
										{
											throw new PDUFormatException("Too few fields in #PC packet.", packet);
										}
									}
									else if (fields[2] != "CCP")
									{
										if (!IgnoreUnknownPackets)
										{
											throw new PDUFormatException("Unknown #PC packet type.", packet);
										}
									}
									else
									{
										switch (fields[3])
										{
											case "VER":
												RaiseVersionRequestReceived(PDUVersionRequest.Parse(fields));
												break;
											case "ID":
											case "DI":
												// These subtypes are deprecated. Ignore.
												break;
											case "HC":
												RaiseHandoffCancelledReceived(PDUHandoffCancelled.Parse(fields));
												break;
											case "ST":
												RaiseFlightStripReceived(PDUFlightStrip.Parse(fields));
												break;
											case "DP":
												RaisePushToDepartureListReceived(PDUPushToDepartureList.Parse(fields));
												break;
											case "PT":
												RaisePointoutReceived(PDUPointout.Parse(fields));
												break;
											case "IH":
												RaiseIHaveTargetReceived(PDUIHaveTarget.Parse(fields));
												break;
											case "SC":
											case "BC":
											case "VT":
											case "TA":
												RaiseSharedStateReceived(PDUSharedState.Parse(fields));
												break;
											case "IC":
											case "IK":
											case "IB":
											case "EC":
											case "OV":
											case "OK":
											case "OB":
											case "EO":
											case "MN":
											case "MK":
											case "MB":
											case "EM":
												RaiseLandLineCommandReceived(PDULandLineCommand.Parse(fields));
												break;
											default:
												if (!IgnoreUnknownPackets)
												{
													throw new PDUFormatException("Unknown #PC packet subtype.", packet);
												}

												break;
										}
									}
									break;
								case "#SB":
									if (fields.Length < 3)
									{
										if (!IgnoreUnknownPackets)
										{
											throw new PDUFormatException("Too few fields in #SB packet.", packet);
										}
									}
									else
									{
										switch (fields[2])
										{
											case "PIR":
												RaisePlaneInfoRequestReceived(PDUPlaneInfoRequest.Parse(fields));
												break;
											case "PI":
												if (fields.Length < 4)
												{
													if (!IgnoreUnknownPackets)
													{
														throw new PDUFormatException("Too few fields in #SB packet.", packet);
													}
												}
												else
												{
													switch (fields[3])
													{
														case "X":
															RaiseLegacyPlaneInfoResponseReceived(PDULegacyPlaneInfoResponse.Parse(fields));
															break;
														case "GEN":
															RaisePlaneInfoResponseReceived(PDUPlaneInfoResponse.Parse(fields));
															break;
														default:
															if (!IgnoreUnknownPackets)
															{
																throw new PDUFormatException("Unknown #SB packet subtype.", packet);
															}

															break;
													}
												}
												break;
											default:
												if (!IgnoreUnknownPackets)
												{
													throw new PDUFormatException("Unknown #SB packet type.", packet);
												}
												break;
										}
									}
									break;
								case "$FP":
									try
									{
										RaiseFlightPlanReceived(PDUFlightPlan.Parse(fields));
									}
									catch (PDUFormatException) { } // Sometimes the server will send a malformed $FP. Ignore it.
									break;
								case "$AM":
									RaiseFlightPlanAmendmentReceived(PDUFlightPlanAmendment.Parse(fields));
									break;
								case "$PI":
									RaisePingReceived(PDUPing.Parse(fields));
									break;
								case "$PO":
									RaisePongReceived(PDUPong.Parse(fields));
									break;
								case "$HO":
									RaiseHandoffReceived(PDUHandoff.Parse(fields));
									break;
								case "$HA":
									RaiseHandoffAcceptReceived(PDUHandoffAccept.Parse(fields));
									break;
								case "$AX":
									RaiseAcarsQueryReceived(PDUMetarRequest.Parse(fields));
									break;
								case "$AR":
									RaiseAcarsResponseReceived(PDUMetarResponse.Parse(fields));
									break;
								case "$CQ":
									RaiseClientQueryReceived(PDUClientQuery.Parse(fields));
									break;
								case "$CR":
									RaiseClientQueryResponseReceived(PDUClientQueryResponse.Parse(fields));
									break;
								case "$ZC":
									if (GetClientKey() != 0)
									{
										PDUAuthChallenge pdu = PDUAuthChallenge.Parse(fields);
										string response = Marshal.PtrToStringAnsi(GenerateAuthResponse(pdu.Challenge, mClientAuthChallengeKey, ClientProperties.ClientHash, ClientProperties.PluginHash));
										mClientAuthChallengeKey = GenerateMD5Digest(mClientAuthSessionKey + response);
										PDUAuthResponse responsePDU = new PDUAuthResponse(pdu.To, pdu.From, response);
										SendPDU(responsePDU);
									}
									else
									{
										RaiseAuthChallengeReceived(PDUAuthChallenge.Parse(fields));
									}
									break;
								case "$ZR":
									{
										PDUAuthResponse pdu = PDUAuthResponse.Parse(fields);
										if (mChallengeServer && (GetClientKey() != 0) && !string.IsNullOrEmpty(mServerAuthChallengeKey) && !string.IsNullOrEmpty(mLastServerAuthChallenge))
										{
											CheckServerAuthChallengeResponse(pdu.Response);
										}
										else
										{
											RaiseAuthResponseReceived(pdu);
										}
										break;
									}
								case "$!!":
									RaiseKillRequestReceived(PDUKillRequest.Parse(fields));
									break;
								case "$ER":
									RaiseProtocolErrorReceived(PDUProtocolError.Parse(fields));
									break;
								default:
									if (!IgnoreUnknownPackets)
									{
										throw new PDUFormatException("Unknown PDU type: " + pduTypeID, packet);
									}
									break;
							}
							break;
						default:
							if (!IgnoreUnknownPackets)
							{
								throw new PDUFormatException("Unknown PDU prefix: " + prefixChar, packet);
							}
							break;
					}
				}
				catch (PDUFormatException ex)
				{
					RaiseNetworkError(string.Format("{0} (Raw packet: {1})", ex.Message, ex.RawMessage));
				}
			}
		}

		private void ProcessTM(string[] fields)
		{
			if (fields.Length < 3)
			{
				throw new PDUFormatException("Invalid field count.", PDUBase.Reassemble(fields));
			}

			// #TMs are allowed to have colons in the message field, so here we need
			// to rejoin the fields then resplit with a limit of 3 substrings.
			string raw = PDUBase.Reassemble(fields);
			fields = raw.Split(new char[] { PDUBase.DELIMITER }, 3);

			// Check for special case recipients.
			switch (fields[1])
			{
				case "*":
					RaiseBroadcastMessageReceived(PDUBroadcastMessage.Parse(fields));
					break;
				case "*S":
					RaiseWallopReceived(PDUWallop.Parse(fields));
					break;
				case "@49999":
					RaiseATCMessageReceived(PDUATCMessage.Parse(fields));
					break;
				default:
					if (fields[1].Substring(0, 1) == "@")
					{
						RaiseRadioMessageReceived(PDURadioMessage.Parse(fields));
					}
					else
					{
						RaiseTextMessageReceived(PDUTextMessage.Parse(fields));
					}
					break;
			}
		}

		public static string GenerateMD5Digest(string value)
		{
			byte[] data = Encoding.ASCII.GetBytes(value);
			using (MD5 md5 = new MD5CryptoServiceProvider())
			{
				byte[] result = md5.ComputeHash(data);
				StringBuilder sb = new StringBuilder();
				for (int i = 0; i < result.Length; i++)
				{
					sb.Append(result[i].ToString("x2"));
				}
				return sb.ToString();
			}
		}
	}
}