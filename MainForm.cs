using MemoryReads64;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Flying47
{
	public partial class MainForm : Form
	{
		// Other variables.
		Process myProcess;
		public string processName;

		float readCoordX = 0;
		float readCoordY = 0;
		float readCoordZ = 0;
		float readSinAlpha = 0;
		float readCosAlpha = 0;

		float moveAmountXYAxis = 5f;
		float moveAmountZAxis = 5f;


		//Block related to position
		bool AnglesEnabled = true;
		Pointer adrSinAlpha;
		bool isSinInverted = false;
		Pointer adrCosAlpha;
		bool isCosInverted = false;

		//Used for storing coordinates
		public Structs.PositionSet_Coordinates storedCoordinates = new Structs.PositionSet_Coordinates();

		PositionSet_Pointer positionAddress,pa1;
		Structs.KeySet KeysSet;
		public Structs.PositionSets ListOfStoredPositions;

		string CurrentKeyChange;
		bool settingInputKey = false;

		Bitmap bitmap;
		Graphics gBuffer;

		public KeyboardHook.KeyboardHook m_KeyboardHook;


		/*------------------
        -- INITIALIZATION --
        ------------------*/
		public MainForm()
		{
			InitializeComponent();
			processName = "";
		}

		private void MainForm_Shown(object sender, EventArgs e)
		{
			try
			{
				if (LoadOtherConfig())
				{
					if (ProgramConfig.LoadFullConfig(out Structs.KeySet LoadedSet))
						KeysSet = LoadedSet;
					else
						KeysSet = new Structs.KeySet();

					TTimer.Start();

					B_KeyForward.Text = KeysSet.Forward.ToString();
					B_StorePosition.Text = KeysSet.StorePosition.ToString();
					B_LoadPosition.Text = KeysSet.LoadPosition.ToString();
					B_KeyUp.Text = KeysSet.Up.ToString();
					B_KeyDown.Text = KeysSet.Down.ToString();

					TB_MoveXYAxis.Text = moveAmountXYAxis.ToString();
					TB_MoveZAxis.Text = moveAmountZAxis.ToString();
					this.TopMost = KeysSet.IsTopMost;
					this.alwaysOnTopToolStripMenuItem.Checked = KeysSet.IsTopMost;
					this.CB_CheckActiveWindow.Checked = KeysSet.CheckActiveWindow;

					bitmap = new Bitmap(vectorDisplay.Width, vectorDisplay.Height);
					gBuffer = Graphics.FromImage(bitmap);
					m_KeyboardHook = new KeyboardHook.KeyboardHook
					{
						KeysEnabled = true
					};
					RegisterKeys();
					m_KeyboardHook.KeyPressed += GlobalHook_KeyDown;
					
					if (adrSinAlpha.IsNull() || adrCosAlpha.IsNull())
					{
						AnglesEnabled = false;
						KeysSet.Forward = Keys.None;
						B_KeyForward.Enabled = false;
					}

				}
				else
					Application.Exit();
			}
			catch (Exception exc)
			{
				MessageBox.Show(exc.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				Application.Exit();
			}
		}


		private void RegisterKeys()
		{
			m_KeyboardHook.RegisterHotKey(KeysSet.StorePosition);
			m_KeyboardHook.RegisterHotKey(KeysSet.LoadPosition);
			m_KeyboardHook.RegisterHotKey(KeysSet.Up);
			m_KeyboardHook.RegisterHotKey(KeysSet.Down);
			m_KeyboardHook.RegisterHotKey(KeysSet.Forward);
		}

		bool foundProcess = false;

		private void Timer_Tick(object sender, EventArgs e)
		{
			try
			{
				m_KeyboardHook.Poll();

				myProcess = Process.GetProcessesByName(processName).FirstOrDefault();
				if (myProcess != null)
				{
					if (foundProcess == false)
					{
						TTimer.Interval = 1000;
					}

					foundProcess = true;
				}
				else
				{
					foundProcess = false;
				}


				if (foundProcess)
				{
					// The game is running, ready for memory reading.
					LB_Running.Text = processName + " is running";
					LB_Running.ForeColor = Color.Green;

					positionAddress.ReadSet(myProcess, out readCoordX, out readCoordY, out readCoordZ);
					var test = Trainer.ReadPointerInteger(myProcess, positionAddress.X);
					L_X.Text = readCoordX.ToString("0.00");
					L_Y.Text = readCoordY.ToString("0.00");
					L_Z.Text = readCoordZ.ToString("0.00");
					if (AnglesEnabled)
					{
						readSinAlpha = Trainer.ReadPointerFloat(myProcess, adrSinAlpha);
						if (isSinInverted)
							readSinAlpha = -readSinAlpha;
						readCosAlpha = Trainer.ReadPointerFloat(myProcess, adrCosAlpha);
						if (isCosInverted)
							readCosAlpha = -readCosAlpha;
						DrawVectorDisplay();
						vectorDisplay.Image = bitmap;
					}

					TTimer.Interval = 100;
				}
				else
				{
					// The game process has not been found, reseting values.
					LB_Running.Text = processName + " is not running";
					LB_Running.ForeColor = Color.Red;
					ResetValues();
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex.ToString());
			}
		}

		private void DrawVectorDisplay()
		{
			gBuffer.Clear(Color.Black);
			float width = bitmap.Width;
			float half = width / 2; //make sure it's square
			gBuffer.DrawLine(new Pen(Color.Blue, 2), half, half, half + half * readSinAlpha, half + half * readCosAlpha);
		}

		// Used to reset all the values.
		private void ResetValues()
		{
			L_X.Text = "NaN";
			L_Y.Text = "NaN";
			L_Z.Text = "NaN";
		}

		private void GlobalHook_KeyDown(object sender, KeyEventArgs e)
		{
			if (!foundProcess)
				return;

			if (KeysSet.CheckActiveWindow)
			{
				var activeWindow = ProcessExtansions.GetForegroundWindow();
				if (activeWindow == null)
					return;

				try
				{
					if (ProcessExtansions.GetWindowThreadProcessId(activeWindow, out uint processID) != 0)
					{
						if (processID != myProcess.Id)
							return;
					}
				}
				catch (Exception) { }
			}

			var hotkey = e.KeyCode;
			if (hotkey == Keys.None)
				return;

			if (!settingInputKey)
			{
				if (hotkey == KeysSet.StorePosition)
				{
					Save_Position();
				}
				else if (hotkey == KeysSet.LoadPosition)
				{
					Load_Position();
				}

				if (hotkey == KeysSet.Up)
				{
					SendMeUp();
				}
				else if (hotkey == KeysSet.Down)
				{
					SendMeDown();
				}

				if (hotkey == KeysSet.Forward)
				{
					SendMeForward();
				}
			}
		}

		private void SendMeForward()
		{
			float rcx = readCoordX, rcy = readCoordY;
			Trainer.WritePointerFloat(myProcess, positionAddress.X, rcx + readSinAlpha * moveAmountXYAxis);
			Trainer.WritePointerFloat(myProcess, positionAddress.Y, rcy + readCosAlpha * moveAmountXYAxis);
			if (!ds) return;
			Trainer.WritePointerFloat(myProcess, pa1.X, rcx + readSinAlpha * moveAmountXYAxis);
			Trainer.WritePointerFloat(myProcess, pa1.Y, rcy + readCosAlpha * moveAmountXYAxis);
		}

		private void SendMeDown()
		{
			Trainer.WritePointerFloat(myProcess, positionAddress.Z, readCoordZ - moveAmountZAxis);
			if (ds) Trainer.WritePointerFloat(myProcess, pa1.Z, readCoordZ - moveAmountZAxis);
		}

		private void SendMeUp()
		{
			Trainer.WritePointerFloat(myProcess, positionAddress.Z, readCoordZ + moveAmountZAxis);
			if (ds) Trainer.WritePointerFloat(myProcess, pa1.Z, readCoordZ + moveAmountZAxis);
		}

		public void Load_Position()
		{
			Trainer.WritePointerFloat(myProcess, positionAddress.X, storedCoordinates.X);
			Trainer.WritePointerFloat(myProcess, positionAddress.Y, storedCoordinates.Y);
			Trainer.WritePointerFloat(myProcess, positionAddress.Z, storedCoordinates.Z);
			if (!ds) return;
			Trainer.WritePointerFloat(myProcess, pa1.X, storedCoordinates.X);
			Trainer.WritePointerFloat(myProcess, pa1.Y, storedCoordinates.Y);
			Trainer.WritePointerFloat(myProcess, pa1.Z, storedCoordinates.Z);
		}

		private void Save_Position()
		{
			storedCoordinates.X = readCoordX;
			storedCoordinates.Y = readCoordY;
			storedCoordinates.Z = readCoordZ;
		}


		private void Form1_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (m_KeyboardHook != null)
			{
				m_KeyboardHook.UnregisterAllHotkeys();
				m_KeyboardHook.KeyPressed -= GlobalHook_KeyDown;
			}
		}

		private void B_ChangeButton(object sender, EventArgs e)
		{
			if (((CheckBox)sender).Checked)
			{
				settingInputKey = true;
				CurrentKeyChange = ((CheckBox)sender).Name;
				if (sender == B_StorePosition)
				{
					B_StorePosition.Text = "";
				}
				else if (sender == B_LoadPosition)
				{
					B_LoadPosition.Text = "";
				}
				else if (sender == B_KeyForward)
				{
					B_KeyForward.Text = "";
				}
				else if (sender == B_KeyUp)
				{
					B_KeyUp.Text = "";
				}
				else if (sender == B_KeyDown)
				{
					B_KeyDown.Text = "";
				}
			}
			else
			{
				settingInputKey = false;
			}
		}

		private void TB_MoveZAxis_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Enter)
			{
				if (float.TryParse(TB_MoveZAxis.Text, out float MovZAxisNew))
				{
					if (MovZAxisNew > 0)
						moveAmountZAxis = MovZAxisNew;
				}
			}
		}

		private void TB_MoveXYAxis_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Enter)
			{
				if (float.TryParse(TB_MoveXYAxis.Text, out float MovXYAxisNew))
				{
					if (MovXYAxisNew > 0)
						moveAmountXYAxis = MovXYAxisNew;
				}
			}
		}

		private void MainForm_KeyDown(object sender, KeyEventArgs e)
		{
			if (settingInputKey)
			{
				var hotkey = e.KeyCode;

				if (hotkey == Keys.Escape)
					hotkey = Keys.None;
				switch (CurrentKeyChange)
				{
					case "B_KeyUp":
						KeysSet.Up = hotkey;
						B_KeyUp.Text = KeysSet.Up.ToString();
						break;
					case "B_KeyDown":
						KeysSet.Down = hotkey;
						B_KeyDown.Text = KeysSet.Down.ToString();
						break;
					case "B_KeyForward":
						KeysSet.Forward = hotkey;
						B_KeyForward.Text = KeysSet.Forward.ToString();
						break;
					case "B_StorePosition":
						KeysSet.StorePosition = hotkey;
						B_StorePosition.Text = KeysSet.StorePosition.ToString();
						break;
					case "B_LoadPosition":
						KeysSet.LoadPosition = hotkey;
						B_LoadPosition.Text = KeysSet.LoadPosition.ToString();
						break;
				}

				m_KeyboardHook.UnregisterAllHotkeys();
				RegisterKeys();

				settingInputKey = false;
			}
		}

		private void B_SaveProgramConfig_Click(object sender, EventArgs e)
		{
			if (ProgramConfig.SaveConfig(KeysSet))
				MessageBox.Show($"Saved config in:\n{ProgramConfig.ActiveConfig}", "Notification", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		private void TeleportListToolStripMenuItem_Click(object sender, EventArgs e)
		{
			bool tempOnTop = this.TopMost;
			this.TopMost = false;
			PositionsListForm posForm = new PositionsListForm(this, ListOfStoredPositions);
			posForm.Left = Left + Width;
			posForm.Top = Top;
			if (cbND.Checked) {posForm.Show(this);} else {posForm.ShowDialog();}
			m_KeyboardHook.KeysEnabled = true;
			this.TopMost = tempOnTop;
		}
		private void LoadOtherConfigToolStripMenuItem_Click(object sender, EventArgs e)
        {
			LoadOtherConfig();
		}
		public bool ds;
		private bool LoadOtherConfig()
		{
			bool x = this.processName != "";
			if (!GameConfigLoader.LoadFullConfig(out string processName, out PositionSet_Pointer positionAddress, out Pointer adrSinAlpha, out bool isSinInverted, out Pointer adrCosAlpha, out bool isCosInverted, out float moveAmountXYAxis, out float moveAmountZAxis, x))
			{return false;}
			ds = processName.Contains("pace"); // regex?
			if (ds)
			{
				var X1 = positionAddress.X; var ofs = X1.Offsets;
				X1.Offsets[ofs.Length - 1] += 0x50;
				pa1 = new PositionSet_Pointer(X1, 4, true);
			}

			this.processName = processName;
			this.positionAddress = positionAddress;
			this.adrSinAlpha = adrSinAlpha;
			this.isSinInverted = isSinInverted;
			this.adrCosAlpha = adrCosAlpha;
			this.isCosInverted = isCosInverted;
			this.moveAmountXYAxis = moveAmountXYAxis;
			this.moveAmountZAxis = moveAmountZAxis;
			TB_MoveXYAxis.Text = moveAmountXYAxis.ToString();
			TB_MoveZAxis.Text = moveAmountZAxis.ToString();

			if (File.Exists(Path.Combine("Stored Lists", processName + ".xpos")))
			{
				ListOfStoredPositions = Structs.PositionSets.Load(Path.Combine("Stored Lists", processName + ".xpos"));
			}
			else ListOfStoredPositions = new Structs.PositionSets();
			label12.Text = Path.GetFileName(GameConfigLoader.LastConfig);
			return true;
		}

		private void AlwaysOnTopToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
		{
			this.KeysSet.IsTopMost = this.alwaysOnTopToolStripMenuItem.Checked;
			this.TopMost = this.alwaysOnTopToolStripMenuItem.Checked;
		}

		private void ReloadConfigToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (GameConfigLoader.ReloadConfig(out string processName, out PositionSet_Pointer positionAddress, out Pointer adrSinAlpha, out bool isSinInverted, out Pointer adrCosAlpha, out bool isCosInverted, out float moveAmountXYAxis, out float moveAmountZAxis))
			{
				this.processName = processName;
				this.positionAddress = positionAddress;
				this.adrSinAlpha = adrSinAlpha;
				this.isSinInverted = isSinInverted;
				this.adrCosAlpha = adrCosAlpha;
				this.isCosInverted = isCosInverted;
				this.moveAmountXYAxis = moveAmountXYAxis;
				this.moveAmountZAxis = moveAmountZAxis;
				TB_MoveXYAxis.Text = moveAmountXYAxis.ToString();
				TB_MoveZAxis.Text = moveAmountZAxis.ToString();
			}
		}

		private void GithubRepositoryToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Process.Start("https://github.com/SuiMachine/Universal-Trainer-For-Testing-Speedrun-Stuff/tree/master/Release/Configs");
		}

		private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
		{
			this.Close();
		}

		private void CB_CheckActiveWindow_CheckedChanged(object sender, EventArgs e)
		{
			KeysSet.CheckActiveWindow = ((CheckBox)sender).Checked;
		}
	}
}
