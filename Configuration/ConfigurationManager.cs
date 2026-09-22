using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using EFT.Trainer.Features;
using EFT.Trainer.Properties;
using EFT.UI;
using Newtonsoft.Json;

#nullable enable

namespace EFT.Trainer.Configuration;

internal static class ConfigurationManager
{
	public static JsonConverter[] Converters => [new TrackedItemConverter(), new ColorConverter(), new KeyCodeConverter()];
	public static string LastStatus { get; private set; } = string.Empty;
	public static bool LastOperationSucceeded { get; private set; }

	private static bool Report(string message, bool success)
	{
		LastStatus = message;
		LastOperationSucceeded = success;
		AddConsoleLog(success ? message : message.Red());
		return success;
	}

	private static void AddConsoleLog(string log)
	{
		if (PreloaderUI.Instantiated)
			ConsoleScreen.Log(log);
	}

	public static bool Load(string filename, Feature[] features, bool warnIfNotExists = true)
	{
		try
		{
			if (!File.Exists(filename))
			{
				if (warnIfNotExists)
					return Report(string.Format(Strings.ErrorFileNotFoundFormat, filename), false);

				return false;
			}

			var lines = File.ReadAllLines(filename);
			int loaded = 0, failed = 0;

			foreach (var feature in features)
			{
				var featureType = feature.GetType();
				var properties = GetOrderedProperties(featureType);

				foreach (var op in properties)
				{
					var key = $"{featureType.FullName}.{op.Property.Name}=";
					try
					{
						var line = lines.FirstOrDefault(l => l.StartsWith(key, StringComparison.Ordinal));
						if (line == null)
							continue;

						var value = JsonConvert.DeserializeObject(line.Substring(key.Length), op.Property.PropertyType, Converters);
						if (value == null && op.Property.GetValue(feature) != null)
							throw new JsonSerializationException("A configured value cannot be null.");
						op.Property.SetValue(feature, value);
						loaded++;
					}
					catch (Exception)
					{
						// A broken converter or property setter must not block the remaining settings.
						failed++;
						AddConsoleLog(string.Format(Strings.ErrorCorruptedPropertyFormat, key, filename).Red());
					}
				}
			}

			if (failed > 0 || loaded == 0)
				return Report(string.Format(Strings.ResourceManager.GetString("ConfigurationLoadPartialFormat", Strings.Culture)!, loaded, failed, filename), false);
			return Report(string.Format(Strings.CommandLoadSuccessFormat, filename), true);
		}
		catch (Exception ioe)
		{
			return Report(string.Format(Strings.ErrorCannotLoadFormat, filename, ioe.Message), false);
		}
	}

	public static void LoadPropertyValue(string filename, Feature feature, string propertyName)
	{
		try
		{
			if (!File.Exists(filename))
			{
				AddConsoleLog(string.Format(Strings.ErrorFileNotFoundFormat, filename).Red());
				return;
			}

			var text = File.ReadAllText(filename);

			var tlProperty = GetOrderedProperties(feature.GetType())
				.First(p => p.Property.Name == propertyName);

			try
			{
				var value = JsonConvert.DeserializeObject(text, tlProperty.Property.PropertyType, Converters);
				tlProperty.Property.SetValue(feature, value);
			}
			catch (JsonException)
			{
				AddConsoleLog(string.Format(Strings.ErrorCorruptedFileFormat, filename).Red());
			}

			AddConsoleLog(string.Format(Strings.CommandLoadSuccessFormat, filename));
		}
		catch (Exception ioe)
		{
			AddConsoleLog(string.Format(Strings.ErrorCannotLoadFormat, filename, ioe.Message).Red());
		}
	}

	public static bool Save(string filename, Feature[] features)
	{
		try
		{
			var content = new StringBuilder();
			content.AppendLine(Comment(Strings.CommandSaveHeader));
			content.AppendLine();

			foreach (var feature in features.OrderBy(f => f.GetType().FullName))
			{
				var featureType = feature.GetType();
				var properties = GetOrderedProperties(featureType);

				foreach (var op in properties)
				{
					var key = $"{featureType.FullName}.{op.Property.Name}";
					var value = JsonConvert.SerializeObject(op.Property.GetValue(feature), Formatting.None, Converters);

					var resourceId = op.Attribute.CommentResourceId;
					if (!string.IsNullOrEmpty(resourceId))
						content.AppendLine(Comment(Strings.ResourceManager.GetString(resourceId)));

					content.AppendLine($"{key}={value}");
				}

				if (properties.Any())
					content.AppendLine();
			}

			WriteSafely(filename, content.ToString());
			return Report(string.Format(Strings.CommandSaveSuccessFormat, filename), true);
		}
		catch (Exception ioe)
		{
			return Report(string.Format(Strings.ErrorCannotSaveFormat, filename, ioe.Message), false);
		}
	}

	private static void WriteSafely(string filename, string content)
	{
		var fullPath = Path.GetFullPath(filename);
		Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
		var temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
		try
		{
			// Replace only after serialization and writing succeed; retain the previous save.
			File.WriteAllText(temporary, content, new UTF8Encoding(false));
			if (File.Exists(fullPath))
				File.Replace(temporary, fullPath, fullPath + ".bak");
			else
				File.Move(temporary, fullPath);
		}
		finally
		{
			try { if (File.Exists(temporary)) File.Delete(temporary); }
			catch (IOException) { }
			catch (UnauthorizedAccessException) { }
		}
	}

	private static string Comment(string? value)
	{
		if (string.IsNullOrEmpty(value))
			return string.Empty;

		const string commentToken = "; ";

		const string resxNewLine = "\n";
		return commentToken + value!.Replace(resxNewLine, resxNewLine + commentToken);
	}

	public static void SavePropertyValue(string filename, Feature feature, string propertyName)
	{
		try
		{
			var tlProperty = GetOrderedProperties(feature.GetType())
				.First(p => p.Property.Name == propertyName);

			var content = JsonConvert.SerializeObject(tlProperty.Property.GetValue(feature), Formatting.Indented, Converters);
			WriteSafely(filename, content);

			AddConsoleLog(string.Format(Strings.CommandSaveSuccessFormat, filename));
		}
		catch (Exception ioe)
		{
			AddConsoleLog(string.Format(Strings.ErrorCannotSaveFormat, filename, ioe.Message).Red());
		}
	}

	public static bool IsSkippedProperty(Feature feature, string name)
	{
		return IsSkippedProperty(feature.GetType(), name);
	}

	public static bool IsSkippedProperty(Type featureType, string name)
	{
		var property = featureType.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
		if (property == null)
			return false;

		var attribute = property.GetCustomAttribute<ConfigurationPropertyAttribute>(true);
		return attribute is { Skip: true };
	}

	public static OrderedProperty[] GetOrderedProperties(Type featureType)
	{
		var properties = featureType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);

		return
		[.. properties
			.Select(p => new { property = p, attribute = p.GetCustomAttribute<ConfigurationPropertyAttribute>(true) })
			.Where(p => p.attribute is { Skip: false })
			.Select(op => new OrderedProperty(op.attribute, op.property))
			.OrderBy(op => op.Attribute.Order)
			.ThenBy(op => op.Property.Name)
		];
	}
}
