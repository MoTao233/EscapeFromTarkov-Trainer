using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EFT.InputSystem;
using EFT.Trainer.Configuration;
using EFT.Trainer.Features;
using EFT.Trainer.Properties;
using EFT.Trainer.UI;
using UnityEngine;

#nullable enable

namespace EFT.Trainer.Features;

internal abstract class FeatureRenderer : ToggleFeature
{
	public abstract float X { get; set; }
	public abstract float Y { get; set; }

	protected const float DefaultX = 40f;
	protected const float DefaultY = 20f;

	protected void SetupWindowCoordinates()
	{
		bool needfix = false;
		X = FixCoordinate(X, Screen.width, DefaultX, ref needfix);
		Y = FixCoordinate(Y, Screen.height, DefaultY, ref needfix);

		if (needfix)
			SaveSettings();
	}

	private static float FixCoordinate(float coord, float maxValue, float defaultValue, ref bool needfix)
	{
		if (coord < 0 || coord >= maxValue)
		{
			coord = defaultValue;
			needfix = true;
		}

		return coord;
	}

	internal abstract class SelectionContext<T>
	{
		protected SelectionContext(IFeature feature, OrderedProperty orderedProperty, float parentX, float parentY, Func<T, Picker<T>> builder)
		{
			Feature = feature;
			OrderedProperty = orderedProperty;
			Picker = builder((T)orderedProperty.Property.GetValue(feature));

			// Convert out of the scroll view before placing a separate GUI window.
			var position = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
			Picker.SetWindowPosition(Mathf.Clamp(parentX + Theme.WindowWidth + 8f, 0, Mathf.Max(0, Screen.width - 220)),
				Mathf.Clamp(position.y - 32f, 0, Mathf.Max(0, Screen.height - 260)));
		}

		public IFeature Feature { get; }
		public OrderedProperty OrderedProperty { get; }
		public Picker<T> Picker { get; }
		public abstract int Id { get; }
	}

	internal class ColorSelectionContext(IFeature feature, OrderedProperty orderedProperty, float parentX, float parentY) : SelectionContext<Color>(feature, orderedProperty, parentX, parentY, color => new ColorPicker(color))
	{
		public override int Id => 1;
	}

	internal class KeyCodeSelectionContext(IFeature feature, OrderedProperty orderedProperty, float parentX, float parentY) : SelectionContext<KeyCode>(feature, orderedProperty, parentX, parentY, color => new EnumPicker<KeyCode>(color))
	{
		public override int Id => 2;
	}

	private Rect _clientWindowRect;
	private ColorSelectionContext? _colorSelectionContext = null;
	private KeyCodeSelectionContext? _keyCodeSelectionContext = null;
	protected override void OnGUIWhenEnabled()
	{
		SetupInputNode();

		var previousSkin = GUI.skin;
		try
		{
			GUI.skin = Theme.Skin;

			_clientWindowRect = new Rect(X, Y, Theme.WindowWidth, _clientWindowRect.height);
			_clientWindowRect = GUILayout.Window(0, _clientWindowRect, RenderFeatureWindow, Strings.FeatureCommandsTitle, GUILayout.ExpandHeight(true));
			X = _clientWindowRect.x;
			Y = _clientWindowRect.y;

			HandleSelectionContext(_colorSelectionContext);
			HandleSelectionContext(_keyCodeSelectionContext);
		}
		finally
		{
			GUI.skin = previousSkin;
		}
	}

	private void HandleSelectionContext<T>(SelectionContext<T>? context)
	{
		if (context == null)
			return;

		var property = context.OrderedProperty.Property;
		var picker = context.Picker;

		picker.DrawWindow(context.Id, GetPropertyDisplay(property.Name));
		property.SetValue(context.Feature, picker.Value);

		if (!picker.IsSelected)
			return;

		if (ReferenceEquals(context, _colorSelectionContext))
			_colorSelectionContext = null;

		if (ReferenceEquals(context, _keyCodeSelectionContext))
			_keyCodeSelectionContext = null;
	}

	private const int SummaryTabIndex = -1;
	private static string SummaryTitle => Strings.FeatureRendererSummary.Trim('[', ']', ' ');

	private int _selectedTabIndex = SummaryTabIndex;
	private FeatureCategory _category = FeatureCatalog.Categories[0];
	private Vector2 _sidebarScroll;
	private Vector2 _featureScroll;
	private Vector2 _settingsScroll;

