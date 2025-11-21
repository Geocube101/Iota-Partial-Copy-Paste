using RichHudFramework.UI;
using RichHudFramework.UI.Client;
using RichHudFramework.UI.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace Partial_Copy_Paste
{
	internal class CurrentSelectionDisplay : WindowBase
	{
		private sealed class ElementWrapper
		{
			public readonly float RelativeX;
			public readonly float RelativeY;
			public readonly float? RelativeWidth;
			public readonly float? RelativeHeight;
			public readonly TextAlignment Alignment;
			public readonly HudElementBase Element;

			public ElementWrapper(HudElementBase element, float relx, float rely, float? relw, float? relh, TextAlignment alignment)
			{
				this.Element = element;
				this.RelativeX = relx;
				this.RelativeY = rely;
				this.RelativeWidth = relw;
				this.RelativeHeight = relh;
				this.Alignment = alignment;
				this.Element.ParentAlignment = ParentAlignments.Top | ParentAlignments.Left | ParentAlignments.Inner | ParentAlignments.UsePadding;
			}

			public void Update(WindowBase parent)
			{
				float vw = parent.Width / 100;
				float vh = parent.Height / 100;
				float x = vw * this.RelativeX;
				float y = vh * this.RelativeY;
				if (this.RelativeWidth != null) this.Element.Width = this.RelativeWidth.Value * vw;
				if (this.RelativeHeight != null) this.Element.Height = this.RelativeHeight.Value * vh;

				switch (this.Alignment)
				{
					case TextAlignment.Center:
						x -= this.Element.Width / 2;
						break;
					case TextAlignment.Right:
						x -= this.Element.Width;
						break;
				}

				this.Element.Offset = new Vector2(x, -this.Element.Height - y);
			}
		}

		private class MyConfirmPositionButton : LabelBoxButton
		{
			public Action OnClicked;

			public MyConfirmPositionButton(CurrentSelectionDisplay parent) : base(parent)
			{
				this.MouseInput.LeftClicked += this.OnLeftClicked;
				this.Text = "Confirm Position";
				this.Color = TerminalFormatting.OuterSpace;
				this.HighlightColor = TerminalFormatting.Atomic;
				this.Size = new Vector2(parent.VW * 30, parent.VH * 15);
				this.VertCenterText = true;
				this.FitToTextElement = true;
				this.TextPadding = new Vector2(parent.VW * 7.5f, parent.VH * 7.5f);
				this.ParentAlignment = ParentAlignments.Top | ParentAlignments.Left | ParentAlignments.Inner | ParentAlignments.UsePadding;
			}

			protected override void Layout()
			{
				base.Layout();
				CurrentSelectionDisplay parent = this.Parent as CurrentSelectionDisplay;
				if (parent == null) return;
				this.Offset = parent.Size / new Vector2(2, -2) - this.Size / 2;
			}

			public void OnLeftClicked(object sender, EventArgs args)
			{
				this.OnClicked?.Invoke();
			}
		}

		private readonly MyConfirmPositionButton ConfirmPositionButton;
		private readonly MyPartialCopyPasteModSession Session;
		private readonly Label SelectionSizeLabel;
		private readonly Label SelectionVolumeLabel;
		private readonly Label SelectionBlocksLabel;
		private readonly Label BeginSelectionLabel;
		private readonly Label EndSelectionLabel;
		private readonly Label CopySelectionLabel;
		private readonly Label PointExtendSelectionLabel;
		private readonly Label[] SelectionControlLabels = new Label[6];
		private readonly List<ElementWrapper> StandardElements = new List<ElementWrapper>();
		private readonly HashSet<IMySlimBlock> SeparateBlocks = new HashSet<IMySlimBlock>();

		internal readonly float[] ArrowAngles = new float[6];

		public float VW => this.Width / 100f;
		public float VH => this.Height / 100f;

		public CurrentSelectionDisplay(HudParentBase parent, MyPartialCopyPasteModSession session) : base(parent)
		{
			this.BodyColor = new Color(41, 54, 62, 150);
			this.BorderColor = new Color(58, 68, 77);
			this.header.Format = new GlyphFormat(GlyphFormat.Blueish.Color, TextAlignment.Center, 1.08f);
			this.header.Height = 30f;
			this.HeaderText = "[ Partial Copy Paste ]";
			this.Size = new Vector2(500f, 300f);
			this.AllowResizing = true;
			this.CanDrag = true;
			this.Visible = false;
			this.Offset = HudMain.GetPixelVector(session.ModConfiguration.HudWindow.Position);
			this.Size = Vector2.Max(new Vector2(HudMain.ScreenWidth, HudMain.ScreenHeight) * session.ModConfiguration.HudWindow.Dimensions, new Vector2(128, 92));
			this.Session = session;
			
			this.ConfirmPositionButton = new MyConfirmPositionButton(this);
			this.ConfirmPositionButton.OnClicked += this.ConfirmPosition;

			Material arrow = new Material(MyStringId.GetOrCompute("Arrow"), new Vector2(128));

			for (byte i = 0; i < 6; ++i)
			{
				byte index = i;
				Label label = this.PrepareElement(new Label(this), (i % 2 == 0) ? 10 : 60, ((i >> 1) * 10) + 10);
				CustomSpaceNode rotation = new CustomSpaceNode(this);
				TexturedBox arrow_sprite = new TexturedBox(rotation);
				arrow_sprite.MatAlignment = MaterialAlignment.FitAuto;
				arrow_sprite.Material = arrow;
				arrow_sprite.Color = session.Directions[i].Color;
				rotation.UpdateMatrixFunc = () => this.UpdateArrorSpriteRotation(index, rotation, arrow_sprite, (index % 2 == 0) ? 5 : 55, ((index >> 1) * 10) + 10, 10, 10);
				this.SelectionControlLabels[i] = label;
			}

			this.BeginSelectionLabel = this.PrepareElement(new Label(this), 10, 40);
			this.EndSelectionLabel = this.PrepareElement(new Label(this), 60, 40);
			this.CopySelectionLabel = this.PrepareElement(new Label(this), 10, 50);
			this.PointExtendSelectionLabel = this.PrepareElement(new Label(this), 60, 50);

			this.SelectionSizeLabel = this.PrepareElement(new Label(this), 35, 60);
			this.SelectionVolumeLabel = this.PrepareElement(new Label(this), 35, 70);
			this.SelectionBlocksLabel = this.PrepareElement(new Label(this), 35, 80);

			this.Session.OnSelectionSizeChanged += this.SelectionSizeChanged;
		}

		private void SelectionSizeChanged()
		{
			this.SeparateBlocks.Clear();
		}

		private T PrepareElement<T>(T element, float relx, float rely, float? relw = null, float? relh = null, TextAlignment alignment = TextAlignment.Left) where T : HudElementBase
		{
			if (element == null) return null;
			this.StandardElements.Add(new ElementWrapper(element, relx, rely, relw, relh, alignment));
			return element;
		}

		private void ConfirmPosition()
		{
			if (!this.ConfirmPositionButton.Visible) return;
			this.Hide();
			this.Session.ModConfiguration.HudWindow.Position = HudMain.GetAbsoluteVector(this.Offset);
			this.Session.ModConfiguration.HudWindow.Dimensions = this.Size / new Vector2(HudMain.ScreenWidth, HudMain.ScreenHeight);
			MyLog.Default.WriteLineAndConsole($"SIZE={this.Size}, POS={this.Position}");
		}

		private MatrixD UpdateArrorSpriteRotation(byte index, CustomSpaceNode space, TexturedBox element, float relx, float rely, float? relw = null, float? relh = null)
		{
			float vw = this.Width / 100;
			float vh = this.Height / 100;
			float x = vw * relx;
			float y = vh * rely;
			if (relw != null) element.Width = relw.Value * vw;
			if (relw != null) element.Height = relh.Value * vh;
			Vector2 offset = new Vector2(x, -element.Height - y);
			MatrixD rotated = MatrixD.CreateRotationZ(this.ArrowAngles[index] + Math.PI) * this.HudSpace.PlaneToWorld;
			rotated.Translation = Vector3D.Transform(new Vector3D(this.Position - this.Size / new Vector2(2, -2) + offset, 0), this.HudSpace.PlaneToWorld);
			return rotated;
		}

		protected override void Layout()
		{
			base.Layout();
			if (this.ConfirmPositionButton.Visible) return;
			foreach (ElementWrapper element in this.StandardElements) element.Update(this);
			string method = (this.Session.IsContracting) ? "Retract" : "Extend";
			BindDefinition[] definitions = this.Session.GetKeybindDefinitions();
			BindDefinition begin_selection_definition = definitions.FirstOrDefault((keybind) => keybind.name == this.Session.ModConfiguration.Keybinds.BeginSelection.name);
			BindDefinition end_selection_definition = definitions.FirstOrDefault((keybind) => keybind.name == this.Session.ModConfiguration.Keybinds.EndSelection.name);
			BindDefinition copy_selection_definition = definitions.FirstOrDefault((keybind) => keybind.name == this.Session.ModConfiguration.Keybinds.CopySelection.name);
			BindDefinition extend_selection_definition = definitions.FirstOrDefault((keybind) => keybind.name == this.Session.ModConfiguration.Keybinds.ExtendPointSelection.name);
			float font_scale = Math.Min(this.Width / 618.25f, this.Height / 268.75f);

			if (begin_selection_definition.name != null && begin_selection_definition.controlNames != null)
			{
				this.BeginSelectionLabel.Text = $"'{string.Join("+", begin_selection_definition.controlNames)}' - Begin Selection";
				this.BeginSelectionLabel.TextBoard.Scale = font_scale;
			}

			if (end_selection_definition.name != null && end_selection_definition.controlNames != null)
			{
				this.EndSelectionLabel.Text = $"'{string.Join("+", end_selection_definition.controlNames)}' - End Selection";
				this.EndSelectionLabel.TextBoard.Scale = font_scale;
			}

			if (copy_selection_definition.name != null && copy_selection_definition.controlNames != null)
			{
				this.CopySelectionLabel.Text = $"'{string.Join("+", copy_selection_definition.controlNames)}' - Copy Selection";
				this.CopySelectionLabel.TextBoard.Scale = font_scale;
			}

			if (extend_selection_definition.name != null && extend_selection_definition.controlNames != null)
			{
				this.PointExtendSelectionLabel.Text = $"'{string.Join("+", extend_selection_definition.controlNames)}' - Extend Selection";
				this.PointExtendSelectionLabel.TextBoard.Scale = font_scale;
			}

			for (byte i = 0; i < 6; ++i)
			{
				MyPartialCopyPasteModSession.MyDirectionInfo direction = this.Session.Directions[i];
				Label label = this.SelectionControlLabels[i];
				BindDefinition definition = (this.Session.IsContracting) ? direction.KeybindContract : direction.KeybindExtend;
				string controls = string.Join("+", definition.controlNames);
				label.TextBoard.Scale = font_scale;

				label.Text = new RichText() {
					{ "'", null },
					{ controls, new GlyphFormat(direction.Color) },
					{ $"' - {method} {direction.Name}", null },
				};
			}

			BoundingBoxI bounds = this.Session.SelectionBounds;
			Vector3D diagonal = bounds.Max - bounds.Min;
			int x = (int) Math.Abs(diagonal.X) + 1;
			int y = (int) Math.Abs(diagonal.Y) + 1;
			int z = (int) Math.Abs(diagonal.Z) + 1;
			int volume = x * y * z;

			for (int i = bounds.Min.X; i <= bounds.Max.X; i++)
			{
				for (int j = bounds.Min.Y; j <= bounds.Max.Y; j++)
				{
					for (int k = bounds.Min.Z; k <= bounds.Max.Z; k++)
					{
						Vector3I pos = new Vector3I(i, j, k);
						IMySlimBlock block = this.Session.TargetedGrid.GetCubeBlock(pos);
						if (block == null || this.SeparateBlocks.Contains(block)) continue;
						this.SeparateBlocks.Add(block);
					}
				}
			}

			this.SelectionSizeLabel.Text = $"Selection Size: {x}x{y}x{z}";
			this.SelectionVolumeLabel.Text = $"Selection Volume: {volume} Block{((volume == 1) ? "" : "s")}";
			this.SelectionBlocksLabel.Text = $"Individual Blocks: {this.SeparateBlocks.Count}";

			this.SelectionSizeLabel.TextBoard.Scale = font_scale;
			this.SelectionVolumeLabel.TextBoard.Scale = font_scale;
			this.SelectionBlocksLabel.TextBoard.Scale = font_scale;
		}

		public void Hide()
		{
			if (!this.Visible) return;
			this.Visible = false;
			if (this.ConfirmPositionButton.Visible) HudMain.EnableCursor = false;
			this.ConfirmPositionButton.Visible = false;
			this.SeparateBlocks.Clear();
		}

		public void Show(bool draggable = false)
		{
			if (this.Visible) return;
			HudMain.EnableCursor = draggable;
			this.ConfirmPositionButton.Visible = draggable;
			//this.AllowResizing = draggable;
			foreach (ElementWrapper element in this.StandardElements) element.Element.Visible = !draggable;
			this.Visible = true;
		}
	}
}
