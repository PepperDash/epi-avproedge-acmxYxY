using System.Collections.Generic;
using PepperDash.Core;
using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.Plugin.AVProEdge
{

  /// <summary>
  /// Plugin device factory for devices that use IBasicCommunication
  /// </summary>
  public class ACMXDeviceFactory : EssentialsPluginDeviceFactory<ACMXDevice>
  {
    /// <summary>
    /// Plugin device factory constructor
    /// </summary>
    public ACMXDeviceFactory()
    {
      // Set the minimum Essentials Framework Version
      // TODO [ ] Update the Essentials minimum framework version which this plugin has been tested against
      MinimumEssentialsFrameworkVersion = "2.12.4";

      // In the constructor we initialize the list with the typenames that will build an instance of this device
      // TODO [ ] Update the TypeNames for the plugin being developed
      TypeNames = new List<string>() { "acmx8x8", "acmx16x16" };
    }

    /// <summary>
    /// Builds and returns an instance of EssentialsPluginDeviceTemplate
    /// </summary>
    /// <param name="dc">device configuration</param>
    /// <returns>plugin device or null</returns>
    /// <seealso cref="PepperDash.Core.eControlMethod"/>
    public override EssentialsDevice BuildDevice(Core.Config.DeviceConfig dc)
    {
      Debug.LogVerbose("[{key}] Factory Attempting to create new device from type: {type}", dc.Key, dc.Type);

      // get the plugin device properties configuration object & check for null 
      var propertiesConfig = dc.Properties.ToObject<ACMXConfig>();
      if (propertiesConfig == null)
      {
        Debug.LogError("[{key}] Factory: failed to read properties config for {name}", dc.Key, dc.Name);
        return null;
      }

      // attempt build the plugin device comms device & check for null
      // TODO { ] As of PepperDash Core 1.0.41, HTTP and HTTPS are not valid eControlMethods and will throw an exception.
      var comms = CommFactory.CreateCommForDevice(dc);
      if (comms != null) return new ACMXDevice(dc.Key, dc.Name, propertiesConfig, comms, dc.Type);

      Debug.LogError("[{key}] Factory Notice: No control object present for device {name}", dc.Key, dc.Name);
      return null;
    }
  }
}

