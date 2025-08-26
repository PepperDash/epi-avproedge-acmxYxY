using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Routing;
using System;
using System.Collections.Generic;

namespace PepperDash.Essentials.Plugin
{
  internal class OutputSlot : IRoutingOutputSlot
  {
    private readonly string key;

    public string RxDeviceKey => string.Empty; // This device doesn't use receivers so this is empty

    private readonly Dictionary<eRoutingSignalType, IRoutingInputSlot> currentRoutes = new()
    {
      { eRoutingSignalType.AudioVideo, default },
      { eRoutingSignalType.Audio, default },
      { eRoutingSignalType.Video, default }
    };

    public Dictionary<eRoutingSignalType, IRoutingInputSlot> CurrentRoutes => currentRoutes;

    public int SlotNumber { get; private set; }

    public eRoutingSignalType SupportedSignalTypes { get; private set; }

    public string Name { get; private set; }

    public string Key => $"{key}";

    public event EventHandler OutputSlotChanged;

    public OutputSlot(string key, string name, int slotNum)
    {
      this.key = key;
      Name = name;
      SlotNumber = slotNum;
    }

    public void SetInputRoute(eRoutingSignalType type, IRoutingInputSlot input)
    {
      if (currentRoutes.ContainsKey(type))
      {
        currentRoutes[type] = input;

        OutputSlotChanged?.Invoke(this, new EventArgs());

        return;
      }

      currentRoutes.Add(type, input);

      OutputSlotChanged?.Invoke(this, new EventArgs());
    }
  }
}
