// For Basic SIMPL# Classes
// For Basic SIMPL#Pro classes

using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.Queues;
using PepperDash.Essentials.Core.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;

namespace PepperDash.Essentials.Plugin.AvProEdge
{
  /// <summary>
  /// Plugin device template for third party devices that use IBasicCommunication
  /// </summary>
  /// <remarks>
  /// Rename the class to match the device plugin being developed.
  /// </remarks>
  /// <example>
  /// "EssentialsPluginDeviceTemplate" renamed to "SamsungMdcDevice"
  /// </example>
  public class ACMXYxYDevice : EssentialsBridgeableDevice, IMatrixRouting, IRoutingWithFeedback, ICommunicationMonitor
  {
    /// <summary>
    /// It is often desirable to store the config
    /// </summary>
    private readonly DeviceConfig config;

    /// <summary>
    /// Provides a queue and dedicated worker thread for processing feedback messages from a device.
    /// </summary>
    private readonly GenericQueue receiveQueue;


    private readonly IBasicCommunication comms;
    private readonly CommunicationGather commsGather;

    /// <summary>
    /// Set this value to that of the delimiter used by the API (if applicable)
    /// </summary>
    private const string commsDelimiter = "\r";

    /// <summary>
    /// Communication monitor for the device
    /// </summary>
    public StatusMonitorBase CommunicationMonitor { get; private set; }

    /// <summary>
    /// Connects/disconnects the comms of the plugin device
    /// </summary>
    /// <remarks>
    /// triggers the comms.Connect/Disconnect as well as thee comms monitor start/stop
    /// </remarks>
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

    public Dictionary<string, IRoutingInputSlot> InputSlots { get; private set; }

    public Dictionary<string, IRoutingOutputSlot> OutputSlots { get; private set; }

    public RoutingPortCollection<RoutingInputPort> InputPorts { get; private set; }

    public RoutingPortCollection<RoutingOutputPort> OutputPorts { get; private set; }

    public Dictionary<int, string> InputNames { get; private set; } = new Dictionary<int, string>();
    public Dictionary<int, string> OutputNames { get; private set; } = new Dictionary<int, string>();

    public event RouteChangedEventHandler RouteChanged;

    public List<RouteSwitchDescriptor> CurrentRoutes { get; private set; }

    /// <summary>
    /// Plugin device constructor for devices that need IBasicCommunication
    /// </summary>
    /// <param name="key"></param>
    /// <param name="name"></param>
    /// <param name="config"></param>
    /// <param name="comms"></param>
    public ACMXYxYDevice(string key, string name, DeviceConfig config, IBasicCommunication comms, string typeName)
  : base(key, name)
    {
      this.LogInformation("Constructing new {0} instance", name);

      this.config = config;

      receiveQueue = new GenericQueue(key + "-rxqueue");  // If you need to set the thread priority, use one of the available overloaded constructors.

      ConnectFeedback = new BoolFeedback("connect", () => Connect);
      OnlineFeedback = new BoolFeedback("online", () => CommunicationMonitor.IsOnline);
      StatusFeedback = new IntFeedback("status", () => (int)CommunicationMonitor.Status);

      this.comms = comms;
      CommunicationMonitor = new GenericCommunicationMonitor(
        this,
        this.comms,
        this.config.PollTimeMs == 0 ? 60000 : this.config.PollTimeMs,
        180000,
        300000,
        Poll);

      #region Communication data event handlers.  Comment out any that don't apply to the API type

      // Only one of the below handlers should be necessary.  

      commsGather = new CommunicationGather(this.comms, commsDelimiter);
      commsGather.LineReceived += Handle_LineRecieved;

      #endregion

      InputSlots = new Dictionary<string, IRoutingInputSlot>();
      OutputSlots = new Dictionary<string, IRoutingOutputSlot>();

      InputPorts = new RoutingPortCollection<RoutingInputPort>();
      OutputPorts = new RoutingPortCollection<RoutingOutputPort>();

      InputNames = this.config.InputNames;
      OutputNames = this.config.OutputNames;

      CurrentRoutes = new List<RouteSwitchDescriptor>();

      if (typeName == DeviceFactory.ACMX8x8)
      {
        for (var i = 1; i <= 8; i++)
        {
          SetupSlots(i);
        }
      }
      else if (typeName == DeviceFactory.ACMX16x16)
      {
        for (var i = 1; i <= 16; i++)
        {
          SetupSlots(i);
        }
      }
      else
      {
        throw new ArgumentOutOfRangeException($"Unsupported type name: {typeName}");
      }
    }

