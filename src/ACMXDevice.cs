// For Basic SIMPL# Classes
// For Basic SIMPL#Pro classes

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.DeviceInfo;
using PepperDash.Essentials.Core.Queues;
using PepperDash.Essentials.Core.Routing;
using PepperDash.Essentials.Plugin.IOs;

namespace PepperDash.Essentials.Plugin.AVProEdge
{
    /// <summary>
    /// Plugin device template for third party devices that use IBasicCommunication
    /// </summary>
    public class ACMXDevice : EssentialsBridgeableDevice, IMatrixRouting, IRoutingWithFeedback, ICommunicationMonitor, IDeviceInfoProvider
    {
        private const string commsDelimiter = "\r";
        private const string gatherDelimiter = "\r";
        private uint inputCount = 8;
        private uint outputCount = 8;

        /// <summary>
        /// It is often desirable to store the config
        /// </summary>
        private readonly ACMXConfig config;

        /// <summary>
        /// Provides a queue and dedicated worker thread for processing feedback messages from a device.
        /// </summary>
        private readonly GenericQueue receiveQueue;

        private readonly IBasicCommunication comms;
        private readonly CommunicationGather commsGather;

        /// <summary>
        /// Communication monitor for the device
        /// </summary>
        public StatusMonitorBase CommunicationMonitor { get; private set; }

        /// <summary>
        /// Connects/disconnects the comms of the plugin device
        /// </summary>
        public bool Connect
        {
            get { return comms.IsConnected; }
            set
            {
                if (value)
                {
                    comms.Connect();
                    CommunicationMonitor.Start();
                }
                else
                {
                    comms.Disconnect();
                    CommunicationMonitor.Stop();
                }
            }
        }

        /// <summary>
        /// Reports connect feedback through the bridge
        /// </summary>
        public BoolFeedback ConnectFeedback { get; private set; }

        /// <summary>
        /// Reports online feedback through the bridge
        /// </summary>
        public BoolFeedback OnlineFeedback { get; private set; }

        /// <summary>
        /// Reports socket status feedback through the bridge
        /// </summary>
        public IntFeedback StatusFeedback { get; private set; }

        public Dictionary<uint, IntFeedback> VideoOutputFeedbacks { get; private set; }
        public Dictionary<uint, IntFeedback> AudioOutputFeedbacks { get; private set; }
        public Dictionary<uint, BoolFeedback> VideoInputSyncFeedbacks { get; private set; }
        public Dictionary<uint, StringFeedback> InputNameFeedbacks { get; private set; }
        public Dictionary<uint, StringFeedback> InputVideoNameFeedbacks { get; private set; }
        public Dictionary<uint, StringFeedback> InputAudioNameFeedbacks { get; private set; }

        public Dictionary<uint, StringFeedback> OutputNameFeedbacks { get; private set; }
        public Dictionary<uint, StringFeedback> OutputVideoNameFeedbacks { get; private set; }
        public Dictionary<uint, StringFeedback> OutputAudioNameFeedbacks { get; private set; }
        public Dictionary<uint, StringFeedback> OutputVideoRouteNameFeedbacks { get; private set; }
        public Dictionary<uint, StringFeedback> OutputAudioRouteNameFeedbacks { get; private set; }

        public Dictionary<string, IRoutingInputSlot> InputSlots { get; private set; }

        public Dictionary<string, IRoutingOutputSlot> OutputSlots { get; private set; }

        public RoutingPortCollection<RoutingInputPort> InputPorts { get; private set; }

        public RoutingPortCollection<RoutingOutputPort> OutputPorts { get; private set; }

        public Dictionary<uint, string> InputNames { get; private set; }
        public Dictionary<uint, string> OutputNames { get; private set; }

        public event RouteChangedEventHandler RouteChanged;
        public event DeviceInfoChangeHandler DeviceInfoChanged;

        public List<RouteSwitchDescriptor> CurrentRoutes { get; private set; }

        public DeviceInfo DeviceInfo { get; private set; }

