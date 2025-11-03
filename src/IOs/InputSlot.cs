using System;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Routing;

namespace PepperDash.Essentials.Plugin.IOs
{
  internal class InputSlot : IRoutingInputSlot
  {
    private readonly string key;

    public string TxDeviceKey => string.Empty; // This device doesn't use transmitters so this is empty

    public int SlotNumber { get; private set; }

    public eRoutingSignalType SupportedSignalTypes => eRoutingSignalType.AudioVideo;

    public string Name { get; private set; }

    public BoolFeedback IsOnline { get; private set; }

    private bool videoSyncDetected = false;

    public bool VideoSyncDetected
    {
      get
      {
        return videoSyncDetected;
      }
      set
      {
        if (videoSyncDetected != value)
        {
          videoSyncDetected = value;
          VideoSyncChanged?.Invoke(this, EventArgs.Empty);
        }
      }
    }

    public string Key => $"{key}";

    public event EventHandler VideoSyncChanged;

    public InputSlot(string key, string name, int slotNum)
    {
      this.key = key;
      Name = name;
      IsOnline = new BoolFeedback("IsOnline", () => true); // Placeholder for actual online status since the input doesn't have it's own status independent of the chasses
      IsOnline.FireUpdate();
      SlotNumber = slotNum;
    }
  }
}