    public override void Initialize()
    {
      base.Initialize();

      var socket = this.comms as ISocketStatus;
      if (socket != null)
      {
        // device comms is IP **ELSE** device comms is RS232
        socket.ConnectionChange += socket_ConnectionChange;
        Connect = true;
      }
    }

    private string GetHdmiInputPortSelector(int slotNum)
    {
      return $"hdmi-in{slotNum}";
    }

    private string GetHdmiOutputPortSelector(int slotNum)
    {
      return $"hdmi-out{slotNum}";
    }

    private string GetAudioOutputPortSelector(int slotNum)
    {
      return $"audio-out{slotNum}";
    }

    private void SetupSlots(int slotNum)
    {
      var inputName = InputNames.ContainsKey(slotNum) ? InputNames[slotNum] : $"Input {slotNum}";
      var inputSlot = new InputSlot($"input{slotNum}", $"{inputName}", slotNum);
      InputSlots.Add(inputSlot.Key, inputSlot);
      var inputKey = GetHdmiInputPortSelector(slotNum);
      InputPorts.Add(
        new RoutingInputPort(
          inputKey,
          eRoutingSignalType.AudioVideo,
          eRoutingPortConnectionType.Hdmi,
          inputKey,
          this)
        {
          FeedbackMatchObject = inputKey,
        });

      var outputName = OutputNames.ContainsKey(slotNum) ? OutputNames[slotNum] : $"Output {slotNum}";
      var outputSlot = new OutputSlot($"output{slotNum}", $"{outputName}", slotNum);
      OutputSlots.Add(outputSlot.Key, outputSlot);

      var hdmiOutputKey = GetHdmiOutputPortSelector(slotNum);
      OutputPorts.Add(
        new RoutingOutputPort(
          hdmiOutputKey,
          eRoutingSignalType.AudioVideo,
          eRoutingPortConnectionType.Hdmi,
          hdmiOutputKey,
          this));

      var balAudOutputKey = GetAudioOutputPortSelector(slotNum);
      OutputPorts.Add(
        new RoutingOutputPort(
          balAudOutputKey,
          eRoutingSignalType.Audio,
          eRoutingPortConnectionType.LineAudio,
          balAudOutputKey,
          this));
    }


    private void socket_ConnectionChange(object sender, GenericSocketStatusChageEventArgs args)
    {
      ConnectFeedback?.FireUpdate();

      StatusFeedback?.FireUpdate();
    }

    private void Handle_LineRecieved(object sender, GenericCommMethodReceiveTextArgs args)
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
      if (message.Contains("VS"))
      {
        var regex = new System.Text.RegularExpressions.Regex(@"OUT(\d+)\s+VS\s+IN(\d+)");
        var match = regex.Match(message);

        if (match.Success)
        {
          var outputNumber = int.Parse(match.Groups[1].Value);
          var inputNumber = int.Parse(match.Groups[2].Value);

          // Use outputNumber and inputNumber as needed
          this.LogDebug("Route detected: Input {0} to Output {1}", inputNumber, outputNumber);

          var outputSlot = OutputSlots.FirstOrDefault(x => x.Value.SlotNumber == outputNumber).Value;
          var inputSlot = InputSlots.FirstOrDefault(x => x.Value.SlotNumber == inputNumber).Value;

          outputSlot.CurrentRoutes[eRoutingSignalType.Video] = inputSlot;

          UpdateCurrentRoutes(GetHdmiInputPortSelector(inputNumber), GetHdmiOutputPortSelector(outputNumber));
        }

        return;
      }