        /// <summary>
        /// Plugin device constructor for devices that need IBasicCommunication
        /// </summary>
        /// <param name="key"></param>
        /// <param name="name"></param>
        /// <param name="config"></param>
        /// <param name="comms"></param>
        public ACMXDevice(string key, string name, ACMXConfig config, IBasicCommunication comms, string typeName)
      : base(key, name)
        {
            this.LogInformation("Constructing new {0} instance", name);

            this.config = config;

            receiveQueue = new GenericQueue(key + "-rxqueue");  // If you need to set the thread priority, use one of the available overloaded constructors.

            InputNames = new Dictionary<uint, string>();
            OutputNames = new Dictionary<uint, string>();

            InputSlots = new Dictionary<string, IRoutingInputSlot>();
            OutputSlots = new Dictionary<string, IRoutingOutputSlot>();

            InputPorts = new RoutingPortCollection<RoutingInputPort>();
            OutputPorts = new RoutingPortCollection<RoutingOutputPort>();

            ConnectFeedback = new BoolFeedback("connect", () => Connect);
            OnlineFeedback = new BoolFeedback("online", () => CommunicationMonitor.IsOnline);
            StatusFeedback = new IntFeedback("status", () => (int)CommunicationMonitor.Status);

            InputNameFeedbacks = new Dictionary<uint, StringFeedback>();
            InputVideoNameFeedbacks = new Dictionary<uint, StringFeedback>();
            InputAudioNameFeedbacks = new Dictionary<uint, StringFeedback>();

            OutputNameFeedbacks = new Dictionary<uint, StringFeedback>();
            OutputVideoNameFeedbacks = new Dictionary<uint, StringFeedback>();
            OutputAudioNameFeedbacks = new Dictionary<uint, StringFeedback>();

            VideoInputSyncFeedbacks = new Dictionary<uint, BoolFeedback>();

            VideoOutputFeedbacks = new Dictionary<uint, IntFeedback>();
            AudioOutputFeedbacks = new Dictionary<uint, IntFeedback>();

            OutputVideoRouteNameFeedbacks = new Dictionary<uint, StringFeedback>();
            OutputAudioRouteNameFeedbacks = new Dictionary<uint, StringFeedback>();

            this.comms = comms;
            CommunicationMonitor = new GenericCommunicationMonitor(
              this,
              this.comms,
              this.config.PollTimeMs == 0 ? 60000 : this.config.PollTimeMs,
              180000,
              300000,
              Poll);

            commsGather = new CommunicationGather(this.comms, gatherDelimiter);
            commsGather.LineReceived += Handle_LineReceived;

            CurrentRoutes = new List<RouteSwitchDescriptor>();

            SetupSlots();
        }

        public override void Initialize()
        {
            base.Initialize();

            var socket = this.comms as ISocketStatus;
            if (socket != null)
            {
                // device comms is IP **ELSE** device comms is RS232
                socket.ConnectionChange += Socket_ConnectionChange;
                Connect = true;

                return;
            }

            CommunicationMonitor.Start();

            PollRoutes();

            PollSignalStatus();
        }

        private void SetupSlots()
        {
            InputNames = config.InputNames;
            OutputNames = config.OutputNames;

            this.LogInformation("SetupSlots: Configured InputNames.Count={0}, OutputNames.Count={1}", InputNames.Count, OutputNames.Count);

            inputCount = (uint)(InputNames.Count > 0 ? InputNames.Count : 8);
            outputCount = (uint)(OutputNames.Count > 0 ? OutputNames.Count : 8);

            this.LogInformation("SetupSlots: Using inputCount={0}, outputCount={1}", inputCount, outputCount);

            for (uint i = 1; i <= inputCount; i++)
            {
                SetupInputSlot(i);
            }

            for (uint i = 1; i <= outputCount; i++)
            {
                SetupOutputSlot(i);
            }

            foreach (var item in InputSlots)
            {
                this.LogInformation($"SetupSlots: InputSlots[{item.Key}] Key={item.Value.Key}, Name={item.Value.Name}, SlotNumber={item.Value.SlotNumber}");
            }

            foreach (var item in OutputSlots)
            {
                this.LogInformation($"SetupSlots: OutputSlots[{item.Key}] Key={item.Value.Key}, Name={item.Value.Name}, SlotNumber={item.Value.SlotNumber}");
            }

            foreach (var item in InputPorts)
            {
                this.LogInformation($"SetupSlots: InputPorts Key={item.Key}, Port={item.Port}, Type={item.Type}, Selector={item.Selector}, ConnectionType={item.ConnectionType}, Parent={item.ParentDevice}");
            }

            foreach (var item in OutputPorts)
            {
                this.LogInformation($"SetupSlots: OutputPorts Key={item.Key}, Port={item.Port}, Type={item.Type}, Selector={item.Selector}, ConnectionType={item.ConnectionType}, Parent={item.ParentDevice}");
            }
        }

