using System;
using UnityEngine;

#nullable enable

namespace EFT.Trainer.UI;

// Dark IMGUI theme for the trainer windows.
// All textures are generated at runtime, so no asset bundle is needed.
// This file only depends on UnityEngine, so it can be previewed in a plain Unity project.
internal static class Theme
{
	public const float WindowWidth = 950f;
	public const float SidebarWidth = 152f;
	public const float FeatureListWidth = 210f;
	public const float ContentWidth = 540f;
	public const float Gap = 12f;
	public const float HeaderHeight = 44f;
	public const float RowHeight = 32f;
	public const float LabelWidth = 286f;
	public const float ControlWidth = 194f;

	public static readonly Color Background = Rgb(0x111418, 0.97f);
	public static readonly Color Panel = Rgb(0x171B21);
	public static readonly Color Surface = Rgb(0x1B2027);
	public static readonly Color Control = Rgb(0x252B34);
	public static readonly Color ControlHover = Rgb(0x2E3540);
	public static readonly Color ControlActive = Rgb(0x363E4B);
	public static readonly Color Border = Rgb(0x2A3039);
	public static readonly Color Divider = Rgb(0x232830);
	public static readonly Color Text = Rgb(0xE6E9EE);
	public static readonly Color TextSecondary = Rgb(0xC3C8D1);
	public static readonly Color TextMuted = Rgb(0x858E9C);
	public static readonly Color Accent = Rgb(0xE8A33D);
	public static readonly Color AccentHover = Rgb(0xF0B458);
	public static readonly Color AccentText = Rgb(0x1C1407);
	public static readonly Color On = Rgb(0x45CC88);
	public static readonly Color Off = Rgb(0x4A515D);
	public static readonly Color Danger = Rgb(0xEE6A5C);
	public static readonly Color Warning = Rgb(0xF2A65A);

	private static readonly string[] FontNames = ["Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI", "PingFang SC", "Noto Sans CJK SC", "Arial"];

	private static GUISkin? _skin;
	private static Texture2D? _white;
	private static Texture2D? _circle;
	private static GUIStyle? _rounded;

	public static GUISkin Skin
	{
		get
		{
			// Unity objects can be destroyed behind our back (scene unloads), use Unity's null check.
			if (_skin == null || _white == null || _circle == null)
				Build();

			return _skin!;
		}
	}

	public static GUIStyle Window { get; private set; } = GUIStyle.none;
	public static GUIStyle PopupWindow { get; private set; } = GUIStyle.none;
	public static GUIStyle Sidebar { get; private set; } = GUIStyle.none;
	public static GUIStyle SidebarText { get; private set; } = GUIStyle.none;
	public static GUIStyle SidebarTextSelected { get; private set; } = GUIStyle.none;
	public static GUIStyle SidebarTextMuted { get; private set; } = GUIStyle.none;
	public static GUIStyle SidebarTextMutedSelected { get; private set; } = GUIStyle.none;
	public static GUIStyle SidebarHeaderStyle { get; private set; } = GUIStyle.none;
	public static GUIStyle Badge { get; private set; } = GUIStyle.none;
	public static GUIStyle Card { get; private set; } = GUIStyle.none;
	public static GUIStyle CardContent { get; private set; } = GUIStyle.none;
	public static GUIStyle PageTitle { get; private set; } = GUIStyle.none;
	public static GUIStyle Description { get; private set; } = GUIStyle.none;
	public static GUIStyle Caption { get; private set; } = GUIStyle.none;
	public static GUIStyle Section { get; private set; } = GUIStyle.none;
	public static GUIStyle Row { get; private set; } = GUIStyle.none;
	public static GUIStyle RowLabel { get; private set; } = GUIStyle.none;
	public static GUIStyle Switch { get; private set; } = GUIStyle.none;
	public static GUIStyle RowButton { get; private set; } = GUIStyle.none;
	public static GUIStyle RowButtonMuted { get; private set; } = GUIStyle.none;
	public static GUIStyle PrimaryButton { get; private set; } = GUIStyle.none;
	public static GUIStyle TextField { get; private set; } = GUIStyle.none;
	public static GUIStyle TextFieldInvalid { get; private set; } = GUIStyle.none;
	public static GUIStyle Swatch { get; private set; } = GUIStyle.none;
	public static GUIStyle SwatchFrame { get; private set; } = GUIStyle.none;
	public static GUIStyle KeyCap { get; private set; } = GUIStyle.none;
	public static GUIStyle ListItem { get; private set; } = GUIStyle.none;
	public static GUIStyle ListItemSelected { get; private set; } = GUIStyle.none;