	private void RenderFeatureWindow(int id)
	{
		GUILayout.BeginHorizontal();

		var panel = Theme.PanelHeight;

		// Categories
		GUILayout.BeginVertical(Theme.Sidebar, GUILayout.Width(Theme.SidebarWidth), GUILayout.Height(panel));
		_sidebarScroll = GUILayout.BeginScrollView(_sidebarScroll, false, false, GUI.skin.horizontalScrollbar, GUI.skin.verticalScrollbar);

		if (Theme.SidebarItem(SummaryTitle, _selectedTabIndex == SummaryTabIndex, null))
			SelectSummary();

		Theme.SidebarDivider();

		foreach (var category in FeatureCatalog.Categories)
		{
			if (FeatureCatalog.GetAreas(category).Length == 0)
				continue;

			if (Theme.SidebarItem(GetCategoryDisplay(category), _selectedTabIndex != SummaryTabIndex && _category == category, null))
				SelectCategory(category);
		}
		GUILayout.EndScrollView();
		GUILayout.EndVertical();

		GUILayout.Space(Theme.Gap);

		// Features of the selected category
		GUILayout.BeginVertical(Theme.Sidebar, GUILayout.Width(Theme.FeatureListWidth), GUILayout.Height(panel));
		_featureScroll = GUILayout.BeginScrollView(_featureScroll, false, false, GUI.skin.horizontalScrollbar, GUI.skin.verticalScrollbar);
		RenderFeatureList();
		GUILayout.EndScrollView();
		GUILayout.EndVertical();

		GUILayout.Space(Theme.Gap);

		// Settings
		GUILayout.BeginVertical(GUILayout.Width(Theme.ContentWidth), GUILayout.Height(panel));
		RenderPage();
		GUILayout.EndVertical();

		GUILayout.EndHorizontal();

		Theme.WindowHeader(Theme.WindowWidth, Key == KeyCode.None ? null : Key.ToString());
		GUI.DragWindow();
	}

	// The selected feature is addressed by its index in the flattened category list,
	// so the flattened order is computed once per frame and used by both the list and the page.
	private (string? Title, Feature[] Features)[] GetListGroups()
	{
		return
		[
			.. FeatureCatalog
				.GetAreas(_category)
				.Select(area => (area == FeatureArea.General ? null : (string?)GetAreaDisplay(area), FeatureCatalog.GetFeatures(_category, area)))
				.Where(group => group.Item2.Length > 0)
		];
	}

	private void RenderFeatureList()
	{
		if (_selectedTabIndex == SummaryTabIndex)
			return;

		var index = 0;
		foreach (var (title, features) in GetListGroups())
		{
			if (title != null)
				Theme.SidebarHeader(title);

			foreach (var feature in features)
			{
				var selected = index++ == _selectedTabIndex;
				var state = feature is ToggleFeature toggle && !ConfigurationManager.IsSkippedProperty(feature, nameof(Enabled)) ? toggle.Enabled : (bool?)null;

				if (Theme.SidebarItem(feature.Name, selected, state, GetInteractionDisplay(feature), FeatureCatalog.IsLegacy(feature.GetType())))
					SelectFeature(feature);
			}
		}
	}

	private Feature? SelectedFeature
	{
		get
		{
			if (_selectedTabIndex == SummaryTabIndex)
				return null;

			var index = _selectedTabIndex;
			foreach (var (_, features) in GetListGroups())
			{
				if (index < features.Length)
					return features[index];

				index -= features.Length;
			}

			return null;
		}
	}

	private void SelectSummary()
	{
		_selectedTabIndex = SummaryTabIndex;
		ResetSelection();
	}

	private void SelectCategory(FeatureCategory category)
	{
		if (_selectedTabIndex != SummaryTabIndex && _category == category)
			return;

		_category = category;
		_selectedTabIndex = 0;
		ResetSelection();
	}

	private void SelectFeature(Feature feature)
	{
		var index = 0;
		foreach (var (_, features) in GetListGroups())
		{
			var found = Array.IndexOf(features, feature);
			if (found >= 0)
			{
				index += found;
				break;
			}

			index += features.Length;
		}

		if (_selectedTabIndex == index)
			return;

		_selectedTabIndex = index;
		ResetSelection();
	}

	private void ResetSelection()
	{
		_settingsScroll = Vector2.zero;
		_featureScroll = Vector2.zero;
		_colorSelectionContext = null;
		_keyCodeSelectionContext = null;
	}

	private void RenderPage()
	{
		var feature = SelectedFeature;
		if (feature == null)
		{
			RenderSummary();
			return;
		}

		RenderFeature(feature);
	}

	private static string GetCategoryDisplay(FeatureCategory category)
	{
		return Strings.ResourceManager.GetString($"Category{category}", Strings.Culture) ?? category.ToString();
	}

	private static string GetAreaDisplay(FeatureArea area)
	{
		return Strings.ResourceManager.GetString($"CategoryGroup{area}", Strings.Culture) ?? area.ToString();
	}

	// Hold and trigger features act while pressed or once per press; the badge tells them apart from plain toggles.
	private static string? GetInteractionDisplay(Feature feature)
	{
		return feature switch
		{
			HoldFeature => Strings.TextHold,
			TriggerFeature => Strings.TextTrigger,
			_ => null,
		};
	}