        private void SetupInputSlot(uint slotNum)
        {
            var name = InputNames.ContainsKey(slotNum) ? InputNames[slotNum] : $"Input {slotNum}";
            var key = $"in{slotNum}";
            var slot = new InputSlot(key, name, (int)slotNum);

            InputSlots.Add(key, slot);

            InputPorts.Add(
              new RoutingInputPort(
                key,
                eRoutingSignalType.Video | eRoutingSignalType.Audio | eRoutingSignalType.AudioVideo | eRoutingSignalType.SecondaryAudio,
                eRoutingPortConnectionType.Hdmi,
                slotNum,
                this,
                true)
              {
                  FeedbackMatchObject = slot,
              });

            InputNameFeedbacks[slotNum] = new StringFeedback($"inputNameFeedback-{slot.Key}", () => slot.Name);
            InputVideoNameFeedbacks[slotNum] = new StringFeedback($"inputVideoNameFeedback-{slot.Key}", () => slot.Name);
            InputAudioNameFeedbacks[slotNum] = new StringFeedback($"inputAudioNameFeedback-{slot.Key}", () => slot.Name);

            VideoInputSyncFeedbacks[slotNum] = new BoolFeedback($"videoInputSyncFeedback-{slot.Key}", () => slot.VideoSyncDetected);
        }

        private void SetupOutputSlot(uint slotNum)
        {
            if (slotNum == 0) return;

            var name = OutputNames.ContainsKey(slotNum) ? OutputNames[slotNum] : $"Output {slotNum}";
            var key = $"out{slotNum}";
            var slot = new OutputSlot(key, name, (int)slotNum);

            OutputSlots.Add(key, slot);

            OutputPorts.Add(
              new RoutingOutputPort(
                key,
                eRoutingSignalType.Video | eRoutingSignalType.Audio | eRoutingSignalType.AudioVideo | eRoutingSignalType.SecondaryAudio,
                eRoutingPortConnectionType.Hdmi,
                slotNum,
                this,
                true));

            OutputNameFeedbacks[slotNum] = new StringFeedback($"outputNameFeedback-{slot.Key}", () => slot.Name);
            OutputVideoNameFeedbacks[slotNum] = new StringFeedback($"outputVideoNameFeedback-{slot.Key}", () => slot.Name);
            OutputAudioNameFeedbacks[slotNum] = new StringFeedback($"outputAudioNameFeedback-{slot.Key}", () => slot.Name);

            VideoOutputFeedbacks[slotNum] = new IntFeedback($"videoOutputFeedback-{slot.Key}", () => slot.CurrentRoutes[eRoutingSignalType.AudioVideo] is InputSlot inputSlot ? inputSlot.SlotNumber : 0);
            AudioOutputFeedbacks[slotNum] = new IntFeedback($"audioOutputFeedback-{slot.Key}", () => slot.CurrentRoutes[eRoutingSignalType.SecondaryAudio] is InputSlot inputSlot ? inputSlot.SlotNumber : 0);

            OutputVideoRouteNameFeedbacks[slotNum] = new StringFeedback($"outputVideoRouteNameFeedback-{slot.Key}", () => slot.CurrentRoutes[eRoutingSignalType.AudioVideo]?.Name ?? config.NoRouteText);
            OutputAudioRouteNameFeedbacks[slotNum] = new StringFeedback($"outputAudioRouteNameFeedback-{slot.Key}", () => slot.CurrentRoutes[eRoutingSignalType.SecondaryAudio]?.Name ?? config.NoRouteText);
        }

        private void Socket_ConnectionChange(object sender, GenericSocketStatusChageEventArgs args)
        {
            ConnectFeedback?.FireUpdate();

            StatusFeedback?.FireUpdate();

            if (!args.Client.IsConnected) return;

            PollRoutes();

            PollSignalStatus();
        }

        private void Handle_LineReceived(object sender, GenericCommMethodReceiveTextArgs args)
        {
            // Enqueues the message to be processed in a dedicated thread, but the specified method
            receiveQueue.Enqueue(new ProcessStringMessage(args.Text, ProcessFeedbackMessage));
        }


        /// <summary>
        /// This method should perform any necessary parsing of feedback messages from the device
        /// </summary>
        /// <param name="message"></param>
        private void ProcessFeedbackMessage(string message)
        {
            if (message.Contains("VS IN"))
            {
                ProcessVideoRouteFeedback(message);
                return;
            }

            if (message.Contains("AS IN"))
            {
                ProcessAudioRouteFeedback(message);
                return;
            }

            if (message.Contains("SIG STA"))
            {
                ProcessSignalStatusFeedback(message);
                return;
            }

            if (message.StartsWith("MAC "))
            {
                ProcessMacAddressMessage(message);
                return;
            }

            // Log unhandled messages for debugging
            this.LogDebug("ProcessFeedbackMessage: Unhandled message '{0}'", message);
        }