      if (message.Contains("AS"))
      {
        var regex = new System.Text.RegularExpressions.Regex(@"OUT(\d+)\s+AS\s+IN(\d+)");
        var match = regex.Match(message);
        if (match.Success)
        {
          var outputNumber = int.Parse(match.Groups[1].Value);
          var inputNumber = int.Parse(match.Groups[2].Value);
          // Use outputNumber and inputNumber as needed
          this.LogDebug("Audio Route detected: Input {0} to Output {1}", inputNumber, outputNumber);
          var outputSlot = OutputSlots.FirstOrDefault(x => x.Value.SlotNumber == outputNumber).Value;
          var inputSlot = InputSlots.FirstOrDefault(x => x.Value.SlotNumber == inputNumber).Value;
          outputSlot.CurrentRoutes[eRoutingSignalType.Audio] = inputSlot;

          UpdateCurrentRoutes(GetHdmiInputPortSelector(inputNumber), GetAudioOutputPortSelector(outputNumber));
        }
        return;
      }

      if (message.Contains("SIG STA"))
      {
        var regex = new System.Text.RegularExpressions.Regex(@"IN(\d+)\s+SIG\s+STA\s+(\d+)");
        var match = regex.Match(message);
        if (match.Success)
        {
          var inputNumber = int.Parse(match.Groups[1].Value);
          var status = int.Parse(match.Groups[2].Value);
          // Use inputNumber and status as needed
          this.LogDebug("Input {0} status: {1}", inputNumber, status);
          var inputSlot = InputSlots.FirstOrDefault(x => x.Value.SlotNumber == inputNumber).Value as InputSlot;
          if (inputSlot != null)
          {
            inputSlot.VideoSyncDetected = status == 1;
          }
        }
      }
    }


    // TODO [ ] If not using an ACII based API, delete the properties below
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

