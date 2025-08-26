using System.Collections.Generic;
using Newtonsoft.Json;
using PepperDash.Essentials.Core;

namespace PepperDash.Essentials.Plugin.AvProEdge
{
  /// <summary>
  /// Represents the "properties" section of the device JSON configuration
  /// </summary>
	public class DeviceConfig
	{
		/// <summary>
		/// JSON control object
		/// </summary>
		[JsonProperty("control")]
		public EssentialsControlPropertiesConfig Control { get; set; }

    /// <summary>
    /// Serializes the poll time value
    /// </summary>
    [JsonProperty("pollTimeMs", NullValueHandling = NullValueHandling.Ignore)]
    public long PollTimeMs { get; set; } = 60000;

    /// <summary>
    /// Serializes the input names dictionary
    /// </summary>
    [JsonProperty("inputNames")]
    public Dictionary<int, string> InputNames { get; set; } = new Dictionary<int, string>();

    /// <summary>
    /// Serializes the output names dictionary
    /// </summary>
    [JsonProperty("outputNames")]
    public Dictionary<int, string> OutputNames { get; set; } = new Dictionary<int, string>();

    [JsonProperty("noRouteText")]
    public string NoRouteText { get; set; } = "Clear";

    /// <summary>
    /// Constuctor
    /// </summary>
    public DeviceConfig()
		{

		}
	}
}