        private void ProcessVideoRouteFeedback(string message)
        {
            // Process switcher response
            // Pattern: "OUT[XX] VS IN[YY]"
            var switchResponseRegex = new System.Text.RegularExpressions.Regex(@"OUT(0?\d|[1-9]\d)\s+VS\s+IN(0?\d|[1-9]\d)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var switchMatch = switchResponseRegex.Match(message);

            if (switchMatch.Success)
            {
                var outputNumber = uint.Parse(switchMatch.Groups[1].Value);
                var inputNumber = uint.Parse(switchMatch.Groups[2].Value);

                this.LogDebug($"ProcessVideoRouteFeedback: Switch response Input-{inputNumber} to Output-{outputNumber}");

                var outputSlot = OutputSlots.FirstOrDefault(x => x.Value.SlotNumber == outputNumber).Value;
                var inputSlot = InputSlots.FirstOrDefault(x => x.Value.SlotNumber == inputNumber).Value;

                if (outputSlot != null && inputSlot != null)
                {
                    this.LogDebug($"ProcessVideoRouteFeedback: route feedback {inputSlot.SlotNumber}-{inputSlot.Name} to {outputSlot.SlotNumber}-{outputSlot.Name}");

                    (outputSlot as OutputSlot)?.SetInputRoute(eRoutingSignalType.AudioVideo, inputSlot);

                    UpdateCurrentRoutes(inputNumber, outputNumber);

                }
                else if (outputSlot == null)
                {
                    this.LogWarning("ProcessVideoRouteFeedback: Could not find outputNum slot {0}", outputNumber);
                }
                else if (inputSlot == null)
                {
                    this.LogWarning("ProcessVideoRouteFeedback: Could not find inputNum slot {0}", inputNumber);
                }

                return;
            }
        }

        private void ProcessAudioRouteFeedback(string message)
        {
            // Process switcher response
            // Pattern: "OUT[XX] AS IN[YY]"
            var switchResponseRegex = new System.Text.RegularExpressions.Regex(@"OUT(0?\d|[1-9]\d)\s+AS\s+IN(0?\d|[1-9]\d)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var switchMatch = switchResponseRegex.Match(message);

            if (switchMatch.Success)
            {
                var outputNumber = uint.Parse(switchMatch.Groups[1].Value);
                var inputNumber = uint.Parse(switchMatch.Groups[2].Value);

                this.LogDebug($"ProcessAudioRouteFeedback: Switch response Input-{inputNumber} to Output-{outputNumber}");

                var outputSlot = OutputSlots.FirstOrDefault(x => x.Value.SlotNumber == outputNumber).Value;
                var inputSlot = InputSlots.FirstOrDefault(x => x.Value.SlotNumber == inputNumber).Value;

                if (outputSlot != null && inputSlot != null)
                {
                    this.LogDebug($"ProcessAudioRouteFeedback: route feedback {inputSlot.SlotNumber}-{inputSlot.Name} to {outputSlot.SlotNumber}-{outputSlot.Name}");

                    (outputSlot as OutputSlot)?.SetInputRoute(eRoutingSignalType.SecondaryAudio, inputSlot);

                    UpdateCurrentRoutes(inputNumber, outputNumber);

                }
                else if (outputSlot == null)
                {
                    this.LogWarning("ProcessAudioRouteFeedback: Could not find outputNum slot {0}", outputNumber);
                }
                else if (inputSlot == null)
                {
                    this.LogWarning("ProcessAudioRouteFeedback: Could not find inputNum slot {0}", inputNumber);
                }

                return;
            }
        }

        private void ProcessSignalStatusFeedback(string message)
        {
            // Input: IN[x] SIG STA [0|1]
            // Output: OUT[x] SIG STA [0|1]
            var signalStatusRegex = new System.Text.RegularExpressions.Regex(@"(IN|OUT)(0?\d|[1-9]\d)\s+SIG\s+STA\s+([0|1])", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var signalMatch = signalStatusRegex.Match(message);

            if (signalMatch.Success)
            {
                var direction = signalMatch.Groups[1].Value;
                var slotNumber = uint.Parse(signalMatch.Groups[2].Value);
                var status = int.Parse(signalMatch.Groups[3].Value);

                this.LogDebug($"ProcessSignalStatusFeedback: Signal status {direction}-{slotNumber} is {status}");

                // Update the signal status for the corresponding slot
                if (direction.Equals("IN", StringComparison.OrdinalIgnoreCase))
                {
                    if (InputSlots.FirstOrDefault(x => x.Value.SlotNumber == slotNumber).Value is not InputSlot inputSlot)
                    {
                        this.LogError("ProcessSignalStatusFeedback: Could not find inputslot.SlotNumber {0} for sync status update", slotNumber);
                        return;
                    }

                    inputSlot.VideoSyncDetected = (status == 1);
                }
                /*
                else if (direction.Equals("OUT", StringComparison.OrdinalIgnoreCase))
                {
                    if (OutputSlots.FirstOrDefault(x => x.Value.SlotNumber == slotNumber).Value is not OutputSlot outputSlot)
                    {
                        this.LogError("ParseSyncStatus: Could not find outputslot.SlotNumber {0} for sync status update", slotNumber);
                        return;
                    }

                    outputSlot.VideoSyncDetected = (status == 1);
                }
                */
            }
        }


        private void ProcessMacAddressMessage(string message)
        {
            // Ex: MAC 00:1A:2B:3C:4D:5E
            var macRegex = new System.Text.RegularExpressions.Regex(@"MAC\s+([0-9A-Fa-f]{2}(:[0-9A-Fa-f]{2}){5})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var macMatch = macRegex.Match(message);
            if (macMatch.Success)
            {
                var macAddress = macMatch.Groups[1].Value;
                UpdateDeviceInfo(DeviceInfo.HostName, DeviceInfo.FirmwareVersion, macAddress, DeviceInfo.SerialNumber);
            }
        }

        /// <summary>
        /// Sends text to the device plugin comms
        /// </summary>
        /// <remarks>
        /// Can be used to test commands with the device plugin using the DEVPROPS and DEVJSON console commands
        /// </remarks>
        /// <param name="text">Command to be sent</param>		
        public void SendText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            this.LogVerbose("SendText: '{0}'", text);

            comms.SendText(string.Format("{0}{1}", text, commsDelimiter));
        }

        /// <summary>
        /// Polls the device
        /// </summary>
        /// <remarks>
        /// Poll method is used by the communication monitor.  Update the poll method as needed for the plugin being developed
        /// </remarks>
        public async void Poll()
        {
            SendText("GET STA");

            await Task.Delay(500);

            PollSignalStatus();
        }

        /// <summary>
        /// Polls device for current routes
        /// </summary>
        public async void PollRoutes()
        {
            // get video routes
            SendText("GET OUT0 VS");

            await Task.Delay(500);

            // get audio routes
            SendText("GET OUT0 AS IN");
        }

        public async void PollSignalStatus()
        {
            // get video sync status
            SendText("GET IN0 SIG STA");

            await Task.Delay(500);

            SendText("GET OUT0 SIG STA");
        }


        #region Overrides of EssentialsBridgeableDevice

        /// <summary>
        /// Links the plugin device to the EISC bridge
        /// </summary>
        /// <param name="trilist"></param>
        /// <param name="joinStart"></param>
        /// <param name="joinMapKey"></param>
        /// <param name="bridge"></param>
        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = new ACMXJoinMap(joinStart);

            // This adds the join map to the collection on the bridge
            bridge?.AddJoinMap(Key, joinMap);

            var customJoins = JoinMapHelper.TryGetJoinMapAdvancedForDevice(joinMapKey);

            if (customJoins != null)
            {
                joinMap.SetCustomJoinData(customJoins);
            }

            this.LogDebug("Linking to Trilist {id}", trilist.ID.ToString("X"));
            this.LogInformation("Linking to Bridge Type {type}", GetType().Name);

            // links to bridge
            trilist.SetString(joinMap.Name.JoinNumber, Name);

            trilist.SetBoolSigAction(joinMap.Connect.JoinNumber, sig => Connect = sig);
            ConnectFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Connect.JoinNumber]);

            StatusFeedback.LinkInputSig(trilist.UShortInput[joinMap.Status.JoinNumber]);
            OnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);

