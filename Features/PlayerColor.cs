using EFT.Trainer.Configuration;
using UnityEngine;

namespace EFT.Trainer.Features;

public class PlayerColor(Color color, Color borderColor, Color infoColor) : IFeature
{
	[ConfigurationProperty(Order = 1)]
	public Color Color { get; set; } = color;

	[ConfigurationProperty(Order = 2)]
	public Color BorderColor { get; set; } = borderColor;

	[ConfigurationProperty(Order = 3)]
	public Color OccludedColor { get; set; } = color;

	[ConfigurationProperty(Order = 4)]
	public Color InfoColor { get; set; } = infoColor;

	public string Name => nameof(PlayerColor);
}
