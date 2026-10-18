namespace Apollo.Web.Client.Components.Stats;

/// <summary>One cell of the activity heatmap: the messages of one weekday from <see cref="StartHour"/> on.</summary>
/// <param name="StartHour">The first Zurich hour of the block (0 to 23).</param>
/// <param name="Count">The messages in the block's hours.</param>
public sealed record HeatmapBlock(int StartHour, long Count);