	public static float PanelHeight => Mathf.Clamp(Screen.height * 0.72f, 360f, 780f);

	// Themed drawing helpers, shared by all trainer windows.

	public static void WindowHeader(float width, string? keyHint)
	{
		Fill(new Rect(0, HeaderHeight - 1, width, 1), Divider);

		if (string.IsNullOrEmpty(keyHint))
			return;

		var content = new GUIContent(keyHint);
		var size = KeyCap.CalcSize(content);
		GUI.Label(new Rect(width - size.x - 16f, (HeaderHeight - size.y) / 2f, size.x, size.y), content, KeyCap);
	}

	public static bool SidebarItem(string text, bool selected, bool? state, string? badge = null, bool muted = false)
	{
		var rect = GUILayoutUtility.GetRect(GUIContent.none, SidebarText, GUILayout.ExpandWidth(true));
		var clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);

		if (Event.current.type == EventType.Repaint)
		{
			if (selected)
			{
				FillRounded(rect, WithAlpha(Accent, 0.13f));
				Fill(new Rect(rect.x, rect.y + 8f, 3f, rect.height - 16f), Accent);
			}
			else if (rect.Contains(Event.current.mousePosition))
			{
				FillRounded(rect, WithAlpha(Color.white, 0.045f));
			}

			if (state.HasValue)
				Dot(new Rect(rect.xMax - 14f, rect.center.y - 4f, 9f, 9f), state.Value ? On : Off);
		}

		var style = selected
			? (muted ? SidebarTextMutedSelected : SidebarTextSelected)
			: (muted ? SidebarTextMuted : SidebarText);

		var right = 20f;

		if (state.HasValue)
			right += 16f;

		if (!string.IsNullOrEmpty(badge))
		{
			var content = new GUIContent(badge);
			var size = Badge.CalcSize(content);
			var badgeRect = new Rect(rect.xMax - right - size.x, rect.center.y - size.y / 2f, size.x, size.y);

			if (Event.current.type == EventType.Repaint)
				GUI.Label(badgeRect, content, Badge);

			right += size.x + 4f;
		}

		var textRect = new Rect(rect.x + 14f, rect.y, rect.width - right - 14f, rect.height);
		GUI.Label(textRect, text, style);