            // Clear inputNum name feedback
            InputNameFeedbacks[0].LinkInputSig(trilist.StringInput[joinMap.NoRouteName.JoinNumber]);

            // inputNum name feedbacks
            for (uint i = 1; i <= inputCount; i++)
            {
                var input = i;

                LinkInputsToApi(trilist, joinMap, input);
            }

            // outputNum name feedbacks and routing control/feedback
            for (uint i = 1; i <= outputCount; i++)
            {
                var output = i;

                LinkOutputsToApi(trilist, joinMap, output);
            }

            UpdateFeedbacks();

            trilist.OnlineStatusChange += (o, a) =>
            {
                if (!a.DeviceOnLine) return;

                trilist.SetString(joinMap.Name.JoinNumber, Name);

                UpdateFeedbacks();
            };
        }

        private void LinkInputsToApi(BasicTriList trilist, ACMXJoinMap joinMap, uint input)
        {
            var inputJoinOffset = input - 1;

            this.LogInformation($"LinkInputsToApi: inputNum={input}, inputJoinOffset={inputJoinOffset}");

            InputNameFeedbacks[input].LinkInputSig(trilist.StringInput[joinMap.InputNames.JoinNumber + inputJoinOffset]);
            InputVideoNameFeedbacks[input].LinkInputSig(trilist.StringInput[joinMap.InputVideoNames.JoinNumber + inputJoinOffset]);
            InputAudioNameFeedbacks[input].LinkInputSig(trilist.StringInput[joinMap.InputAudioNames.JoinNumber + inputJoinOffset]);
            VideoInputSyncFeedbacks[input].LinkInputSig(trilist.BooleanInput[joinMap.VideoSyncStatus.JoinNumber + inputJoinOffset]);
        }