	private void RenderSummary()
	{
		Theme.PageHeader(SummaryTitle, Strings.FeatureRendererWelcome.Trim());

		GUILayout.BeginVertical(Theme.Card);

		GUILayout.BeginHorizontal();
		if (GUILayout.Button(Strings.CommandLoadDescription, Theme.PrimaryButton))
			LoadSettings();

		GUILayout.Space(8);
		if (GUILayout.Button(Strings.CommandSaveDescription, Theme.PrimaryButton))
			SaveSettings();
		GUILayout.EndHorizontal();

		GUILayout.Label(Context.ConfigFile, Theme.Caption);
		if (!string.IsNullOrEmpty(ConfigurationManager.LastStatus))
			GUILayout.Label(ConfigurationManager.LastStatus, new GUIStyle(Theme.Caption)
			{
				normal = { textColor = ConfigurationManager.LastOperationSucceeded ? Theme.On : Theme.Warning }
			});

		GUILayout.EndVertical();
	}

	protected static void SaveSettings()
	{
		ConfigurationManager.Save(Context.ConfigFile, Context.Features.Value);
	}

	protected void LoadSettings(bool warnIfNotExists = true)
	{
		var cx = X;
		var cy = Y;

		ConfigurationManager.Load(Context.ConfigFile, Context.Features.Value, warnIfNotExists);
		_controlValues.Clear();
		_colorSelectionContext = null;
		_keyCodeSelectionContext = null;

		if (!Enabled)
			return;

		X = cx;
		Y = cy;
	}

	private void RenderFeature(Feature feature)
	{
		var orderedProperties = ConfigurationManager.GetOrderedProperties(feature.GetType());

		Theme.PageHeader(feature.Name, feature.Description);

		_settingsScroll = GUILayout.BeginScrollView(_settingsScroll, false, false, GUI.skin.horizontalScrollbar, GUI.skin.verticalScrollbar, GUILayout.Width(Theme.ContentWidth), GUILayout.ExpandHeight(true));
		GUILayout.BeginVertical(Theme.Card);
		GUILayout.BeginVertical(Theme.CardContent);
		foreach (var property in orderedProperties)
			RenderFeatureProperty(feature, property);
		GUILayout.EndVertical();
		GUILayout.EndVertical();

		GUILayout.EndScrollView();
	}

	private static readonly Dictionary<string, string> _controlValues = [];
	private void RenderFeatureProperty(Feature feature, OrderedProperty orderedProperty)
	{
		if (!orderedProperty.Attribute.Browsable)
			return;

		var property = orderedProperty.Property;
		var currentValue = property.GetValue(feature);

		if (currentValue == null)
			return;

		Theme.BeginRow();

		GUILayout.Label(GetPropertyDisplay(property.Name), Theme.RowLabel);
		GUILayout.FlexibleSpace();

		var width = GUILayout.Width(Theme.ControlWidth);
		var newValue = RenderFeaturePropertyAsUIComponent(feature, orderedProperty, currentValue, width);

		if (currentValue != newValue)
			property.SetValue(feature, newValue);

		Theme.EndRow();

		var focused = GUI.GetNameOfFocusedControl();

		if (ShouldResetSelectionContext(focused, _colorSelectionContext))
			_colorSelectionContext = null;

		if (ShouldResetSelectionContext(focused, _keyCodeSelectionContext))
			_keyCodeSelectionContext = null;
	}

	protected abstract string GetPropertyDisplay(string propertyName);

	private object RenderFeaturePropertyAsUIComponent(IFeature feature, OrderedProperty orderedProperty, object currentValue, GUILayoutOption width, string? parentName = null)
	{
		var property = orderedProperty.Property;
		var propertyType = property.PropertyType;

		var newValue = currentValue;
		var controlName = $"{parentName ?? feature.Name}.{property.Name}-{propertyType.Name}";
		GUI.SetNextControlName(controlName);

		switch (propertyType.Name)
		{
			case nameof(Boolean):
				newValue = RenderBooleanProperty(currentValue, width);
				break;

			case nameof(KeyCode):
				RenderKeyCodeProperty(currentValue, controlName, feature, orderedProperty, width);
				break;

			case nameof(Single):
				newValue = RenderFloatProperty(currentValue, controlName, width);
				break;

			case nameof(Int32):
				newValue = RenderIntProperty(currentValue, width);
				break;

			case nameof(Color):
				RenderColorProperty(currentValue, controlName, feature, orderedProperty, width);
				break;

			case nameof(String):
				newValue = RenderStringProperty(currentValue, width);
				break;

			default:
				// Look for inner properties
				if (currentValue is IFeature subFeature)
				{
					var subProperties = ConfigurationManager.GetOrderedProperties(propertyType);
					var length = subProperties.Length;

					if (length > 0)
					{
						width = GUILayout.Width(SubControlWidth(length));

						foreach (var innerOrderedProperty in subProperties)
						{
							var innerProperty = innerOrderedProperty.Property;
							var innerPropertyValue = innerProperty.GetValue(subFeature);
							RenderFeaturePropertyAsUIComponent(subFeature, innerOrderedProperty, innerPropertyValue, width, controlName);
						}

						break;
					}

				}

				GUILayout.Label(string.Format(Strings.ErrorUnsupportedTypeFormat, propertyType.FullName), Theme.Caption);
				break;
		}