		return clicked;
	}

	public static void SidebarHeader(string text)
	{
		GUILayout.Label(text, SidebarHeaderStyle);
	}

	public static void SidebarDivider()
	{
		var rect = GUILayoutUtility.GetRect(1f, 9f, GUILayout.ExpandWidth(true));
		Fill(new Rect(rect.x + 8f, rect.y + 4f, rect.width - 16f, 1f), Divider);
	}

	public static void PageHeader(string title, string? description)
	{
		var rect = GUILayoutUtility.GetRect(new GUIContent(title), PageTitle);
		if (Event.current.type == EventType.Repaint)
		{
			Fill(new Rect(rect.x, rect.y + 2f, 3f, rect.height - 4f), Accent);
			GUI.Label(rect, title, PageTitle);
		}

		if (!string.IsNullOrEmpty(description))
			GUILayout.Label(description, Description);
	}

	public static void SectionHeader(string title)
	{
		GUILayout.Label(title, Section);
	}

	public static void BeginRow()
	{
		GUILayout.BeginHorizontal(Row);
	}

	public static void EndRow()
	{
		GUILayout.EndHorizontal();

		// A whole-row hover makes it obvious which setting the mouse is over.
		if (Event.current.type != EventType.Repaint)
			return;

		var rect = GUILayoutUtility.GetLastRect();
		if (rect.Contains(Event.current.mousePosition))
			FillRounded(rect, WithAlpha(Color.white, 0.035f));
	}

	public static bool SwitchToggle(bool value)
	{
		return GUILayout.Toggle(value, GUIContent.none, Switch);
	}

	public static bool ColorSwatch(Color color, params GUILayoutOption[] options)
	{
		var background = GUI.backgroundColor;
		GUI.backgroundColor = color;
		var clicked = GUILayout.Button(GUIContent.none, Swatch, options);
		GUI.backgroundColor = background;

		if (Event.current.type == EventType.Repaint)
		{
			var rect = GUILayoutUtility.GetLastRect();
			SwatchFrame.Draw(rect, false, false, rect.Contains(Event.current.mousePosition), false);
		}

		return clicked;
	}

	public static void Fill(Rect rect, Color color)
	{
		if (Event.current.type != EventType.Repaint)
			return;

		var previous = GUI.color;
		GUI.color = color;
		GUI.DrawTexture(rect, _white);
		GUI.color = previous;
	}

	public static void FillRounded(Rect rect, Color color)
	{
		if (Event.current.type != EventType.Repaint || _rounded == null)
			return;

		var previous = GUI.backgroundColor;
		GUI.backgroundColor = color;
		_rounded.Draw(rect, false, false, false, false);
		GUI.backgroundColor = previous;
	}

	public static void Dot(Rect rect, Color color)
	{
		if (Event.current.type != EventType.Repaint)
			return;

		var previous = GUI.color;
		GUI.color = color;
		GUI.DrawTexture(rect, _circle);
		GUI.color = previous;
	}

	private static void Build()
	{
		_white = RoundedRect(Color.white, Color.white, 0f, 0f, 4);
		_circle = RoundedRect(Color.white, Color.white, 16f, 0f, 32);
		_rounded = new GUIStyle { normal = { background = RoundedRect(Color.white, Color.white, 6f, 0f) }, border = Uniform(8) };

		var skin = UnityEngine.Object.Instantiate(GUI.skin);
		skin.hideFlags = HideFlags.HideAndDontSave;
		skin.font = CreateFont() ?? skin.font;

		// Styles owned by the skin are updated in place: IMGUI looks up companion styles by name
		// (e.g. "verticalscrollbarthumb"), so replacing the instances would break scroll views.
		var windowBackground = RoundedRect(Background, Border, 10f, 1f);
		Set(skin.window, windowBackground, Uniform(12));
		skin.window.onNormal.background = windowBackground;
		skin.window.padding = new RectOffset(14, 14, (int)HeaderHeight + 12, 14);
		skin.window.contentOffset = new Vector2(4f, -HeaderHeight - 12 + 13f);
		skin.window.alignment = TextAnchor.UpperLeft;
		skin.window.fontSize = 15;
		skin.window.fontStyle = FontStyle.Bold;
		skin.window.normal.textColor = skin.window.onNormal.textColor = Text;
		Window = skin.window;

		PopupWindow = new GUIStyle(skin.window)
		{
			padding = new RectOffset(10, 10, 36, 10),
			contentOffset = new Vector2(2f, -26f),
			fontSize = 13,
		};

		var card = RoundedRect(Surface, Border, 8f, 1f);
		Set(skin.box, card, Uniform(10));
		skin.box.padding = Uniform(6);
		skin.box.margin = new RectOffset(0, 0, 0, 0);

		SetText(skin.label, 13, TextSecondary);
		skin.label.hover.textColor = Accent;
		skin.label.padding = new RectOffset(6, 6, 3, 3);
		skin.label.richText = true;

		SetButton(skin.button, Control, ControlHover, ControlActive, Text);
		skin.button.fixedHeight = 30f;
		skin.button.padding = new RectOffset(14, 14, 4, 4);
		skin.button.margin = new RectOffset(0, 0, 0, 0);

		RowButton = new GUIStyle(skin.button) { fixedHeight = 24f, margin = new RectOffset(0, 0, 4, 4), fontSize = 12 };
		RowButtonMuted = new GUIStyle(RowButton);
		RowButtonMuted.normal.textColor = RowButtonMuted.hover.textColor = TextMuted;

		PrimaryButton = new GUIStyle(skin.button) { fontStyle = FontStyle.Bold };
		SetButton(PrimaryButton, Accent, AccentHover, Accent, AccentText);

		Switch = CreateSwitchStyle();
		CopyStyle(Switch, skin.toggle);

		TextField = CreateTextFieldStyle(Border);
		TextFieldInvalid = CreateTextFieldStyle(Danger);
		CopyStyle(TextField, skin.textField);
		CopyStyle(TextField, skin.textArea);
		skin.textArea.fixedHeight = 0;
		skin.textArea.wordWrap = true;

		ConfigureScrollbars(skin);

		skin.settings.cursorColor = Accent;
		skin.settings.selectionColor = WithAlpha(Accent, 0.35f);

		Sidebar = new GUIStyle { normal = { background = RoundedRect(Panel, Border, 8f, 1f) }, border = Uniform(10), padding = Uniform(6) };
		SidebarText = CreateText(13, TextSecondary);
		SidebarText.fixedHeight = 30f;
		SidebarText.alignment = TextAnchor.MiddleLeft;
		SidebarText.clipping = TextClipping.Clip;
		SidebarTextSelected = new GUIStyle(SidebarText) { fontStyle = FontStyle.Bold };
		SidebarTextSelected.normal.textColor = Text;

		SidebarTextMuted = new GUIStyle(SidebarText);
		SidebarTextMuted.normal.textColor = TextMuted;
		SidebarTextMutedSelected = new GUIStyle(SidebarTextMuted) { fontStyle = FontStyle.Bold };
		SidebarTextMutedSelected.normal.textColor = TextSecondary;

		SidebarHeaderStyle = CreateText(10, TextMuted);
		SidebarHeaderStyle.fontStyle = FontStyle.Bold;
		SidebarHeaderStyle.fixedHeight = 26f;
		SidebarHeaderStyle.alignment = TextAnchor.LowerLeft;
		SidebarHeaderStyle.padding = new RectOffset(14, 8, 0, 6);
		SidebarHeaderStyle.margin = new RectOffset(0, 0, 8, 0);
		SidebarHeaderStyle.clipping = TextClipping.Clip;
		SidebarHeaderStyle.richText = false;

		Badge = CreateText(10, TextMuted);
		Badge.normal.background = RoundedRect(Control, Border, 4f, 1f, 16);
		Badge.border = Uniform(5);
		Badge.padding = new RectOffset(6, 6, 1, 2);
		Badge.alignment = TextAnchor.MiddleCenter;
		Badge.fixedHeight = 17f;

		Card = new GUIStyle { normal = { background = card }, border = Uniform(10), padding = new RectOffset(6, 4, 6, 6) };
		CardContent = new GUIStyle { padding = new RectOffset(10, 8, 0, 0) };

		PageTitle = CreateText(17, Text);
		PageTitle.fontStyle = FontStyle.Bold;
		PageTitle.padding = new RectOffset(14, 0, 3, 3);
		PageTitle.margin = new RectOffset(0, 0, 0, 6);

		Section = CreateText(11, Accent);
		Section.fontStyle = FontStyle.Bold;
		Section.padding = new RectOffset(4, 0, 0, 0);
		Section.margin = new RectOffset(0, 0, 10, 6);

		Description = CreateText(12, TextMuted);
		Description.wordWrap = true;
		Description.padding = new RectOffset(2, 0, 0, 0);
		Description.margin = new RectOffset(0, 0, 0, 12);

		Caption = CreateText(11, TextMuted);
		Caption.wordWrap = true;
		Caption.margin = new RectOffset(2, 0, 10, 0);

		Row = new GUIStyle { normal = { background = BottomLine(Divider) }, border = new RectOffset(0, 0, 0, 1), fixedHeight = RowHeight };

		RowLabel = CreateText(13, TextSecondary);
		RowLabel.fixedHeight = RowHeight;
		RowLabel.fixedWidth = LabelWidth;
		RowLabel.alignment = TextAnchor.MiddleLeft;
		RowLabel.clipping = TextClipping.Clip;

		Swatch = new GUIStyle { normal = { background = RoundedRect(Color.white, Color.white, 4f, 0f, 16) }, border = Uniform(5), fixedHeight = 20f, margin = new RectOffset(4, 0, 6, 6) };
		var swatchFrame = RoundedRect(Color.clear, WithAlpha(Color.white, 0.16f), 4f, 1f, 16);
		var swatchFrameHover = RoundedRect(Color.clear, WithAlpha(Color.white, 0.55f), 4f, 1f, 16);
		SwatchFrame = new GUIStyle { normal = { background = swatchFrame }, onNormal = { background = swatchFrameHover }, border = Uniform(5) };

		KeyCap = CreateText(11, TextMuted);
		KeyCap.normal.background = RoundedRect(Control, Border, 5f, 1f, 16);
		KeyCap.border = Uniform(6);
		KeyCap.padding = new RectOffset(8, 8, 3, 4);
		KeyCap.alignment = TextAnchor.MiddleCenter;

		ListItem = new GUIStyle(skin.label) { padding = new RectOffset(8, 8, 4, 4) };
		ListItem.hover.background = RoundedRect(WithAlpha(Color.white, 0.05f), WithAlpha(Color.white, 0.05f), 4f, 0f, 16);
		ListItem.border = Uniform(5);
		ListItemSelected = new GUIStyle(ListItem) { fontStyle = FontStyle.Bold };
		ListItemSelected.normal.background = RoundedRect(WithAlpha(Accent, 0.16f), WithAlpha(Accent, 0.16f), 4f, 0f, 16);
		ListItemSelected.normal.textColor = Accent;

		_skin = skin;
	}

	private static Font? CreateFont()
	{
		try
		{
			return Font.CreateDynamicFontFromOSFont(FontNames, 13);
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static GUIStyle CreateSwitchStyle()
	{
		const int width = 36, height = 20;

		var style = new GUIStyle
		{
			fixedWidth = width,
			fixedHeight = height,
			margin = new RectOffset(0, 0, (int)(RowHeight - height) / 2, (int)(RowHeight - height) / 2),
			padding = new RectOffset(0, 0, 0, 0),
			border = new RectOffset(0, 0, 0, 0),
		};

		var off = SwitchTexture(width, height, Off, Rgb(0xC9CED6), false);
		var offHover = SwitchTexture(width, height, Rgb(0x59616E), Rgb(0xE3E6EB), false);
		var on = SwitchTexture(width, height, Accent, Color.white, true);
		var onHover = SwitchTexture(width, height, AccentHover, Color.white, true);

		style.normal.background = style.focused.background = off;
		style.hover.background = style.active.background = offHover;
		style.onNormal.background = style.onFocused.background = on;
		style.onHover.background = style.onActive.background = onHover;

		return style;
	}

	private static GUIStyle CreateTextFieldStyle(Color border)
	{
		var style = new GUIStyle
		{
			border = Uniform(6),
			padding = new RectOffset(8, 8, 3, 3),
			margin = new RectOffset(0, 0, 4, 4),
			fixedHeight = 24f,
			fontSize = 12,
			alignment = TextAnchor.MiddleLeft,
			clipping = TextClipping.Clip,
		};

		var normal = RoundedRect(Rgb(0x0F1216), border, 5f, 1f, 16);
		var hover = RoundedRect(Rgb(0x0F1216), border == Border ? Rgb(0x3A424E) : border, 5f, 1f, 16);
		var focused = RoundedRect(Rgb(0x0F1216), border == Border ? Accent : border, 5f, 1f, 16);

		style.normal.background = normal;
		style.hover.background = hover;
		style.focused.background = style.active.background = focused;
		style.onNormal.background = normal;
		style.onFocused.background = focused;
		style.normal.textColor = style.hover.textColor = style.focused.textColor = style.active.textColor = Text;
		style.onNormal.textColor = style.onFocused.textColor = Text;

		return style;
	}

	private static void ConfigureScrollbars(GUISkin skin)
	{
		var thumb = RoundedRect(Rgb(0x3A414C), Rgb(0x3A414C), 3f, 0f, 12);
		var thumbHover = RoundedRect(Rgb(0x505865), Rgb(0x505865), 3f, 0f, 12);

		foreach (var bar in new[] { skin.verticalScrollbar, skin.horizontalScrollbar })
		{
			Clear(bar);
			bar.border = bar.padding = new RectOffset(0, 0, 0, 0);
			bar.fixedWidth = bar.fixedHeight = 6f;
		}

		skin.verticalScrollbar.fixedWidth = 6f;
		skin.verticalScrollbar.margin = new RectOffset(4, 0, 2, 2);
		skin.horizontalScrollbar.fixedHeight = 6f;
		skin.horizontalScrollbar.margin = new RectOffset(2, 2, 4, 0);

		foreach (var bar in new[] { skin.verticalScrollbarThumb, skin.horizontalScrollbarThumb })
		{
			Clear(bar);
			bar.normal.background = bar.focused.background = thumb;
			bar.hover.background = bar.active.background = thumbHover;
			bar.border = Uniform(3);
			bar.padding = bar.margin = new RectOffset(0, 0, 0, 0);
		}

		skin.verticalScrollbarThumb.fixedWidth = 6f;
		skin.horizontalScrollbarThumb.fixedHeight = 6f;

		foreach (var button in new[] { skin.verticalScrollbarUpButton, skin.verticalScrollbarDownButton, skin.horizontalScrollbarLeftButton, skin.horizontalScrollbarRightButton })
		{
			Clear(button);
			button.fixedWidth = button.fixedHeight = 0f;
			button.border = button.padding = button.margin = new RectOffset(0, 0, 0, 0);
		}

		skin.scrollView.normal.background = null;
		skin.scrollView.padding = new RectOffset(0, 0, 0, 0);
	}

	private static GUIStyle CreateText(int size, Color color)
	{
		var style = new GUIStyle { fontSize = size, richText = true, wordWrap = false };
		style.normal.textColor = color;
		return style;
	}

	private static void SetText(GUIStyle style, int size, Color color)
	{
		style.fontSize = size;
		style.normal.textColor = style.onNormal.textColor = color;
		style.hover.textColor = style.onHover.textColor = color;
		style.active.textColor = style.onActive.textColor = color;
		style.focused.textColor = style.onFocused.textColor = color;
	}

	private static void SetButton(GUIStyle style, Color normal, Color hover, Color active, Color text)
	{
		var normalTexture = RoundedRect(normal, Border.a > 0 && normal != Accent ? Rgb(0x323945) : normal, 6f, 1f, 20);
		var hoverTexture = RoundedRect(hover, hover == AccentHover ? hover : Rgb(0x3B4350), 6f, 1f, 20);
		var activeTexture = RoundedRect(active, active, 6f, 0f, 20);

		style.normal.background = style.focused.background = style.onNormal.background = style.onFocused.background = normalTexture;
		style.hover.background = style.onHover.background = hoverTexture;
		style.active.background = style.onActive.background = activeTexture;
		style.border = Uniform(7);
		style.alignment = TextAnchor.MiddleCenter;
		style.clipping = TextClipping.Clip;
		SetText(style, 13, text);
	}

	private static void Set(GUIStyle style, Texture2D background, RectOffset border)
	{
		Clear(style);
		style.normal.background = background;
		style.border = border;
	}

	private static void CopyStyle(GUIStyle source, GUIStyle target)
	{
		target.normal = source.normal;
		target.hover = source.hover;
		target.active = source.active;
		target.focused = source.focused;
		target.onNormal = source.onNormal;
		target.onHover = source.onHover;
		target.onActive = source.onActive;
		target.onFocused = source.onFocused;
		target.border = source.border;
		target.padding = source.padding;
		target.margin = source.margin;
		target.overflow = new RectOffset(0, 0, 0, 0);
		target.fixedWidth = source.fixedWidth;
		target.fixedHeight = source.fixedHeight;
		target.fontSize = source.fontSize;
		target.alignment = source.alignment;
		target.clipping = source.clipping;
		target.contentOffset = Vector2.zero;
		target.imagePosition = ImagePosition.ImageLeft;
	}

	private static void Clear(GUIStyle style)
	{
		foreach (var state in new[] { style.normal, style.hover, style.active, style.focused, style.onNormal, style.onHover, style.onActive, style.onFocused })
			state.background = null;

		style.overflow = new RectOffset(0, 0, 0, 0);
	}

	private static RectOffset Uniform(int value) => new(value, value, value, value);

	public static Color WithAlpha(Color color, float alpha) => new(color.r, color.g, color.b, alpha);

	private static Color Rgb(int rgb, float alpha = 1f)
	{
		return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);
	}

	private static Texture2D NewTexture(int width, int height)
	{
		return new Texture2D(width, height, TextureFormat.RGBA32, false)
		{
			filterMode = FilterMode.Bilinear,
			wrapMode = TextureWrapMode.Clamp,
			hideFlags = HideFlags.HideAndDontSave,
		};
	}

	// Anti-aliased rounded rectangle using a signed distance field; meant to be 9-sliced.
	private static Texture2D RoundedRect(Color fill, Color border, float radius, float borderWidth, int size = 32)
	{
		var texture = NewTexture(size, size);
		var pixels = new Color[size * size];

		for (var y = 0; y < size; y++)
		{
			for (var x = 0; x < size; x++)
			{
				var distance = RoundedRectDistance(x + 0.5f, y + 0.5f, size, size, radius);
				pixels[y * size + x] = Shade(distance, fill, border, borderWidth);
			}
		}

		texture.SetPixels(pixels);
		texture.Apply();
		return texture;
	}

	private static Texture2D SwitchTexture(int width, int height, Color track, Color knob, bool knobRight)
	{
		// Render at 2x and let bilinear filtering downsample for smoother edges.
		const int scale = 2;
		width *= scale;
		height *= scale;

		var texture = NewTexture(width, height);
		var pixels = new Color[width * height];
		var knobRadius = height / 2f - 3f * scale;
		var knobCenter = new Vector2(knobRight ? width - height / 2f : height / 2f, height / 2f);

		for (var y = 0; y < height; y++)
		{
			for (var x = 0; x < width; x++)
			{
				var point = new Vector2(x + 0.5f, y + 0.5f);
				var trackCoverage = Mathf.Clamp01(0.5f - RoundedRectDistance(point.x, point.y, width, height, height / 2f) / scale);
				var knobCoverage = Mathf.Clamp01(0.5f - (Vector2.Distance(point, knobCenter) - knobRadius) / scale);

				var color = Color.Lerp(track, knob, knobCoverage);
				color.a = Mathf.Max(track.a * trackCoverage, knob.a * knobCoverage);
				pixels[y * width + x] = color;
			}
		}

		texture.SetPixels(pixels);
		texture.Apply();
		return texture;
	}

	private static Texture2D BottomLine(Color color)
	{
		var texture = NewTexture(4, 4);
		var pixels = new Color[16];
		for (var i = 0; i < pixels.Length; i++)
			pixels[i] = i < 4 ? color : Color.clear; // row 0 is the bottom of the texture

		texture.SetPixels(pixels);
		texture.Apply();
		return texture;
	}

	private static Color Shade(float distance, Color fill, Color border, float borderWidth)
	{
		var coverage = Mathf.Clamp01(0.5f - distance);
		if (coverage <= 0f)
			return Color.clear;

		var inner = borderWidth <= 0f ? 1f : Mathf.Clamp01(0.5f - (distance + borderWidth));
		var color = Color.Lerp(border, fill, inner);
		color.a *= coverage;
		return color;
	}

	private static float RoundedRectDistance(float x, float y, float width, float height, float radius)
	{
		var qx = Mathf.Abs(x - width / 2f) - (width / 2f - radius);
		var qy = Mathf.Abs(y - height / 2f) - (height / 2f - radius);
		var outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
		return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
	}
}
