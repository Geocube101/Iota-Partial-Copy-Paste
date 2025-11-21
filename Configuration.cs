using ProtoBuf;
using RichHudFramework.UI;
using RichHudFramework.UI.Client;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using VRage.Input;
using VRage.Utils;
using VRageMath;

namespace Partial_Copy_Paste
{
	[XmlRoot, XmlType(TypeName = "BuildVisionSettings")]
	public sealed class Configuration
	{
		[ProtoContract]
		public sealed class MyKeybindsConfig
		{
			[ProtoMember(1)]
			public BindDefinition BeginSelection;
			[ProtoMember(2)]
			public BindDefinition EndSelection;
			[ProtoMember(3)]
			public BindDefinition ExtendPointSelection;
			[ProtoMember(4)]
			public BindDefinition CopySelection;
			[ProtoMember(5)]
			public BindDefinition ExtendSelectionForward;
			[ProtoMember(6)]
			public BindDefinition RetractSelectionForward;
			[ProtoMember(7)]
			public BindDefinition ExtendSelectionBackward;
			[ProtoMember(8)]
			public BindDefinition RetractSelectionBackward;
			[ProtoMember(9)]
			public BindDefinition ExtendSelectionUp;
			[ProtoMember(10)]
			public BindDefinition RetractSelectionUp;
			[ProtoMember(11)]
			public BindDefinition ExtendSelectionDown;
			[ProtoMember(12)]
			public BindDefinition RetractSelectionDown;
			[ProtoMember(13)]
			public BindDefinition ExtendSelectionLeft;
			[ProtoMember(14)]
			public BindDefinition RetractSelectionLeft;
			[ProtoMember(15)]
			public BindDefinition ExtendSelectionRight;
			[ProtoMember(16)]
			public BindDefinition RetractSelectionRight;

			private static string[] NamesFor(params ControlData[] controls)
			{
				string[] names = new string[controls.Length];
				for (int i = 0; i < controls.Length; ++i) names[i] = BindManager.Controls[controls[i]].Name;
				return names;
			}

			public static BindDefinition FromBind(IBind bind)
			{
				return new BindDefinition(bind.Name, bind.GetCombo().Select((control) => control.Name).ToArray());
			}

			public static MyKeybindsConfig Defaults()
			{
				return new MyKeybindsConfig()
				{
					BeginSelection = new BindDefinition("Begin Selection", MyKeybindsConfig.NamesFor(MyKeys.Shift, MyKeys.LeftButton)),
					EndSelection = new BindDefinition("End Selection", MyKeybindsConfig.NamesFor(MyKeys.Shift, MyKeys.RightButton)),
					ExtendPointSelection = new BindDefinition("Extend Selection", MyKeybindsConfig.NamesFor(MyKeys.Shift, MyKeys.MiddleButton)),
					CopySelection = new BindDefinition("Copy Selection", MyKeybindsConfig.NamesFor(MyKeys.MiddleButton)),
					ExtendSelectionForward = new BindDefinition("Extend Selection Forward", MyKeybindsConfig.NamesFor(MyKeys.NumPad9)),
					RetractSelectionForward = new BindDefinition("Retract Selection Forward", MyKeybindsConfig.NamesFor(MyKeys.PageUp)),
					ExtendSelectionBackward = new BindDefinition("Extend Selection Backward", MyKeybindsConfig.NamesFor(MyKeys.NumPad3)),
					RetractSelectionBackward = new BindDefinition("Retract Selection Backward", MyKeybindsConfig.NamesFor(MyKeys.PageDown)),
					ExtendSelectionLeft = new BindDefinition("Extend Selection Left", MyKeybindsConfig.NamesFor(MyKeys.NumPad4)),
					RetractSelectionLeft = new BindDefinition("Retract Selection Left", MyKeybindsConfig.NamesFor(MyKeys.Left)),
					ExtendSelectionRight = new BindDefinition("Extend Selection Right", MyKeybindsConfig.NamesFor(MyKeys.NumPad6)),
					RetractSelectionRight = new BindDefinition("Retract Selection Right", MyKeybindsConfig.NamesFor(MyKeys.Right)),
					ExtendSelectionUp = new BindDefinition("Extend Selection Up", MyKeybindsConfig.NamesFor(MyKeys.NumPad8)),
					RetractSelectionUp = new BindDefinition("Retract Selection Up", MyKeybindsConfig.NamesFor(MyKeys.Up)),
					ExtendSelectionDown = new BindDefinition("Extend Selection Down", MyKeybindsConfig.NamesFor(MyKeys.NumPad2)),
					RetractSelectionDown = new BindDefinition("Retract Selection Down", MyKeybindsConfig.NamesFor(MyKeys.Down)),
				};
			}
		}

		[ProtoContract]
		public sealed class MyHUDWindowConfig
		{
			[ProtoMember(1)]
			public Vector2 Position;
			[ProtoMember(2)]
			public Vector2 Dimensions;

			public static MyHUDWindowConfig Defaults()
			{
				return new MyHUDWindowConfig {
					Position = new Vector2(0.2618286f, 0.29296875f),
					Dimensions = new Vector2(0.205834955f, 0.1390625f),
				};
			}
		}

		[ProtoMember(1)]
		public MyKeybindsConfig Keybinds;
		[ProtoMember(2)]
		public MyHUDWindowConfig HudWindow;

		public static Configuration Defaults()
		{
			return new Configuration() {
				Keybinds = MyKeybindsConfig.Defaults(),
				HudWindow = MyHUDWindowConfig.Defaults(),
			};
		}

		public static Configuration TryLoadOrDefaults(string path)
		{
			if (!MyAPIGateway.Utilities.FileExistsInGlobalStorage(path)) return Configuration.Defaults();

			try
			{
				TextReader reader = MyAPIGateway.Utilities.ReadFileInGlobalStorage(path);
				string content;
				if (reader == null || (content = reader.ReadToEnd()).Length == 0) return Configuration.Defaults();
				return MyAPIGateway.Utilities.SerializeFromXML<Configuration>(content);
			}
			catch
			{
				return Configuration.Defaults();
			}
		}

		public void Save(string path)
		{
			try
			{
				TextWriter writer = MyAPIGateway.Utilities.WriteFileInGlobalStorage(path);
				if (writer == null) throw new Exception($"Failed to acquire file writer");
				string content = MyAPIGateway.Utilities.SerializeToXML(this);
				if (content.Length == 0) throw new InvalidOperationException("Resulting config data was empty");
				writer.Write(content);
				writer.Flush();
				MyLog.Default.WriteLineAndConsole("[Partial Copy Paste]: Mod config saved");
			}
			catch (Exception e)
			{
				MyLog.Default.WriteLineAndConsole($"[Partial Copy Paste]: Failed to save mod-config - {e.Message}");
			}
		}
	}
}