        private void LinkOutputsToApi(BasicTriList trilist, ACMXJoinMap joinMap, uint output)
        {
            if (output == 0)
                return;

            var outputJoinOffset = output - 1;

            this.LogInformation($"LinkOutputsToApi: outputNum={output}, outputJoinOffset={outputJoinOffset}");


            // Routing Control
            trilist.SetUShortSigAction(joinMap.OutputVideo.JoinNumber + outputJoinOffset,
                o => ExecuteSwitch(o, (ushort)output, eRoutingSignalType.Video));

            trilist.SetUShortSigAction(joinMap.OutputAudio.JoinNumber + outputJoinOffset,
                o => ExecuteSwitch(o, (ushort)output, eRoutingSignalType.Audio));

            // Routing Feedbacks
            OutputNameFeedbacks[output].LinkInputSig(trilist.StringInput[joinMap.OutputNames.JoinNumber + outputJoinOffset]);
            OutputVideoNameFeedbacks[output].LinkInputSig(trilist.StringInput[joinMap.OutputVideoNames.JoinNumber + outputJoinOffset]);
            OutputAudioNameFeedbacks[output].LinkInputSig(trilist.StringInput[joinMap.OutputAudioNames.JoinNumber + outputJoinOffset]);

            VideoOutputFeedbacks[output].LinkInputSig(trilist.UShortInput[joinMap.OutputVideo.JoinNumber + outputJoinOffset]);
            AudioOutputFeedbacks[output].LinkInputSig(trilist.UShortInput[joinMap.OutputAudio.JoinNumber + outputJoinOffset]);

            OutputVideoRouteNameFeedbacks[output].LinkInputSig(
                trilist.StringInput[joinMap.OutputCurrentVideoInputNames.JoinNumber + outputJoinOffset]);
            OutputAudioRouteNameFeedbacks[output].LinkInputSig(
                trilist.StringInput[joinMap.OutputCurrentAudioInputNames.JoinNumber + outputJoinOffset]);
        }

        #endregion

        private void UpdateFeedbacks()
        {
            // TODO [ ] Update as needed for the plugin being developed
            ConnectFeedback?.FireUpdate();
            OnlineFeedback?.FireUpdate();
            StatusFeedback?.FireUpdate();

            foreach (var item in InputNameFeedbacks)
                item.Value.FireUpdate();

            foreach (var item in InputVideoNameFeedbacks)
                item.Value.FireUpdate();

            foreach (var item in InputAudioNameFeedbacks)
                item.Value.FireUpdate();

            foreach (var item in VideoInputSyncFeedbacks)
                item.Value.FireUpdate();

            foreach (var item in OutputNameFeedbacks)
                item.Value.FireUpdate();

            foreach (var item in OutputVideoNameFeedbacks)
                item.Value.FireUpdate();

            foreach (var item in OutputAudioNameFeedbacks)
                item.Value.FireUpdate();

            foreach (var item in VideoOutputFeedbacks)
                item.Value.FireUpdate();

            foreach (var item in AudioOutputFeedbacks)
                item.Value.FireUpdate();

            foreach (var item in OutputVideoRouteNameFeedbacks)
                item.Value.FireUpdate();

            foreach (var item in OutputAudioRouteNameFeedbacks)
                item.Value.FireUpdate();
        }