		return newValue;
	}

	private static float SubControlWidth(int count)
	{
		// Swatches carry a 4px left margin each, keep the last one inside the control column.
		return (Theme.ControlWidth - 4f * count) / count;
	}

	private static bool ShouldResetSelectionContext<T>(string focused, SelectionContext<T>? context)
	{
		return !string.IsNullOrEmpty(focused)
			   && !focused.EndsWith($"-{typeof(T).Name}")
			   && context != null;
	}

	private static object RenderIntProperty(object currentValue, GUILayoutOption option)
	{
		object newValue = currentValue;

		if (int.TryParse(GUILayout.TextField(currentValue.ToString(), Theme.TextField, option), out var intValue))
			newValue = intValue;

		return newValue;
	}

	private static object RenderStringProperty(object currentValue, GUILayoutOption width)
	{
		return GUILayout.TextField(currentValue.ToString(), Theme.TextField, width);
	}

	private void RenderKeyCodeProperty(object currentValue, string controlName, IFeature feature, OrderedProperty orderedProperty, GUILayoutOption option)
	{
		var key = (KeyCode)currentValue;

		if (!GUILayout.Button(key == KeyCode.None ? "—" : key.ToString(), Theme.RowButton, option))
			return;

		_keyCodeSelectionContext = new KeyCodeSelectionContext(feature, orderedProperty, X, Y);
		GUI.FocusControl(controlName);
	}

	private void RenderColorProperty(object currentValue, string controlName, IFeature feature, OrderedProperty orderedProperty, GUILayoutOption option)
	{
		if (!Theme.ColorSwatch((Color)currentValue, option))
			return;

		_colorSelectionContext = new ColorSelectionContext(feature, orderedProperty, X, Y);
		GUI.FocusControl(controlName);
	}

	private static object RenderFloatProperty(object currentValue, string controlName, GUILayoutOption width)
	{
		const string decimalSeparator = ".";
		const string altDecimalSeparator = ",";

		var culture = CultureInfo.InvariantCulture;
		var newValue = currentValue;

		if (!_controlValues.TryGetValue(controlName, out var controlText))
			controlText = currentValue.ToString();

		var style = controlText == currentValue.ToString() ? Theme.TextField : Theme.TextFieldInvalid;

		controlText = GUILayout
			.TextField(controlText, style, width)
			.Replace(altDecimalSeparator, decimalSeparator);

		if (!controlText.EndsWith(decimalSeparator) && float.TryParse(controlText, NumberStyles.Float, culture, out var floatValue))
		{
			newValue = floatValue;
			controlText = newValue.ToString();
		}

		_controlValues[controlName] = controlText;
		return newValue;
	}

	private object RenderBooleanProperty(object currentValue, GUILayoutOption option)
	{
		var boolValue = (bool)currentValue;
		var newValue = Theme.SwitchToggle(boolValue);
		if (newValue != boolValue)
		{
			_colorSelectionContext = null;
			_keyCodeSelectionContext = null;
		}

		return newValue;
	}

#if EFT_LIVE 
	protected
#else
	public
#endif
	override ETranslateResult TranslateCommand(ECommand command)
	{
		return command switch
		{
			// We do not want the player to shoot while clicking on a menu button
			ECommand.ToggleShooting when Enabled => ETranslateResult.BlockAll,
			_ => ETranslateResult.Ignore
		};
	}

#if EFT_LIVE 
	protected
#else
	public
#endif
	override void TranslateAxes(ref float[] axes)
	{
		// this will disable the axes for player movement
		if (Enabled)
			axes = null!;
	}

#if EFT_LIVE 
	protected
#else
	public
#endif
	override ECursorResult ShouldLockCursor()
	{
		return Enabled ? ECursorResult.ShowCursor : ECursorResult.Ignore;
	}

	private void SetupInputNode()
	{
		var player = GameState.Current?.LocalPlayer;
		if (player == null)
			return;

		if (!player.TryGetComponent<PlayerOwner>(out var owner))
			return;

		if (owner.InputTree.Contains(this))
			return;

		owner.InputTree.Add(this);
	}
}