      comms.SendText(string.Format("{0}{1}", text, commsDelimiter));
    }

    /// <summary>
    /// Polls the device
    /// </summary>
    /// <remarks>
    /// Poll method is used by the communication monitor.  Update the poll method as needed for the plugin being developed
    /// </remarks>
    public void Poll()
    {
      SendText("GET STA");

      SendText("GET IN0 SIG STA");
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
      var joinMap = new JoinMap(joinStart);

      // This adds the join map to the collection on the bridge
      bridge?.AddJoinMap(Key, joinMap);

      var customJoins = JoinMapHelper.TryGetJoinMapAdvancedForDevice(joinMapKey);

      if (customJoins != null)
      {
        joinMap.SetCustomJoinData(customJoins);
      }

      this.LogDebug("Linking to Trilist {id}", trilist.ID.ToString("X"));
      this.LogInformation("Linking to Bridge Type {type}", GetType().Name);

      // TODO [ ] Implement bridge links as needed

      // links to bridge
      trilist.SetString(joinMap.DeviceName.JoinNumber, Name);

      trilist.SetBoolSigAction(joinMap.Connect.JoinNumber, sig => Connect = sig);
      ConnectFeedback.LinkInputSig(trilist.BooleanInput[joinMap.Connect.JoinNumber]);

      StatusFeedback.LinkInputSig(trilist.UShortInput[joinMap.Status.JoinNumber]);
      OnlineFeedback.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);

      UpdateFeedbacks();

      trilist.OnlineStatusChange += (o, a) =>
      {
        if (!a.DeviceOnLine) return;

        trilist.SetString(joinMap.DeviceName.JoinNumber, Name);
        UpdateFeedbacks();
      };
    }

    #endregion

    private void UpdateFeedbacks()
    {
      // TODO [ ] Update as needed for the plugin being developed
      ConnectFeedback?.FireUpdate();
      OnlineFeedback?.FireUpdate();
      StatusFeedback?.FireUpdate();
    }

    /// <summary>
    /// Routes an input to an output for the specified signal type(s)
    /// </summary>
    /// <param name="inputSlotKey"></param>
    /// <param name="outputSlotKey"></param>
    /// <param name="type"></param>
    public void Route(string inputSlotKey, string outputSlotKey, eRoutingSignalType type)
    {
      if (type.HasFlag(eRoutingSignalType.Video))
      {
        var input = InputSlots[inputSlotKey] as InputSlot;
        var output = OutputSlots[outputSlotKey] as OutputSlot;
        if (input == null || output == null)
        {
          Debug.LogError("Invalid input or output slot key");
          return;
        }
        SetVideoRoute(input.SlotNumber, output.SlotNumber);
      }

      if (type.HasFlag(eRoutingSignalType.Audio))
      {
        var input = InputSlots[inputSlotKey] as InputSlot;
        var output = OutputSlots[outputSlotKey] as OutputSlot;
        if (input == null || output == null)
        {
          Debug.LogError("Invalid input or output slot key");
          return;
        }
        SetAudioRoute(input.SlotNumber, output.SlotNumber);
      }
    }

    /// <summary>
    /// Executes a switch from an input to an output for the specified signal type(s)
    /// </summary>
    /// <param name="inputSelector"></param>
    /// <param name="outputSelector"></param>
    /// <param name="signalType"></param>
    public void ExecuteSwitch(object inputSelector, object outputSelector, eRoutingSignalType signalType)
    {
      Debug.LogVerbose(this, "Making route from input {0} to output {1}", inputSelector, outputSelector);

      if (signalType.HasFlag(eRoutingSignalType.Video))
      {
        SetVideoRoute((int)inputSelector, (int)outputSelector);

        UpdateCurrentRoutes((string)inputSelector, (string)outputSelector);
      }
      if (signalType.HasFlag(eRoutingSignalType.Audio))
      {
        SetAudioRoute((int)inputSelector, (int)outputSelector);

        UpdateCurrentRoutes((string)inputSelector, (string)outputSelector);
      }

    }

    /// <summary>
    /// Updates the current routes based on the input and output numbers.
    /// </summary>
    /// <param name="inputSelector"></param>
    /// <param name="outputSelector"></param>
    private void UpdateCurrentRoutes(string inputSelector, string outputSelector)
    {
      RouteSwitchDescriptor descriptor;

      descriptor = GetRouteDescriptorByOutputPort(outputSelector);

      var inputPort = GetRoutingInputPortForSelector(inputSelector);

      var outputPort = GetRoutingOutputPortForSelector(outputSelector);

      if (outputPort is null)
      {
        Debug.LogMessage(Serilog.Events.LogEventLevel.Warning, "Unable to find port for {outputNum}", this, outputSelector);
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
    /// Gets the route descriptor for the specified output port number.
    /// </summary>
    /// <param name="selector"></param>
    /// <returns></returns>
    private RouteSwitchDescriptor GetRouteDescriptorByOutputPort(string selector)
    {
      return CurrentRoutes.FirstOrDefault(rd =>
      {
        if (rd.OutputPort.Selector is not string opSelector)
        {
          return false;
        }

        return opSelector == selector;
      });
    }

    /// <summary>
    /// Gets the routing input port for the specified input number.
    /// </summary>
    /// <param name="selector"></param>
    /// <returns></returns>
    private RoutingInputPort GetRoutingInputPortForSelector(string selector)
    {

      return InputPorts.FirstOrDefault(ip =>
      {
        if (ip.Selector is not string ipSelector)
        {
          return false;
        }

        return ipSelector == selector;
      });
    }


    /// <summary>
    /// Gets the routing output port for the specified output number.
    /// </summary>
    /// <param name="selector"></param>
    /// <returns></returns>
    private RoutingOutputPort GetRoutingOutputPortForSelector(string selector)
    {

      return OutputPorts.FirstOrDefault(op =>
      {
        if (op.Selector is not string opSelector)
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
  }
}