        /// <summary>
        /// Routes an inputNum to an outputNum for the specified signal type(s)
        /// </summary>
        /// <param name="inputSlotKey"></param>
        /// <param name="outputSlotKey"></param>
        /// <param name="type"></param>
        public void Route(string inputSlotKey, string outputSlotKey, eRoutingSignalType type)
        {
            this.LogInformation($"Route: Making {type.ToString().ToLower()} route from inputSlotKey {inputSlotKey} to outputSlotKey {outputSlotKey}");

            try
            {
                var inputSlot = InputSlots.TryGetValue(inputSlotKey, out var inSlot) ? inSlot as InputSlot : null;
                var outputSlot = OutputSlots.TryGetValue(outputSlotKey, out var outSlot) ? outSlot as OutputSlot : null;

                if (inputSlot == null)
                {
                    this.LogError($"Route: failed to find inputSlotKey `{inputSlotKey}`");
                    return;
                }

                if (outputSlot == null)
                {
                    this.LogError($"Route: failed to find outputSlotKey `{outputSlotKey}`");
                    return;
                }
                // route a/v (hdmi with embedded audio)
                if (type.HasFlag(eRoutingSignalType.AudioVideo))
                {
                    SetVideoRoute(inputSlot.SlotNumber, outputSlot.SlotNumber);
                    return;
                }
                // route video (hdmi video only)
                if (type.HasFlag(eRoutingSignalType.Video))
                {
                    SetVideoRoute(inputSlot.SlotNumber, outputSlot.SlotNumber);
                }
                // route audio (hdmi embedded audio)
                if (type.HasFlag(eRoutingSignalType.Audio))
                {
                    SetVideoRoute(inputSlot.SlotNumber, outputSlot.SlotNumber);
                }
                // route secondary audio (extracted audio)
                if (type.HasFlag(eRoutingSignalType.SecondaryAudio))
                {
                    SetAudioRoute(inputSlot.SlotNumber, outputSlot.SlotNumber);
                }
            }
            catch (Exception ex)
            {
                this.LogError("Route: {inputNum} to {outputNum} exception {message}", inputSlotKey, outputSlotKey, ex.Message);
                this.LogDebug(ex, "Route: Exception StackTrace");
                return;
            }
        }

        /// <summary>
        /// Executes a switch from an inputNum to an outputNum for the specified signal type(s)
        /// </summary>
        /// <param name="inputSelector"></param>
        /// <param name="outputSelector"></param>
        /// <param name="signalType"></param>
        public void ExecuteSwitch(object inputSelector, object outputSelector, eRoutingSignalType signalType)
        {
            try
            {
                this.LogVerbose($"ExecuteSwitch: Making {signalType.ToString().ToLower()} route from inputNum {inputSelector} to outputNum {outputSelector}");

                var inputNum = Convert.ToUInt16(inputSelector);
                var outputNum = Convert.ToUInt16(outputSelector);

                // route a/v (hdmi with embedded audio)
                if (signalType.HasFlag(eRoutingSignalType.AudioVideo))
                {
                    SetVideoRoute(inputNum, outputNum);
                    UpdateCurrentRoutes(inputNum, outputNum);
                    return;
                }
                // route video (hdmi video only)
                if (signalType.HasFlag(eRoutingSignalType.Video))
                {
                    SetVideoRoute(inputNum, outputNum);
                    UpdateCurrentRoutes(inputNum, outputNum);
                }
                // route audio (hdmi embedded audio)
                if (signalType.HasFlag(eRoutingSignalType.Audio))
                {
                    SetVideoRoute(inputNum, outputNum);
                    UpdateCurrentRoutes(inputNum, outputNum);
                }
                // route secondary audio (extracted audio)
                if (signalType.HasFlag(eRoutingSignalType.SecondaryAudio))
                {
                    SetAudioRoute(inputNum, outputNum);
                    UpdateCurrentRoutes(inputNum, outputNum);
                }
            }
            catch (Exception ex)
            {
                this.LogError("ExecuteSwitch: in-{inputNum} to out-{outputNum} exception {message}", inputSelector, outputSelector, ex.Message);
                this.LogDebug(ex, "ExecuteSwitch: Exception StackTrace");
                return;
            }

        }

        /// <summary>
        /// Updates the current routes based on the inputNum and outputNum numbers.
        /// </summary>
        /// <param name="inputSelector"></param>
        /// <param name="outputSelector"></param>
        private void UpdateCurrentRoutes(uint inputSelector, uint outputSelector)
        {
            RouteSwitchDescriptor descriptor;

            descriptor = GetRouteDescriptorByOutputPort(outputSelector);
            this.LogDebug("UpdateCurrentRoutes: Found existing descriptor: {0}", descriptor != null ? "Yes" : "No");

            var inputPort = GetRoutingInputPortForSelector(inputSelector);
            var outputPort = GetRoutingOutputPortForSelector(outputSelector);

            if (inputPort is null)
            {
                this.LogDebug("UpdateCurrentRoutes: Unable to find port for in-{inputNum}", inputSelector);
                return;
            }

            if (outputPort is null)
            {
                this.LogDebug("UpdateCurrentRoutes: Unable to find port for out-{outputNum}", outputSelector);
                return;
            }

            this.LogDebug("UpdateCurrentRoutes: Updating route in-{inputNum} to out-{outputNum}", inputSelector, outputSelector);

            if (outputPort is null)
            {
                this.LogDebug("UpdateCurrentRoutes: Unable to find port for out-{outputNum}", outputSelector);
                return;
            }

            if (descriptor is null && outputPort is not null)
            {
                descriptor = new(outputPort, inputPort);

                CurrentRoutes.Add(descriptor);
            }
            else
            {
                descriptor.InputPort = inputPort;
            }

            RouteChanged?.Invoke(this, descriptor);
        }

