using Draygo.API;
using RichHudFramework.Client;
using RichHudFramework.Internal;
using RichHudFramework.UI;
using RichHudFramework.UI.Client;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace Partial_Copy_Paste
{
	[MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
	internal class MyPartialCopyPasteModSession : ModBase
	{
		public sealed class MyDirectionInfo
		{
			private readonly MyPartialCopyPasteModSession Session;

			public readonly Color Color;
			public readonly IBind BindingExtend;
			public readonly IBind BindingContract;
			public readonly string Name;
			public BindDefinition KeybindExtend => this.Session.GetKeybindDefinitions().First((bind) => bind.name == this.BindingExtend.Name);
			public BindDefinition KeybindContract => this.Session.GetKeybindDefinitions().First((bind) => bind.name == this.BindingContract.Name);

			public MyDirectionInfo(string name, Color color, IBind extend_binding, IBind contract_binding, MyPartialCopyPasteModSession session)
			{
				this.Color = color;
				this.BindingExtend = extend_binding;
				this.BindingContract = contract_binding;
				this.Name = name;
				this.Session = session;
			}
		}

		#region Private Variables
		/// <summary>
		/// Frame counter
		/// </summary>
		private ulong Counter = 0;

		/// <summary>
		/// The selection's minimum grid position
		/// </summary>
		private Vector3I MinPoint;

		/// <summary>
		/// The selection's maximum grid position
		/// </summary>
		private Vector3I MaxPoint;

		/// <summary>
		/// The selection's origin grid position
		/// </summary>
		private Vector3I Center;

		/// <summary>
		/// The mod configuration file name
		/// </summary>
		private string ModConfigFileName = "PartialCopyPaste.cfg";

		/// <summary>
		/// The RHM keybind group
		/// </summary>
		private IBindGroup Keybinds;

		/// <summary>
		/// The TextHudAPI client
		/// </summary>
		private HudAPIv2 TextHudAPI;

		/// <summary>
		/// The HUD info displaying controls and selection information
		/// </summary>
		private CurrentSelectionDisplay HudInfo;

		/// <summary>
		/// Floating keybind messages
		/// </summary>
		private HudAPIv2.SpaceMessage[] KeybindMessages = new HudAPIv2.SpaceMessage[6];
		#endregion

		#region RHM Keybinds
		public IBind BindingBeginSelection { get; private set; }
		public IBind BindingEndSelection { get; private set; }
		public IBind BindingExtendPointSelection { get; private set; }
		public IBind BindingCopySelection { get; private set; }
		public IBind BindingExtendSelectionForward { get; private set; }
		public IBind BindingRetractSelectionForward { get; private set; }
		public IBind BindingExtendSelectionBackward { get; private set; }
		public IBind BindingRetractSelectionBackward { get; private set; }
		public IBind BindingExtendSelectionUp { get; private set; }
		public IBind BindingRetractSelectionUp { get; private set; }
		public IBind BindingExtendSelectionDown { get; private set; }
		public IBind BindingRetractSelectionDown { get; private set; }
		public IBind BindingExtendSelectionLeft { get; private set; }
		public IBind BindingRetractSelectionLeft { get; private set; }
		public IBind BindingExtendSelectionRight { get; private set; }
		public IBind BindingRetractSelectionRight { get; private set; }
		#endregion

		#region Public Variables
		/// <summary>
		/// Whether RichHudMasterAPI is connected
		/// </summary>
		public bool RichHudAPIActive { get; private set; } = false;

		/// <summary>
		/// Whether TextHudAPI is connected
		/// </summary>
		public bool TextHudAPIActive { get; private set; } = false;

		/// <summary>
		/// Whether the selection is being contracted
		/// </summary>
		public bool IsContracting => this.TargetedGrid != null && MyAPIGateway.Input.IsAnyShiftKeyPressed()
			|| (this.BindingRetractSelectionForward?.IsPressed ?? false)
			|| (this.BindingRetractSelectionBackward?.IsPressed ?? false)
			|| (this.BindingRetractSelectionLeft?.IsPressed ?? false)
			|| (this.BindingRetractSelectionRight?.IsPressed ?? false)
			|| (this.BindingRetractSelectionUp?.IsPressed ?? false)
			|| (this.BindingRetractSelectionDown?.IsPressed ?? false);

		/// <summary>
		/// Whether selection can be modified via keybinds
		/// </summary>
		public bool CanModifySelection => this.TargetedGrid != null && !MyAPIGateway.CubeBuilder.IsActivated;

		/// <summary>
		/// The current selection bounds<br />
		/// Do not use if no selection active
		/// </summary>
		public BoundingBoxI SelectionBounds => new BoundingBoxI(this.MinPoint, this.MaxPoint);

		/// <summary>
		/// The currently selected grid
		/// </summary>
		public IMyCubeGrid TargetedGrid { get; private set; }

		/// <summary>
		/// The mod configuration
		/// </summary>
		public Configuration ModConfiguration { get; private set; }

		/// <summary>
		/// List of direction names and colors
		/// </summary>
		public MyDirectionInfo[] Directions;

		/// <summary>
		/// Event for when selection size changed
		/// </summary>
		public event Action OnSelectionSizeChanged;
		#endregion

		#region Public Static Methods
		/// <summary>
		/// Converts a world direction vector to a local direction vector
		/// </summary>
		public static void WorldVectorToLocalVectorD(ref MatrixD world_matrix, ref Vector3D world_direction, out Vector3D local_direction)
		{
			MatrixD transposed;
			MatrixD.Transpose(ref world_matrix, out transposed);
			Vector3D.TransformNormal(ref world_direction, ref transposed, out local_direction);
		}

		/// <summary>
		/// Converts a local direction vector to a world direction vector
		/// </summary>
		public static void LocalVectorToWorldVectorD(ref MatrixD world_matrix, ref Vector3D local_direction, out Vector3D world_direction)
		{
			Vector3D.TransformNormal(ref local_direction, ref world_matrix, out world_direction);
		}

		/// <summary>
		/// Converts a world position vector to a local position vector
		/// </summary>
		public static void WorldVectorToLocalVectorP(ref MatrixD world_matrix, ref Vector3D world_pos, out Vector3D local_pos)
		{
			MatrixD transposed;
			MatrixD.Transpose(ref world_matrix, out transposed);
			Vector3D dir = world_pos - world_matrix.Translation;
			Vector3D.TransformNormal(ref dir, ref transposed, out local_pos);
		}

		/// <summary>
		/// Converts a local position vector to a world position vector
		/// </summary>
		public static void LocalVectorToWorldVectorP(ref MatrixD world_matrix, ref Vector3D local_pos, out Vector3D world_pos)
		{
			Vector3D.Transform(ref local_pos, ref world_matrix, out world_pos);
		}
		#endregion

		public MyPartialCopyPasteModSession() : base(false, true) { }

		#region Private Methods
		/// <summary>
		/// Starts a new selection if targeting a grid
		/// </summary>
		private void BeginSelection()
		{
			this.HudInfo?.Hide();
			foreach (HudAPIv2.SpaceMessage msg in this.KeybindMessages) if (msg != null) msg.Visible = false;
			Vector3D start = MyAPIGateway.Session.Camera.Position;
			Vector3D end = start + MyAPIGateway.Session.Camera.WorldMatrix.Forward * 25;
			IHitInfo hit_info;
			MyAPIGateway.Physics.CastRay(start, end, out hit_info);
			MyCubeGrid target;

			if (hit_info == null || (target = hit_info.HitEntity as MyCubeGrid) == null || target.IsPreview || target.MarkedForClose || target.Physics == null || !target.Physics.Enabled)
			{
				this.TargetedGrid = null;
				return;
			}

			Vector3I center = target.WorldToGridInteger(hit_info.Position);
			IMySlimBlock hit_block = target.GetCubeBlock(center);

			if (hit_block == null)
			{
				Vector3I? block_pos = target.RayCastBlocks(hit_info.Position, end);
				if (block_pos == null) return;
				center = block_pos.Value;
				hit_block = target.GetCubeBlock(center);
			}

			this.Center = center;
			this.TargetedGrid = target;
			this.Counter = 0;
			this.MinPoint = hit_block.Min;
			this.MaxPoint = hit_block.Max;
			this.HudInfo?.Show();
		}

		/// <summary>
		/// Copies the selection into a new blueprint
		/// </summary>
		private void CopySelection()
		{
			long builder = MyAPIGateway.Session.Player.Identity.IdentityId;
			MyCubeGrid source = (MyCubeGrid) this.TargetedGrid;
			MyObjectBuilder_CubeGrid buffer_builder = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_CubeGrid>();
			buffer_builder.PositionAndOrientation = new VRage.MyPositionAndOrientation(Vector3D.Zero, Vector3D.Forward, Vector3D.Up);
			buffer_builder.GridSizeEnum = source.GridSizeEnum;
			buffer_builder.IsStatic = source.IsStatic;
			buffer_builder.CreatePhysics = false;
			buffer_builder.PersistentFlags = MyPersistentEntityFlags2.Enabled | MyPersistentEntityFlags2.InScene;
			HashSet<IMySlimBlock> scanned_blocks = new HashSet<IMySlimBlock>();

			for (int x = this.MinPoint.X; x <= this.MaxPoint.X; x++)
			{
				for (int y = this.MinPoint.Y; y <= this.MaxPoint.Y; y++)
				{
					for (int z = this.MinPoint.Z; z <= this.MaxPoint.Z; z++)
					{
						Vector3I pos = new Vector3I(x, y, z);
						IMySlimBlock block = this.TargetedGrid.GetCubeBlock(pos);
						if (block == null || scanned_blocks.Contains(block)) continue;
						scanned_blocks.Add(block);
						MyObjectBuilder_CubeBlock object_builder = block.GetObjectBuilder(true);
						object_builder.EntityId = 0;
						object_builder.Min = block.Min - this.Center;
						object_builder.Owner = builder;
						buffer_builder.CubeBlocks.Add(object_builder);
					}
				}
			}

			MyAPIGateway.Entities.CreateFromObjectBuilderParallel(buffer_builder, true, (entity) => {
				MyCubeGrid buffer = entity as MyCubeGrid;
				buffer.DisplayName = $"Partial Copy Paste - Copy of {this.TargetedGrid.CustomName}";
				buffer.IsPreview = true;
				buffer.NeedsUpdate = VRage.ModAPI.MyEntityUpdateEnum.NONE;
				buffer.DestructibleBlocks = false;
				buffer.Editable = true;
				buffer.Flags = VRage.ModAPI.EntityFlags.Visible | VRage.ModAPI.EntityFlags.InvalidateOnMove | VRage.ModAPI.EntityFlags.Transparent | VRage.ModAPI.EntityFlags.IsNotGamePrunningStructureObject | VRage.ModAPI.EntityFlags.UpdateRender;
				buffer.GridSizeEnum = source.GridSizeEnum;
				buffer.Immune = true;
				buffer.InitFromClipboard = true;
				buffer.IsRespawnGrid = false;
				if (buffer.Physics != null) buffer.Physics.Enabled = false;
				MyVisualScriptLogicProvider.CreateLocalBlueprint(buffer.Name, buffer.DisplayName);
				MyLog.Default.WriteLineAndConsole($"[PCP]: Copied selection from grid '{this.TargetedGrid.DisplayName}' to blueprint '{buffer.DisplayName}'");
				MyAPIGateway.Utilities.ShowNotification($"Saved blueprint as '{buffer.DisplayName}'");
				buffer.Close();
			});
		}

		/// <summary>
		/// Ends selection without starting new one
		/// </summary>
		private void EndSelection()
		{
			this.HudInfo?.Hide();
			this.TargetedGrid = null;
			foreach (HudAPIv2.SpaceMessage msg in this.KeybindMessages) if (msg != null) msg.Visible = false;
		}

		/// <summary>
		/// Extends the selection bounds
		/// </summary>
		/// <param name="direction">The world direction to extend</param>
		/// <param name="amount">The amount of grid positions to extend by (negative to contract)</param>
		private void ExtendSelection(Vector3D direction, int amount)
		{
			if (this.TargetedGrid == null || this.TargetedGrid.MarkedForClose) return;
			MatrixD world_matrix = this.TargetedGrid.WorldMatrix;
			MyPartialCopyPasteModSession.WorldVectorToLocalVectorD(ref world_matrix, ref direction, out direction);
			Base6Directions.Direction axis = Base6Directions.GetClosestDirection(direction);
			
			switch (axis)
			{
				case Base6Directions.Direction.Forward:
					this.MinPoint.Z = MathHelper.Clamp(this.MinPoint.Z - amount, this.TargetedGrid.Min.Z, this.Center.Z);
					break;
				case Base6Directions.Direction.Backward:
					this.MaxPoint.Z = MathHelper.Clamp(this.MaxPoint.Z + amount, this.Center.Z, this.TargetedGrid.Max.Z);
					break;
				case Base6Directions.Direction.Up:
					this.MaxPoint.Y = MathHelper.Clamp(this.MaxPoint.Y + amount, this.Center.Y, this.TargetedGrid.Max.Y);
					break;
				case Base6Directions.Direction.Down:
					this.MinPoint.Y = MathHelper.Clamp(this.MinPoint.Y - amount, this.TargetedGrid.Min.Y, this.Center.Y);
					break;
				case Base6Directions.Direction.Left:
					this.MinPoint.X = MathHelper.Clamp(this.MinPoint.X - amount, this.TargetedGrid.Min.X, this.Center.X);
					break;
				case Base6Directions.Direction.Right:
					this.MaxPoint.X = MathHelper.Clamp(this.MaxPoint.X + amount, this.Center.X, this.TargetedGrid.Max.X);
					break;
			}

			this.OnSelectionSizeChanged?.Invoke();
		}

		/// <summary>
		/// Extends selection to include the specified point
		/// </summary>
		private void ExtendPointSelection()
		{
			if (this.TargetedGrid == null) return;
			Vector3D start = MyAPIGateway.Session.Camera.Position;
			Vector3D end = start + MyAPIGateway.Session.Camera.WorldMatrix.Forward * 25;
			IHitInfo hit_info;
			MyAPIGateway.Physics.CastRay(start, end, out hit_info);
			if (hit_info == null || hit_info.HitEntity as MyCubeGrid != this.TargetedGrid) return;
			Vector3I center = this.TargetedGrid.WorldToGridInteger(hit_info.Position);
			IMySlimBlock hit_block = this.TargetedGrid.GetCubeBlock(center);

			if (hit_block == null)
			{
				Vector3I? block_pos = this.TargetedGrid.RayCastBlocks(hit_info.Position, end);
				if (block_pos == null) return;
				center = block_pos.Value;
				hit_block = this.TargetedGrid.GetCubeBlock(center);
			}

			BoundingBoxI bounds = new BoundingBoxI(this.MinPoint, this.MaxPoint).Include(ref center);
			this.MinPoint = bounds.Min;
			this.MaxPoint = bounds.Max;
			this.OnSelectionSizeChanged?.Invoke();
		}

		/// <summary>
		/// Callback when RichHudMaster fully loaded
		/// </summary>
		private void OnRichHudAPILoaded()
		{
			this.RichHudAPIActive = true;
			RichHudTerminal.Root.Enabled = true;
			this.ModConfiguration = Configuration.TryLoadOrDefaults(this.ModConfigFileName);
			this.Keybinds = BindManager.GetOrCreateGroup("Keybinds");

			Configuration.MyKeybindsConfig keybinds_config = this.ModConfiguration.Keybinds;
			this.BindingBeginSelection = this.Keybinds.AddBind(keybinds_config.BeginSelection.name, keybinds_config.BeginSelection.controlNames);
			this.BindingEndSelection = this.Keybinds.AddBind(keybinds_config.EndSelection.name, keybinds_config.EndSelection.controlNames);
			this.BindingExtendPointSelection = this.Keybinds.AddBind(keybinds_config.ExtendPointSelection.name, keybinds_config.ExtendPointSelection.controlNames);
			this.BindingCopySelection = this.Keybinds.AddBind(keybinds_config.CopySelection.name, keybinds_config.CopySelection.controlNames);
			this.BindingExtendSelectionForward = this.Keybinds.AddBind(keybinds_config.ExtendSelectionForward.name, keybinds_config.ExtendSelectionForward.controlNames);
			this.BindingRetractSelectionForward = this.Keybinds.AddBind(keybinds_config.RetractSelectionForward.name, keybinds_config.RetractSelectionForward.controlNames);
			this.BindingExtendSelectionBackward = this.Keybinds.AddBind(keybinds_config.ExtendSelectionBackward.name, keybinds_config.ExtendSelectionBackward.controlNames);
			this.BindingRetractSelectionBackward = this.Keybinds.AddBind(keybinds_config.RetractSelectionBackward.name, keybinds_config.RetractSelectionBackward.controlNames);
			this.BindingExtendSelectionLeft = this.Keybinds.AddBind(keybinds_config.ExtendSelectionLeft.name, keybinds_config.ExtendSelectionLeft.controlNames);
			this.BindingRetractSelectionLeft = this.Keybinds.AddBind(keybinds_config.RetractSelectionLeft.name, keybinds_config.RetractSelectionLeft.controlNames);
			this.BindingExtendSelectionRight = this.Keybinds.AddBind(keybinds_config.ExtendSelectionRight.name, keybinds_config.ExtendSelectionRight.controlNames);
			this.BindingRetractSelectionRight = this.Keybinds.AddBind(keybinds_config.RetractSelectionRight.name, keybinds_config.RetractSelectionRight.controlNames);
			this.BindingExtendSelectionUp = this.Keybinds.AddBind(keybinds_config.ExtendSelectionUp.name, keybinds_config.ExtendSelectionUp.controlNames);
			this.BindingRetractSelectionUp = this.Keybinds.AddBind(keybinds_config.RetractSelectionUp.name, keybinds_config.RetractSelectionUp.controlNames);
			this.BindingExtendSelectionDown = this.Keybinds.AddBind(keybinds_config.ExtendSelectionDown.name, keybinds_config.ExtendSelectionDown.controlNames);
			this.BindingRetractSelectionDown = this.Keybinds.AddBind(keybinds_config.RetractSelectionDown.name, keybinds_config.RetractSelectionDown.controlNames);

			this.BindingBeginSelection.Released += this.OnBeginSelection;
			this.BindingEndSelection.Released += this.OnEndSelection;
			this.BindingExtendPointSelection.Released += this.OnExtendPointSelection;
			this.BindingCopySelection.Released += this.OnCopySelection;
			this.BindingExtendSelectionForward.Released += this.OnExtendSelectionForward;
			this.BindingRetractSelectionForward.Released += this.OnRetractSelectionForward;
			this.BindingExtendSelectionBackward.Released += this.OnExtendSelectionBackward;
			this.BindingRetractSelectionBackward.Released += this.OnRetractSelectionBackward;
			this.BindingExtendSelectionLeft.Released += this.OnExtendSelectionLeft;
			this.BindingRetractSelectionLeft.Released += this.OnRetractSelectionLeft;
			this.BindingExtendSelectionRight.Released += this.OnExtendSelectionRight;
			this.BindingRetractSelectionRight.Released += this.OnRetractSelectionRight;
			this.BindingExtendSelectionUp.Released += this.OnExtendSelectionUp;
			this.BindingRetractSelectionUp.Released += this.OnRetractSelectionUp;
			this.BindingExtendSelectionDown.Released += this.OnExtendSelectionDown;
			this.BindingRetractSelectionDown.Released += this.OnRetractSelectionDown;

			RebindPage page_keybinds = new RebindPage()
			{
				Name = "Keybinds",
				GroupContainer = {
					{ this.Keybinds },
				}
			};

			ControlPage page_move_window = new ControlPage()
			{
				Name = "General Settings",
				CategoryContainer = {
					new ControlCategory() {
						HeaderText = "Selection Info",
						SubheaderText = "Change Selection Window Position",
						TileContainer = {
							new ControlTile() {
								new TerminalButton {
									Name = "Change Window Position",
									ControlChangedHandler = (sender, args) => {
										RichHudTerminal.CloseMenu();
										this.HudInfo.Show(true);
									}
								},
							},
						}
					},
				},
			};

			RichHudTerminal.Root.AddRange(new IModRootMember[] {
				page_keybinds,
				page_move_window,
			});

			this.Directions = new MyDirectionInfo[6] {
				new MyDirectionInfo("Forward", Color.Red, this.BindingExtendSelectionForward, this.BindingRetractSelectionForward, this),
				new MyDirectionInfo("Backward", Color.Magenta, this.BindingExtendSelectionBackward, this.BindingRetractSelectionBackward, this),
				new MyDirectionInfo("Left", Color.Blue, this.BindingExtendSelectionLeft, this.BindingRetractSelectionLeft, this),
				new MyDirectionInfo("Right", Color.Cyan, this.BindingExtendSelectionRight, this.BindingRetractSelectionRight, this),
				new MyDirectionInfo("Up", Color.Lime, this.BindingExtendSelectionUp, this.BindingRetractSelectionUp, this),
				new MyDirectionInfo("Down", Color.Yellow, this.BindingExtendSelectionDown, this.BindingRetractSelectionDown, this),
			};

			this.HudInfo = new CurrentSelectionDisplay(HudMain.HighDpiRoot, this);
		}

		/// <summary>
		/// Callback when TextHudAPI fully loaded
		/// </summary>
		private void OnTextHudAPILoaded()
		{

		}

		/// <summary>
		/// Called if RHM resets
		/// </summary>
		private void OnRichHudAPIReset() { }
		#endregion

		#region Key Binding Callbacks
		private void OnBeginSelection(object sender, EventArgs e)
		{
			if (!MyAPIGateway.CubeBuilder.IsActivated) this.BeginSelection();
		}

		private void OnEndSelection(object sender, EventArgs e)
		{
			if (this.CanModifySelection) this.EndSelection();
		}

		private void OnExtendPointSelection(object sender, EventArgs e)
		{
			if (this.CanModifySelection) this.ExtendPointSelection();
		}

		private void OnCopySelection(object sender, EventArgs e)
		{
			if (this.CanModifySelection) this.CopySelection();
		}

		private void OnExtendSelectionForward(object sender, EventArgs e)
		{
			if (this.CanModifySelection) this.ExtendSelection(MyAPIGateway.Session.Camera.WorldMatrix.Forward, 1);
		}
		private void OnRetractSelectionForward(object sender, EventArgs e)
		{
			if (this.CanModifySelection) this.ExtendSelection(MyAPIGateway.Session.Camera.WorldMatrix.Forward, -1);
		}

		private void OnExtendSelectionBackward(object sender, EventArgs e)
		{
			if (this.CanModifySelection) this.ExtendSelection(MyAPIGateway.Session.Camera.WorldMatrix.Backward, 1);
		}
		private void OnRetractSelectionBackward(object sender, EventArgs e)
		{
			if (this.CanModifySelection) this.ExtendSelection(MyAPIGateway.Session.Camera.WorldMatrix.Backward, -1);
		}

		private void OnExtendSelectionLeft(object sender, EventArgs e)
		{
			if (this.CanModifySelection) this.ExtendSelection(MyAPIGateway.Session.Camera.WorldMatrix.Left, 1);
		}
		private void OnRetractSelectionLeft(object sender, EventArgs e)
		{
			if (this.CanModifySelection) this.ExtendSelection(MyAPIGateway.Session.Camera.WorldMatrix.Left, -1);
		}

		private void OnExtendSelectionRight(object sender, EventArgs e)
		{
			if (this.CanModifySelection) this.ExtendSelection(MyAPIGateway.Session.Camera.WorldMatrix.Right, 1);
		}
		private void OnRetractSelectionRight(object sender, EventArgs e)
		{
			if (this.CanModifySelection) this.ExtendSelection(MyAPIGateway.Session.Camera.WorldMatrix.Right, -1);
		}

		private void OnExtendSelectionUp(object sender, EventArgs e)
		{
			if (this.CanModifySelection) this.ExtendSelection(MyAPIGateway.Session.Camera.WorldMatrix.Up, 1);
		}
		private void OnRetractSelectionUp(object sender, EventArgs e)
		{
			if (this.CanModifySelection) this.ExtendSelection(MyAPIGateway.Session.Camera.WorldMatrix.Up, -1);
		}

		private void OnExtendSelectionDown(object sender, EventArgs e)
		{
			if (this.CanModifySelection) this.ExtendSelection(MyAPIGateway.Session.Camera.WorldMatrix.Down, 1);
		}
		private void OnRetractSelectionDown(object sender, EventArgs e)
		{
			if (this.CanModifySelection) this.ExtendSelection(MyAPIGateway.Session.Camera.WorldMatrix.Down, -1);
		}
		#endregion

		#region Protected Methods
		/// <summary>
		/// Deinitialize mod
		/// </summary>
		protected override void UnloadData()
		{
			base.UnloadData();
			if (this.RichHudAPIActive) RichHudClient.Reset();
			this.RichHudAPIActive = false;
		}

		/// <summary>
		/// Initialize mod
		/// </summary>
		protected override void AfterInit()
		{
			base.AfterInit();
			if (MyAPIGateway.Utilities.IsDedicated) return;
			RichHudClient.Init("Partial Copy Paste", this.OnRichHudAPILoaded, this.OnRichHudAPIReset);
			this.TextHudAPI = new HudAPIv2(this.OnTextHudAPILoaded);
		}

		/// <summary>
		/// Update once every frame
		/// </summary>
		protected override void Update()
		{
			base.Update();
			if (MyAPIGateway.Utilities.IsDedicated) return;
			if (this.TargetedGrid != null && (this.TargetedGrid.MarkedForClose || this.TargetedGrid.Physics == null || !this.TargetedGrid.Physics.Enabled)) this.TargetedGrid = null;
			++this.Counter;
		}
		#endregion

		#region Public Methods
		/// <summary>
		/// Save mod config before RHM closes
		/// </summary>
		public override void BeforeClose()
		{
			base.BeforeClose();

			foreach (BindDefinition binding in this.Keybinds.GetBindDefinitions())
			{
				if (binding.name == this.ModConfiguration.Keybinds.BeginSelection.name) this.ModConfiguration.Keybinds.BeginSelection = binding;
				else if (binding.name == this.ModConfiguration.Keybinds.EndSelection.name) this.ModConfiguration.Keybinds.EndSelection = binding;
				else if (binding.name == this.ModConfiguration.Keybinds.ExtendPointSelection.name) this.ModConfiguration.Keybinds.ExtendPointSelection = binding;
				else if (binding.name == this.ModConfiguration.Keybinds.CopySelection.name) this.ModConfiguration.Keybinds.CopySelection = binding;

				else if (binding.name == this.ModConfiguration.Keybinds.ExtendSelectionForward.name) this.ModConfiguration.Keybinds.ExtendSelectionForward = binding;
				else if (binding.name == this.ModConfiguration.Keybinds.RetractSelectionForward.name) this.ModConfiguration.Keybinds.RetractSelectionForward = binding;

				else if (binding.name == this.ModConfiguration.Keybinds.ExtendSelectionBackward.name) this.ModConfiguration.Keybinds.ExtendSelectionBackward = binding;
				else if (binding.name == this.ModConfiguration.Keybinds.RetractSelectionBackward.name) this.ModConfiguration.Keybinds.RetractSelectionBackward = binding;

				else if (binding.name == this.ModConfiguration.Keybinds.ExtendSelectionLeft.name) this.ModConfiguration.Keybinds.ExtendSelectionLeft = binding;
				else if (binding.name == this.ModConfiguration.Keybinds.RetractSelectionLeft.name) this.ModConfiguration.Keybinds.RetractSelectionLeft = binding;

				else if (binding.name == this.ModConfiguration.Keybinds.ExtendSelectionRight.name) this.ModConfiguration.Keybinds.ExtendSelectionRight = binding;
				else if (binding.name == this.ModConfiguration.Keybinds.RetractSelectionRight.name) this.ModConfiguration.Keybinds.RetractSelectionRight = binding;

				else if (binding.name == this.ModConfiguration.Keybinds.ExtendSelectionUp.name) this.ModConfiguration.Keybinds.ExtendSelectionUp = binding;
				else if (binding.name == this.ModConfiguration.Keybinds.RetractSelectionUp.name) this.ModConfiguration.Keybinds.RetractSelectionUp = binding;

				else if (binding.name == this.ModConfiguration.Keybinds.ExtendSelectionDown.name) this.ModConfiguration.Keybinds.ExtendSelectionDown = binding;
				else if (binding.name == this.ModConfiguration.Keybinds.RetractSelectionDown.name) this.ModConfiguration.Keybinds.RetractSelectionDown = binding;
			}

			this.ModConfiguration.Save(this.ModConfigFileName);

			this.BindingBeginSelection.Released -= this.OnBeginSelection;
			this.BindingEndSelection.Released -= this.OnEndSelection;
			this.BindingExtendPointSelection.Released -= this.OnExtendPointSelection;
			this.BindingCopySelection.Released -= this.OnCopySelection;
			this.BindingExtendSelectionForward.Released -= this.OnExtendSelectionForward;
			this.BindingRetractSelectionForward.Released -= this.OnRetractSelectionForward;
			this.BindingExtendSelectionBackward.Released -= this.OnExtendSelectionBackward;
			this.BindingRetractSelectionBackward.Released -= this.OnRetractSelectionBackward;
			this.BindingExtendSelectionLeft.Released -= this.OnExtendSelectionLeft;
			this.BindingRetractSelectionLeft.Released -= this.OnRetractSelectionLeft;
			this.BindingExtendSelectionRight.Released -= this.OnExtendSelectionRight;
			this.BindingRetractSelectionRight.Released -= this.OnRetractSelectionRight;
			this.BindingExtendSelectionUp.Released -= this.OnExtendSelectionUp;
			this.BindingRetractSelectionUp.Released -= this.OnRetractSelectionUp;
			this.BindingExtendSelectionDown.Released -= this.OnExtendSelectionDown;
			this.BindingRetractSelectionDown.Released -= this.OnRetractSelectionDown;
		}

		/// <summary>
		/// Draw selection
		/// </summary>
		public override void Draw()
		{
			base.Draw();
			if (this.TargetedGrid == null || MyParticlesManager.Paused || this.Directions == null || MyAPIGateway.Utilities.IsDedicated) return;
			uint local_tick = (uint) (this.Counter % 60);
			float ratio = -Math.Abs(local_tick / 30f - 1) + 1;
			ratio = (float) -(Math.Cos(Math.PI * ratio) - 1) / 2;
			Color selt_color = (MyAPIGateway.CubeBuilder.IsActivated) ? Color.Red : Color.Cyan;
			Color wire_color = Color.Wheat;
			Color face_color = wire_color.Alpha(MathHelper.Lerp(0.0625f, 0.25f, ratio));
			Vector3D half = new Vector3D(this.TargetedGrid.GridSize / 2);
			MatrixD world_matrix = this.TargetedGrid.WorldMatrix;
			Vector3D local_min = this.MinPoint * half * 2;
			Vector3D local_max = this.MaxPoint * half * 2;
			Vector3D center = this.TargetedGrid.GridIntegerToWorld(this.Center);
			BoundingBoxD bounds = new BoundingBoxD(local_min - half, local_max + half).GetInflated(0.01);
			BoundingBoxD selector = bounds.GetInflated(MathHelper.Lerp(0f, 0.125f, ratio));

			MySimpleObjectDraw.DrawTransparentBox(ref world_matrix, ref selector, ref selt_color, MySimpleObjectRasterizer.Wireframe, 1, MathHelper.Lerp(0.005f, 0.01f, ratio), intensity: 3, lineMaterial: MyStringId.GetOrCompute("WeaponLaser"));
			MySimpleObjectDraw.DrawTransparentBox(ref world_matrix, ref bounds, ref wire_color, MySimpleObjectRasterizer.Wireframe, 1, MathHelper.Lerp(0.005f, 0.01f, ratio), lineMaterial: MyStringId.GetOrCompute("WeaponLaser"));
			MySimpleObjectDraw.DrawTransparentBox(ref world_matrix, ref bounds, ref face_color, MySimpleObjectRasterizer.Solid, 1, MathHelper.Lerp(0.005f, 0.01f, ratio), lineMaterial: MyStringId.GetOrCompute("WeaponLaser"));

			MyTransparentGeometry.AddPointBillboard(MyStringId.GetOrCompute("WhiteDot"), Color.Cyan, center, 0.25f, 0f);
			MyTransparentGeometry.AddPointBillboard(MyStringId.GetOrCompute("WhiteDot"), Color.Magenta, world_matrix.Translation, 0.25f, 0f);

			BoundingBoxD message_display = bounds.GetInflated(this.TargetedGrid.GridSize / 2);
			Vector3D message_center = message_display.Center;
			MyPartialCopyPasteModSession.LocalVectorToWorldVectorP(ref world_matrix, ref message_center, out message_center);
			Vector3D extents = message_display.HalfExtents + new Vector3D(this.TargetedGrid.GridSize / 2);
			Vector3D center_screen_depth = MyAPIGateway.Session.Camera.WorldToScreen(ref message_center);
			center_screen_depth.Z = 0;
			Vector3D axis;
			Vector3D[] perspectives = new Vector3D[6] {
				MyAPIGateway.Session.Camera.WorldMatrix.Forward, MyAPIGateway.Session.Camera.WorldMatrix.Backward,
				MyAPIGateway.Session.Camera.WorldMatrix.Left, MyAPIGateway.Session.Camera.WorldMatrix.Right,
				MyAPIGateway.Session.Camera.WorldMatrix.Up, MyAPIGateway.Session.Camera.WorldMatrix.Down
			};
			
			for (byte i = 0; i < perspectives.Length; ++i)
			{
				MyDirectionInfo direction_info = this.Directions[i];
				Vector3D direction = perspectives[i];
				MyPartialCopyPasteModSession.WorldVectorToLocalVectorD(ref world_matrix, ref direction, out direction);
				direction = axis = Base6Directions.GetVector(Base6Directions.GetClosestDirection(direction));
				MyPartialCopyPasteModSession.LocalVectorToWorldVectorD(ref world_matrix, ref direction, out direction);
				double direction_scale = (extents * Vector3D.Abs(axis)).Length();
				Vector3D arrow_direction = direction * (direction_scale / 2);
				direction *= direction_scale;
				Vector3D arrow_pos = message_center + direction;
				Vector3D arrow_up = direction.Normalized();
				Vector3D arrow_left = Vector3D.Cross(MyAPIGateway.Session.Camera.WorldMatrix.Forward.Normalized(), arrow_up);
				float distance = (float) Vector3D.Distance(MyAPIGateway.Session.Camera.Position, arrow_pos);
				float scale = distance * 0.125f / 2f;
				float alpha = MathHelper.Lerp(1, 0, MathHelper.Clamp(distance - 32, 0, 64) / 64);
				MyTransparentGeometry.AddBillboardOriented(MyStringId.GetOrCompute("Arrow"), direction_info.Color.Alpha(alpha), arrow_pos, arrow_left, (this.IsContracting) ? -arrow_up : arrow_up, scale, scale);

				if (this.HudInfo == null || !this.HudInfo.Visible) continue;
				Vector3D arrow_screen_depth = MyAPIGateway.Session.Camera.WorldToScreen(ref arrow_pos);
				arrow_screen_depth.Z = 0;
				arrow_direction = arrow_screen_depth - center_screen_depth;
				double angle = Math.Atan2(arrow_direction.X, -arrow_direction.Y);
				if (this.IsContracting) angle += Math.PI;
				this.HudInfo.ArrowAngles[i] = (float) angle;

				if (this.TextHudAPI == null || !this.TextHudAPI.Heartbeat) continue;
				HudAPIv2.SpaceMessage message = this.KeybindMessages[i];
				BindDefinition binding = (this.IsContracting) ? direction_info.KeybindContract : direction_info.KeybindExtend;
				if (message == null) message = this.KeybindMessages[i] = new HudAPIv2.SpaceMessage();
				message.Message = new StringBuilder($"<color={direction_info.Color.R},{direction_info.Color.G},{direction_info.Color.B}>{string.Join("+", binding.controlNames)}");
				message.Left = MyAPIGateway.Session.Camera.WorldMatrix.Left;
				message.Up = MyAPIGateway.Session.Camera.WorldMatrix.Up;
				message.WorldPosition = arrow_pos + (direction .Normalized() * this.TargetedGrid.GridSize / 2);
				message.Scale = scale / 2;
				message.Visible = alpha > 0.5f;
				angle = Vector3D.Angle(arrow_direction, new Vector3D(1, 0, 0));
				if (angle <= Math.PI / 4) message.TxtOrientation = HudAPIv2.TextOrientation.ltr;
				else if (angle <= 3 * Math.PI / 4) message.TxtOrientation = HudAPIv2.TextOrientation.center;
				else message.TxtOrientation = HudAPIv2.TextOrientation.rtl;
			}
		}

		/// <returns>All keybind definitions</returns>
		public BindDefinition[] GetKeybindDefinitions()
		{
			return this.Keybinds.GetBindDefinitions();
		}
		#endregion
	}
}