        /// <summary>
        /// Gets the route descriptor for the specified outputNum port number.
        /// </summary>
        /// <param name="selector"></param>
        /// <returns></returns>
        private RouteSwitchDescriptor GetRouteDescriptorByOutputPort(uint selector)
        {
            this.LogDebug("GetRouteDescriptorByOutputPort: Looking for route descriptor with outputNum port selector {0}", selector);
            return CurrentRoutes.FirstOrDefault(rd =>
            {
                this.LogDebug("GetRouteDescriptorByOutputPort: Checking descriptor with outputNum port selector {0}", rd.OutputPort.Selector);
                if (rd.OutputPort.Selector is not uint opSelector)
                {
                    this.LogDebug("GetRouteDescriptorByOutputPort: Output port selector is not a uint");
                    return false;
                }

                this.LogDebug("GetRouteDescriptorByOutputPort: Comparing {0} to {1}", opSelector, selector);
                return opSelector == selector;
            });
        }

        /// <summary>
        /// Gets the routing inputNum port for the specified inputNum number.
        /// </summary>
        /// <param name="selector"></param>
        /// <returns></returns>
        private RoutingInputPort GetRoutingInputPortForSelector(uint selector)
        {
            this.LogDebug("GetRoutingInputPortForSelector: Looking for inputNum port with selector {0}", selector);

            return InputPorts.FirstOrDefault(ip =>
            {
                this.LogDebug("GetRoutingInputPortForSelector: Checking inputNum port with selector {0}", ip.Selector);
                if (ip.Selector is not uint ipSelector)
                {
                    this.LogDebug("GetRoutingInputPortForSelector: Input port selector is not a uint");
                    return false;
                }

                this.LogDebug("GetRoutingInputPortForSelector: Comparing {0} to {1}", ipSelector, selector);
                return ipSelector == selector;
            });
        }


        /// <summary>
        /// Gets the routing outputNum port for the specified outputNum number.
        /// </summary>
        /// <param name="selector"></param>
        /// <returns></returns>
        private RoutingOutputPort GetRoutingOutputPortForSelector(uint selector)
        {

            return OutputPorts.FirstOrDefault(op =>
            {
                if (op.Selector is not uint opSelector)
                {
                    return false;
                }

                return opSelector == selector;
            });
        }

        private void SetVideoRoute(int input, int output)
        {
            SendText($"SET OUT{output} VS IN{input}");
        }

        private void SetAudioRoute(int input, int output)
        {
            SendText($"SET OUT{output} AS IN{input}");
        }

        public void UpdateDeviceInfo()
        {
            var socket = comms as GenericTcpIpClient;

            // Initialize DeviceInfo if it doesn't exist or preserve existing firmware version
            var existingFirmware = DeviceInfo?.FirmwareVersion ?? "";
            var existingHostName = DeviceInfo?.HostName ?? "";

            DeviceInfo = new DeviceInfo
            {
                FirmwareVersion = existingFirmware,
                HostName = existingHostName,
                IpAddress = socket?.Hostname ?? "",
                MacAddress = "",
                SerialNumber = ""
            };

            var handler = DeviceInfoChanged;
            if (handler == null) return;

            handler(this, new DeviceInfoEventArgs { DeviceInfo = DeviceInfo });
        }

        /// <summary>
        /// Updates the DeviceInfo with firmware version information
        /// </summary>
        /// <param name="firmwareVersion">The parsed firmware version string</param>
        /// <param name="modelNumber">The extracted model number (if available)</param>
        private void UpdateDeviceInfo(string modelNumber, string firmwareVersion, string macAddress, string serialNumber)
        {
            var socket = comms as GenericTcpIpClient;

            // Create or update DeviceInfo
            DeviceInfo = new DeviceInfo
            {
                FirmwareVersion = firmwareVersion,
                HostName = modelNumber,
                IpAddress = socket?.Hostname ?? "",
                MacAddress = macAddress,
                SerialNumber = serialNumber
            };

            // Fire the DeviceInfoChanged event
            var handler = DeviceInfoChanged;
            if (handler != null)
            {
                handler(this, new DeviceInfoEventArgs { DeviceInfo = DeviceInfo });
            }

            this.LogDebug("UpdateDeviceInfoWithFirmware: Update firmwarVersion to {0}", firmwareVersion);
        }
    }
}